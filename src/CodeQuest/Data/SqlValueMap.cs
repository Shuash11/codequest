// =============================================================================
// CodeQuest — Phase P4: Data Access Layer
// The mapping between the types of the domain models and the types of the
// SQLite columns. The mappings are the ones the model headers of phase P3
// document — this class does not invent new ones:
//   * DateTimeOffset  <-> ISO-8601 UTC text  ("yyyy-MM-ddTHH:mm:ssZ")
//   * bool            <-> 0 / 1 (the is_correct column)
//   * Difficulty      <-> the member name as text ("Easy", "Medium", "Hard")
//   * QuizStatus      <-> the member name as text ("Draft", "Approved")
// Look at: ToStorage()/FromStorage() — every repository uses these helpers, so
//          one row is always written and read the same way.
// =============================================================================

using System.Globalization;
using CodeQuest.Models;

namespace CodeQuest.Data;

/// <summary>
/// Converts the values of the domain models into the values of the SQLite
/// columns and back.
/// </summary>
internal static class SqlValueMap
{
    /// <summary>The format of a timestamp column: ISO-8601 in UTC.</summary>
    internal const string DateTimeFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    /// <summary>Writes a timestamp as ISO-8601 UTC text.</summary>
    /// <param name="value">The timestamp of the model.</param>
    /// <returns>The text that goes into the column.</returns>
    internal static string ToStorage(DateTimeOffset value)
        => value.UtcDateTime.ToString(DateTimeFormat, CultureInfo.InvariantCulture);

    /// <summary>
    /// Reads a timestamp column as a UTC timestamp. A value that is not a date
    /// at all is reported as a <see cref="DataFormatException"/>.
    /// </summary>
    /// <param name="text">The text of the column.</param>
    /// <returns>The timestamp of the model.</returns>
    internal static DateTimeOffset FromStorage(string text)
    {
        if (DateTimeOffset.TryParseExact(
                text,
                DateTimeFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTimeOffset exact))
        {
            return exact.ToUniversalTime();
        }

        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset parsed))
        {
            return parsed.ToUniversalTime();
        }

        throw new DataFormatException($"The stored date and time '{text}' cannot be read as a date and time.");
    }

    /// <summary>Writes a flag as 1 (true) or 0 (false).</summary>
    /// <param name="value">The flag of the model.</param>
    /// <returns>The number that goes into the column.</returns>
    internal static int ToStorage(bool value) => value ? 1 : 0;

    /// <summary>Reads a flag from the 0 / 1 column.</summary>
    /// <param name="value">The number of the column.</param>
    /// <returns>The flag of the model.</returns>
    internal static bool FromStorage(long value) => value != 0;

    /// <summary>Writes the difficulty as the name of its member.</summary>
    /// <param name="value">The difficulty of the model.</param>
    /// <returns>The text that goes into the column.</returns>
    internal static string ToStorage(Difficulty value) => value.ToString();

    /// <summary>Writes the status as the name of its member.</summary>
    /// <param name="value">The status of the model.</param>
    /// <returns>The text that goes into the column.</returns>
    internal static string ToStorage(QuizStatus value) => value.ToString();

    /// <summary>Reads a difficulty column.</summary>
    /// <param name="text">The text of the column.</param>
    /// <returns>The difficulty of the model.</returns>
    internal static Difficulty ParseDifficulty(string text)
    {
        if (Enum.TryParse(text, false, out Difficulty difficulty) && Enum.IsDefined(difficulty))
        {
            return difficulty;
        }

        throw new DataFormatException($"The stored difficulty '{text}' is not one of the known difficulties.");
    }

    /// <summary>Reads a status column.</summary>
    /// <param name="text">The text of the column.</param>
    /// <returns>The status of the model.</returns>
    internal static QuizStatus ParseQuizStatus(string text)
    {
        if (Enum.TryParse(text, false, out QuizStatus status) && Enum.IsDefined(status))
        {
            return status;
        }

        throw new DataFormatException($"The stored status '{text}' is not one of the known statuses.");
    }
}
