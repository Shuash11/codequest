// =============================================================================
// CodeQuest — Phase P3: Core OOP Domain Layer
// OOP rubric: ABSTRACTION + INHERITANCE — an abstract base class that fixes what
//              every question must offer without saying how it behaves.
// Look at: the abstract members (QuestionKind, ExpectedChoiceCount,
//          ValidateAnswer) — they are implemented by MultipleChoiceQuestion and
//          TrueFalseQuestion; and the shared, concrete helpers at the bottom
//          (GetCorrectChoice, IsCorrectChoice, FormatForDisplay).
// Table:   questions (database/schema.sql) — columns: question_id, quiz_id,
//          question_text, explanation, position, created_at.
//          The options of a question are the child rows of the choices table and
//          are held in a private list, published only as a read-only view.
// =============================================================================

namespace CodeQuest.Models;

/// <summary>
/// Base class of every quiz question (schema table <c>questions</c>).
/// It is abstract on purpose: a question is always one concrete kind, and each
/// kind validates an answer in its own way (polymorphism).
/// </summary>
public abstract class Question
{
    private int _questionId;
    private int _quizId;
    private string _questionText = string.Empty;
    private string _explanation = string.Empty;
    private int _position;
    private DateTimeOffset _createdAt;
    private readonly List<Choice> _choices = new();

    /// <summary>Primary key of the question (schema column <c>question_id</c>).</summary>
    public int QuestionId
    {
        get => _questionId;
        set => _questionId = value;
    }

    /// <summary>Owning quiz (schema column <c>quiz_id</c>).</summary>
    public int QuizId
    {
        get => _quizId;
        set => _quizId = value;
    }

    /// <summary>The question as shown to the student (schema column <c>question_text</c>).</summary>
    public string QuestionText
    {
        get => _questionText;
        set => _questionText = value;
    }

    /// <summary>
    /// Why the answer is right — shown in the answer review (P10) and in the review
    /// screen (P13). The schema requires a value; the editor supplies an empty
    /// string when there is none (plan ruling R-2 #6).
    /// </summary>
    public string Explanation
    {
        get => _explanation;
        set => _explanation = value;
    }

    /// <summary>Display order inside the quiz, unique per quiz (schema column <c>position</c>).</summary>
    public int Position
    {
        get => _position;
        set => _position = value;
    }

    /// <summary>
    /// Creation timestamp (schema column <c>created_at</c>). The schema stores ISO-8601 UTC
    /// text; the data layer (P4) writes and reads the format <c>yyyy-MM-ddTHH:mm:ssZ</c>.
    /// </summary>
    public DateTimeOffset CreatedAt
    {
        get => _createdAt;
        set => _createdAt = value;
    }

    /// <summary>
    /// The options of this question, read-only. The list itself stays private so that
    /// the only way to add an option is <see cref="AddChoice"/> (encapsulation).
    /// </summary>
    public IReadOnlyList<Choice> Choices => _choices;

    /// <summary>Adds one option to this question (a row of the <c>choices</c> table).</summary>
    public void AddChoice(Choice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        _choices.Add(choice);
    }

    /// <summary>
    /// The kind of question, used for display — "Multiple Choice" or "True/False".
    /// Each derived class answers for itself (polymorphism).
    /// </summary>
    public abstract string QuestionKind { get; }

    /// <summary>
    /// How many options a complete question of this kind has — 4 for a multiple choice
    /// question, 2 for a true/false question.
    /// </summary>
    public abstract int ExpectedChoiceCount { get; }

    /// <summary>
    /// Checks whether the picked option is a valid answer for this question.
    /// Each derived class applies its own rules (polymorphism).
    /// </summary>
    public abstract bool ValidateAnswer(Choice? chosenChoice);

    /// <summary>
    /// One-line display text of the question. The base version uses
    /// <see cref="QuestionKind"/>; the derived classes refine the wording.
    /// </summary>
    public virtual string FormatForDisplay() => $"[{QuestionKind}] {QuestionText}";

    /// <summary>The option that carries the answer key, or <c>null</c> when the key is missing.</summary>
    public Choice? GetCorrectChoice() => _choices.FirstOrDefault(choice => choice.IsCorrect);

    /// <summary>Whether the given option is the option marked correct in the answer key.</summary>
    public bool IsCorrectChoice(Choice? chosenChoice)
        => chosenChoice is not null && BelongsToThisQuestion(chosenChoice) && chosenChoice.IsCorrect;

    /// <summary>
    /// Whether the given option belongs to this question. The same rule is enforced by
    /// the composite foreign key of the <c>submitted_answers</c> table.
    /// </summary>
    protected bool BelongsToThisQuestion(Choice? option)
    {
        if (option is null)
        {
            return false;
        }

        // In-memory objects: the option was added to this very list.
        if (_choices.Contains(option))
        {
            return true;
        }

        // Objects loaded from the database: compare the foreign keys.
        return option.QuestionId != 0 && option.QuestionId == QuestionId;
    }
}
