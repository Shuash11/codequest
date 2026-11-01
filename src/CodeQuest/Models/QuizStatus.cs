// =============================================================================
// CodeQuest — Phase P3: Core OOP Domain Layer
// OOP rubric: ENCAPSULATION — the allowed values live inside the type itself.
// Look at: the two members below; a quiz can only be in one of these two states,
//          so an invalid status cannot be expressed at all.
// Schema:  quizzes.status CHECK (status IN ('Draft','Approved')) — exactly two
//          values; this enum must never grow a third member.
// Note:    the member names are identical to the stored TEXT values, so
//          value.ToString() is the storage representation the data layer writes.
// =============================================================================

namespace CodeQuest.Models;

/// <summary>
/// The review status of a quiz (schema column <c>quizzes.status</c>).
/// A generated quiz is born <see cref="Draft"/> and only the review screen (P13)
/// flips it to <see cref="Approved"/>; only approved quizzes are takeable.
/// </summary>
public enum QuizStatus
{
    /// <summary>Not reviewed yet — never takeable by a student (stored value <c>'Draft'</c>).</summary>
    Draft,

    /// <summary>Answer key reviewed — the quiz is takeable (stored value <c>'Approved'</c>).</summary>
    Approved,
}
