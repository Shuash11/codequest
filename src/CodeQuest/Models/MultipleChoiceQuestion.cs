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
/// A multiple choice question: one question, exactly four options, one of them
/// carries the answer key.
/// </summary>
public sealed class MultipleChoiceQuestion : Question
{
    /// <summary>The label shown in the UI for this question kind.</summary>
    public override string QuestionKind => "Multiple Choice";

    /// <summary>A complete multiple choice question has four options.</summary>
    public override int ExpectedChoiceCount => 4;

    /// <summary>
    /// A picked option is valid when the question carries its full option set and the
    /// option really belongs to this question.
    /// </summary>
    public override bool ValidateAnswer(Choice? chosenChoice)
    {
        if (Choices.Count != ExpectedChoiceCount)
        {
            return false;
        }

        return BelongsToThisQuestion(chosenChoice);
    }

    /// <summary>The display text of a multiple choice question.</summary>
    public override string FormatForDisplay()
        => $"Multiple Choice: {QuestionText} ({Choices.Count} options)";
}
