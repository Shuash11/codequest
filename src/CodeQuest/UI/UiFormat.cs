// Formats text and numbers for the screens.

using System.Globalization;

namespace CodeQuest.UI;

public static class UiFormat
{
    public static string FormatDate(DateTimeOffset value)
        => value.UtcDateTime.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
}
