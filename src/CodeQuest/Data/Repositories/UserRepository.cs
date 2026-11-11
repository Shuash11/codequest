// Reads and writes player accounts. (Abstraction)

using CodeQuest.Models;
using Microsoft.Data.Sqlite;

namespace CodeQuest.Data.Repositories;

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

    public UserRepository(Db db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    // Reads all player accounts.
    public IReadOnlyList<UserAccount> GetAll()
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectAllSql;

        return ReadUsers(command);
    }

    // Reads one player account.
    public UserAccount? GetById(int userId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectByIdSql;
        command.Parameters.AddWithValue("$userId", userId);

        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadUser(reader) : null;
    }

    // Finds one player by name.
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

    // Adds a new player account.
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
            throw DataAccessException.FromSqlite(exception);
        }

        user.UserId = ReadLastInsertRowId(connection);
        return user.UserId;
    }

    // Updates a player account.
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

    // Deletes a player account.
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
            throw DataAccessException.FromSqlite(exception);
        }
    }

    private static int ReadLastInsertRowId(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = LastInsertRowIdSql;

        object? value = command.ExecuteScalar();
        return value is null or DBNull ? 0 : Convert.ToInt32(value);
    }

    private static UserAccount ReadUser(SqliteDataReader reader) => new()
    {
        UserId = reader.GetInt32(0),
        Username = reader.GetString(1),
        PasswordHash = reader.GetString(2),
        DisplayName = reader.GetString(3),
        CreatedAt = SqlValueMap.FromStorage(reader.GetString(4)),
    };

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
