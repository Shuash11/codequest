// =============================================================================
// CodeQuest — Phase P3: Core OOP Domain Layer
// OOP rubric: ENCAPSULATION — the allowed values live inside the type itself.
// Look at: the three members below; nothing else can be a difficulty, because the
//          enum is the only way to express one.
// Schema:  quizzes.difficulty CHECK (difficulty IN ('Easy','Medium','Hard')).
// Note:    the member names are identical to the stored TEXT values, so
//          value.ToString() is the storage representation the data layer writes.
// =============================================================================

namespace CodeQuest.Models;

/// <summary>
/// The difficulty of a quiz (schema column <c>quizzes.difficulty</c>).
/// The three members mirror the schema CHECK constraint and the seeded values.
/// </summary>
public enum Difficulty
{
    /// <summary>Beginner level — matches the stored value <c>'Easy'</c>.</summary>
    Easy,

    /// <summary>Intermediate level — matches the stored value <c>'Medium'</c>.</summary>
    Medium,

    /// <summary>Advanced level — matches the stored value <c>'Hard'</c>.</summary>
    Hard,
}
