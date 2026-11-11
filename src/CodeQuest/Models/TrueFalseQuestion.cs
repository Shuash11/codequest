// A true-or-false question. (Inheritance, Polymorphism)

namespace CodeQuest.Models;

public sealed class TrueFalseQuestion : Question
{
    private const string TrueOptionText = "True";
    private const string FalseOptionText = "False";

    public override string QuestionKind => "True/False";

    public override int ExpectedChoiceCount => 2;

    public override bool ValidateAnswer(Choice? chosenChoice)
    {
        if (Choices.Count != ExpectedChoiceCount)
        {
            return false;
        }

        bool hasTrueOption = false;
        bool hasFalseOption = false;

        foreach (Choice choice in Choices)
        {
            string text = choice.ChoiceText.Trim();

            if (string.Equals(text, TrueOptionText, StringComparison.OrdinalIgnoreCase))
            {
                hasTrueOption = true;
            }
            else if (string.Equals(text, FalseOptionText, StringComparison.OrdinalIgnoreCase))
            {
                hasFalseOption = true;
            }
        }

        if (!hasTrueOption || !hasFalseOption)
        {
            return false;
        }

        return BelongsToThisQuestion(chosenChoice);
    }

    public override string FormatForDisplay()
        => $"True/False: {QuestionText}";
}
