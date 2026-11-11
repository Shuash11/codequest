// Reads and writes answer options. (Abstraction)

using CodeQuest.Models;
using Microsoft.Data.Sqlite;

namespace CodeQuest.Data.Repositories;

public sealed class ChoiceRepository : IChoiceRepository
{
    private const string SelectByQuestionSql =
        """
        SELECT choice_id, question_id, choice_text, is_correct, position
        FROM choices
        WHERE question_id = $questionId
        ORDER BY position, choice_id;
        """;

    private const string SelectByIdSql =
        """
        SELECT choice_id, question_id, choice_text, is_correct, position
        FROM choices
        WHERE choice_id = $choiceId;
        """;

    private const string InsertSql =
        """
        INSERT INTO choices (question_id, choice_text, is_correct, position)
        VALUES ($questionId, $choiceText, $isCorrect, $position);
        """;

    private const string UpdateSql =
        """
        UPDATE choices
        SET choice_text = $choiceText,
            is_correct = $isCorrect,
            position = $position
        WHERE choice_id = $choiceId;
        """;

    private const string DeleteSql =
        """
        DELETE FROM choices
        WHERE choice_id = $choiceId;
        """;

    private const string LastInsertRowIdSql = "SELECT last_insert_rowid();";

    private readonly Db _db;

    public ChoiceRepository(Db db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    // Reads the choices of one question.
    public IReadOnlyList<Choice> GetByQuestion(int questionId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectByQuestionSql;
        command.Parameters.AddWithValue("$questionId", questionId);

        return ReadChoices(command);
    }

    // Reads one choice.
    public Choice? GetById(int choiceId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectByIdSql;
        command.Parameters.AddWithValue("$choiceId", choiceId);

        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadChoice(reader) : null;
    }

    // Adds a new choice.
    public int Add(Choice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);

        using SqliteConnection connection = _db.OpenConnection();

        try
        {
            int choiceId = InsertChoice(connection, null, choice);
            choice.ChoiceId = choiceId;
            return choiceId;
        }
        catch (SqliteException exception)
        {
            throw DataAccessException.FromSqlite(exception);
        }
    }

    // Updates a choice.
    public void Update(Choice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);

        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = UpdateSql;
        command.Parameters.AddWithValue("$choiceText", choice.ChoiceText);
        command.Parameters.AddWithValue("$isCorrect", SqlValueMap.ToStorage(choice.IsCorrect));
        command.Parameters.AddWithValue("$position", choice.Position);
        command.Parameters.AddWithValue("$choiceId", choice.ChoiceId);

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
            throw new EntityNotFoundException($"There is no option with the id {choice.ChoiceId}.");
        }
    }

    // Deletes a choice.
    public bool Delete(int choiceId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = DeleteSql;
        command.Parameters.AddWithValue("$choiceId", choiceId);

        try
        {
            return command.ExecuteNonQuery() > 0;
        }
        catch (SqliteException exception)
        {
            throw DataAccessException.FromSqlite(exception);
        }
    }

    internal static int InsertChoice(SqliteConnection connection, SqliteTransaction? transaction, Choice choice)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(choice);

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = InsertSql;
        command.Parameters.AddWithValue("$questionId", choice.QuestionId);
        command.Parameters.AddWithValue("$choiceText", choice.ChoiceText);
        command.Parameters.AddWithValue("$isCorrect", SqlValueMap.ToStorage(choice.IsCorrect));
        command.Parameters.AddWithValue("$position", choice.Position);
        command.Transaction = transaction;
        command.ExecuteNonQuery();

        return ReadLastInsertRowId(connection, transaction);
    }

    internal static IReadOnlyList<Choice> ReadChoices(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        int questionId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectByQuestionSql;
        command.Parameters.AddWithValue("$questionId", questionId);
        command.Transaction = transaction;

        return ReadChoices(command);
    }

    internal static Choice ReadChoice(SqliteDataReader reader) => new()
    {
        ChoiceId = reader.GetInt32(0),
        QuestionId = reader.GetInt32(1),
        ChoiceText = reader.GetString(2),
        IsCorrect = SqlValueMap.FromStorage(reader.GetInt64(3)),
        Position = reader.GetInt32(4),
    };

    private static IReadOnlyList<Choice> ReadChoices(SqliteCommand command)
    {
        List<Choice> choices = new();

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            choices.Add(ReadChoice(reader));
        }

        return choices;
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
