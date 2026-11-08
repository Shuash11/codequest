// =============================================================================
// CodeQuest — WP4: AI Topic Generation (generation tier)
// ONE wire tier of the topic chain, bound to ONE configured entry — the topic
// twin of AiQuizProvider. The chain itself — order, skips, first success wins
// — is TopicGenerationService; this class performs the single attempt of its
// tier (contract 3.3: one attempt, no in-tier retry).
// Look at: GenerateTopic() — build the prompt (a strict system prompt in the
//          same style as the quiz tier's, then the user prompt carrying the
//          user's description VERBATIM), send it through the one wire client
//          (Api/ChatCompletionsClient.cs, parameterized per provider) and
//          parse the answer with the strict pipeline: code fences stripped
//          (reusing StrictQuizJsonParser.StripCodeFences — the models wrap
//          JSON despite instructions), the whole remaining text must be one
//          JSON object, unknown fields are rejected, and the first violation
//          names its field. Every failure — HTTP, quota, malformed output,
//          timeout — ends as a TYPED failure reason
//          ("<tier>:<class>[:<detail>]", contract 2 and 7.7); no exception
//          crosses to the caller (contract 7.8, invariant 1).
// Secrets: the API key travels only in the Authorization header of the
//          outgoing request — it never appears in a reason, a result or an
//          exception message (contract 7.8, invariant 3).
// =============================================================================

using System.Text.Json;
using CodeQuest.Api;

namespace CodeQuest.Services;

/// <summary>
/// One AI wire tier of the topic chain. It authors a new topic by asking the
/// configured model for the strict topic JSON of plan FR1.
/// </summary>
public sealed class AiTopicProvider
{
    /// <summary>The wire role of the prompt that pins the output schema (contract 4.1).</summary>
    private const string SystemRole = "system";

    /// <summary>The wire role of the prompt that carries the user's description (contract 4.1).</summary>
    private const string UserRole = "user";

    private readonly AiProviderConfig _provider;
    private readonly Func<AiProviderConfig, ChatCompletionsClient> _clientFactory;

    /// <summary>Creates the topic generation tier for one configured provider entry.</summary>
    /// <param name="provider">The configured entry (name, base URL, key, model, switch).</param>
    public AiTopicProvider(AiProviderConfig provider)
        : this(provider, clientFactory: null)
    {
    }

    /// <summary>
    /// Creates the topic generation tier with an injected client factory — the
    /// seam the tests use to substitute the network with a stub handler.
    /// </summary>
    public AiTopicProvider(
        AiProviderConfig provider,
        Func<AiProviderConfig, ChatCompletionsClient>? clientFactory)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _provider = provider;
        _clientFactory = clientFactory ?? DefaultClientFactory;
    }

    /// <summary>The tier identifier of this provider — its configured name (contract 2).</summary>
    public string TierId => _provider.Name;

    /// <summary>
    /// Performs this tier's single generation attempt (contract 3.3). On success
    /// the outcome names this tier and carries the draft topic; on any failure
    /// it carries the one typed reason of this tier.
    /// </summary>
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
            // Class 4 — the model output did not survive the strict pipeline
            // (contract 7.4); the chain moves on to the next tier.
            return TopicGenerationOutcome.Failure(
                new[] { $"{_provider.Name}:{ChatCompletionErrorClasses.BadOutput}:{failureCause}" });
        }

        return TopicGenerationOutcome.Success(_provider.Name, draft);
    }

    /// <summary>The default client of this tier: the one wire client, parameterized.</summary>
    private static ChatCompletionsClient DefaultClientFactory(AiProviderConfig provider)
        => new(provider.Name, provider.BaseUrl, provider.ApiKey, provider.Model);

    /// <summary>
    /// Builds the two messages of the request envelope (contract 4.1): the
    /// system message that pins the strict topic JSON, then the user message
    /// with the user's description verbatim (§5.3a — the description is the
    /// only generation input, never embellished by the caller).
    /// </summary>
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

    /// <summary>Formats this tier's reason string ("<tier>:<class>[:<detail>]", contract 2).</summary>
    private string FormatReason(string errorClass, string? errorDetail)
        => string.IsNullOrEmpty(errorDetail)
            ? $"{_provider.Name}:{errorClass}"
            : $"{_provider.Name}:{errorClass}:{errorDetail}";
}

/// <summary>
/// The strict-JSON pipeline of the topic draft: strip code fences, parse the
/// whole text, enforce every field rule of plan FR1, reject unknown fields.
/// Internal — it exists only for the provider above and is exercised through
/// it in the tests. Same discipline as the quiz parser of AiQuizProvider.cs:
/// no substring salvage, no response healing, the first violation names its
/// field. There are no echo checks here (a topic has no requested values to
/// echo back) — the length and enum rules carry the strictness instead.
/// </summary>
internal static class StrictTopicDraftJsonParser
{
    /// <summary>
    /// Parses the model output into a validated draft, or reports the first
    /// violated rule.
    /// </summary>
    public static TopicDraft? Parse(string content, out string? failureCause)
    {
        // The fence stripper is the quiz pipeline's — one markdown-fence
        // dialect, one implementation (the handoff's reuse rule).
        string json = StrictQuizJsonParser.StripCodeFences(content);

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            // The whole text must be one JSON object; no substring salvage.
            failureCause = "json-parse-failed";
            return null;
        }

        using (document)
        {
            return Validate(document.RootElement, out failureCause);
        }
    }

    /// <summary>Enforces every field rule of plan FR1; the first violation stops.</summary>
    private static TopicDraft? Validate(JsonElement root, out string? failureCause)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            failureCause = "root-not-object";
            return null;
        }

        // Strictness: the schema is exactly the three fields of the system
        // prompt — anything else is a malformed output (contract 7.4), the
        // same "Do not add any other field" discipline the quiz prompt pins.
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (property.Name is not ("name" or "category" or "description"))
            {
                // The reason grammar is printable ASCII on one line (contract
                // 2) — the field name comes from the wire, so it is sanitized.
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

        // The draft constructor re-asserts the domain invariants — defense in
        // depth only; every rule above already held.
        failureCause = null;
        return new TopicDraft(name!, category!, description);
    }

    /// <summary>Reads one required, trimmed, non-empty string property.</summary>
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

    /// <summary>The cause text of a missing, mistyped or empty required field.</summary>
    private static string FieldCause(string field) => $"{field}-missing-or-empty";
}
