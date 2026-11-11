// The answers a player picked.

namespace CodeQuest.Models;

public sealed class SubmittedAnswer
{
    private int _attemptId;
    private int _questionId;
    private int _chosenChoiceId;

    public int AttemptId
    {
        get => _attemptId;
        set => _attemptId = value;
    }

    public int QuestionId
    {
        get => _questionId;
        set => _questionId = value;
    }

    public int ChosenChoiceId
    {
        get => _chosenChoiceId;
        set => _chosenChoiceId = value;
    }
}
