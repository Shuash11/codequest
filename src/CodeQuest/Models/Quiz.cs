// =============================================================================
// CodeQuest — Phase P3: Core OOP Domain Layer
// OOP rubric: ENCAPSULATION — private fields, public properties.
// Look at: the private fields below and the public properties that expose them;
//          the questions of the quiz (and their options) are held in a private
//          list and are published only as a read-only view.
// Table:   quizzes (database/schema.sql) — columns: quiz_id, topic_id, title,
//          difficulty, status, created_at.
//          The questions are the child rows of the questions table; the status
//          follows the review workflow Draft -> Approved (plan ruling R-2 #4).
// =============================================================================

namespace CodeQuest.Models;

/// <summary>
/// A quiz header with its questions (schema table <c>quizzes</c>).
/// </summary>
public sealed class Quiz
{
    private int _quizId;
    private int _topicId;
    private string _title = string.Empty;
    private Difficulty _difficulty;
    private QuizStatus _status = QuizStatus.Draft;
    private DateTimeOffset _createdAt;
    private readonly List<Question> _questions = new();

    /// <summary>Primary key of the quiz (schema column <c>quiz_id</c>).</summary>
    public int QuizId
    {
        get => _quizId;
        set => _quizId = value;
    }

    /// <summary>Owning topic (schema column <c>topic_id</c>).</summary>
    public int TopicId
    {
        get => _topicId;
        set => _topicId = value;
    }

    /// <summary>Quiz title, unique per topic (schema column <c>title</c>).</summary>
    public string Title
    {
        get => _title;
        set => _title = value;
    }

    /// <summary>Quiz level (schema column <c>difficulty</c>).</summary>
    public Difficulty Difficulty
    {
        get => _difficulty;
        set => _difficulty = value;
    }

    /// <summary>
    /// Review status (schema column <c>status</c>). A generated quiz starts as
    /// <see cref="QuizStatus.Draft"/> and becomes takeable only after the reviewer
    /// approves it.
    /// </summary>
    public QuizStatus Status
    {
        get => _status;
        set => _status = value;
    }

    /// <summary>
    /// Creation timestamp (schema column <c>created_at</c>). The schema stores ISO-8601 UTC
    /// text; the data layer (P4) writes and reads the format <c>yyyy-MM-ddTHH:mm:ssZ</c>.
    /// </summary>
    public DateTimeOffset CreatedAt
    {
        get => _createdAt;
        set => _createdAt = value;
    }

    /// <summary>
    /// The questions of this quiz, read-only. The list itself stays private so that the
    /// only way to add a question is <see cref="AddQuestion"/> (encapsulation).
    /// </summary>
    public IReadOnlyList<Question> Questions => _questions;

    /// <summary>Adds one question to this quiz (a row of the <c>questions</c> table).</summary>
    public void AddQuestion(Question question)
    {
        ArgumentNullException.ThrowIfNull(question);
        _questions.Add(question);
    }

    /// <summary>Finds one question of this quiz by its id, or <c>null</c> when it is unknown.</summary>
    public Question? FindQuestion(int questionId)
        => _questions.FirstOrDefault(question => question.QuestionId == questionId);
}
