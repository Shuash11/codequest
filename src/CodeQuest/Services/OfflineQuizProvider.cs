// Makes quizzes from the ones already saved. (Polymorphism)

using CodeQuest.Data;
using CodeQuest.Models;

namespace CodeQuest.Services;

public sealed class OfflineQuizProvider : IQuizProvider
{
    public const string OfflineTierId = "offline";

    public const string GenerationRefusalReason = OfflineTierId + ":not-configured:generation-not-supported";

    private readonly IQuizRepository _quizzes;

    public OfflineQuizProvider(IQuizRepository quizzes)
    {
        ArgumentNullException.ThrowIfNull(quizzes);
        _quizzes = quizzes;
    }

    public string TierId => OfflineTierId;

    public QuizGenerationOutcome GenerateQuiz(QuizGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return QuizGenerationOutcome.Failure(new[] { GenerationRefusalReason });
    }

    public IReadOnlyList<Quiz> GetTakeableQuizzes()
        => _quizzes.GetByStatus(QuizStatus.Approved);
}
