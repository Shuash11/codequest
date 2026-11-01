// =============================================================================
// CodeQuest — Phase P3: Core OOP Domain Layer
// OOP rubric: ABSTRACTION — the contract of the quiz data access.
// Look at: IQuizRepository below. It describes what the application needs from
//          the quizzes table; the concrete ADO.NET implementation is written in
//          phase P4 against exactly this interface.
// Scope:   interface only — this folder holds no queries, no connection handling
//          and no database library reference (P4 fills it in).
// Table:   quizzes (database/schema.sql) — together with its questions and their
//          options, which are the child rows of the questions and choices tables.
// =============================================================================

using CodeQuest.Models;

namespace CodeQuest.Data;

/// <summary>
/// Reads and writes the quizzes of the application (schema table <c>quizzes</c>).
/// </summary>
public interface IQuizRepository
{
    /// <summary>All quizzes, ordered by creation date.</summary>
    IReadOnlyList<Quiz> GetAll();

    /// <summary>The quizzes of one topic.</summary>
    IReadOnlyList<Quiz> GetByTopic(int topicId);

    /// <summary>The quizzes with one review status — for example every draft waiting for review (P13).</summary>
    IReadOnlyList<Quiz> GetByStatus(QuizStatus status);

    /// <summary>The quizzes of one topic that have one review status.</summary>
    IReadOnlyList<Quiz> GetByTopicAndStatus(int topicId, QuizStatus status);

    /// <summary>
    /// One quiz by its id, including its questions and their options, or <c>null</c>
    /// when the id is unknown.
    /// </summary>
    Quiz? GetById(int quizId);

    /// <summary>
    /// Stores a new quiz with its questions and options in one transaction and
    /// returns its generated id.
    /// </summary>
    int Add(Quiz quiz);

    /// <summary>Saves the changed values of an existing quiz.</summary>
    void Update(Quiz quiz);

    /// <summary>
    /// Deletes a quiz. Returns false when no quiz has that id. A delete that the
    /// database refuses (the quiz already owns attempts) is not swallowed — the
    /// error travels to the caller, which shows a friendly message (P8).
    /// </summary>
    bool Delete(int quizId);

    /// <summary>
    /// Whether this topic already holds a quiz with this title — the duplicate
    /// check of the quiz form, because the title is unique per topic
    /// (plan ruling R-2 #5).
    /// </summary>
    bool ExistsByTitle(int topicId, string title, int? ignoreQuizId = null);
}
