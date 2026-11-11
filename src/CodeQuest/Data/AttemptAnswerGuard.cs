// Checks answers before they are saved.

using System.Globalization;
using CodeQuest.Models;
using Microsoft.Data.Sqlite;

namespace CodeQuest.Data;

internal static class AttemptAnswerGuard
{
    internal static void EnsureAnswersBelongToQuiz(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        int quizId,
        IEnumerable<SubmittedAnswer> answers)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(answers);

        HashSet<int> questionIds = LoadQuestionIdsOfQuiz(connection, transaction, quizId);

        foreach (SubmittedAnswer answer in answers)
        {
            if (answer is null)
            {
                throw new ArgumentException("An answer of the attempt cannot be null.", nameof(answers));
            }

            if (!questionIds.Contains(answer.QuestionId))
            {
                throw new AnswerValidationException(
                    answer.QuestionId,
                    quizId,
                    $"Question {answer.QuestionId} does not belong to quiz {quizId}, so the answer cannot be stored.");
            }
        }
    }

    internal static int GetQuizIdOfAttempt(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        int attemptId)
    {
        const string sql =
            """
            SELECT quiz_id
            FROM quiz_attempts
            WHERE attempt_id = $attemptId;
            """;

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$attemptId", attemptId);
        command.Transaction = transaction;

        object? value = command.ExecuteScalar();

        if (value is null || value is DBNull)
        {
            throw new EntityNotFoundException($"There is no attempt with the id {attemptId}.");
        }

        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    internal static HashSet<int> LoadQuestionIdsOfQuiz(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        int quizId)
    {
        const string sql =
            """
            SELECT question_id
            FROM questions
            WHERE quiz_id = $quizId;
            """;

        HashSet<int> questionIds = new();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$quizId", quizId);
        command.Transaction = transaction;

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            questionIds.Add(reader.GetInt32(0));
        }

        return questionIds;
    }
}
