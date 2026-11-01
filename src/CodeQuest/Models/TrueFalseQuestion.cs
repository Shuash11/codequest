// =============================================================================
// CodeQuest — Phase P3: Core OOP Domain Layer
// OOP rubric: INHERITANCE + POLYMORPHISM — a concrete question kind that inherits
//              the shared behaviour of Question and overrides what differs.
// Look at: the three "override" members below — QuestionKind, ExpectedChoiceCount
//          and ValidateAnswer — plus the refined display text.
// Base:    Question (abstract)
// =============================================================================

namespace CodeQuest.Models;

/// <summary>
/// A true/false question: one question, exactly two options ("True" and "False"),
/// one of them carries the answer key.
/// </summary>
public sealed class TrueFalseQuestion : Question
{
    private const string TrueOptionText = "True";
    private const string FalseOptionText = "False";

    /// <summary>The label shown in the UI for this question kind.</summary>
    public override string QuestionKind => "True/False";

    /// <summary>A complete true/false question has two options.</summary>
    public override int ExpectedChoiceCount => 2;

    /// <summary>
    /// A picked option is valid when the question carries its full option set, the two
    /// options are the "True" and "False" options, and the picked option belongs to
    /// this question.
    /// </summary>
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

    /// <summary>The display text of a true/false question.</summary>
    public override string FormatForDisplay()
        => $"True/False: {QuestionText}";
}
