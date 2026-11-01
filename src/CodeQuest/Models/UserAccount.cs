// =============================================================================
// CodeQuest — Phase P3: Core OOP Domain Layer
// OOP rubric: ENCAPSULATION — private fields, public properties.
// Look at: the private fields below and the public properties that expose them;
//          no public mutable field exists anywhere in this class.
// Table:   users (database/schema.sql) — columns: user_id, username,
//          password_hash, display_name, created_at.
// =============================================================================

namespace CodeQuest.Models;

/// <summary>
/// One local account of the application (schema table <c>users</c>).
/// The plaintext password is never part of this object — only the stored hash
/// (<c>pbkdf2-sha256:<iterations>:<salt-b64>:<hash-b64></c>) is mapped.
/// </summary>
public sealed class UserAccount
{
    private int _userId;
    private string _username = string.Empty;
    private string _passwordHash = string.Empty;
    private string _displayName = string.Empty;
    private DateTimeOffset _createdAt;

    /// <summary>Primary key of the account (schema column <c>user_id</c>).</summary>
    public int UserId
    {
        get => _userId;
        set => _userId = value;
    }

    /// <summary>Login name, unique in the schema (schema column <c>username</c>).</summary>
    public string Username
    {
        get => _username;
        set => _username = value;
    }

    /// <summary>The stored password hash — never the plaintext password (schema column <c>password_hash</c>).</summary>
    public string PasswordHash
    {
        get => _passwordHash;
        set => _passwordHash = value;
    }

    /// <summary>Name shown in the UI (schema column <c>display_name</c>).</summary>
    public string DisplayName
    {
        get => _displayName;
        set => _displayName = value;
    }

    /// <summary>
    /// Creation timestamp (schema column <c>created_at</c>). The schema stores ISO-8601 UTC
    /// text; the data layer (P4) writes and reads the format <c>yyyy-MM-ddTHH:mm:ssZ</c>.
    /// </summary>
    public DateTimeOffset CreatedAt
    {
        get => _createdAt;
        set => _createdAt = value;
    }
}
