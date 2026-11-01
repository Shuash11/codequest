// =============================================================================
// CodeQuest — Phase P3: Core OOP Domain Layer
// OOP rubric: ABSTRACTION — the contract of the attempt data access.
// Look at: IAttemptRepository below. It describes what the application needs from
//          the quiz_attempts table; the concrete ADO.NET implementation is written
//          in phase P4 against exactly this interface.
// Scope:   interface only — this folder holds no queries, no connection handling
//          and no database library reference (P4 fills it in).
// Table:   quiz_attempts (database/schema.sql)
// =============================================================================

using CodeQuest.Models;

namespace CodeQuest.Data;

/// <summary>
/// Reads and writes the completed quiz runs (schema table <c>quiz_attempts</c>).
/// </summary>
public interface IAttemptRepository
{
    /// <summary>The attempts of one user, newest first — the history list (P10).</summary>
    IReadOnlyList<QuizAttempt> GetByUser(int userId);

    /// <summary>The attempts on one quiz, newest first.</summary>
    IReadOnlyList<QuizAttempt> GetByQuiz(int quizId);

    /// <summary>The attempts of one user on one quiz, newest first.</summary>
    IReadOnlyList<QuizAttempt> GetByUserAndQuiz(int userId, int quizId);

    /// <summary>One attempt by its id, or <c>null</c> when the id is unknown.</summary>
    QuizAttempt? GetById(int attemptId);

    /// <summary>Stores a new attempt and returns its generated id.</summary>
    int Add(QuizAttempt attempt);

    /// <summary>Saves the changed values of an existing attempt.</summary>
    void Update(QuizAttempt attempt);

    /// <summary>
    /// Deletes an attempt. Returns false when no attempt has that id. The answers of
    /// the attempt go with it, because the schema deletes them as well.
    /// </summary>
    bool Delete(int attemptId);
}
