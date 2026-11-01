// =============================================================================
// CodeQuest — Phase P3: Core OOP Domain Layer
// OOP rubric: ENCAPSULATION — private fields, public properties.
// Look at: the private fields below and the public properties that expose them;
//          no public mutable field exists anywhere in this class.
// Table:   submitted_answers (database/schema.sql) — columns: attempt_id,
//          question_id, chosen_choice_id (composite primary key).
// Note:    R-2 #1 — there is deliberately NO correctness flag on this model. Whether
//          an answer was right is derived by joining the choices of the answered
//          question; the stored rows only say what the student picked.
// =============================================================================

namespace CodeQuest.Models;

/// <summary>
/// What the student picked for one question of one attempt
/// (schema table <c>submitted_answers</c>).
/// </summary>
public sealed class SubmittedAnswer
{
    private int _attemptId;
    private int _questionId;
    private int _chosenChoiceId;

    /// <summary>The attempt this answer belongs to (schema column <c>attempt_id</c>).</summary>
    public int AttemptId
    {
        get => _attemptId;
        set => _attemptId = value;
    }

    /// <summary>The answered question (schema column <c>question_id</c>).</summary>
    public int QuestionId
    {
        get => _questionId;
        set => _questionId = value;
    }

    /// <summary>
    /// The picked option (schema column <c>chosen_choice_id</c>). The schema's
    /// composite foreign key proves that this option really belongs to the answered
    /// question.
    /// </summary>
    public int ChosenChoiceId
    {
        get => _chosenChoiceId;
        set => _chosenChoiceId = value;
    }
}
