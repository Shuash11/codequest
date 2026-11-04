// =============================================================================
// CodeQuest — Phase P4: Data Access Layer
// OOP rubric: ABSTRACTION — SubmittedAnswerRepository is the concrete
//              implementation behind the ISubmittedAnswerRepository contract of
//              phase P3.
// Look at: Add() below — before an answer row is written, the write-time rule
//          of plan ruling R-2 #3 is checked: the answered question has to
//          belong to the quiz of the attempt. The check lives in
//          AttemptAnswerGuard; a violated rule raises AnswerValidationException
//          and nothing is written. InsertAnswer() — the one place where an
//          answer row is written; the attempt repository uses it when it stores
//          an attempt together with all of its answers.
// Table:   submitted_answers (database/schema.sql) — the primary key is the
//          pair (attempt_id, question_id), so a question is answered once per
//          attempt; the composite foreign key proves that the picked option
//          belongs to the answered question.
// =============================================================================

using CodeQuest.Models;
using Microsoft.Data.Sqlite;

namespace CodeQuest.Data.Repositories;

/// <summary>
/// Reads and writes the answers a student picked
/// (schema table <c>submitted_answers</c>).
/// </summary>
public sealed class SubmittedAnswerRepository : ISubmittedAnswerRepository
{
    private const string SelectByAttemptSql =
        """
        SELECT attempt_id, question_id, chosen_choice_id
        FROM submitted_answers
        WHERE attempt_id = $attemptId
        ORDER BY question_id;
        """;

    private const string SelectByQuestionSql =
        """
        SELECT attempt_id, question_id, chosen_choice_id
        FROM submitted_answers
        WHERE question_id = $questionId
        ORDER BY attempt_id;
        """;

    private const string SelectByAttemptAndQuestionSql =
        """
        SELECT attempt_id, question_id, chosen_choice_id
        FROM submitted_answers
        WHERE attempt_id = $attemptId
          AND question_id = $questionId;
        """;

    private const string InsertSql =
        """
        INSERT INTO submitted_answers (attempt_id, question_id, chosen_choice_id)
        VALUES ($attemptId, $questionId, $chosenChoiceId);
        """;

    private const string DeleteOneSql =
        """
        DELETE FROM submitted_answers
        WHERE attempt_id = $attemptId
          AND question_id = $questionId;
        """;

    private const string DeleteByAttemptSql =
        """
        DELETE FROM submitted_answers
        WHERE attempt_id = $attemptId;
        """;

    private readonly Db _db;

    /// <summary>Creates the repository.</summary>
    /// <param name="db">The connection factory of the database.</param>
    public SubmittedAnswerRepository(Db db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    /// <inheritdoc />
    public IReadOnlyList<SubmittedAnswer> GetByAttempt(int attemptId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectByAttemptSql;
        command.Parameters.AddWithValue("$attemptId", attemptId);

        return ReadAnswers(command);
    }

    /// <inheritdoc />
    public IReadOnlyList<SubmittedAnswer> GetByQuestion(int questionId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectByQuestionSql;
        command.Parameters.AddWithValue("$questionId", questionId);

        return ReadAnswers(command);
    }

    /// <inheritdoc />
    public SubmittedAnswer? GetByAttemptAndQuestion(int attemptId, int questionId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectByAttemptAndQuestionSql;
        command.Parameters.AddWithValue("$attemptId", attemptId);
        command.Parameters.AddWithValue("$questionId", questionId);

        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadAnswer(reader) : null;
    }

    /// <inheritdoc />
    public int Add(SubmittedAnswer submittedAnswer)
    {
        ArgumentNullException.ThrowIfNull(submittedAnswer);

        using SqliteConnection connection = _db.OpenConnection();

        // Plan ruling R-2 #3: the answered question has to belong to the quiz
        // of the attempt. An unknown attempt is refused as well.
        int quizId = AttemptAnswerGuard.GetQuizIdOfAttempt(connection, null, submittedAnswer.AttemptId);
        AttemptAnswerGuard.EnsureAnswersBelongToQuiz(connection, null, quizId, new[] { submittedAnswer });

        try
        {
            InsertAnswer(connection, null, submittedAnswer);
        }
        catch (SqliteException exception)
        {
            // An answer whose option does not belong to the answered question
            // (composite foreign key) or whose question was already answered in
            // this attempt (primary key) is refused by the database.
            throw DataAccessException.FromSqlite(exception);
        }

        // The table has no single generated id — its primary key is the pair
        // attempt_id + question_id. The attempt id is what the caller can go
        // on with, so it is returned.
        return submittedAnswer.AttemptId;
    }

    /// <inheritdoc />
    public bool Delete(int attemptId, int questionId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = DeleteOneSql;
        command.Parameters.AddWithValue("$attemptId", attemptId);
        command.Parameters.AddWithValue("$questionId", questionId);

        return command.ExecuteNonQuery() > 0;
    }

    /// <inheritdoc />
    public void DeleteByAttempt(int attemptId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = DeleteByAttemptSql;
        command.Parameters.AddWithValue("$attemptId", attemptId);

        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Writes one answer row. The caller decides whether the write belongs to a
    /// transaction — an attempt with all of its answers is one unit of work; a
    /// single answer carries no transaction. The caller has already checked the
    /// R-2 #3 rule for this answer.
    /// </summary>
    /// <param name="connection">The connection of the write.</param>
    /// <param name="transaction">The transaction of the write, or null.</param>
    /// <param name="answer">The answer to write.</param>
    internal static void InsertAnswer(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        SubmittedAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(answer);

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = InsertSql;
        command.Parameters.AddWithValue("$attemptId", answer.AttemptId);
        command.Parameters.AddWithValue("$questionId", answer.QuestionId);
        command.Parameters.AddWithValue("$chosenChoiceId", answer.ChosenChoiceId);
        command.Transaction = transaction;
        command.ExecuteNonQuery();
    }

    /// <summary>Reads one answer from the current row.</summary>
    /// <param name="reader">The reader on the current row.</param>
    /// <returns>The answer of the row.</returns>
    private static SubmittedAnswer ReadAnswer(SqliteDataReader reader) => new()
    {
        AttemptId = reader.GetInt32(0),
        QuestionId = reader.GetInt32(1),
        ChosenChoiceId = reader.GetInt32(2),
    };

    /// <summary>Reads every answer of an open command.</summary>
    private static IReadOnlyList<SubmittedAnswer> ReadAnswers(SqliteCommand command)
    {
        List<SubmittedAnswer> answers = new();

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            answers.Add(ReadAnswer(reader));
        }

        return answers;
    }
}
