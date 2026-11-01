// =============================================================================
// CodeQuest — Phase P3: Core OOP Domain Layer
// OOP rubric: ABSTRACTION — the contract of the question data access.
// Look at: IQuestionRepository below. It describes what the application needs from
//          the questions table; the concrete ADO.NET implementation is written in
//          phase P4 against exactly this interface.
// Scope:   interface only — this folder holds no queries, no connection handling
//          and no database library reference (P4 fills it in).
// Table:   questions (database/schema.sql) — with the options of the choices table.
// =============================================================================

using CodeQuest.Models;

namespace CodeQuest.Data;

/// <summary>
/// Reads and writes the questions of a quiz (schema table <c>questions</c>).
/// </summary>
public interface IQuestionRepository
{
    /// <summary>The questions of one quiz, including their options, in display order.</summary>
    IReadOnlyList<Question> GetByQuiz(int quizId);

    /// <summary>One question by its id, including its options, or <c>null</c> when the id is unknown.</summary>
    Question? GetById(int questionId);

    /// <summary>
    /// Stores a new question with its options in one transaction and returns its
    /// generated id.
    /// </summary>
    int Add(Question question);

    /// <summary>Saves the changed values of an existing question.</summary>
    void Update(Question question);

    /// <summary>Deletes a question. Returns false when no question has that id.</summary>
    bool Delete(int questionId);

    /// <summary>
    /// Moves the questions of a quiz into a new order. The positions are unique per
    /// quiz, so the write happens in one transaction (plan ruling R-2 #8).
    /// </summary>
    /// <param name="quizId">The quiz whose questions are moved.</param>
    /// <param name="questionIdsInOrder">The question ids, first one first.</param>
    void Reorder(int quizId, IReadOnlyList<int> questionIdsInOrder);
}
