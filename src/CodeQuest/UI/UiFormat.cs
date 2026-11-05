// =============================================================================
// CodeQuest — Phase P8: Topic & Quiz Management
// The one shared display formatter of the management screens (topics grid,
// quiz grid, question list). Copy and value formats follow the design of
// record (designs/codequest-ui-spec.md) — dates read like the spec's own
// example "Nov 4, 2026" (§5.9 score line).
// Look at: FormatDate — the single place that turns a stored timestamp into
//          the display text, invariant and deterministic so the grids and
//          tests agree.
// =============================================================================

using System.Globalization;

namespace CodeQuest.UI;

/// <summary>Display formatting shared by the management screens.</summary>
public static class UiFormat
{
    /// <summary>
    /// Formats a stored timestamp as the spec's display date ("Nov 4, 2026").
    /// </summary>
    /// <param name="value">The timestamp of the model.</param>
    /// <returns>The display text.</returns>
    public static string FormatDate(DateTimeOffset value)
        => value.UtcDateTime.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
}
