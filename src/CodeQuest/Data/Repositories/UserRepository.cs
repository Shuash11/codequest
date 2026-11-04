// =============================================================================
// CodeQuest — Phase P4: Data Access Layer
// OOP rubric: ABSTRACTION — UserRepository is the concrete implementation
//              behind the IUserRepository contract of phase P3.
// Look at: GetByUsername() below — the lookup the login screen (P7) validates
//          against: the account is found by its exact login name; the caller
//          receives the stored hash and verifies the password against it
//          (PasswordHasher, phase P7) — this repository never sees a plaintext
//          password, only the stored hash travels through it.
// Table:   users (database/schema.sql) — the username is unique in the schema,
//          so a duplicate registration surfaces as a DuplicateEntityException
//          (plan ruling R-2 #5).
// =============================================================================

using CodeQuest.Models;
using Microsoft.Data.Sqlite;

namespace CodeQuest.Data.Repositories;

/// <summary>
/// Reads and writes the accounts of the application (schema table
/// <c>users</c>).
/// </summary>
public sealed class UserRepository : IUserRepository
{
    private const string SelectAllSql =
        """
        SELECT user_id, username, password_hash, display_name, created_at
        FROM users
        ORDER BY username;
        """;

    private const string SelectByIdSql =
        """
        SELECT user_id, username, password_hash, display_name, created_at
        FROM users
        WHERE user_id = $userId;
        """;

    private const string SelectByUsernameSql =
        """
        SELECT user_id, username, password_hash, display_name, created_at
        FROM users
        WHERE username = $username;
        """;

    private const string InsertSql =
        """
        INSERT INTO users (username, password_hash, display_name, created_at)
        VALUES ($username, $passwordHash, $displayName, $createdAt);
        """;

    private const string UpdateSql =
        """
        UPDATE users
        SET username = $username,
            password_hash = $passwordHash,
            display_name = $displayName
        WHERE user_id = $userId;
        """;

    private const string DeleteSql =
        """
        DELETE FROM users
        WHERE user_id = $userId;
        """;

    private const string LastInsertRowIdSql = "SELECT last_insert_rowid();";

    private readonly Db _db;

    /// <summary>Creates the repository.</summary>
    /// <param name="db">The connection factory of the database.</param>
    public UserRepository(Db db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    /// <inheritdoc />
    public IReadOnlyList<UserAccount> GetAll()
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectAllSql;

        return ReadUsers(command);
    }

    /// <inheritdoc />
    public UserAccount? GetById(int userId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectByIdSql;
        command.Parameters.AddWithValue("$userId", userId);

        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadUser(reader) : null;
    }

    /// <inheritdoc />
    public UserAccount? GetByUsername(string username)
    {
        ArgumentNullException.ThrowIfNull(username);

        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectByUsernameSql;
        command.Parameters.AddWithValue("$username", username);

        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadUser(reader) : null;
    }

    /// <inheritdoc />
    public int Add(UserAccount user)
    {
        ArgumentNullException.ThrowIfNull(user);

        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = InsertSql;
        command.Parameters.AddWithValue("$username", user.Username);
        command.Parameters.AddWithValue("$passwordHash", user.PasswordHash);
        command.Parameters.AddWithValue("$displayName", user.DisplayName);
        command.Parameters.AddWithValue("$createdAt", SqlValueMap.ToStorage(user.CreatedAt));

        try
        {
            command.ExecuteNonQuery();
        }
        catch (SqliteException exception)
        {
            // A username that already exists is refused by the schema
            // (UNIQUE); the caller shows a friendly duplicate message (P8).
            throw DataAccessException.FromSqlite(exception);
        }

        user.UserId = ReadLastInsertRowId(connection);
        return user.UserId;
    }

    /// <inheritdoc />
    public void Update(UserAccount user)
    {
        ArgumentNullException.ThrowIfNull(user);

        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = UpdateSql;
        command.Parameters.AddWithValue("$username", user.Username);
        command.Parameters.AddWithValue("$passwordHash", user.PasswordHash);
        command.Parameters.AddWithValue("$displayName", user.DisplayName);
        command.Parameters.AddWithValue("$userId", user.UserId);

        int changed;
        try
        {
            changed = command.ExecuteNonQuery();
        }
        catch (SqliteException exception)
        {
            throw DataAccessException.FromSqlite(exception);
        }

        if (changed == 0)
        {
            throw new EntityNotFoundException($"There is no account with the id {user.UserId}.");
        }
    }

    /// <inheritdoc />
    public bool Delete(int userId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = DeleteSql;
        command.Parameters.AddWithValue("$userId", userId);

        try
        {
            return command.ExecuteNonQuery() > 0;
        }
        catch (SqliteException exception)
        {
            // An account that still owns attempts is protected by the database
            // (quiz_attempts.user_id ON DELETE RESTRICT); the history stays.
            throw DataAccessException.FromSqlite(exception);
        }
    }

    /// <summary>
    /// Reads the id the last insert produced. SQLite reports it through
    /// last_insert_rowid() — the value belongs to the connection that inserted.
    /// </summary>
    private static int ReadLastInsertRowId(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = LastInsertRowIdSql;

        object? value = command.ExecuteScalar();
        return value is null or DBNull ? 0 : Convert.ToInt32(value);
    }

    /// <summary>Reads one account from the current row.</summary>
    private static UserAccount ReadUser(SqliteDataReader reader) => new()
    {
        UserId = reader.GetInt32(0),
        Username = reader.GetString(1),
        PasswordHash = reader.GetString(2),
        DisplayName = reader.GetString(3),
        CreatedAt = SqlValueMap.FromStorage(reader.GetString(4)),
    };

    /// <summary>Reads every account of an open command.</summary>
    private static IReadOnlyList<UserAccount> ReadUsers(SqliteCommand command)
    {
        List<UserAccount> users = new();

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            users.Add(ReadUser(reader));
        }

        return users;
    }
}
