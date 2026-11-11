// The quiz generator contract. (Abstraction)

using CodeQuest.Models;

namespace CodeQuest.Services;

public interface IQuizProvider
{
    string TierId { get; }

    QuizGenerationOutcome GenerateQuiz(QuizGenerationRequest request);

    IReadOnlyList<Quiz> GetTakeableQuizzes();
}

public sealed class QuizGenerationRequest
{
    public Topic Topic { get; }

    public Difficulty Difficulty { get; }

    public int QuestionCount { get; }

    public QuizGenerationRequest(Topic topic, Difficulty difficulty, int questionCount)
    {
        ArgumentNullException.ThrowIfNull(topic);

        if (questionCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(questionCount),
                questionCount,
                "A quiz needs at least one question.");
        }

        Topic = topic;
        Difficulty = difficulty;
        QuestionCount = questionCount;
    }
}

public sealed class QuizGenerationOutcome
{
    private QuizGenerationOutcome(
        bool isSuccess,
        string? servedByTier,
        Quiz? quizDraft,
        IReadOnlyList<string> providerReasons)
    {
        IsSuccess = isSuccess;
        ServedByTier = servedByTier;
        QuizDraft = quizDraft;
        ProviderReasons = providerReasons;
    }

    public bool IsSuccess { get; }

    public string? ServedByTier { get; }

    public Quiz? QuizDraft { get; }

    public IReadOnlyList<string> ProviderReasons { get; }

    public static QuizGenerationOutcome Success(string servedByTier, Quiz quizDraft)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(servedByTier);
        ArgumentNullException.ThrowIfNull(quizDraft);

        if (quizDraft.Status != QuizStatus.Draft)
        {
            throw new ArgumentException(
                "A generated quiz must be a draft until the review screen approves it.",
                nameof(quizDraft));
        }

        return new QuizGenerationOutcome(
            isSuccess: true,
            servedByTier: servedByTier,
            quizDraft: quizDraft,
            providerReasons: Array.Empty<string>());
    }

    public static QuizGenerationOutcome Failure(IEnumerable<string> providerReasons)
    {
        ArgumentNullException.ThrowIfNull(providerReasons);

        return new QuizGenerationOutcome(
            isSuccess: false,
            servedByTier: null,
            quizDraft: null,
            providerReasons: providerReasons.ToArray());
    }
}
