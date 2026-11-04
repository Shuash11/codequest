// =============================================================================
// CodeQuest — Phase P4: Data Access Layer
// OOP rubric: ABSTRACTION — QuizRepository is the concrete implementation
//              behind the IQuizRepository contract of phase P3. The screens of
//              the later phases only ever talk to the interface.
// Look at: Add() below — a quiz is never stored alone: the quiz header, its
//          questions and their options go into the database in ONE transaction
//          (a forced failure leaves no partial quiz behind, plan phase P4
//          acceptance). GetById() — the one query that rebuilds the whole quiz
//          with its questions and their options, in display order.
// Status:  the review workflow Draft -> Approved lives in the status column
//          (plan ruling R-2 #4); GetByStatus() and GetByTopicAndStatus() read
//          each state, so the review screen (P13) can list its drafts and the
//          quiz-taking screen (P9) can list the takeable, approved quizzes.
// Table:   quizzes (database/schema.sql) — the questions are the child rows of
//          the questions table and the options the child rows of the choices
//          table.
// =============================================================================

using CodeQuest.Models;
using Microsoft.Data.Sqlite;

namespace CodeQuest.Data.Repositories;

/// <summary>
/// Reads and writes the quizzes of the application (schema table
/// <c>quizzes</c>).
/// </summary>
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

    private const string LastInsertRowIdSql = "SELECT last_insert_rowid();";

    private readonly Db _db;

    /// <summary>Creates the repository.</summary>
    /// <param name="db">The connection factory of the database.</param>
    public QuizRepository(Db db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    /// <inheritdoc />
    public IReadOnlyList<Quiz> GetAll() => ReadQuizzes(SelectAllSql);

    /// <inheritdoc />
    public IReadOnlyList<Quiz> GetByTopic(int topicId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectByTopicSql;
        command.Parameters.AddWithValue("$topicId", topicId);

        return ReadQuizzes(command);
    }

    /// <inheritdoc />
    public IReadOnlyList<Quiz> GetByStatus(QuizStatus status)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectByStatusSql;
        command.Parameters.AddWithValue("$status", SqlValueMap.ToStorage(status));

        return ReadQuizzes(command);
    }

    /// <inheritdoc />
    public IReadOnlyList<Quiz> GetByTopicAndStatus(int topicId, QuizStatus status)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectByTopicAndStatusSql;
        command.Parameters.AddWithValue("$topicId", topicId);
        command.Parameters.AddWithValue("$status", SqlValueMap.ToStorage(status));

        return ReadQuizzes(command);
    }

    /// <inheritdoc />
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

        // The questions and their options are child rows of the quiz; they are
        // read on the same connection, in display order.
        foreach (Question question in QuestionRepository.ReadQuestionsOfQuiz(connection, null, quizId))
        {
            quiz.AddQuestion(question);
        }

        return quiz;
    }

    /// <inheritdoc />
    public int Add(Quiz quiz)
    {
        ArgumentNullException.ThrowIfNull(quiz);

        // The quiz header, its questions and their options are one unit of
        // work: a failure anywhere rolls the whole quiz back.
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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
            // A quiz that has been taken is history and protected by the
            // database (quiz_attempts.quiz_id ON DELETE RESTRICT); its
            // questions and options, in contrast, are deleted with it
            // (CASCADE) when it was never attempted.
            throw DataAccessException.FromSqlite(exception);
        }
    }

    /// <inheritdoc />
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

    /// <summary>
    /// Writes one quiz header row. The caller decides whether the write belongs
    /// to a transaction — the full quiz with its questions is one unit of work.
    /// </summary>
    /// <param name="connection">The connection of the write.</param>
    /// <param name="transaction">The transaction of the write, or null.</param>
    /// <param name="quiz">The quiz to write.</param>
    /// <returns>The id the database gave to the quiz.</returns>
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

    /// <summary>
    /// Turns an optional id into the value of a parameter: the id itself, or
    /// NULL when there is none ("no id to ignore").
    /// </summary>
    private static object AsParameterValue(int? value)
        => value.HasValue ? value.Value : DBNull.Value;

    /// <summary>Reads every quiz of a query.</summary>
    private IReadOnlyList<Quiz> ReadQuizzes(string sql)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;

        return ReadQuizzes(command);
    }

    /// <summary>Reads every quiz of an open command.</summary>
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

    /// <summary>Reads one quiz header from the current row.</summary>
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
