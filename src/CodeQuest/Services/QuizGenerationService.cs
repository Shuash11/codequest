// =============================================================================
// CodeQuest — Phase P5: AI Provider Chain (orchestrator)
// Contract: contracts/ai-quiz-api.md section 3 — the chain semantics:
//             3.1  the aiProviders array order IS the chain priority;
//             3.2  an entry is skipped (no HTTP attempt) when it is disabled,
//                  its key is empty, or its base URL / model is missing — the
//                  skip always shows up in the failure report;
//             3.3  each non-skipped entry is attempted ONCE, in order; the
//                  first tier whose response parses into a valid quiz wins;
//             7.6  with no entry at all (or every entry skipped) the chain
//                  ends in the terminal typed failure — never an exception.
// Look at: GenerateQuiz() below — the walk over the configured entries that
//          collects one reason per entry, in config order, and returns either
//          the first success or the typed failure. A generation request is
//          routed ONLY to the AI chain: the offline tier never authors a quiz
//          (contract 3.4) and is not part of this walk.
// =============================================================================

using System.Diagnostics.CodeAnalysis;
using CodeQuest.Models;

namespace CodeQuest.Services;

/// <summary>
/// Orchestrates one quiz generation: it walks the configured AI providers in
/// config order and returns the typed outcome of the chain.
/// </summary>
public sealed class QuizGenerationService
{
    /// <summary>The chain-level reason of contract 7.6: there is nothing to try.</summary>
    public const string EmptyProviderListReason = "chain:empty-provider-list";

    private readonly AppConfig _config;
    private readonly Func<AiProviderConfig, AiQuizProvider> _providerFactory;

    /// <summary>Creates the service over the application configuration.</summary>
    /// <param name="config">The configuration read once at startup (contract 8).</param>
    public QuizGenerationService(AppConfig config)
        : this(config, providerFactory: null)
    {
    }

    /// <summary>
    /// Creates the service with an injected provider factory — the seam the
    /// tests use to substitute the network with stub handlers.
    /// </summary>
    public QuizGenerationService(
        AppConfig config,
        Func<AiProviderConfig, AiQuizProvider>? providerFactory)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
        _providerFactory = providerFactory ?? (provider => new AiQuizProvider(provider));
    }

    /// <summary>
    /// Runs the chain for one generation request (contract 3). The result is
    /// always a typed outcome — a success served by the first tier that
    /// produced a valid quiz, or a failure with one reason per entry, in
    /// config order. No exception crosses to the caller (contract 7.8).
    /// </summary>
    /// <param name="topic">The topic the quiz is about (the picker selection).</param>
    /// <param name="difficulty">The requested difficulty.</param>
    /// <param name="questionCount">How many questions the quiz should have.</param>
    public QuizGenerationOutcome GenerateQuiz(Topic topic, Difficulty difficulty, int questionCount)
    {
        ArgumentNullException.ThrowIfNull(topic);

        QuizGenerationRequest request = new(topic, difficulty, questionCount);

        // Class 6 — no entry to try at all (contract 7.6).
        if (_config.AiProviders.Count == 0)
        {
            return QuizGenerationOutcome.Failure(new[] { EmptyProviderListReason });
        }

        List<string> reasons = new(_config.AiProviders.Count);

        foreach (AiProviderConfig entry in _config.AiProviders)
        {
            if (TryGetSkipReason(entry, out string? skipReason))
            {
                // Contract 3.2 — skipped silently by design (no HTTP attempt),
                // but always visible in the failure report.
                reasons.Add(skipReason);
                continue;
            }

            QuizGenerationOutcome attempt;

            try
            {
                // Contract 3.3 — ONE attempt per tier, in order.
                attempt = _providerFactory(entry).GenerateQuiz(request);
            }
            catch (Exception)
            {
                // Defensive net around the tier boundary: the client already
                // maps every failure, so this only guards against a factory or
                // wiring defect — reported as the invocation-error class, never
                // thrown across to the UI (contract 7.8).
                attempt = QuizGenerationOutcome.Failure(
                    new[] { $"{entry.Name}:http-error:unexpected" });
            }

            if (attempt.IsSuccess)
            {
                // Contract 3.3 — the first tier whose response parses into a
                // valid quiz wins; the collected reasons are dropped.
                return attempt;
            }

            reasons.AddRange(attempt.ProviderReasons);
        }

        // Terminal typed failure: one reason per entry, in config order.
        return QuizGenerationOutcome.Failure(reasons);
    }

    /// <summary>
    /// Evaluates the skip rules of contract 3.2, in the order of the
    /// contract's table: disabled first, then the empty key, then the missing
    /// base URL or model. A skipped entry performs no HTTP attempt.
    /// </summary>
    private static bool TryGetSkipReason(AiProviderConfig entry, [NotNullWhen(true)] out string? reason)
    {
        string tier = entry.Name;

        if (!entry.Enabled)
        {
            reason = $"{tier}:not-configured:disabled";
            return true;
        }

        if (string.IsNullOrWhiteSpace(entry.ApiKey))
        {
            reason = $"{tier}:not-configured:api-key-empty";
            return true;
        }

        if (string.IsNullOrWhiteSpace(entry.BaseUrl) || string.IsNullOrWhiteSpace(entry.Model))
        {
            reason = $"{tier}:not-configured:invalid-config";
            return true;
        }

        reason = null;
        return false;
    }
}
