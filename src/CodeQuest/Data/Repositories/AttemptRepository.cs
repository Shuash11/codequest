// =============================================================================
// CodeQuest — Phase P4: Data Access Layer
// OOP rubric: ABSTRACTION — AttemptRepository is the concrete implementation
//              behind the IAttemptRepository contract of phase P3.
// Look at: AddWithAnswers() below — the write the quiz-taking screen (P9) uses:
//          the attempt row and every submitted answer go into the database in
//          ONE transaction, and the write-time rule of plan ruling R-2 #3 is
//          checked before anything is written, so an answer pointing at a
//          question of another quiz refuses the whole attempt
//          (AnswerValidationException, nothing stays in the database).
//          Add() — stores the bare attempt row without answers, for the flows
//          that record an attempt on its own.
// History: the read methods order newest first, so the history list (P10) can
//          show the latest run of a quiz first.
// Table:   quiz_attempts (database/schema.sql) — the score is the number of
//          correct answers (plan ruling R-2 #1); the answers are the child rows
//          of the submitted_answers table, which the schema deletes together
//          with their attempt (ON DELETE CASCADE).
// =============================================================================

using CodeQuest.Models;
using Microsoft.Data.Sqlite;

namespace CodeQuest.Data.Repositories;

/// <summary>
/// Reads and writes the completed quiz runs (schema table
/// <c>quiz_attempts</c>).
/// </summary>
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

    /// <summary>Creates the repository.</summary>
    /// <param name="db">The connection factory of the database.</param>
    public AttemptRepository(Db db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    /// <inheritdoc />
    public IReadOnlyList<QuizAttempt> GetByUser(int userId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectByUserSql;
        command.Parameters.AddWithValue("$userId", userId);

        return ReadAttempts(command);
    }

    /// <inheritdoc />
    public IReadOnlyList<QuizAttempt> GetByQuiz(int quizId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectByQuizSql;
        command.Parameters.AddWithValue("$quizId", quizId);

        return ReadAttempts(command);
    }

    /// <inheritdoc />
    public IReadOnlyList<QuizAttempt> GetByUserAndQuiz(int userId, int quizId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectByUserAndQuizSql;
        command.Parameters.AddWithValue("$userId", userId);
        command.Parameters.AddWithValue("$quizId", quizId);

        return ReadAttempts(command);
    }

    /// <inheritdoc />
    public QuizAttempt? GetById(int attemptId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectByIdSql;
        command.Parameters.AddWithValue("$attemptId", attemptId);

        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadAttempt(reader) : null;
    }

    /// <inheritdoc />
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

    /// <summary>
    /// Stores a new attempt together with all of its answers in one transaction
    /// and returns the generated id of the attempt. Before anything is written,
    /// every answer is checked against plan ruling R-2 #3: the answered
    /// question has to belong to the quiz of the attempt. A violated rule
    /// refuses the whole attempt — nothing stays in the database. The generated
    /// attempt id is written onto the attempt and onto every answer.
    /// </summary>
    /// <param name="attempt">The attempt to store.</param>
    /// <param name="answers">The answers of the attempt; they are bound to it.</param>
    /// <returns>The id the database gave to the attempt.</returns>
    public int AddWithAnswers(QuizAttempt attempt, IEnumerable<SubmittedAnswer> answers)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        ArgumentNullException.ThrowIfNull(answers);

        // The answers are read twice — once by the guard, once by the insert —
        // so they are materialised once here and a caller may pass any
        // collection, even a query that can be walked only once.
        IReadOnlyList<SubmittedAnswer> answerList =
            answers as IReadOnlyList<SubmittedAnswer> ?? answers.ToList();

        return _db.RunInTransaction((connection, transaction) =>
        {
            // Plan ruling R-2 #3, checked before any row is written.
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <summary>
    /// Writes one attempt row. The caller decides whether the write belongs to
    /// a transaction — an attempt with its answers is one unit of work.
    /// </summary>
    /// <param name="connection">The connection of the write.</param>
    /// <param name="transaction">The transaction of the write, or null.</param>
    /// <param name="attempt">The attempt to write.</param>
    /// <returns>The id the database gave to the attempt.</returns>
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

    /// <summary>Reads one attempt from the current row.</summary>
    private static QuizAttempt ReadAttempt(SqliteDataReader reader) => new()
    {
        AttemptId = reader.GetInt32(0),
        QuizId = reader.GetInt32(1),
        UserId = reader.GetInt32(2),
        Score = reader.GetInt32(3),
        AttemptedAt = SqlValueMap.FromStorage(reader.GetString(4)),
    };

    /// <summary>Reads every attempt of an open command.</summary>
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
