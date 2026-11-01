// =============================================================================
// CodeQuest — Phase P3: Core OOP Domain Layer
// OOP rubric: ENCAPSULATION — private fields, public properties.
// Look at: the private fields below and the public properties that expose them;
//          no public mutable field exists anywhere in this class.
// Table:   quiz_attempts (database/schema.sql) — columns: attempt_id, quiz_id,
//          user_id, score, attempted_at.
// Note:    R-2 #1 — the score is the number of CORRECT answers and is the only
//          result stored here. A percentage or a "total questions" column would be
//          derived data and is deliberately NOT part of this model; the scoring
//          service (ScoringService) derives them from the answers.
// =============================================================================

namespace CodeQuest.Models;

/// <summary>
/// One completed run of a quiz by one user (schema table <c>quiz_attempts</c>).
/// </summary>
public sealed class QuizAttempt
{
    private int _attemptId;
    private int _quizId;
    private int _userId;
    private int _score;
    private DateTimeOffset _attemptedAt;

    /// <summary>Primary key of the attempt (schema column <c>attempt_id</c>).</summary>
    public int AttemptId
    {
        get => _attemptId;
        set => _attemptId = value;
    }

    /// <summary>The attempted quiz (schema column <c>quiz_id</c>).</summary>
    public int QuizId
    {
        get => _quizId;
        set => _quizId = value;
    }

    /// <summary>The user who took the quiz (schema column <c>user_id</c>).</summary>
    public int UserId
    {
        get => _userId;
        set => _userId = value;
    }

    /// <summary>
    /// Number of correct answers of this attempt (schema column <c>score</c>). The
    /// number of answered questions is counted from the submitted answers, so it is
    /// not stored here.
    /// </summary>
    public int Score
    {
        get => _score;
        set => _score = value;
    }

    /// <summary>
    /// When the quiz was taken (schema column <c>attempted_at</c>). The schema stores
    /// ISO-8601 UTC text; the data layer (P4) writes and reads the format
    /// <c>yyyy-MM-ddTHH:mm:ssZ</c>.
    /// </summary>
    public DateTimeOffset AttemptedAt
    {
        get => _attemptedAt;
        set => _attemptedAt = value;
    }
}
