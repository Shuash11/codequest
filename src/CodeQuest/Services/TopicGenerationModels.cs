// Data shapes for AI topics.

namespace CodeQuest.Services;

public sealed class TopicGenerationRequest
{
    public const int MinDescriptionLength = 5;

    public const int MaxDescriptionLength = 300;

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

    public string Description { get; }
}

public sealed class TopicDraft
{
    public const int MaxNameLength = 60;

    public const int MinDescriptionLength = 10;

    public const int MaxDescriptionLength = 200;

    public static readonly IReadOnlyList<string> AllowedCategories = ["Programming", "Cybersecurity"];

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

    public string Name { get; }

    public string Category { get; }

    public string Description { get; }
}

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

    public bool IsSuccess { get; }

    public string? ServedByTier { get; }

    public TopicDraft? Draft { get; }

    public IReadOnlyList<string> ProviderReasons { get; }

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
