// =============================================================================
// CodeQuest — WP4: AI Topic Generation (chain orchestrator)
// The topic-generation twin of QuizGenerationService — the chain semantics of
// contracts/ai-quiz-api.md section 3, carried over unchanged for topics:
//             3.1  the aiProviders array order IS the chain priority;
//             3.2  an entry is skipped (no HTTP attempt) when it is disabled,
//                  its key is empty, or its base URL / model is missing — the
//                  skip always shows up in the failure report;
//             3.3  each non-skipped entry is attempted ONCE, in order; the
//                  first tier whose response parses into a valid topic wins;
//             7.6  with no entry at all (or every entry skipped) the chain
//                  ends in the terminal typed failure — never an exception.
// Look at: GenerateTopic() below — the walk over the configured entries that
//          collects one reason per entry, in config order, and returns either
//          the first success or the typed failure. Topics are authored ONLY
//          by the AI chain: there is no offline topic tier (the offline tier
//          of contract 3.4 serves stored quizzes, it never authors anything).
// =============================================================================

using System.Diagnostics.CodeAnalysis;

namespace CodeQuest.Services;

/// <summary>
/// Orchestrates one topic generation: it walks the configured AI providers in
/// config order and returns the typed outcome of the chain.
/// </summary>
public sealed class TopicGenerationService : ITopicGenerationService
{
    /// <summary>The chain-level reason of contract 7.6: there is nothing to try.</summary>
    public const string EmptyProviderListReason = "chain:empty-provider-list";

    private readonly AppConfig _config;
    private readonly Func<AiProviderConfig, AiTopicProvider> _providerFactory;

    /// <summary>Creates the service over the application configuration.</summary>
    /// <param name="config">The configuration read once at startup (contract 8).</param>
    public TopicGenerationService(AppConfig config)
        : this(config, providerFactory: null)
    {
    }

    /// <summary>
    /// Creates the service with an injected provider factory — the seam the
    /// tests use to substitute the network with stub handlers.
    /// </summary>
    public TopicGenerationService(
        AppConfig config,
        Func<AiProviderConfig, AiTopicProvider>? providerFactory)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
        _providerFactory = providerFactory ?? (provider => new AiTopicProvider(provider));
    }

    /// <summary>
    /// Runs the chain for one topic generation request. The result is always a
    /// typed outcome — a success served by the first tier that produced a
    /// valid topic, or a failure with one reason per entry, in config order.
    /// No exception crosses to the caller (contract 7.8).
    /// </summary>
    /// <param name="request">The validated description of the topic to create.</param>
    public TopicGenerationOutcome GenerateTopic(TopicGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Class 6 — no entry to try at all (contract 7.6).
        if (_config.AiProviders.Count == 0)
        {
            return TopicGenerationOutcome.Failure(new[] { EmptyProviderListReason });
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

            TopicGenerationOutcome attempt;

            try
            {
                // Contract 3.3 — ONE attempt per tier, in order.
                attempt = _providerFactory(entry).GenerateTopic(request);
            }
            catch (Exception)
            {
                // Defensive net around the tier boundary: the client already
                // maps every failure, so this only guards against a factory or
                // wiring defect — reported as the invocation-error class, never
                // thrown across to the UI (contract 7.8).
                attempt = TopicGenerationOutcome.Failure(
                    new[] { $"{entry.Name}:http-error:unexpected" });
            }

            if (attempt.IsSuccess)
            {
                // Contract 3.3 — the first tier whose response parses into a
                // valid topic wins; the collected reasons are dropped.
                return attempt;
            }

            reasons.AddRange(attempt.ProviderReasons);
        }

        // Terminal typed failure: one reason per entry, in config order.
        return TopicGenerationOutcome.Failure(reasons);
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
