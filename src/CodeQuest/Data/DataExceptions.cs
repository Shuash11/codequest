// =============================================================================
// CodeQuest — Phase P4: Data Access Layer
// The typed errors of the data layer. The repositories never swallow a database
// error and never let a raw SqliteException reach the caller: every constraint
// the schema defines (UNIQUE per plan ruling R-2 #5, FOREIGN KEY per R-2 #1)
// arrives as one of the types below, so the screens of the later phases can
// show a friendly message instead of a crash (P8 maps these types to text).
//
// Look at: DataAccessException.FromSqlite() — the single place that reads a
//          SQLite error code and decides which type the caller gets.
// =============================================================================

using Microsoft.Data.Sqlite;

namespace CodeQuest.Data;

/// <summary>
/// The base class of every error the data access layer raises. Catching this
/// type catches everything the repositories can throw about the database.
/// </summary>
public class DataAccessException : Exception
{
    /// <summary>Creates the exception with its message.</summary>
    /// <param name="message">What went wrong.</param>
    public DataAccessException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with its message and the original error.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">The error of the database that caused it.</param>
    public DataAccessException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Turns a SQLite error into the typed error that describes it:
    /// a UNIQUE violation becomes a <see cref="DuplicateEntityException"/>, a
    /// FOREIGN KEY violation becomes a <see cref="ReferentialIntegrityException"/>,
    /// a CHECK or NOT NULL violation becomes a
    /// <see cref="ConstraintViolationException"/>.
    /// </summary>
    /// <param name="exception">The error the database raised.</param>
    /// <returns>The typed error for the caller.</returns>
    public static DataAccessException FromSqlite(SqliteException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        int extendedCode = exception.SqliteExtendedErrorCode;
        string message = exception.Message;

        if (extendedCode == SqliteConstraintUnique || extendedCode == SqliteConstraintPrimaryKey)
        {
            return new DuplicateEntityException(
                "The value already exists in the database (a UNIQUE rule was broken). " + message,
                exception);
        }

        if (extendedCode == SqliteConstraintForeignKey)
        {
            return new ReferentialIntegrityException(
                "The row still belongs to another row that the database protects (a FOREIGN KEY rule was broken). " + message,
                exception);
        }

        if (extendedCode == SqliteConstraintCheck || extendedCode == SqliteConstraintNotNull)
        {
            return new ConstraintViolationException(
                "The value is not allowed by the schema (a CHECK or NOT NULL rule was broken). " + message,
                exception);
        }

        // Older SQLite builds do not report the extended code; the message of the
        // driver then names the rule that was broken.
        if (message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
            || message.Contains("PRIMARY KEY", StringComparison.OrdinalIgnoreCase))
        {
            return new DuplicateEntityException(
                "The value already exists in the database (a UNIQUE rule was broken). " + message,
                exception);
        }

        if (message.Contains("FOREIGN KEY constraint failed", StringComparison.OrdinalIgnoreCase))
        {
            return new ReferentialIntegrityException(
                "The row still belongs to another row that the database protects (a FOREIGN KEY rule was broken). " + message,
                exception);
        }

        if (message.Contains("CHECK constraint failed", StringComparison.OrdinalIgnoreCase)
            || message.Contains("NOT NULL constraint failed", StringComparison.OrdinalIgnoreCase))
        {
            return new ConstraintViolationException(
                "The value is not allowed by the schema (a CHECK or NOT NULL rule was broken). " + message,
                exception);
        }

        if (exception.SqliteErrorCode == SqliteConstraint)
        {
            return new ConstraintViolationException(
                "The database refused the value because of one of its rules. " + message,
                exception);
        }

        return new DataAccessException("The database refused the operation. " + message, exception);
    }

    // The SQLite result codes this layer distinguishes (sqlite3.h).
    private const int SqliteConstraint = 19;
    private const int SqliteConstraintCheck = 275;
    private const int SqliteConstraintForeignKey = 787;
    private const int SqliteConstraintNotNull = 1299;
    private const int SqliteConstraintPrimaryKey = 1555;
    private const int SqliteConstraintUnique = 2067;
}

/// <summary>
/// A UNIQUE rule of the schema was broken: the name of a topic, the title of a
/// quiz inside its topic, the position of a question inside its quiz or the text
/// of an option inside its question already exists (plan ruling R-2 #5).
/// </summary>
public sealed class DuplicateEntityException : DataAccessException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">The error of the database that caused it.</param>
    public DuplicateEntityException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// A FOREIGN KEY rule of the schema was broken: the row still belongs to a row
/// that the database protects (a topic that owns quizzes, a quiz that owns
/// attempts, a question that owns answers).
/// </summary>
public sealed class ReferentialIntegrityException : DataAccessException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">The error of the database that caused it.</param>
    public ReferentialIntegrityException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// A CHECK or NOT NULL rule of the schema was broken: for example a topic with
/// a category outside the two the schema allows.
/// </summary>
public sealed class ConstraintViolationException : DataAccessException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">The error of the database that caused it.</param>
    public ConstraintViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// The row that should have been changed does not exist (a row that was deleted
/// in the meantime, or an unknown id).
/// </summary>
public sealed class EntityNotFoundException : DataAccessException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">What went wrong.</param>
    public EntityNotFoundException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// A value that is stored in the database cannot be read as the type of the
/// model — for example a status that is neither Draft nor Approved.
/// </summary>
public sealed class DataFormatException : DataAccessException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">What went wrong.</param>
    public DataFormatException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// An answered question does not belong to the quiz of the attempt
/// (plan ruling R-2 #3). The attempt with such an answer is refused; nothing
/// is written.
/// </summary>
public sealed class AnswerValidationException : DataAccessException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="questionId">The question that was answered.</param>
    /// <param name="quizId">The quiz of the attempt.</param>
    /// <param name="message">What went wrong.</param>
    public AnswerValidationException(int questionId, int quizId, string message)
        : base(message)
    {
        QuestionId = questionId;
        QuizId = quizId;
    }

    /// <summary>The question that was answered.</summary>
    public int QuestionId { get; }

    /// <summary>The quiz the attempt belongs to.</summary>
    public int QuizId { get; }
}
