// =============================================================================
// CodeQuest — Phase P3: Core OOP Domain Layer
// OOP rubric: ABSTRACTION — the contract of the answer data access.
// Look at: ISubmittedAnswerRepository below. It describes what the application
//          needs from the submitted_answers table; the concrete ADO.NET
//          implementation is written in phase P4 against exactly this interface.
// Scope:   interface only — this folder holds no queries, no connection handling
//          and no database library reference (P4 fills it in).
// Table:   submitted_answers (database/schema.sql)
// =============================================================================

using CodeQuest.Models;

namespace CodeQuest.Data;

/// <summary>
/// Reads and writes the answers a student picked
/// (schema table <c>submitted_answers</c>).
/// </summary>
public interface ISubmittedAnswerRepository
{
    /// <summary>The answers of one attempt — the answer review of the history screen (P10).</summary>
    IReadOnlyList<SubmittedAnswer> GetByAttempt(int attemptId);

    /// <summary>The answers of one question over all attempts.</summary>
    IReadOnlyList<SubmittedAnswer> GetByQuestion(int questionId);

    /// <summary>
    /// The answer of one attempt to one question, or <c>null</c> when that question
    /// was not answered in that attempt.
    /// </summary>
    SubmittedAnswer? GetByAttemptAndQuestion(int attemptId, int questionId);

    /// <summary>Stores a new answer.</summary>
    int Add(SubmittedAnswer submittedAnswer);

    /// <summary>Deletes one answer. Returns false when no such answer exists.</summary>
    bool Delete(int attemptId, int questionId);

    /// <summary>Deletes every answer of one attempt.</summary>
    void DeleteByAttempt(int attemptId);
}
