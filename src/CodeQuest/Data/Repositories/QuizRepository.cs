// Reads and writes quizzes. (Abstraction)

using CodeQuest.Models;
using Microsoft.Data.Sqlite;

namespace CodeQuest.Data.Repositories;

public sealed class QuizRepository : IQuizRepository
{
    private const string SelectAllSql =
        """
        SELECT quiz_id, topic_id, title, difficulty, status, created_at
        FROM quizzes
        ORDER BY created_at, quiz_id;
        """;

    private const string SelectByTopicSql =
        """
        SELECT quiz_id, topic_id, title, difficulty, status, created_at
        FROM quizzes
        WHERE topic_id = $topicId
        ORDER BY created_at, quiz_id;
        """;

    private const string SelectByStatusSql =
        """
        SELECT quiz_id, topic_id, title, difficulty, status, created_at
        FROM quizzes
        WHERE status = $status
        ORDER BY created_at, quiz_id;
        """;

    private const string SelectByTopicAndStatusSql =
        """
        SELECT quiz_id, topic_id, title, difficulty, status, created_at
        FROM quizzes
        WHERE topic_id = $topicId
          AND status = $status
        ORDER BY created_at, quiz_id;
        """;

    private const string SelectByIdSql =
        """
        SELECT quiz_id, topic_id, title, difficulty, status, created_at
        FROM quizzes
        WHERE quiz_id = $quizId;
        """;

    private const string CountByTitleSql =
        """
        SELECT COUNT(1)
        FROM quizzes
        WHERE topic_id = $topicId
          AND title = $title
          AND ($ignoreQuizId IS NULL OR quiz_id <> $ignoreQuizId);
        """;

    private const string InsertSql =
        """
        INSERT INTO quizzes (topic_id, title, difficulty, status, created_at)
        VALUES ($topicId, $title, $difficulty, $status, $createdAt);
        """;

    private const string UpdateSql =
        """
        UPDATE quizzes
        SET topic_id = $topicId,
            title = $title,
            difficulty = $difficulty,
            status = $status
        WHERE quiz_id = $quizId;
        """;

    private const string DeleteSql =
        """
        DELETE FROM quizzes
        WHERE quiz_id = $quizId;
        """;

    // The status guard is the belt-and-braces: an approved quiz never matches.
    private const string DeleteDraftByTitleSql =
        """
        DELETE FROM quizzes
        WHERE topic_id = $topicId
          AND title = $title
          AND status = 'Draft';
        """;

    private const string LastInsertRowIdSql = "SELECT last_insert_rowid();";

    private readonly Db _db;

    public QuizRepository(Db db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    // Reads all quizzes.
    public IReadOnlyList<Quiz> GetAll() => ReadQuizzes(SelectAllSql);

    // Reads the quizzes of one topic.
    public IReadOnlyList<Quiz> GetByTopic(int topicId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectByTopicSql;
        command.Parameters.AddWithValue("$topicId", topicId);

        return ReadQuizzes(command);
    }

    // Reads the quizzes with one status.
    public IReadOnlyList<Quiz> GetByStatus(QuizStatus status)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectByStatusSql;
        command.Parameters.AddWithValue("$status", SqlValueMap.ToStorage(status));

        return ReadQuizzes(command);
    }

    // Reads the quizzes of one topic and status.
    public IReadOnlyList<Quiz> GetByTopicAndStatus(int topicId, QuizStatus status)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectByTopicAndStatusSql;
        command.Parameters.AddWithValue("$topicId", topicId);
        command.Parameters.AddWithValue("$status", SqlValueMap.ToStorage(status));

        return ReadQuizzes(command);
    }

    // Reads one quiz.
    public Quiz? GetById(int quizId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectByIdSql;
        command.Parameters.AddWithValue("$quizId", quizId);

        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        Quiz quiz = ReadQuiz(reader);

        foreach (Question question in QuestionRepository.ReadQuestionsOfQuiz(connection, null, quizId))
        {
            quiz.AddQuestion(question);
        }

        return quiz;
    }

    // Adds a new quiz.
    public int Add(Quiz quiz)
    {
        ArgumentNullException.ThrowIfNull(quiz);

        return _db.RunInTransaction((connection, transaction) =>
        {
            int quizId = InsertQuiz(connection, transaction, quiz);
            quiz.QuizId = quizId;

            foreach (Question question in quiz.Questions)
            {
                ArgumentNullException.ThrowIfNull(question);
                question.QuizId = quizId;
                QuestionRepository.InsertQuestionWithChoices(connection, transaction, question);
            }

            return quizId;
        });
    }

    // Updates a quiz.
    public void Update(Quiz quiz)
    {
        ArgumentNullException.ThrowIfNull(quiz);

        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = UpdateSql;
        command.Parameters.AddWithValue("$topicId", quiz.TopicId);
        command.Parameters.AddWithValue("$title", quiz.Title);
        command.Parameters.AddWithValue("$difficulty", SqlValueMap.ToStorage(quiz.Difficulty));
        command.Parameters.AddWithValue("$status", SqlValueMap.ToStorage(quiz.Status));
        command.Parameters.AddWithValue("$quizId", quiz.QuizId);

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
            throw new EntityNotFoundException($"There is no quiz with the id {quiz.QuizId}.");
        }
    }

    // Deletes a quiz.
    public bool Delete(int quizId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = DeleteSql;
        command.Parameters.AddWithValue("$quizId", quizId);

        try
        {
            return command.ExecuteNonQuery() > 0;
        }
        catch (SqliteException exception)
        {
            throw DataAccessException.FromSqlite(exception);
        }
    }

    // Deletes the Draft with this exact title in one topic; approved quizzes never match.
    public bool DeleteDraftWithTitle(int topicId, string title)
    {
        ArgumentNullException.ThrowIfNull(title);

        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = DeleteDraftByTitleSql;
        command.Parameters.AddWithValue("$topicId", topicId);
        command.Parameters.AddWithValue("$title", title);

        try
        {
            return command.ExecuteNonQuery() > 0;
        }
        catch (SqliteException exception)
        {
            throw DataAccessException.FromSqlite(exception);
        }
    }

    // Checks if a quiz title already exists.
    public bool ExistsByTitle(int topicId, string title, int? ignoreQuizId = null)
    {
        ArgumentNullException.ThrowIfNull(title);

        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = CountByTitleSql;
        command.Parameters.AddWithValue("$topicId", topicId);
        command.Parameters.AddWithValue("$title", title);
        command.Parameters.AddWithValue("$ignoreQuizId", AsParameterValue(ignoreQuizId));

        object? count = command.ExecuteScalar();
        return count is not null and not DBNull && Convert.ToInt64(count) > 0;
    }

    private static int InsertQuiz(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Quiz quiz)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = InsertSql;
        command.Parameters.AddWithValue("$topicId", quiz.TopicId);
        command.Parameters.AddWithValue("$title", quiz.Title);
        command.Parameters.AddWithValue("$difficulty", SqlValueMap.ToStorage(quiz.Difficulty));
        command.Parameters.AddWithValue("$status", SqlValueMap.ToStorage(quiz.Status));
        command.Parameters.AddWithValue("$createdAt", SqlValueMap.ToStorage(quiz.CreatedAt));
        command.Transaction = transaction;
        command.ExecuteNonQuery();

        using SqliteCommand idCommand = connection.CreateCommand();
        idCommand.CommandText = LastInsertRowIdSql;
        idCommand.Transaction = transaction;

        object? value = idCommand.ExecuteScalar();
        return value is null or DBNull ? 0 : Convert.ToInt32(value);
    }

    private static object AsParameterValue(int? value)
        => value.HasValue ? value.Value : DBNull.Value;

    private IReadOnlyList<Quiz> ReadQuizzes(string sql)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;

        return ReadQuizzes(command);
    }

    private static IReadOnlyList<Quiz> ReadQuizzes(SqliteCommand command)
    {
        List<Quiz> quizzes = new();

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            quizzes.Add(ReadQuiz(reader));
        }

        return quizzes;
    }

    private static Quiz ReadQuiz(SqliteDataReader reader) => new()
    {
        QuizId = reader.GetInt32(0),
        TopicId = reader.GetInt32(1),
        Title = reader.GetString(2),
        Difficulty = SqlValueMap.ParseDifficulty(reader.GetString(3)),
        Status = SqlValueMap.ParseQuizStatus(reader.GetString(4)),
        CreatedAt = SqlValueMap.FromStorage(reader.GetString(5)),
    };
}
