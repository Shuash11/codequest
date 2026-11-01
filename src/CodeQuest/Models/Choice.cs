// =============================================================================
// CodeQuest — Phase P3: Core OOP Domain Layer
// OOP rubric: ENCAPSULATION — private fields, public properties.
// Look at: the private fields below and the public properties that expose them;
//          the child rows of the quiz (questions, and their choices) are held in a
//          private list and are published only as a read-only view.
// Table:   choices (database/schema.sql) — columns: choice_id, question_id,
//          choice_text, is_correct, position.
// =============================================================================

namespace CodeQuest.Models;

/// <summary>
/// One answer option of a question (schema table <c>choices</c>).
/// The answer key lives here and not on the submitted answers: correctness is a
/// property of the question, not of one student's pick (plan ruling R-2 #1).
/// </summary>
public sealed class Choice
{
    private int _choiceId;
    private int _questionId;
    private string _choiceText = string.Empty;
    private bool _isCorrect;
    private int _position;

    /// <summary>Primary key of the option (schema column <c>choice_id</c>).</summary>
    public int ChoiceId
    {
        get => _choiceId;
        set => _choiceId = value;
    }

    /// <summary>
    /// Owning question (schema column <c>question_id</c>). Together with
    /// <see cref="ChoiceId"/> it forms the composite key the submitted answers point at.
    /// </summary>
    public int QuestionId
    {
        get => _questionId;
        set => _questionId = value;
    }

    /// <summary>Text of the option (schema column <c>choice_text</c>).</summary>
    public string ChoiceText
    {
        get => _choiceText;
        set => _choiceText = value;
    }

    /// <summary>
    /// The answer key (schema column <c>is_correct</c>, stored as 0/1 in the database).
    /// Exactly one option of a question carries <c>true</c>.
    /// </summary>
    public bool IsCorrect
    {
        get => _isCorrect;
        set => _isCorrect = value;
    }

    /// <summary>Display order inside the question (schema column <c>position</c>).</summary>
    public int Position
    {
        get => _position;
        set => _position = value;
    }
}
