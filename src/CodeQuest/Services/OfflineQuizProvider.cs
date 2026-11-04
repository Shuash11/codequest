// =============================================================================
// CodeQuest — Phase P5: AI Provider Chain (offline tier)
// Contract: contracts/ai-quiz-api.md section 3.4 — the seeded-database tier is
//           the last resort of the quiz sources, OUTSIDE the wire contract: it
//           performs no HTTP, has no entry in aiProviders, and never reports a
//           wire tier. Its reserved identifier is "offline" (contract 2).
// Look at: the two consequences the contract fixes for this tier:
//           1. It NEVER serves generation — a generation request that reaches
//              it ends in the typed failure (the AI chain has already failed
//              by then); the quizzes it holds are served through
//              GetTakeableQuizzes instead. The generation UI (P13) points the
//              user at exactly this offline path when the chain fails.
//           2. Retrieval reports the "offline" tier — the approved quizzes of
//              the database, read through the repository layer of P4.
// =============================================================================

using CodeQuest.Data;
using CodeQuest.Models;

namespace CodeQuest.Services;

/// <summary>
/// The seeded-database tier: it serves the approved quizzes the application
/// already holds and never authors a new one.
/// </summary>
public sealed class OfflineQuizProvider : IQuizProvider
{
    /// <summary>The reserved tier identifier of this provider (contract 2).</summary>
    public const string OfflineTierId = "offline";

    /// <summary>
    /// The typed refusal this tier returns when asked to author a quiz: the
    /// reason grammar of contract 2 with the closest class code — the tier has
    /// no generation capability, so it counts as not configured to generate.
    /// (The chain never routes generation here; this is the defensive answer
    /// if a caller does it anyway.)
    /// </summary>
    public const string GenerationRefusalReason = OfflineTierId + ":not-configured:generation-not-supported";

    private readonly IQuizRepository _quizzes;

    /// <summary>Creates the offline tier over the quiz repository of the data layer.</summary>
    /// <param name="quizzes">The quiz repository (the quizzes table, with its children).</param>
    public OfflineQuizProvider(IQuizRepository quizzes)
    {
        ArgumentNullException.ThrowIfNull(quizzes);
        _quizzes = quizzes;
    }

    /// <summary>The reserved identifier of the seeded-database tier (contract 2).</summary>
    public string TierId => OfflineTierId;

    /// <summary>
    /// Never authors a quiz (contract 3.4): the typed failure, always — the
    /// seeded quizzes are served through <see cref="GetTakeableQuizzes"/>.
    /// </summary>
    public QuizGenerationOutcome GenerateQuiz(QuizGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return QuizGenerationOutcome.Failure(new[] { GenerationRefusalReason });
    }

    /// <summary>
    /// The takeable quizzes of the database: exactly the approved ones — a
    /// draft is never takeable by a student (contract 9). The quiz headers are
    /// returned; the quiz-taking flow loads the full quiz (questions and
    /// choices) through the repository when it is started.
    /// </summary>
    public IReadOnlyList<Quiz> GetTakeableQuizzes()
        => _quizzes.GetByStatus(QuizStatus.Approved);
}
