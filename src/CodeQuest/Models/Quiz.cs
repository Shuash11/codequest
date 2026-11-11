// A quiz with its questions. (Encapsulation)

namespace CodeQuest.Models;

public sealed class Quiz
{
    private int _quizId;
    private int _topicId;
    private string _title = string.Empty;
    private Difficulty _difficulty;
    private QuizStatus _status = QuizStatus.Draft;
    private DateTimeOffset _createdAt;
    private readonly List<Question> _questions = new();

    public int QuizId
    {
        get => _quizId;
        set => _quizId = value;
    }

    public int TopicId
    {
        get => _topicId;
        set => _topicId = value;
    }

    public string Title
    {
        get => _title;
        set => _title = value;
    }

    public Difficulty Difficulty
    {
        get => _difficulty;
        set => _difficulty = value;
    }

    public QuizStatus Status
    {
        get => _status;
        set => _status = value;
    }

    public DateTimeOffset CreatedAt
    {
        get => _createdAt;
        set => _createdAt = value;
    }

    public IReadOnlyList<Question> Questions => _questions;

    public void AddQuestion(Question question)
    {
        ArgumentNullException.ThrowIfNull(question);
        _questions.Add(question);
    }

    public Question? FindQuestion(int questionId)
        => _questions.FirstOrDefault(question => question.QuestionId == questionId);
}
