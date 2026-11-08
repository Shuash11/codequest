// =============================================================================
// CodeQuest — WP4: AI Topic Generation (typed pipeline models)
// The topic-generation twins of the quiz chain's request/outcome types
// (Services/IQuizProvider.cs): ONE request type in, ONE typed outcome out —
// never an exception at the UI boundary. The chain semantics (config order,
// skip rules, first success wins) live in TopicGenerationService.cs; the wire
// tier and the strict parser live in AiTopicProvider.cs.
// Look at: TopicGenerationRequest — the service-level guard of the FR1 range
//          (required, trimmed 5..300). The topic dialog validates with the
//          same range before it ever calls the service (§5.3a), so this guard
//          is the second net — it keeps every other caller honest.
//          TopicDraft — the validated topic the model must produce: the schema
//          CHECK constrains category to exactly two values, the name is
//          1..60 characters and the description 10..200 (plan FR1); the
//          constructor enforces the same invariants as defense in depth.
//          TopicGenerationOutcome — mirrors QuizGenerationOutcome: IsSuccess,
//          the winning tier's id, the draft, and on failure one typed reason
//          string per tried tier, in config order ("<tier>:<class>[:<detail>]",
//          contracts/ai-quiz-api.md section 2 grammar — key-free by contract
//          7.8 invariant 3).
// =============================================================================

namespace CodeQuest.Services;

/// <summary>
/// What a caller asks the topic chain for: the user's description of the topic
/// to create. The description is the verbatim AI input (§5.3a) — the service
/// trims it once here and never again.
/// </summary>
public sealed class TopicGenerationRequest
{
    /// <summary>The shortest description the plan's FR1 range allows.</summary>
    public const int MinDescriptionLength = 5;

    /// <summary>The longest description the plan's FR1 range allows.</summary>
    public const int MaxDescriptionLength = 300;

    /// <summary>Creates a topic generation request.</summary>
    /// <param name="description">
    /// The user's description of the topic. Required; trimmed length 5..300
    /// (plan FR1) — the same range the topic dialog validates inline (§5.3a).
    /// </param>
    public TopicGenerationRequest(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        string trimmed = description.Trim();

        if (trimmed.Length < MinDescriptionLength || trimmed.Length > MaxDescriptionLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(description),
                trimmed.Length,
                $"A topic description needs between {MinDescriptionLength} and {MaxDescriptionLength} characters.");
        }

        Description = trimmed;
    }

    /// <summary>The trimmed description of the topic to create (5..300 characters).</summary>
    public string Description { get; }
}

/// <summary>
/// One validated topic the AI chain produced (plan FR1): the values the
/// New-topic dialog prefills as the editable preview before the user saves.
/// </summary>
public sealed class TopicDraft
{
    /// <summary>The longest topic name the plan's FR1 range allows.</summary>
    public const int MaxNameLength = 60;

    /// <summary>The shortest draft description the plan's FR1 range allows.</summary>
    public const int MinDescriptionLength = 10;

    /// <summary>The longest draft description the plan's FR1 range allows.</summary>
    public const int MaxDescriptionLength = 200;

    /// <summary>The exactly-two category values the schema CHECK allows (R-2 #7).</summary>
    public static readonly IReadOnlyList<string> AllowedCategories = ["Programming", "Cybersecurity"];

    /// <summary>Creates a validated topic draft.</summary>
    /// <param name="name">The topic name, 1..60 characters (plan FR1).</param>
    /// <param name="category">Exactly "Programming" or "Cybersecurity" (R-2 #7).</param>
    /// <param name="description">The description, 10..200 characters (plan FR1).</param>
    public TopicDraft(string name, string category, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        string trimmedName = name.Trim();
        string trimmedDescription = description.Trim();

        if (trimmedName.Length > MaxNameLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(name),
                trimmedName.Length,
                $"A topic name is at most {MaxNameLength} characters.");
        }

        if (!AllowedCategories.Contains(category, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "A topic category is exactly \"Programming\" or \"Cybersecurity\".",
                nameof(category));
        }

        if (trimmedDescription.Length < MinDescriptionLength
            || trimmedDescription.Length > MaxDescriptionLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(description),
                trimmedDescription.Length,
                $"A topic description needs between {MinDescriptionLength} and {MaxDescriptionLength} characters.");
        }

        Name = trimmedName;
        Category = category;
        Description = trimmedDescription;
    }

    /// <summary>The topic name (1..60 characters).</summary>
    public string Name { get; }

    /// <summary>The category — exactly "Programming" or "Cybersecurity" (R-2 #7).</summary>
    public string Category { get; }

    /// <summary>The description (10..200 characters).</summary>
    public string Description { get; }
}

/// <summary>
/// The typed result of one topic generation request: either a success or a
/// failure — never an exception that reaches the UI. Exactly one of the two
/// cases happens per request. Mirrors <see cref="QuizGenerationOutcome"/>.
/// </summary>
public sealed class TopicGenerationOutcome
{
    private TopicGenerationOutcome(
        bool isSuccess,
        string? servedByTier,
        TopicDraft? draft,
        IReadOnlyList<string> providerReasons)
    {
        IsSuccess = isSuccess;
        ServedByTier = servedByTier;
        Draft = draft;
        ProviderReasons = providerReasons;
    }

    /// <summary>True for a success, false for a failure.</summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// The tier that served the request — its configured identifier. Null on a
    /// failure, because nobody served the request.
    /// </summary>
    public string? ServedByTier { get; }

    /// <summary>The validated topic draft the dialog prefills; null on a failure.</summary>
    public TopicDraft? Draft { get; }

    /// <summary>
    /// One reason string per tier that was tried, in the order the tiers were
    /// tried. Empty on a success.
    /// </summary>
    public IReadOnlyList<string> ProviderReasons { get; }

    /// <summary>Builds a success: the tier that served the request and the draft.</summary>
    /// <param name="servedByTier">The identifier of the tier that served the request.</param>
    /// <param name="draft">The validated topic draft.</param>
    public static TopicGenerationOutcome Success(string servedByTier, TopicDraft draft)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(servedByTier);
        ArgumentNullException.ThrowIfNull(draft);

        return new TopicGenerationOutcome(
            isSuccess: true,
            servedByTier: servedByTier,
            draft: draft,
            providerReasons: Array.Empty<string>());
    }

    /// <summary>
    /// Builds a failure: nobody served the request, and every tier that was
    /// tried left a reason behind.
    /// </summary>
    /// <param name="providerReasons">
    /// One reason string per tier that was tried, in the order the tiers were tried.
    /// </param>
    public static TopicGenerationOutcome Failure(IEnumerable<string> providerReasons)
    {
        ArgumentNullException.ThrowIfNull(providerReasons);

        return new TopicGenerationOutcome(
            isSuccess: false,
            servedByTier: null,
            draft: null,
            providerReasons: providerReasons.ToArray());
    }
}
