// =============================================================================
// CodeQuest — Phase P4: Data Access Layer
// OOP rubric: ABSTRACTION + POLYMORPHISM — QuestionRepository is the concrete
//              implementation behind the IQuestionRepository contract of phase
//              P3, and it rebuilds the right derived class of Question for every
//              row it reads.
// Look at: BuildQuestion() below — the schema stores no "kind" column, so the
//          concrete type is derived from the option set the row carries: a
//          question whose options are exactly the two "True"/"False" options is
//          a TrueFalseQuestion (the rule TrueFalseQuestion itself validates),
//          every other question is a MultipleChoiceQuestion — including a
//          question that is still missing some of its options while it is being
//          edited (P8). InsertQuestionWithChoices() — the one place where a
//          question row and its options are written; the quiz repository uses
//          it, so a question is always written the same way.
//          Reorder() — the positions of a quiz are unique per quiz
//          (plan ruling R-2 #5), so renumbering runs in two steps inside one
//          transaction: every position of the quiz is lifted above the real
//          range first, then the new positions are written; a failure in the
//          middle rolls the whole renumbering back (plan ruling R-2 #8).
// Table:   questions (database/schema.sql) — with the options of the choices
//          table, which the schema deletes together with their question
//          (ON DELETE CASCADE).
// =============================================================================

using CodeQuest.Models;
using Microsoft.Data.Sqlite;

namespace CodeQuest.Data.Repositories;

/// <summary>
/// Reads and writes the questions of a quiz (schema table <c>questions</c>).
/// </summary>
public sealed class QuestionRepository : IQuestionRepository
{
    private const string SelectByQuizSql =
        """
        SELECT question_id, quiz_id, question_text, explanation, position, created_at
        FROM questions
        WHERE quiz_id = $quizId
        ORDER BY position, question_id;
        """;

    private const string SelectByIdSql =
        """
        SELECT question_id, quiz_id, question_text, explanation, position, created_at
        FROM questions
        WHERE question_id = $questionId;
        """;

    private const string InsertSql =
        """
        INSERT INTO questions (quiz_id, question_text, explanation, position, created_at)
        VALUES ($quizId, $questionText, $explanation, $position, $createdAt);
        """;

    private const string UpdateSql =
        """
        UPDATE questions
        SET question_text = $questionText,
            explanation = $explanation,
            position = $position
        WHERE question_id = $questionId;
        """;

    private const string DeleteSql =
        """
        DELETE FROM questions
        WHERE question_id = $questionId;
        """;

    private const string ReorderLiftSql =
        """
        UPDATE questions
        SET position = position + $reorderOffset
        WHERE quiz_id = $quizId;
        """;

    private const string ReorderSetSql =
        """
        UPDATE questions
        SET position = $position
        WHERE question_id = $questionId
          AND quiz_id = $quizId;
        """;

    private const string SelectUnorderedSql =
        """
        SELECT question_id
        FROM questions
        WHERE quiz_id = $quizId
          AND position > $reorderOffset
        ORDER BY position, question_id;
        """;

    private const string LastInsertRowIdSql = "SELECT last_insert_rowid();";

    /// <summary>
    /// The temporary range questions are lifted into while a quiz is
    /// renumbered — far above every real position of a quiz (a quiz holds
    /// dozens of questions, not a million).
    /// </summary>
    private const int ReorderOffset = 1_000_000;

    private readonly Db _db;

    /// <summary>Creates the repository.</summary>
    /// <param name="db">The connection factory of the database.</param>
    public QuestionRepository(Db db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    /// <inheritdoc />
    public IReadOnlyList<Question> GetByQuiz(int quizId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        return ReadQuestionsOfQuiz(connection, null, quizId);
    }

    /// <inheritdoc />
    public Question? GetById(int questionId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectByIdSql;
        command.Parameters.AddWithValue("$questionId", questionId);

        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return BuildQuestion(connection, null, reader);
    }

    /// <inheritdoc />
    public int Add(Question question)
    {
        ArgumentNullException.ThrowIfNull(question);

        // A question is written together with its options — one unit of work.
        return _db.RunInTransaction((connection, transaction) =>
            InsertQuestionWithChoices(connection, transaction, question));
    }

    /// <inheritdoc />
    public void Update(Question question)
    {
        ArgumentNullException.ThrowIfNull(question);

        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = UpdateSql;
        command.Parameters.AddWithValue("$questionText", question.QuestionText);
        command.Parameters.AddWithValue("$explanation", question.Explanation);
        command.Parameters.AddWithValue("$position", question.Position);
        command.Parameters.AddWithValue("$questionId", question.QuestionId);

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
            throw new EntityNotFoundException($"There is no question with the id {question.QuestionId}.");
        }
    }

    /// <inheritdoc />
    public bool Delete(int questionId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = DeleteSql;
        command.Parameters.AddWithValue("$questionId", questionId);

        try
        {
            return command.ExecuteNonQuery() > 0;
        }
        catch (SqliteException exception)
        {
            // A question that an answer of a finished attempt points at is
            // protected by the database (ON DELETE RESTRICT); the options of
            // the question, in contrast, are deleted with it (CASCADE).
            throw DataAccessException.FromSqlite(exception);
        }
    }

    /// <inheritdoc />
    public void Reorder(int quizId, IReadOnlyList<int> questionIdsInOrder)
    {
        ArgumentNullException.ThrowIfNull(questionIdsInOrder);

        _db.RunInTransaction<object?>((connection, transaction) =>
        {
            // Step 1: lift every position of the quiz out of the real range,
            // so no new position can collide with an old one.
            using (SqliteCommand lift = connection.CreateCommand())
            {
                lift.CommandText = ReorderLiftSql;
                lift.Parameters.AddWithValue("$reorderOffset", ReorderOffset);
                lift.Parameters.AddWithValue("$quizId", quizId);
                lift.Transaction = transaction;
                lift.ExecuteNonQuery();
            }

            // Step 2: write the new positions 1..n in the given order. A
            // question that does not belong to this quiz changes no row and is
            // refused instead of being skipped silently.
            int position = 0;
            foreach (int questionId in questionIdsInOrder)
            {
                position++;

                using SqliteCommand set = connection.CreateCommand();
                set.CommandText = ReorderSetSql;
                set.Parameters.AddWithValue("$position", position);
                set.Parameters.AddWithValue("$questionId", questionId);
                set.Parameters.AddWithValue("$quizId", quizId);
                set.Transaction = transaction;

                if (set.ExecuteNonQuery() == 0)
                {
                    throw new EntityNotFoundException(
                        $"There is no question with the id {questionId} in the quiz {quizId}.");
                }
            }

            // Step 3: questions the caller did not list keep their old relative
            // order and follow after the renumbered ones.
            using (SqliteCommand unordered = connection.CreateCommand())
            {
                unordered.CommandText = SelectUnorderedSql;
                unordered.Parameters.AddWithValue("$reorderOffset", ReorderOffset);
                unordered.Parameters.AddWithValue("$quizId", quizId);
                unordered.Transaction = transaction;

                using SqliteDataReader reader = unordered.ExecuteReader();
                List<int> remainingIds = new();
                while (reader.Read())
                {
                    remainingIds.Add(reader.GetInt32(0));
                }

                foreach (int questionId in remainingIds)
                {
                    position++;

                    using SqliteCommand set = connection.CreateCommand();
                    set.CommandText = ReorderSetSql;
                    set.Parameters.AddWithValue("$position", position);
                    set.Parameters.AddWithValue("$questionId", questionId);
                    set.Parameters.AddWithValue("$quizId", quizId);
                    set.Transaction = transaction;
                    set.ExecuteNonQuery();
                }
            }

            return null;
        });
    }

    /// <summary>
    /// Writes one question and its options. The caller decides whether the
    /// write belongs to a transaction — a quiz with all of its questions is one
    /// unit of work, a single question carries its transaction with it.
    /// The generated ids are written back onto the question and its options.
    /// </summary>
    /// <param name="connection">The connection of the write.</param>
    /// <param name="transaction">The transaction of the write, or null.</param>
    /// <param name="question">The question to write.</param>
    /// <returns>The id the database gave to the question.</returns>
    internal static int InsertQuestionWithChoices(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        Question question)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(question);

        int questionId = InsertQuestion(connection, transaction, question);
        question.QuestionId = questionId;

        foreach (Choice choice in question.Choices)
        {
            ArgumentNullException.ThrowIfNull(choice);
            choice.QuestionId = questionId;
            choice.ChoiceId = ChoiceRepository.InsertChoice(connection, transaction, choice);
        }

        return questionId;
    }

    /// <summary>
    /// Reads every question of one quiz, with their options, in display order.
    /// The quiz repository uses this helper when it loads a whole quiz.
    /// </summary>
    /// <param name="connection">The connection to read from.</param>
    /// <param name="transaction">The transaction of the read, or null.</param>
    /// <param name="quizId">The quiz whose questions are read.</param>
    /// <returns>The questions of the quiz, first one first.</returns>
    internal static IReadOnlyList<Question> ReadQuestionsOfQuiz(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        int quizId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectByQuizSql;
        command.Parameters.AddWithValue("$quizId", quizId);
        command.Transaction = transaction;

        List<Question> questions = new();

        using (SqliteDataReader reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                questions.Add(BuildQuestion(connection, transaction, reader));
            }
        }

        return questions;
    }

    /// <summary>
    /// Writes one question row. The caller decides whether the write belongs to
    /// a transaction; the options are written by
    /// <see cref="InsertQuestionWithChoices"/>, which wraps this method.
    /// </summary>
    /// <param name="connection">The connection of the write.</param>
    /// <param name="transaction">The transaction of the write, or null.</param>
    /// <param name="question">The question to write.</param>
    /// <returns>The id the database gave to the question.</returns>
    private static int InsertQuestion(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        Question question)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = InsertSql;
        command.Parameters.AddWithValue("$quizId", question.QuizId);
        command.Parameters.AddWithValue("$questionText", question.QuestionText);
        command.Parameters.AddWithValue("$explanation", question.Explanation);
        command.Parameters.AddWithValue("$position", question.Position);
        command.Parameters.AddWithValue("$createdAt", SqlValueMap.ToStorage(question.CreatedAt));
        command.Transaction = transaction;
        command.ExecuteNonQuery();

        return ReadLastInsertRowId(connection, transaction);
    }

    /// <summary>
    /// Builds the concrete question of the row under the reader, with its
    /// options. See the file header for how the derived class is chosen.
    /// </summary>
    /// <param name="connection">The connection to read the options from.</param>
    /// <param name="transaction">The transaction of the read, or null.</param>
    /// <param name="reader">The reader on the question row.</param>
    /// <returns>The question of the row.</returns>
    private static Question BuildQuestion(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        SqliteDataReader reader)
    {
        int questionId = reader.GetInt32(0);
        int quizId = reader.GetInt32(1);
        string questionText = reader.GetString(2);
        string explanation = reader.GetString(3);
        int position = reader.GetInt32(4);
        DateTimeOffset createdAt = SqlValueMap.FromStorage(reader.GetString(5));

        IReadOnlyList<Choice> choices = ChoiceRepository.ReadChoices(connection, transaction, questionId);

        Question question = IsTrueFalseOptionSet(choices) ? new TrueFalseQuestion() : new MultipleChoiceQuestion();
        question.QuestionId = questionId;
        question.QuizId = quizId;
        question.QuestionText = questionText;
        question.Explanation = explanation;
        question.Position = position;
        question.CreatedAt = createdAt;

        foreach (Choice choice in choices)
        {
            question.AddChoice(choice);
        }

        return question;
    }

    /// <summary>
    /// Whether the given options are exactly the two options a true/false
    /// question carries — one "True" and one "False", ignoring upper and lower
    /// case and surrounding spaces. This is the same rule
    /// <see cref="TrueFalseQuestion.ValidateAnswer"/> applies.
    /// </summary>
    /// <param name="choices">The options of the question.</param>
    /// <returns>True when the options are the True/False pair.</returns>
    private static bool IsTrueFalseOptionSet(IReadOnlyList<Choice> choices)
    {
        if (choices.Count != 2)
        {
            return false;
        }

        bool hasTrue = false;
        bool hasFalse = false;

        foreach (Choice choice in choices)
        {
            string text = choice.ChoiceText.Trim();

            if (string.Equals(text, "True", StringComparison.OrdinalIgnoreCase))
            {
                hasTrue = true;
            }
            else if (string.Equals(text, "False", StringComparison.OrdinalIgnoreCase))
            {
                hasFalse = true;
            }
        }

        return hasTrue && hasFalse;
    }

    /// <summary>
    /// Reads the id the last insert produced. SQLite reports it through
    /// last_insert_rowid() — the value belongs to the connection that inserted.
    /// </summary>
    private static int ReadLastInsertRowId(SqliteConnection connection, SqliteTransaction? transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = LastInsertRowIdSql;
        command.Transaction = transaction;

        object? value = command.ExecuteScalar();
        return value is null or DBNull ? 0 : Convert.ToInt32(value);
    }
}
