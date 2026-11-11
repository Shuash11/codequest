// Converts values between the app and the database.

using System.Globalization;
using CodeQuest.Models;

namespace CodeQuest.Data;

internal static class SqlValueMap
{
    internal const string DateTimeFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    internal static string ToStorage(DateTimeOffset value)
        => value.UtcDateTime.ToString(DateTimeFormat, CultureInfo.InvariantCulture);

    internal static DateTimeOffset FromStorage(string text)
    {
        if (DateTimeOffset.TryParseExact(
                text,
                DateTimeFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
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

    internal static int ToStorage(bool value) => value ? 1 : 0;

    internal static bool FromStorage(long value) => value != 0;

    internal static string ToStorage(Difficulty value) => value.ToString();

    internal static string ToStorage(QuizStatus value) => value.ToString();

    internal static Difficulty ParseDifficulty(string text)
    {
        if (Enum.TryParse(text, false, out Difficulty difficulty) && Enum.IsDefined(difficulty))
        {
            return difficulty;
        }

        throw new DataFormatException($"The stored difficulty '{text}' is not one of the known difficulties.");
    }

    internal static QuizStatus ParseQuizStatus(string text)
    {
        if (Enum.TryParse(text, false, out QuizStatus status) && Enum.IsDefined(status))
        {
            return status;
        }

        throw new DataFormatException($"The stored status '{text}' is not one of the known statuses.");
    }
}
