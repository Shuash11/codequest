// Tries each topic source until one works.

using System.Diagnostics.CodeAnalysis;

namespace CodeQuest.Services;

public sealed class TopicGenerationService : ITopicGenerationService
{
    public const string EmptyProviderListReason = "chain:empty-provider-list";

    private readonly AppConfig _config;
    private readonly Func<AiProviderConfig, AiTopicProvider> _providerFactory;

    public TopicGenerationService(AppConfig config)
        : this(config, providerFactory: null)
    {
    }

    public TopicGenerationService(
        AppConfig config,
        Func<AiProviderConfig, AiTopicProvider>? providerFactory)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
        _providerFactory = providerFactory ?? (provider => new AiTopicProvider(provider));
    }

    public TopicGenerationOutcome GenerateTopic(TopicGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_config.AiProviders.Count == 0)
        {
            return TopicGenerationOutcome.Failure(new[] { EmptyProviderListReason });
        }

        List<string> reasons = new(_config.AiProviders.Count);

        foreach (AiProviderConfig entry in _config.AiProviders)
        {
            if (TryGetSkipReason(entry, out string? skipReason))
            {
                reasons.Add(skipReason);
                continue;
            }

            TopicGenerationOutcome attempt;

            try
            {
                attempt = _providerFactory(entry).GenerateTopic(request);
            }
            catch (Exception)
            {
                attempt = TopicGenerationOutcome.Failure(
                    new[] { $"{entry.Name}:http-error:unexpected" });
            }

            if (attempt.IsSuccess)
            {
                return attempt;
            }

            reasons.AddRange(attempt.ProviderReasons);
        }

        return TopicGenerationOutcome.Failure(reasons);
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
