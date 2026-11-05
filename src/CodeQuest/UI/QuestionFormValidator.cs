// =============================================================================
// CodeQuest — Phase P8: Topic & Quiz Management
// The pure validation of the question form (spec 5.5) — no UI types, so the
// rules are unit-testable on their own (plan: "xUnit unit tests for pure
// logic (scoring, validation)"). The messages are the design of record's
// exact copy (§5.5 form errors), and the rules are the schema's:
//   * the question text is required            → "Enter the question text."
//   * every choice needs text                  → "Fill in every choice."
//   * exactly one correct choice (R-2 #3)      → "Mark exactly one correct
//                                                answer."
//   * choice text unique per question (R-2 #5) → "This choice text is already
//                                                used for this question."
// The empty/duplicate guards run BEFORE the repository call (the F8 pattern):
// bad input is rejected with a specific message and never reaches the DB.
// =============================================================================

namespace CodeQuest.UI;

/// <summary>Which field of the question form an issue belongs to.</summary>
public enum QuestionFormField
{
    /// <summary>The question text field.</summary>
    QuestionText,

    /// <summary>One choice row — <see cref="QuestionFormIssue.ChoiceIndex"/> says which.</summary>
    Choice,

    /// <summary>The correct-answer marks as a block (the checkboxes of the choice rows).</summary>
    CorrectMark,
}

/// <summary>One failed rule of the question form: the message and where it belongs.</summary>
public sealed record QuestionFormIssue(string Message, QuestionFormField Field, int ChoiceIndex = -1);

/// <summary>
/// The validation rules of the question form, as pure functions over the
/// form's values.
/// </summary>
public static class QuestionFormValidator
{
    /// <summary>The design of record's exact message copy (spec 5.5).</summary>
    public const string EmptyQuestionTextMessage = "Enter the question text.";
    public const string EmptyChoiceMessage = "Fill in every choice.";
    public const string NoCorrectChoiceMessage = "Mark exactly one correct answer.";
    public const string DuplicateChoiceMessage = "This choice text is already used for this question.";

    /// <summary>
    /// Validates one question form's values. Returns null when the question
    /// can be saved; otherwise the first failed rule in the spec's order.
    /// </summary>
    /// <param name="questionText">The entered question text.</param>
    /// <param name="choiceTexts">The entered choice texts, top row first.</param>
    /// <param name="correctChoiceIndex">The marked correct row, or -1 when none is marked.</param>
    /// <returns>The first issue, or null when the values are valid.</returns>
    public static QuestionFormIssue? Validate(
        string? questionText,
        IReadOnlyList<string?> choiceTexts,
        int correctChoiceIndex)
    {
        ArgumentNullException.ThrowIfNull(choiceTexts);

        if (choiceTexts.Count == 0)
        {
            return new QuestionFormIssue(NoCorrectChoiceMessage, QuestionFormField.CorrectMark);
        }

        if (string.IsNullOrWhiteSpace(questionText))
        {
            return new QuestionFormIssue(EmptyQuestionTextMessage, QuestionFormField.QuestionText);
        }

        // The trimmed texts are what the editor saves, so they are what the
        // rules check.
        string[] trimmedTexts = new string[choiceTexts.Count];

        for (int i = 0; i < choiceTexts.Count; i++)
        {
            trimmedTexts[i] = choiceTexts[i]?.Trim() ?? string.Empty;

            if (trimmedTexts[i].Length == 0)
            {
                return new QuestionFormIssue(EmptyChoiceMessage, QuestionFormField.Choice, i);
            }
        }

        // R-2 #3: a question cannot be saved with zero or multiple correct
        // choices — the form marks exactly one row, so only "none marked"
        // can fail here.
        if (correctChoiceIndex < 0 || correctChoiceIndex >= choiceTexts.Count)
        {
            return new QuestionFormIssue(NoCorrectChoiceMessage, QuestionFormField.CorrectMark);
        }

        // R-2 #5: the schema keeps (question_id, choice_text) UNIQUE. The
        // comparison is ordinal — the same rule the database applies.
        for (int i = 0; i < trimmedTexts.Length; i++)
        {
            for (int j = i + 1; j < trimmedTexts.Length; j++)
            {
                if (string.Equals(trimmedTexts[i], trimmedTexts[j], StringComparison.Ordinal))
                {
                    return new QuestionFormIssue(DuplicateChoiceMessage, QuestionFormField.Choice, j);
                }
            }
        }

        return null;
    }
}
