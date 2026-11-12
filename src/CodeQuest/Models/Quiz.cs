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

    // Ownership (task 5): the AI rival stamps the player it generated this
    // level for; built-in campaign levels and the Random Mix stay unowned
    // (null) and are visible to everyone.
    public int? GeneratedForUserId
    {
        get => _generatedForUserId;
        set => _generatedForUserId = value;
    }
    private int? _generatedForUserId;

    public IReadOnlyList<Question> Questions => _questions;

    public void AddQuestion(Question question)
    {
        ArgumentNullException.ThrowIfNull(question);
        _questions.Add(question);
    }

    public Question? FindQuestion(int questionId)
        => _questions.FirstOrDefault(question => question.QuestionId == questionId);

    // Orders the questions for a run: easier tiers first, shuffled within each tier.
    public void OrderQuestionsForRun(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        List<Question> ordered = _questions
            .GroupBy(question => TierRank(question.Difficulty))
            .OrderBy(group => group.Key)
            .SelectMany(group => Shuffle(group.ToList(), random))
            .ToList();

        _questions.Clear();
        _questions.AddRange(ordered);

        for (int index = 0; index < _questions.Count; index++)
        {
            _questions[index].Position = index + 1;
        }
    }

    private static int TierRank(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => 0,
        Difficulty.Medium => 1,
        Difficulty.Hard => 2,
        Difficulty.Insane => 3,
        _ => 4,
    };

    private static List<Question> Shuffle(List<Question> questions, Random random)
    {
        for (int index = questions.Count - 1; index > 0; index--)
        {
            int swapIndex = random.Next(index + 1);
            (questions[index], questions[swapIndex]) = (questions[swapIndex], questions[index]);
        }

        return questions;
    }
}
