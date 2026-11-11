// The base of every question. (Inheritance, Abstraction, Encapsulation)

namespace CodeQuest.Models;

public abstract class Question
{
    private int _questionId;
    private int _quizId;
    private string _questionText = string.Empty;
    private string _explanation = string.Empty;
    private int _position;
    private DateTimeOffset _createdAt;
    private Difficulty _difficulty = Difficulty.Medium;
    private readonly List<Choice> _choices = new();

    public int QuestionId
    {
        get => _questionId;
        set => _questionId = value;
    }

    public int QuizId
    {
        get => _quizId;
        set => _quizId = value;
    }

    public string QuestionText
    {
        get => _questionText;
        set => _questionText = value;
    }

    public string Explanation
    {
        get => _explanation;
        set => _explanation = value;
    }

    public int Position
    {
        get => _position;
        set => _position = value;
    }

    public DateTimeOffset CreatedAt
    {
        get => _createdAt;
        set => _createdAt = value;
    }

    public Difficulty Difficulty
    {
        get => _difficulty;
        set => _difficulty = value;
    }

    public IReadOnlyList<Choice> Choices => _choices;

    public void AddChoice(Choice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        _choices.Add(choice);
    }

    public abstract string QuestionKind { get; }

    public abstract int ExpectedChoiceCount { get; }

    public abstract bool ValidateAnswer(Choice? chosenChoice);

    public virtual string FormatForDisplay() => $"[{QuestionKind}] {QuestionText}";

    // Reorders the choices for a run and reassigns their display positions.
    public void ShuffleChoices(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        for (int index = _choices.Count - 1; index > 0; index--)
        {
            int swapIndex = random.Next(index + 1);
            (_choices[index], _choices[swapIndex]) = (_choices[swapIndex], _choices[index]);
        }

        for (int index = 0; index < _choices.Count; index++)
        {
            _choices[index].Position = index + 1;
        }
    }

    public Choice? GetCorrectChoice() => _choices.FirstOrDefault(choice => choice.IsCorrect);

    public bool IsCorrectChoice(Choice? chosenChoice)
        => chosenChoice is not null && BelongsToThisQuestion(chosenChoice) && chosenChoice.IsCorrect;

    protected bool BelongsToThisQuestion(Choice? option)
    {
        if (option is null)
        {
            return false;
        }

        if (_choices.Contains(option))
        {
            return true;
        }

        return option.QuestionId != 0 && option.QuestionId == QuestionId;
    }
}
