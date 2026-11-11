// Reads and writes topics. (Abstraction)

using CodeQuest.Models;
using Microsoft.Data.Sqlite;

namespace CodeQuest.Data.Repositories;

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

    public TopicRepository(Db db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    // Reads all topics.
    public IReadOnlyList<Topic> GetAll() => ReadTopics(SelectAllSql);

    // Reads the topics of one category.
    public IReadOnlyList<Topic> GetByCategory(string category)
    {
        ArgumentNullException.ThrowIfNull(category);

        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectByCategorySql;
        command.Parameters.AddWithValue("$category", category);

        return ReadTopics(command);
    }

    // Finds topics by search text.
    public IReadOnlyList<Topic> Search(string searchText)
    {
        ArgumentNullException.ThrowIfNull(searchText);

        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectBySearchTextSql;
        command.Parameters.AddWithValue("$searchText", searchText);

        return ReadTopics(command);
    }

    // Reads one topic.
    public Topic? GetById(int topicId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectByIdSql;
        command.Parameters.AddWithValue("$topicId", topicId);

        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadTopic(reader) : null;
    }

    // Adds a new topic.
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

    // Updates a topic.
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

    // Deletes a topic.
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
            throw DataAccessException.FromSqlite(exception);
        }
    }

    // Checks if a topic name already exists.
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

    private static int ReadLastInsertRowId(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = LastInsertRowIdSql;

        object? value = command.ExecuteScalar();
        return value is null or DBNull ? 0 : Convert.ToInt32(value);
    }

    private static object AsParameterValue(int? value)
        => value.HasValue ? value.Value : DBNull.Value;

    private IReadOnlyList<Topic> ReadTopics(string sql)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;

        return ReadTopics(command);
    }

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

    private static Topic ReadTopic(SqliteDataReader reader) => new()
    {
        TopicId = reader.GetInt32(0),
        Name = reader.GetString(1),
        Category = reader.GetString(2),
        Description = reader.GetString(3),
        CreatedAt = SqlValueMap.FromStorage(reader.GetString(4)),
    };
}
