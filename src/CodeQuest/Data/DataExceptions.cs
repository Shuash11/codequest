// Clear errors for database problems. (Inheritance)

using Microsoft.Data.Sqlite;

namespace CodeQuest.Data;

public class DataAccessException : Exception
{
    public DataAccessException(string message)
        : base(message)
    {
    }

    public DataAccessException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

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

    private const int SqliteConstraint = 19;
    private const int SqliteConstraintCheck = 275;
    private const int SqliteConstraintForeignKey = 787;
    private const int SqliteConstraintNotNull = 1299;
    private const int SqliteConstraintPrimaryKey = 1555;
    private const int SqliteConstraintUnique = 2067;
}

public sealed class DuplicateEntityException : DataAccessException
{
    public DuplicateEntityException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class ReferentialIntegrityException : DataAccessException
{
    public ReferentialIntegrityException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class ConstraintViolationException : DataAccessException
{
    public ConstraintViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class EntityNotFoundException : DataAccessException
{
    public EntityNotFoundException(string message)
        : base(message)
    {
    }
}

public sealed class DataFormatException : DataAccessException
{
    public DataFormatException(string message)
        : base(message)
    {
    }
}

public sealed class AnswerValidationException : DataAccessException
{
    public AnswerValidationException(int questionId, int quizId, string message)
        : base(message)
    {
        QuestionId = questionId;
        QuizId = quizId;
    }

    public int QuestionId { get; }

    public int QuizId { get; }
}
