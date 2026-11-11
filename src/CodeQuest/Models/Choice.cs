// One answer option. (Encapsulation)

namespace CodeQuest.Models;

public sealed class Choice
{
    private int _choiceId;
    private int _questionId;
    private string _choiceText = string.Empty;
    private bool _isCorrect;
    private int _position;

    public int ChoiceId
    {
        get => _choiceId;
        set => _choiceId = value;
    }

    public int QuestionId
    {
        get => _questionId;
        set => _questionId = value;
    }

    public string ChoiceText
    {
        get => _choiceText;
        set => _choiceText = value;
    }

    public bool IsCorrect
    {
        get => _isCorrect;
        set => _isCorrect = value;
    }

    public int Position
    {
        get => _position;
        set => _position = value;
    }
}
