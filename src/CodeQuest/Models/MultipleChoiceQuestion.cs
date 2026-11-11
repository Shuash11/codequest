// A question with four options. (Inheritance, Polymorphism)

namespace CodeQuest.Models;

public sealed class MultipleChoiceQuestion : Question
{
    public override string QuestionKind => "Multiple Choice";

    public override int ExpectedChoiceCount => 4;

    public override bool ValidateAnswer(Choice? chosenChoice)
    {
        if (Choices.Count != ExpectedChoiceCount)
        {
            return false;
        }

        return BelongsToThisQuestion(chosenChoice);
    }

    public override string FormatForDisplay()
        => $"Multiple Choice: {QuestionText} ({Choices.Count} options)";
}
