// Asks the AI to make a topic.

using System.Text.Json;
using CodeQuest.Api;

namespace CodeQuest.Services;

public sealed class AiTopicProvider
{
    private const string SystemRole = "system";

    private const string UserRole = "user";

    private readonly AiProviderConfig _provider;
    private readonly Func<AiProviderConfig, ChatCompletionsClient> _clientFactory;

    public AiTopicProvider(AiProviderConfig provider)
        : this(provider, clientFactory: null)
    {
    }

    public AiTopicProvider(
        AiProviderConfig provider,
        Func<AiProviderConfig, ChatCompletionsClient>? clientFactory)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _provider = provider;
        _clientFactory = clientFactory ?? DefaultClientFactory;
    }

    public string TierId => _provider.Name;

    public TopicGenerationOutcome GenerateTopic(TopicGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        using ChatCompletionsClient client = _clientFactory(_provider);
        ChatCompletionResult result = client.Complete(BuildMessages(request));

        if (!result.IsSuccess)
        {
            return TopicGenerationOutcome.Failure(
                new[] { FormatReason(result.ErrorClass!, result.ErrorDetail) });
        }

        TopicDraft? draft = StrictTopicDraftJsonParser.Parse(
            result.Content!,
            out string? failureCause);

        if (draft is null)
        {
            return TopicGenerationOutcome.Failure(
                new[] { $"{_provider.Name}:{ChatCompletionErrorClasses.BadOutput}:{failureCause}" });
        }

        return TopicGenerationOutcome.Success(_provider.Name, draft);
    }

    private static ChatCompletionsClient DefaultClientFactory(AiProviderConfig provider)
        => new(provider.Name, provider.BaseUrl, provider.ApiKey, provider.Model);

    private IReadOnlyList<ChatMessage> BuildMessages(TopicGenerationRequest request)
    {
        string systemPrompt =
            $"""
            You create quiz topics for a study application.
            Answer with ONLY one JSON object - no markdown code fences, no prose before or after it.
            The JSON object must have exactly these fields:
            "name": the topic name, at most {TopicDraft.MaxNameLength} characters.
            "category": exactly one of "Programming" or "Cybersecurity".
            "description": one sentence of {TopicDraft.MinDescriptionLength} to {TopicDraft.MaxDescriptionLength} characters describing what the topic covers.
            Do not add any other field at the root.
            """;

        string userPrompt =
            $"Create one quiz topic. The user described it as: \"{request.Description}\"";

        return new[]
        {
            new ChatMessage(SystemRole, systemPrompt),
            new ChatMessage(UserRole, userPrompt),
        };
    }

    private string FormatReason(string errorClass, string? errorDetail)
        => string.IsNullOrEmpty(errorDetail)
            ? $"{_provider.Name}:{errorClass}"
            : $"{_provider.Name}:{errorClass}:{errorDetail}";
}

internal static class StrictTopicDraftJsonParser
{
    public static TopicDraft? Parse(string content, out string? failureCause)
    {
        string json = StrictQuizJsonParser.StripCodeFences(content);

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            failureCause = "json-parse-failed";
            return null;
        }

        using (document)
        {
            return Validate(document.RootElement, out failureCause);
        }
    }

    private static TopicDraft? Validate(JsonElement root, out string? failureCause)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            failureCause = "root-not-object";
            return null;
        }

        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (property.Name is not ("name" or "category" or "description"))
            {
                failureCause = $"unknown-field-{ChatCompletionsClient.Sanitize(property.Name)}";
                return null;
            }
        }

        if (!TryReadTrimmedString(root, "name", out string? name))
        {
            failureCause = FieldCause("name");
            return null;
        }

        if (name!.Length > TopicDraft.MaxNameLength)
        {
            failureCause = $"name-longer-than-{TopicDraft.MaxNameLength}";
            return null;
        }

        if (!TryReadTrimmedString(root, "category", out string? category))
        {
            failureCause = FieldCause("category");
            return null;
        }

        if (!TopicDraft.AllowedCategories.Contains(category!, StringComparer.Ordinal))
        {
            failureCause = "category-not-allowed";
            return null;
        }

        if (!TryReadTrimmedString(root, "description", out string? description))
        {
            failureCause = FieldCause("description");
            return null;
        }

        if (description!.Length < TopicDraft.MinDescriptionLength)
        {
            failureCause = $"description-shorter-than-{TopicDraft.MinDescriptionLength}";
            return null;
        }

        if (description.Length > TopicDraft.MaxDescriptionLength)
        {
            failureCause = $"description-longer-than-{TopicDraft.MaxDescriptionLength}";
            return null;
        }

        failureCause = null;
        return new TopicDraft(name!, category!, description);
    }

    private static bool TryReadTrimmedString(JsonElement element, string propertyName, out string? value)
    {
        value = null;

        if (!element.TryGetProperty(propertyName, out JsonElement item)
            || item.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        string? text = item.GetString();

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        value = text.Trim();
        return true;
    }

    private static string FieldCause(string field) => $"{field}-missing-or-empty";
}
