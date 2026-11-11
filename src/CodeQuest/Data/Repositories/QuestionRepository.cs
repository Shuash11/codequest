// Reads and writes questions. (Abstraction)

using CodeQuest.Models;
using Microsoft.Data.Sqlite;

namespace CodeQuest.Data.Repositories;

public sealed class QuestionRepository : IQuestionRepository
{
    private const string SelectByQuizSql =
        """
        SELECT question_id, quiz_id, question_text, explanation, difficulty, position, created_at
        FROM questions
        WHERE quiz_id = $quizId
        ORDER BY position, question_id;
        """;

    private const string SelectByIdSql =
        """
        SELECT question_id, quiz_id, question_text, explanation, difficulty, position, created_at
        FROM questions
        WHERE question_id = $questionId;
        """;

    private const string InsertSql =
        """
        INSERT INTO questions (quiz_id, question_text, explanation, difficulty, position, created_at)
        VALUES ($quizId, $questionText, $explanation, $difficulty, $position, $createdAt);
        """;

    private const string UpdateSql =
        """
        UPDATE questions
        SET question_text = $questionText,
            explanation = $explanation,
            difficulty = $difficulty,
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

    private const int ReorderOffset = 1_000_000;

    private readonly Db _db;

    public QuestionRepository(Db db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    // Reads the questions of one quiz.
    public IReadOnlyList<Question> GetByQuiz(int quizId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        return ReadQuestionsOfQuiz(connection, null, quizId);
    }

    // Reads one question.
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

    // Adds a new question.
    public int Add(Question question)
    {
        ArgumentNullException.ThrowIfNull(question);

        return _db.RunInTransaction((connection, transaction) =>
            InsertQuestionWithChoices(connection, transaction, question));
    }

    // Updates a question.
    public void Update(Question question)
    {
        ArgumentNullException.ThrowIfNull(question);

        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = UpdateSql;
        command.Parameters.AddWithValue("$questionText", question.QuestionText);
        command.Parameters.AddWithValue("$explanation", question.Explanation);
        command.Parameters.AddWithValue("$difficulty", SqlValueMap.ToStorage(question.Difficulty));
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

    // Deletes a question.
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
            throw DataAccessException.FromSqlite(exception);
        }
    }

    // Saves a new question order.
    public void Reorder(int quizId, IReadOnlyList<int> questionIdsInOrder)
    {
        ArgumentNullException.ThrowIfNull(questionIdsInOrder);

        _db.RunInTransaction<object?>((connection, transaction) =>
        {
            using (SqliteCommand lift = connection.CreateCommand())
            {
                lift.CommandText = ReorderLiftSql;
                lift.Parameters.AddWithValue("$reorderOffset", ReorderOffset);
                lift.Parameters.AddWithValue("$quizId", quizId);
                lift.Transaction = transaction;
                lift.ExecuteNonQuery();
            }

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
        command.Parameters.AddWithValue("$difficulty", SqlValueMap.ToStorage(question.Difficulty));
        command.Parameters.AddWithValue("$position", question.Position);
        command.Parameters.AddWithValue("$createdAt", SqlValueMap.ToStorage(question.CreatedAt));
        command.Transaction = transaction;
        command.ExecuteNonQuery();

        return ReadLastInsertRowId(connection, transaction);
    }

    private static Question BuildQuestion(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        SqliteDataReader reader)
    {
        int questionId = reader.GetInt32(0);
        int quizId = reader.GetInt32(1);
        string questionText = reader.GetString(2);
        string explanation = reader.GetString(3);
        Difficulty difficulty = SqlValueMap.ParseDifficulty(reader.GetString(4));
        int position = reader.GetInt32(5);
        DateTimeOffset createdAt = SqlValueMap.FromStorage(reader.GetString(6));

        IReadOnlyList<Choice> choices = ChoiceRepository.ReadChoices(connection, transaction, questionId);

        Question question = IsTrueFalseOptionSet(choices) ? new TrueFalseQuestion() : new MultipleChoiceQuestion();
        question.QuestionId = questionId;
        question.QuizId = quizId;
        question.QuestionText = questionText;
        question.Explanation = explanation;
        question.Difficulty = difficulty;
        question.Position = position;
        question.CreatedAt = createdAt;

        foreach (Choice choice in choices)
        {
            question.AddChoice(choice);
        }

        return question;
    }

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

    private static int ReadLastInsertRowId(SqliteConnection connection, SqliteTransaction? transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = LastInsertRowIdSql;
        command.Transaction = transaction;

        object? value = command.ExecuteScalar();
        return value is null or DBNull ? 0 : Convert.ToInt32(value);
    }
}
