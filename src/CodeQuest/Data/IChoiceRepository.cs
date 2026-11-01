// =============================================================================
// CodeQuest — Phase P3: Core OOP Domain Layer
// OOP rubric: ABSTRACTION — the contract of the option data access.
// Look at: IChoiceRepository below. It describes what the application needs from
//          the choices table; the concrete ADO.NET implementation is written in
//          phase P4 against exactly this interface.
// Scope:   interface only — this folder holds no queries, no connection handling
//          and no database library reference (P4 fills it in).
// Table:   choices (database/schema.sql)
// =============================================================================

using CodeQuest.Models;

namespace CodeQuest.Data;

/// <summary>
/// Reads and writes the answer options of a question (schema table <c>choices</c>).
/// </summary>
public interface IChoiceRepository
{
    /// <summary>The options of one question, in display order.</summary>
    IReadOnlyList<Choice> GetByQuestion(int questionId);

    /// <summary>One option by its id, or <c>null</c> when the id is unknown.</summary>
    Choice? GetById(int choiceId);

    /// <summary>Stores a new option and returns its generated id.</summary>
    int Add(Choice choice);

    /// <summary>Saves the changed values of an existing option.</summary>
    void Update(Choice choice);

    /// <summary>Deletes an option. Returns false when no option has that id.</summary>
    bool Delete(int choiceId);
}
