// =============================================================================
// CodeQuest — Phase P3: Core OOP Domain Layer
// OOP rubric: ABSTRACTION — one interface, two implementations, zero knowledge of
//              the concrete provider behind it.
// Look at: IQuizProvider below. It describes *what* a quiz source can do
//          (generate a new quiz, serve stored quizzes) and not *how* it talks to
//          a remote service. The AI chain provider and the seeded-database
//          provider of phase P5 both implement this one seam, so the UI never
//          learns which one answered.
// Contract: contracts/ai-quiz-api.md sections 2, 3 and 3.4 — the generation
//          outcome is a typed result (never an exception at the UI boundary):
//          success carries the serving tier id and the generated draft quiz,
//          failure carries one reason string per provider, in provider order.
// Note:    no provider is named anywhere in this file on purpose — the tier id
//          comes from the configuration at run time (P5 reads it).
// =============================================================================

using CodeQuest.Models;

namespace CodeQuest.Services;

/// <summary>
/// A source of quizzes. Implementations either author a new quiz (a remote
/// generation tier) or serve quizzes that already exist (the seeded-database
/// tier); the consumer of this interface does not care which.
/// </summary>
public interface IQuizProvider
{
    /// <summary>
    /// The stable identifier of this tier — the configured name of the tier, or the
    /// reserved identifier of the seeded-database tier. It is reported verbatim in
    /// results and reason strings.
    /// </summary>
    string TierId { get; }

    /// <summary>
    /// Asks this tier to author a new quiz. The result is always a typed outcome —
    /// a success with the drafted quiz, or a failure with the reasons of every tier
    /// that was tried. No exception is thrown to the caller.
    /// The seeded-database tier never authors a quiz: a generation request that
    /// reaches it ends in the typed failure, and the quizzes it already holds are
    /// served through <see cref="GetTakeableQuizzes"/>.
    /// </summary>
    QuizGenerationOutcome GenerateQuiz(QuizGenerationRequest request);

    /// <summary>
    /// Serves the quizzes this tier can offer without generating them: the
    /// seeded-database tier returns the approved quizzes of the database, a
    /// generation-only tier returns none. This is the retrieval path — a generation
    /// result never reports the seeded-database tier.
    /// </summary>
    IReadOnlyList<Quiz> GetTakeableQuizzes();
}

/// <summary>
/// What a caller asks a provider for: one topic, one difficulty and how many
/// questions the quiz should have.
/// </summary>
public sealed class QuizGenerationRequest
{
    /// <summary>The topic the quiz is about (the topic picker selection of the UI).</summary>
    public Topic Topic { get; }

    /// <summary>The difficulty the quiz should have.</summary>
    public Difficulty Difficulty { get; }

    /// <summary>How many questions the quiz should have.</summary>
    public int QuestionCount { get; }

    /// <summary>Creates a generation request.</summary>
    /// <param name="topic">The topic the quiz is about.</param>
    /// <param name="difficulty">The difficulty the quiz should have.</param>
    /// <param name="questionCount">
    /// How many questions the quiz should have. The UI offers 5 to 20; the seam only
    /// insists on a positive number so that tests can use small quizzes.
    /// </param>
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

/// <summary>
/// The typed result of a generation request: either a success or a failure —
/// never an exception that reaches the UI. Exactly one of the two cases happens
/// per request.
/// </summary>
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

    /// <summary>True for a success, false for a failure.</summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// The tier that served the request — its configured identifier. Null on a
    /// failure, because nobody served the request.
    /// </summary>
    public string? ServedByTier { get; }

    /// <summary>
    /// The drafted quiz, with the status <see cref="QuizStatus.Draft"/> — a generated
    /// quiz waits for the review screen before it becomes takeable. Null on a failure.
    /// </summary>
    public Quiz? QuizDraft { get; }

    /// <summary>
    /// One reason string per tier that was tried, in the order the tiers were tried.
    /// Empty on a success.
    /// </summary>
    public IReadOnlyList<string> ProviderReasons { get; }

    /// <summary>
    /// Builds a success: the tier that served the request and the drafted quiz.
    /// </summary>
    /// <param name="servedByTier">The identifier of the tier that served the request.</param>
    /// <param name="quizDraft">
    /// The drafted quiz. It must be a draft — a generated quiz is never approved
    /// automatically.
    /// </param>
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

    /// <summary>
    /// Builds a failure: nobody served the request, and every tier that was tried
    /// left a reason behind.
    /// </summary>
    /// <param name="providerReasons">
    /// One reason string per tier that was tried, in the order the tiers were tried.
    /// </param>
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
