// =============================================================================
// CodeQuest — Phase P3: Core OOP Domain Layer
// OOP rubric: ABSTRACTION — the contract of the user data access.
// Look at: IUserRepository below. It describes what the application needs from
//          the users table; the concrete ADO.NET implementation is written in
//          phase P4 against exactly this interface.
// Scope:   interface only — this folder holds no queries, no connection handling
//          and no database library reference (P4 fills it in).
// Table:   users (database/schema.sql)
// =============================================================================

using CodeQuest.Models;

namespace CodeQuest.Data;

/// <summary>
/// Reads and writes the accounts of the application (schema table <c>users</c>).
/// </summary>
public interface IUserRepository
{
    /// <summary>All accounts, ordered by name.</summary>
    IReadOnlyList<UserAccount> GetAll();

    /// <summary>One account by its id, or <c>null</c> when the id is unknown.</summary>
    UserAccount? GetById(int userId);

    /// <summary>One account by its login name, or <c>null</c> when the name is unknown.</summary>
    UserAccount? GetByUsername(string username);

    /// <summary>Stores a new account and returns its generated id.</summary>
    int Add(UserAccount user);

    /// <summary>Saves the changed values of an existing account.</summary>
    void Update(UserAccount user);

    /// <summary>
    /// Deletes an account. Returns false when no account has that id. A delete that
    /// the database refuses (the account still owns attempts) is not swallowed — the
    /// error travels to the caller, which shows a friendly message (P8).
    /// </summary>
    bool Delete(int userId);
}
