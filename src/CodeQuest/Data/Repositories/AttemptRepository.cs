// Reads and writes quiz attempts. (Abstraction)

using CodeQuest.Models;
using Microsoft.Data.Sqlite;

namespace CodeQuest.Data.Repositories;

public sealed class AttemptRepository : IAttemptRepository
{
    private const string SelectByUserSql =
        """
        SELECT attempt_id, quiz_id, user_id, score, attempted_at
        FROM quiz_attempts
        WHERE user_id = $userId
        ORDER BY attempted_at DESC, attempt_id DESC;
        """;

    private const string SelectByQuizSql =
        """
        SELECT attempt_id, quiz_id, user_id, score, attempted_at
        FROM quiz_attempts
        WHERE quiz_id = $quizId
        ORDER BY attempted_at DESC, attempt_id DESC;
        """;

    private const string SelectByUserAndQuizSql =
        """
        SELECT attempt_id, quiz_id, user_id, score, attempted_at
        FROM quiz_attempts
        WHERE user_id = $userId
          AND quiz_id = $quizId
        ORDER BY attempted_at DESC, attempt_id DESC;
        """;

    private const string SelectByIdSql =
        """
        SELECT attempt_id, quiz_id, user_id, score, attempted_at
        FROM quiz_attempts
        WHERE attempt_id = $attemptId;
        """;

    private const string InsertSql =
        """
        INSERT INTO quiz_attempts (quiz_id, user_id, score, attempted_at)
        VALUES ($quizId, $userId, $score, $attemptedAt);
        """;

    private const string UpdateSql =
        """
        UPDATE quiz_attempts
        SET score = $score,
            attempted_at = $attemptedAt
        WHERE attempt_id = $attemptId;
        """;

    private const string DeleteSql =
        """
        DELETE FROM quiz_attempts
        WHERE attempt_id = $attemptId;
        """;

    private const string LastInsertRowIdSql = "SELECT last_insert_rowid();";

    private readonly Db _db;

    public AttemptRepository(Db db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    // Reads the attempts of one player.
    public IReadOnlyList<QuizAttempt> GetByUser(int userId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectByUserSql;
        command.Parameters.AddWithValue("$userId", userId);

        return ReadAttempts(command);
    }

    // Reads the attempts of one quiz.
    public IReadOnlyList<QuizAttempt> GetByQuiz(int quizId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectByQuizSql;
        command.Parameters.AddWithValue("$quizId", quizId);

        return ReadAttempts(command);
    }

    // Reads one player's attempts of one quiz.
    public IReadOnlyList<QuizAttempt> GetByUserAndQuiz(int userId, int quizId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectByUserAndQuizSql;
        command.Parameters.AddWithValue("$userId", userId);
        command.Parameters.AddWithValue("$quizId", quizId);

        return ReadAttempts(command);
    }

    // Reads one attempt.
    public QuizAttempt? GetById(int attemptId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectByIdSql;
        command.Parameters.AddWithValue("$attemptId", attemptId);

        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadAttempt(reader) : null;
    }

    // Adds a new attempt.
    public int Add(QuizAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        using SqliteConnection connection = _db.OpenConnection();

        try
        {
            attempt.AttemptId = InsertAttempt(connection, null, attempt);
        }
        catch (SqliteException exception)
        {
            throw DataAccessException.FromSqlite(exception);
        }

        return attempt.AttemptId;
    }

    // Saves an attempt with its answers.
    public int AddWithAnswers(QuizAttempt attempt, IEnumerable<SubmittedAnswer> answers)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        ArgumentNullException.ThrowIfNull(answers);

        IReadOnlyList<SubmittedAnswer> answerList =
            answers as IReadOnlyList<SubmittedAnswer> ?? answers.ToList();

        return _db.RunInTransaction((connection, transaction) =>
        {
            AttemptAnswerGuard.EnsureAnswersBelongToQuiz(connection, transaction, attempt.QuizId, answerList);

            int attemptId = InsertAttempt(connection, transaction, attempt);
            attempt.AttemptId = attemptId;

            foreach (SubmittedAnswer answer in answerList)
            {
                answer.AttemptId = attemptId;
                SubmittedAnswerRepository.InsertAnswer(connection, transaction, answer);
            }

            return attemptId;
        });
    }

    // Updates a attempt.
    public void Update(QuizAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = UpdateSql;
        command.Parameters.AddWithValue("$score", attempt.Score);
        command.Parameters.AddWithValue("$attemptedAt", SqlValueMap.ToStorage(attempt.AttemptedAt));
        command.Parameters.AddWithValue("$attemptId", attempt.AttemptId);

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
            throw new EntityNotFoundException($"There is no attempt with the id {attempt.AttemptId}.");
        }
    }

    // Deletes a attempt.
    public bool Delete(int attemptId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = DeleteSql;
        command.Parameters.AddWithValue("$attemptId", attemptId);

        try
        {
            return command.ExecuteNonQuery() > 0;
        }
        catch (SqliteException exception)
        {
            throw DataAccessException.FromSqlite(exception);
        }
    }

    private static int InsertAttempt(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        QuizAttempt attempt)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = InsertSql;
        command.Parameters.AddWithValue("$quizId", attempt.QuizId);
        command.Parameters.AddWithValue("$userId", attempt.UserId);
        command.Parameters.AddWithValue("$score", attempt.Score);
        command.Parameters.AddWithValue("$attemptedAt", SqlValueMap.ToStorage(attempt.AttemptedAt));
        command.Transaction = transaction;
        command.ExecuteNonQuery();

        using SqliteCommand idCommand = connection.CreateCommand();
        idCommand.CommandText = LastInsertRowIdSql;
        idCommand.Transaction = transaction;

        object? value = idCommand.ExecuteScalar();
        return value is null or DBNull ? 0 : Convert.ToInt32(value);
    }

    private static QuizAttempt ReadAttempt(SqliteDataReader reader) => new()
    {
        AttemptId = reader.GetInt32(0),
        QuizId = reader.GetInt32(1),
        UserId = reader.GetInt32(2),
        Score = reader.GetInt32(3),
        AttemptedAt = SqlValueMap.FromStorage(reader.GetString(4)),
    };

    private static IReadOnlyList<QuizAttempt> ReadAttempts(SqliteCommand command)
    {
        List<QuizAttempt> attempts = new();

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            attempts.Add(ReadAttempt(reader));
        }

        return attempts;
    }
}
