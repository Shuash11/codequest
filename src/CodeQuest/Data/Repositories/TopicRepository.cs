// =============================================================================
// CodeQuest — Phase P4: Data Access Layer
// OOP rubric: ABSTRACTION — TopicRepository is the concrete implementation
//              behind the ITopicRepository contract of phase P3. The screens of
//              the later phases only ever talk to the interface.
// Look at: the SQL constants below — every statement is constant text and every
//          value travels as a $parameter. No statement is ever built from
//          pieces, so a topic named "'; DROP TABLE topics;--" is stored as the
//          literal text it is.
// Table:   topics (database/schema.sql)
// =============================================================================

using CodeQuest.Models;
using Microsoft.Data.Sqlite;

namespace CodeQuest.Data.Repositories;

/// <summary>
/// Reads and writes the subject areas of the application (schema table
/// <c>topics</c>).
/// </summary>
public sealed class TopicRepository : ITopicRepository
{
    private const string SelectAllSql =
        """
        SELECT topic_id, name, category, description, created_at
        FROM topics
        ORDER BY name;
        """;

    private const string SelectByCategorySql =
        """
        SELECT topic_id, name, category, description, created_at
        FROM topics
        WHERE category = $category
        ORDER BY name;
        """;

    private const string SelectBySearchTextSql =
        """
        SELECT topic_id, name, category, description, created_at
        FROM topics
        WHERE instr(lower(name), lower($searchText)) > 0
           OR instr(lower(description), lower($searchText)) > 0
        ORDER BY name;
        """;

    private const string SelectByIdSql =
        """
        SELECT topic_id, name, category, description, created_at
        FROM topics
        WHERE topic_id = $topicId;
        """;

    private const string CountByNameSql =
        """
        SELECT COUNT(1)
        FROM topics
        WHERE name = $name
          AND ($ignoreTopicId IS NULL OR topic_id <> $ignoreTopicId);
        """;

    private const string InsertSql =
        """
        INSERT INTO topics (name, category, description, created_at)
        VALUES ($name, $category, $description, $createdAt);
        """;

    private const string UpdateSql =
        """
        UPDATE topics
        SET name = $name,
            category = $category,
            description = $description
        WHERE topic_id = $topicId;
        """;

    private const string DeleteSql =
        """
        DELETE FROM topics
        WHERE topic_id = $topicId;
        """;

    private const string LastInsertRowIdSql = "SELECT last_insert_rowid();";

    private readonly Db _db;

    /// <summary>Creates the repository.</summary>
    /// <param name="db">The connection factory of the database.</param>
    public TopicRepository(Db db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    /// <inheritdoc />
    public IReadOnlyList<Topic> GetAll() => ReadTopics(SelectAllSql);

    /// <inheritdoc />
    public IReadOnlyList<Topic> GetByCategory(string category)
    {
        ArgumentNullException.ThrowIfNull(category);

        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectByCategorySql;
        command.Parameters.AddWithValue("$category", category);

        return ReadTopics(command);
    }

    /// <inheritdoc />
    public IReadOnlyList<Topic> Search(string searchText)
    {
        ArgumentNullException.ThrowIfNull(searchText);

        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectBySearchTextSql;
        command.Parameters.AddWithValue("$searchText", searchText);

        return ReadTopics(command);
    }

    /// <inheritdoc />
    public Topic? GetById(int topicId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectByIdSql;
        command.Parameters.AddWithValue("$topicId", topicId);

        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadTopic(reader) : null;
    }

    /// <inheritdoc />
    public int Add(Topic topic)
    {
        ArgumentNullException.ThrowIfNull(topic);

        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = InsertSql;
        command.Parameters.AddWithValue("$name", topic.Name);
        command.Parameters.AddWithValue("$category", topic.Category);
        command.Parameters.AddWithValue("$description", topic.Description);
        command.Parameters.AddWithValue("$createdAt", SqlValueMap.ToStorage(topic.CreatedAt));

        try
        {
            command.ExecuteNonQuery();
        }
        catch (SqliteException exception)
        {
            throw DataAccessException.FromSqlite(exception);
        }

        topic.TopicId = ReadLastInsertRowId(connection);
        return topic.TopicId;
    }

    /// <inheritdoc />
    public void Update(Topic topic)
    {
        ArgumentNullException.ThrowIfNull(topic);

        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = UpdateSql;
        command.Parameters.AddWithValue("$name", topic.Name);
        command.Parameters.AddWithValue("$category", topic.Category);
        command.Parameters.AddWithValue("$description", topic.Description);
        command.Parameters.AddWithValue("$topicId", topic.TopicId);

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
            throw new EntityNotFoundException($"There is no topic with the id {topic.TopicId}.");
        }
    }

    /// <inheritdoc />
    public bool Delete(int topicId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = DeleteSql;
        command.Parameters.AddWithValue("$topicId", topicId);

        try
        {
            return command.ExecuteNonQuery() > 0;
        }
        catch (SqliteException exception)
        {
            // A topic that still owns quizzes is refused by the database; the
            // caller shows the friendly message (plan ruling R-2 #1).
            throw DataAccessException.FromSqlite(exception);
        }
    }

    /// <inheritdoc />
    public bool ExistsByName(string name, int? ignoreTopicId = null)
    {
        ArgumentNullException.ThrowIfNull(name);

        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = CountByNameSql;
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$ignoreTopicId", AsParameterValue(ignoreTopicId));

        object? count = command.ExecuteScalar();
        return count is not null and not DBNull && Convert.ToInt64(count) > 0;
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

    /// <summary>
    /// Turns an optional id into the value of a parameter: the id itself, or
    /// NULL when there is none ("no id to ignore").
    /// </summary>
    private static object AsParameterValue(int? value)
        => value.HasValue ? value.Value : DBNull.Value;

    /// <summary>Reads every topic of a query.</summary>
    private IReadOnlyList<Topic> ReadTopics(string sql)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;

        return ReadTopics(command);
    }

    /// <summary>Reads every topic of an open command.</summary>
    private static IReadOnlyList<Topic> ReadTopics(SqliteCommand command)
    {
        List<Topic> topics = new();

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            topics.Add(ReadTopic(reader));
        }

        return topics;
    }

    /// <summary>Reads one topic from the current row.</summary>
    private static Topic ReadTopic(SqliteDataReader reader) => new()
    {
        TopicId = reader.GetInt32(0),
        Name = reader.GetString(1),
        Category = reader.GetString(2),
        Description = reader.GetString(3),
        CreatedAt = SqlValueMap.FromStorage(reader.GetString(4)),
    };
}
