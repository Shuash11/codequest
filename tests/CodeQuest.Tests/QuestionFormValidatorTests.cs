// =============================================================================
// CodeQuest — Phase P8: Topic & Quiz Management
// Tests for: the pure validation of the question form
// (CodeQuest.UI.QuestionFormValidator). The rules and messages are the design
// of record's (spec 5.5 form errors; R-2 #3 exactly-one-correct, R-2 #5
// duplicate choice text) — the same rules the P8 screen applies before any
// repository call, so a bad question never reaches the database. Pure logic:
// no database, no file system, no scratch state (the fresh-runner convention
// has nothing to provision here).
// =============================================================================

using CodeQuest.UI;

namespace CodeQuest.Tests;

/// <summary>
/// Phase P8 question-form validation tests: one rule per case, each hand-
/// checkable against the spec's message copy.
/// </summary>
public class QuestionFormValidatorTests
{
    private static readonly string[] FourChoices = { "A", "B", "C", "D" };

    [Fact]
    public void ValidForm_ReturnsNoIssue()
    {
        QuestionFormIssue? issue = QuestionFormValidator.Validate("What is 2 + 2?", FourChoices, correctChoiceIndex: 1);

        Assert.Null(issue);
    }

    [Fact]
    public void EmptyQuestionText_IsRejectedWithTheSpecMessage()
    {
        QuestionFormIssue? issue = QuestionFormValidator.Validate("   ", FourChoices, 0);

        Assert.NotNull(issue);
        Assert.Equal(QuestionFormField.QuestionText, issue.Field);
        Assert.Equal("Enter the question text.", issue.Message);
    }

    [Fact]
    public void NullQuestionText_IsRejectedWithTheSpecMessage()
    {
        QuestionFormIssue? issue = QuestionFormValidator.Validate(null, FourChoices, 0);

        Assert.NotNull(issue);
        Assert.Equal("Enter the question text.", issue.Message);
    }

    [Fact]
    public void BlankChoice_IsRejectedWithTheSpecMessageOnItsRow()
    {
        string[] choices = { "A", "  ", "C", "D" };

        QuestionFormIssue? issue = QuestionFormValidator.Validate("Question?", choices, 0);

        Assert.NotNull(issue);
        Assert.Equal(QuestionFormField.Choice, issue.Field);
        Assert.Equal(1, issue.ChoiceIndex);
        Assert.Equal("Fill in every choice.", issue.Message);
    }

    [Fact]
    public void NoMarkedCorrectChoice_IsRejectedWithTheSpecMessage()
    {
        QuestionFormIssue? issue = QuestionFormValidator.Validate("Question?", FourChoices, correctChoiceIndex: -1);

        Assert.NotNull(issue);
        Assert.Equal(QuestionFormField.CorrectMark, issue.Field);
        Assert.Equal("Mark exactly one correct answer.", issue.Message);
    }

    [Fact]
    public void CorrectIndexBeyondTheChoices_IsRejectedLikeNoMark()
    {
        QuestionFormIssue? issue = QuestionFormValidator.Validate("Question?", FourChoices, correctChoiceIndex: 4);

        Assert.NotNull(issue);
        Assert.Equal("Mark exactly one correct answer.", issue.Message);
    }

    [Fact]
    public void DuplicateChoiceText_IsRejectedWithTheSpecMessageOnTheSecondRow()
    {
        string[] choices = { "const", "let", "const", "var" };

        QuestionFormIssue? issue = QuestionFormValidator.Validate("Question?", choices, 0);

        Assert.NotNull(issue);
        Assert.Equal(QuestionFormField.Choice, issue.Field);
        Assert.Equal(2, issue.ChoiceIndex);
        Assert.Equal("This choice text is already used for this question.", issue.Message);
    }

    [Fact]
    public void DuplicateCheck_MatchesTheDatabaseOrdinalRule()
    {
        // The schema's UNIQUE (question_id, choice_text) is binary: "True"
        // and "true" are distinct rows, so the pre-check must not reject them.
        string[] choices = { "True", "true", "Maybe", "Never" };

        QuestionFormIssue? issue = QuestionFormValidator.Validate("Question?", choices, 0);

        Assert.Null(issue);
    }

    [Fact]
    public void TrimmedValues_AreWhatTheRulesCheck()
    {
        // The editor saves trimmed texts, so trimming two rows into the same
        // text is a duplicate.
        string[] choices = { "Answer", "  Answer  ", "Other", "Last" };

        QuestionFormIssue? issue = QuestionFormValidator.Validate("Question?", choices, 0);

        Assert.NotNull(issue);
        Assert.Equal("This choice text is already used for this question.", issue.Message);
    }

    [Fact]
    public void TrueFalsePair_WithOneMarked_IsValid()
    {
        string[] choices = { "True", "False" };

        QuestionFormIssue? issue = QuestionFormValidator.Validate("The sky is blue.", choices, correctChoiceIndex: 0);

        Assert.Null(issue);
    }

    [Fact]
    public void NoChoicesAtAll_IsRejectedAsACorrectMarkProblem()
    {
        QuestionFormIssue? issue = QuestionFormValidator.Validate("Question?", Array.Empty<string>(), -1);

        Assert.NotNull(issue);
        Assert.Equal("Mark exactly one correct answer.", issue.Message);
    }
}
