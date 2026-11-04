// =============================================================================
// CodeQuest — Phase P4: Data Access Layer
// The write-time rules that guard the answers of an attempt.
//
// Plan ruling R-2 #3: "P4 must validate that an answered question belongs to
// the attempt's quiz". The schema alone cannot prove this: the composite
// foreign key of submitted_answers proves that the picked option belongs to
// the answered question, but nothing links the answered question to the quiz
// of the attempt. This class closes that gap BEFORE the row is written, so a
// wrong answer set is refused instead of stored.
//
// Look at: EnsureAnswersBelongToQuiz() — the check that rejects the attempt.
// =============================================================================

using System.Globalization;
using CodeQuest.Models;
using Microsoft.Data.Sqlite;

namespace CodeQuest.Data;

/// <summary>
/// Checks the answers of an attempt before they are written: every answered
/// question has to belong to the quiz of the attempt (plan ruling R-2 #3).
/// </summary>
internal static class AttemptAnswerGuard
{
    /// <summary>
    /// Refuses the answers when one of them points at a question of another
    /// quiz. Nothing is written when this method throws.
    /// </summary>
    /// <param name="connection">The connection of the write.</param>
    /// <param name="transaction">The transaction of the write, or null.</param>
    /// <param name="quizId">The quiz of the attempt.</param>
    /// <param name="answers">The answers that should be written.</param>
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

    /// <summary>
    /// Reads the quiz of an existing attempt.
    /// </summary>
    /// <param name="connection">The connection of the write.</param>
    /// <param name="transaction">The transaction of the write, or null.</param>
    /// <param name="attemptId">The attempt to look at.</param>
    /// <returns>The id of the quiz of the attempt.</returns>
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

    /// <summary>
    /// Reads the ids of the questions of one quiz.
    /// </summary>
    /// <param name="connection">The connection of the write.</param>
    /// <param name="transaction">The transaction of the write, or null.</param>
    /// <param name="quizId">The quiz to look at.</param>
    /// <returns>The ids of its questions.</returns>
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
