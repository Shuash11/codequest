// Reads and writes picked answers. (Abstraction)

using CodeQuest.Models;
using Microsoft.Data.Sqlite;

namespace CodeQuest.Data.Repositories;

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

    public SubmittedAnswerRepository(Db db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    // Reads the picked answers of one attempt.
    public IReadOnlyList<SubmittedAnswer> GetByAttempt(int attemptId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectByAttemptSql;
        command.Parameters.AddWithValue("$attemptId", attemptId);

        return ReadAnswers(command);
    }

    // Reads the picked answers of one question.
    public IReadOnlyList<SubmittedAnswer> GetByQuestion(int questionId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectByQuestionSql;
        command.Parameters.AddWithValue("$questionId", questionId);

        return ReadAnswers(command);
    }

    // Reads one picked answer.
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

    // Adds a new picked answer.
    public int Add(SubmittedAnswer submittedAnswer)
    {
        ArgumentNullException.ThrowIfNull(submittedAnswer);

        using SqliteConnection connection = _db.OpenConnection();

        int quizId = AttemptAnswerGuard.GetQuizIdOfAttempt(connection, null, submittedAnswer.AttemptId);
        AttemptAnswerGuard.EnsureAnswersBelongToQuiz(connection, null, quizId, new[] { submittedAnswer });

        try
        {
            InsertAnswer(connection, null, submittedAnswer);
        }
        catch (SqliteException exception)
        {
            throw DataAccessException.FromSqlite(exception);
        }

        return submittedAnswer.AttemptId;
    }

    // Deletes a picked answer.
    public bool Delete(int attemptId, int questionId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = DeleteOneSql;
        command.Parameters.AddWithValue("$attemptId", attemptId);
        command.Parameters.AddWithValue("$questionId", questionId);

        return command.ExecuteNonQuery() > 0;
    }

    // Deletes the picked answers of one attempt.
    public void DeleteByAttempt(int attemptId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = DeleteByAttemptSql;
        command.Parameters.AddWithValue("$attemptId", attemptId);

        command.ExecuteNonQuery();
    }

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

    private static SubmittedAnswer ReadAnswer(SqliteDataReader reader) => new()
    {
        AttemptId = reader.GetInt32(0),
        QuestionId = reader.GetInt32(1),
        ChosenChoiceId = reader.GetInt32(2),
    };

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
