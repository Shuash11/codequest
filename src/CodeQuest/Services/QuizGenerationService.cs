// Tries each quiz source until one works.

using System.Diagnostics.CodeAnalysis;
using CodeQuest.Models;

namespace CodeQuest.Services;

public sealed class QuizGenerationService
{
    public const string EmptyProviderListReason = "chain:empty-provider-list";

    private readonly AppConfig _config;
    private readonly Func<AiProviderConfig, AiQuizProvider> _providerFactory;

    public QuizGenerationService(AppConfig config)
        : this(config, providerFactory: null)
    {
    }

    public QuizGenerationService(
        AppConfig config,
        Func<AiProviderConfig, AiQuizProvider>? providerFactory)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
        _providerFactory = providerFactory ?? (provider => new AiQuizProvider(provider));
    }

    public QuizGenerationOutcome GenerateQuiz(Topic topic, Difficulty difficulty, int questionCount)
    {
        ArgumentNullException.ThrowIfNull(topic);

        QuizGenerationRequest request = new(topic, difficulty, questionCount);

        if (_config.AiProviders.Count == 0)
        {
            return QuizGenerationOutcome.Failure(new[] { EmptyProviderListReason });
        }

        List<string> reasons = new(_config.AiProviders.Count);

        foreach (AiProviderConfig entry in _config.AiProviders)
        {
            if (TryGetSkipReason(entry, out string? skipReason))
            {
                reasons.Add(skipReason);
                continue;
            }

            QuizGenerationOutcome attempt;

            try
            {
                attempt = _providerFactory(entry).GenerateQuiz(request);
            }
            catch (Exception)
            {
                attempt = QuizGenerationOutcome.Failure(
                    new[] { $"{entry.Name}:http-error:unexpected" });
            }

            if (attempt.IsSuccess)
            {
                return attempt;
            }

            reasons.AddRange(attempt.ProviderReasons);
        }

        return QuizGenerationOutcome.Failure(reasons);
    }

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
