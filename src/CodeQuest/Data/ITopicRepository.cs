// =============================================================================
// CodeQuest — Phase P3: Core OOP Domain Layer
// OOP rubric: ABSTRACTION — the contract of the topic data access.
// Look at: ITopicRepository below. It describes what the application needs from
//          the topics table; the concrete ADO.NET implementation is written in
//          phase P4 against exactly this interface.
// Scope:   interface only — this folder holds no queries, no connection handling
//          and no database library reference (P4 fills it in).
// Table:   topics (database/schema.sql)
// =============================================================================

using CodeQuest.Models;

namespace CodeQuest.Data;

/// <summary>
/// Reads and writes the subject areas of the application (schema table <c>topics</c>).
/// </summary>
public interface ITopicRepository
{
    /// <summary>All topics, ordered by name.</summary>
    IReadOnlyList<Topic> GetAll();

    /// <summary>The topics of one category — the reporting groups of the schema.</summary>
    IReadOnlyList<Topic> GetByCategory(string category);

    /// <summary>
    /// The topics whose name or description contains the search text — the live
    /// search of the management screen (P8).
    /// </summary>
    IReadOnlyList<Topic> Search(string searchText);

    /// <summary>One topic by its id, or <c>null</c> when the id is unknown.</summary>
    Topic? GetById(int topicId);

    /// <summary>Stores a new topic and returns its generated id.</summary>
    int Add(Topic topic);

    /// <summary>Saves the changed values of an existing topic.</summary>
    void Update(Topic topic);

    /// <summary>
    /// Deletes a topic. Returns false when no topic has that id. A delete that the
    /// database refuses (the topic still owns quizzes) is not swallowed — the error
    /// travels to the caller, which shows a friendly message (P8).
    /// </summary>
    bool Delete(int topicId);

    /// <summary>
    /// Whether a topic with this name already exists — the duplicate check of the
    /// topic form, because the name is unique in the schema (plan ruling R-2 #5).
    /// </summary>
    bool ExistsByName(string name, int? ignoreTopicId = null);
}
