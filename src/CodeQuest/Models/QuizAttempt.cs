// One finished quiz attempt.

namespace CodeQuest.Models;

public sealed class QuizAttempt
{
    private int _attemptId;
    private int _quizId;
    private int _userId;
    private int _score;
    private DateTimeOffset _attemptedAt;

    public int AttemptId
    {
        get => _attemptId;
        set => _attemptId = value;
    }

    public int QuizId
    {
        get => _quizId;
        set => _quizId = value;
    }

    public int UserId
    {
        get => _userId;
        set => _userId = value;
    }

    public int Score
    {
        get => _score;
        set => _score = value;
    }

    public DateTimeOffset AttemptedAt
    {
        get => _attemptedAt;
        set => _attemptedAt = value;
    }
}
