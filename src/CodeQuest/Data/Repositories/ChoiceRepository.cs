// =============================================================================
// CodeQuest — Phase P4: Data Access Layer
// OOP rubric: ABSTRACTION — ChoiceRepository is the concrete implementation
//              behind the IChoiceRepository contract of phase P3.
// Look at: InsertChoice() below — the one place where an option row is written;
//          the quiz repository and the question repository both use it, so an
//          option is always written the same way.
// Table:   choices (database/schema.sql)
// =============================================================================

using CodeQuest.Models;
using Microsoft.Data.Sqlite;

namespace CodeQuest.Data.Repositories;

/// <summary>
/// Reads and writes the answer options of a question (schema table
/// <c>choices</c>).
/// </summary>
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

    /// <summary>Creates the repository.</summary>
    /// <param name="db">The connection factory of the database.</param>
    public ChoiceRepository(Db db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    /// <inheritdoc />
    public IReadOnlyList<Choice> GetByQuestion(int questionId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectByQuestionSql;
        command.Parameters.AddWithValue("$questionId", questionId);

        return ReadChoices(command);
    }

    /// <inheritdoc />
    public Choice? GetById(int choiceId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectByIdSql;
        command.Parameters.AddWithValue("$choiceId", choiceId);

        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadChoice(reader) : null;
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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
            // An option that an answer of a finished attempt points at is
            // protected by the database (ON DELETE RESTRICT).
            throw DataAccessException.FromSqlite(exception);
        }
    }

    /// <summary>
    /// Writes one option row. The caller decides whether the row belongs to a
    /// transaction — a quiz with its questions and options is one unit of work,
    /// a single option is not.
    /// </summary>
    /// <param name="connection">The connection of the write.</param>
    /// <param name="transaction">The transaction of the write, or null.</param>
    /// <param name="choice">The option to write.</param>
    /// <returns>The id the database gave to the option.</returns>
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

    /// <summary>
    /// Reads every option of one question from an open connection. The quiz
    /// repository and the question repository use this helper when they load a
    /// whole quiz, so the options of a question are always read with the same
    /// query and in the same order.
    /// </summary>
    /// <param name="connection">The connection to read from.</param>
    /// <param name="transaction">The transaction of the read, or null.</param>
    /// <param name="questionId">The question whose options are read.</param>
    /// <returns>The options of the question, in display order.</returns>
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

    /// <summary>Reads one option from the current row.</summary>
    /// <param name="reader">The reader on the current row.</param>
    /// <returns>The option of the row.</returns>
    internal static Choice ReadChoice(SqliteDataReader reader) => new()
    {
        ChoiceId = reader.GetInt32(0),
        QuestionId = reader.GetInt32(1),
        ChoiceText = reader.GetString(2),
        IsCorrect = SqlValueMap.FromStorage(reader.GetInt64(3)),
        Position = reader.GetInt32(4),
    };

    /// <summary>Reads every option of an open command.</summary>
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
