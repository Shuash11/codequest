// =============================================================================
// CodeQuest — UI theme (tokens re-issued by owner directive D3, 2026-11-08:
// the grey and modern theme replaces the blue accent; the contrast pairs the
// reviews flagged — F3, F7, F8, F9 — now meet WCAG AA)
// The design tokens of the UI design of record (designs/codequest-ui-spec.md,
// section 2 "Visual system") as constants, plus the small lookups that turn
// its section-4 control recipes into concrete colors.
// Look at: the token names below are the spec's token names (section 2.1);
//          every custom control and form reads its colors, fonts, spacing and
//          radii from here — no hard-coded colors or sizes in forms
//          (spec section 8, "Implementation handoff").
// Values:  pinned from the spec's tables (§2.1, D3 re-issue) — the neutral
//          grey ramp, the Segoe UI scale (§2.2), the 8px spacing grid (§2.3)
//          and the 12/8/4 radius classes (§2.4). The designer-derived state
//          colors named inside the recipes (§4.1 secondary pressed
//          "FillPressed", §4.1 destructive hover/pressed/disabled, §4.3 nav
//          pressed) are tokens here too, so recipes never inline a hex value.
// Scaling: the spec's pixel numbers are standard-DPI values (§4 preamble);
//          Scaled() converts them with a control's DeviceDpi. Fonts are
//          declared in points and scale with the display automatically.
// =============================================================================

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CodeQuest.UI;

/// <summary>
/// The single source of the design system: colors, fonts, spacing and radii of
/// the design of record, plus the recipe-to-color lookups of section 4.
/// </summary>
public static class UITheme
{
    // ------------------------------------------------------------------ §2.1
    // Surfaces and neutrals (the D3 grey ramp, light mode).

    /// <summary>Window and sidebar background (grouped "primary").</summary>
    public static readonly Color Background = ColorTranslator.FromHtml("#F5F5F7");

    /// <summary>Cards, list containers, input fields (grouped "secondary").</summary>
    public static readonly Color Surface = ColorTranslator.FromHtml("#FFFFFF");

    /// <summary>Selected/hover row fill, pressed chips, quiet control fills.</summary>
    public static readonly Color Fill = ColorTranslator.FromHtml("#E9E9EE");

    /// <summary>
    /// Text-input and combo borders — and the unchecked control border (F9):
    /// 3.25:1 on Surface, above the 3:1 WCAG non-text floor.
    /// </summary>
    public static readonly Color BorderControl = ColorTranslator.FromHtml("#8E8E93");

    /// <summary>1px row separators (see spec section 2.5).</summary>
    public static readonly Color SeparatorHairline = ColorTranslator.FromHtml("#D8D8DC");

    // Text (three opaque desktop tokens; the first two meet AA on Surface and
    // Background — F7's 2.92:1 header text becomes 4.66:1).

    /// <summary>Primary content.</summary>
    public static readonly Color TextPrimary = ColorTranslator.FromHtml("#1D1D1F");

    /// <summary>Metadata: dates, sub-labels, quiet column headers (5.07:1 on Surface).</summary>
    public static readonly Color TextSecondary = ColorTranslator.FromHtml("#6E6E73");

    /// <summary>Placeholder text, disabled text, decorative hints (AA-exempt roles).</summary>
    public static readonly Color TextTertiary = ColorTranslator.FromHtml("#98989D");

    /// <summary>Text/icons on filled accent or filled danger buttons.</summary>
    public static readonly Color TextOnAccent = ColorTranslator.FromHtml("#FFFFFF");

    // Accent — exactly one, and grey (directive D3: the blue accent is gone).
    // White text on the filled accent: 9.1:1; the accent as a text label on
    // Surface (secondary buttons): 9.1:1.

    /// <summary>Primary action fill, sidebar active state, focused input border, links.</summary>
    public static readonly Color Accent = ColorTranslator.FromHtml("#3A3A3C");

    /// <summary>Pointer hover on filled accent buttons (darken).</summary>
    public static readonly Color AccentHover = ColorTranslator.FromHtml("#2C2C2E");

    /// <summary>Button while held (darken further).</summary>
    public static readonly Color AccentPressed = ColorTranslator.FromHtml("#232326");

    /// <summary>Disabled filled buttons (disabled text/fills are AA-exempt).</summary>
    public static readonly Color AccentDisabled = ColorTranslator.FromHtml("#C7C7CC");

    /// <summary>Selected sidebar item background, focused control borders.</summary>
    public static readonly Color AccentTint = ColorTranslator.FromHtml("#E0E0E5");

    // Semantic colors — the text variants meet AA on their tints and on white
    // (F3: the old 4.07/4.08:1 pair is gone; F8's badge text is 4.75:1).

    /// <summary>Approved badge fill, correct-answer marks.</summary>
    public static readonly Color Success = ColorTranslator.FromHtml("#2E9E48");

    /// <summary>Success feedback text — 5.5:1 on white, 4.75:1 on its tint.</summary>
    public static readonly Color SuccessText = ColorTranslator.FromHtml("#157A38");

    /// <summary>Success banner/badge background (mix on white).</summary>
    public static readonly Color SuccessTint = ColorTranslator.FromHtml("#DFF5E7");

    /// <summary>Destructive buttons, incorrect marks (white label 5.5:1).</summary>
    public static readonly Color Danger = ColorTranslator.FromHtml("#C62B25");

    /// <summary>Error text — 6.6:1 on white, 5.6:1 on its tint (F3).</summary>
    public static readonly Color DangerText = ColorTranslator.FromHtml("#B3241F");

    /// <summary>Error banner/badge background (mix on white).</summary>
    public static readonly Color DangerTint = ColorTranslator.FromHtml("#FDE8E9");

    /// <summary>Generation banner border, needs-practice emphasis (3.6:1 on its tint).</summary>
    public static readonly Color Warning = ColorTranslator.FromHtml("#A97400");

    /// <summary>Warning text — 6.0:1 on white, 5.4:1 on its tint.</summary>
    public static readonly Color WarningText = ColorTranslator.FromHtml("#8A5A00");

    /// <summary>Warning banner/badge background (mix on white).</summary>
    public static readonly Color WarningTint = ColorTranslator.FromHtml("#FFF3E0");

    // Designer-derived state colors named inside the recipes (spec section 4).

    /// <summary>Secondary flat button pressed (recipe 4.1 "FillPressed").</summary>
    public static readonly Color FillPressed = ColorTranslator.FromHtml("#DCDCE2");

    /// <summary>Sidebar nav item pressed (recipe 4.3).</summary>
    public static readonly Color NavItemPressed = ColorTranslator.FromHtml("#D5D5DA");

    /// <summary>Destructive flat button hover (recipe 4.1).</summary>
    public static readonly Color DangerHover = ColorTranslator.FromHtml("#AD241E");

    /// <summary>Destructive flat button pressed (recipe 4.1).</summary>
    public static readonly Color DangerPressed = ColorTranslator.FromHtml("#982019");

    /// <summary>Destructive flat button disabled (recipe 4.1).</summary>
    public static readonly Color DangerDisabled = ColorTranslator.FromHtml("#EFC9C6");

    /// <summary>Unselected-segment hover on the segmented control (recipe 4.9's derived quiet step).</summary>
    public static readonly Color SegmentHover = ColorTranslator.FromHtml("#F0F0F4");

    // ------------------------------------------------------------------ §2.2
    // The 3-step size hierarchy plus the two in-size weight variants, and the
    // system glyph font for the few icons that earn their place (§3.7).

    /// <summary>Screen titles in the content header (Segoe UI Semibold 16pt).</summary>
    public static readonly Font Title = new("Segoe UI Semibold", 16f);

    /// <summary>Everything else by default (Segoe UI Regular 10pt).</summary>
    public static readonly Font Body = new("Segoe UI", 10f);

    /// <summary>Metadata: dates, sub-labels, hints (Segoe UI Regular 9pt).</summary>
    public static readonly Font Caption = new("Segoe UI", 9f);

    /// <summary>Row/card titles, primary button labels, form field labels (Segoe UI Semibold 10pt).</summary>
    public static readonly Font BodySemibold = new("Segoe UI Semibold", 10f);

    /// <summary>Badge text where extra presence is needed (Segoe UI Semibold 9pt).</summary>
    public static readonly Font CaptionSemibold = new("Segoe UI Semibold", 9f);

    /// <summary>Sidebar nav item glyphs (Segoe MDL2 Assets 12pt, recipe 4.3).</summary>
    public static readonly Font NavGlyph = new("Segoe MDL2 Assets", 12f);

    // ------------------------------------------------------------------ §2.3
    // Spacing scale (8px grid). No other value appears in any layout.

    /// <summary>Tight internal pairs only: icon ↔ label, badge text padding.</summary>
    public const int Spacing4 = 4;

    /// <summary>Control ↔ control inside a group; button padding horizontal.</summary>
    public const int Spacing8 = 8;

    /// <summary>List-row vertical padding; input vertical padding.</summary>
    public const int Spacing12 = 12;

    /// <summary>Screen margin; card ↔ card; field ↔ field in a form.</summary>
    public const int Spacing16 = 16;

    /// <summary>Section gap: filter bar ↔ grid, grouped cards.</summary>
    public const int Spacing24 = 24;

    /// <summary>Content-header block ↔ content below it.</summary>
    public const int Spacing32 = 32;

    // ------------------------------------------------------------------ §2.4
    // Corner radius conventions — one radius per element class.

    /// <summary>Large containers: cards, grouped list containers, dialogs.</summary>
    public const int RadiusCard = 12;

    /// <summary>Controls: buttons, text inputs, combo boxes, progress track.</summary>
    public const int RadiusControl = 8;

    /// <summary>Small chips: status badges.</summary>
    public const int RadiusChip = 4;

    /// <summary>
    /// Applies the theme to the running application: the default font of every
    /// control becomes <see cref="Body"/> (spec section 2.2 — one typeface,
    /// Body is the default size). Called once at startup, before any window.
    /// </summary>
    public static void Apply()
    {
        Application.SetDefaultFont(Body);
    }

    /// <summary>
    /// Converts one standard-DPI pixel value of the spec (§4 preamble) to the
    /// device pixels of the given control's display.
    /// </summary>
    public static int Scaled(Control control, int pixels)
    {
        return (int)Math.Round(pixels * control.DeviceDpi / 96f);
    }

    /// <summary>
    /// Builds the rounded-rectangle path of the spec's corner-radius
    /// conventions (§2.4) — GDI+ has no native rounded rectangle fill, so the
    /// controls draw one with arcs. The caller owns the returned path.
    /// </summary>
    public static GraphicsPath RoundedPath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return path;
        }

        radius = Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2);
        int diameter = radius * 2;

        if (radius == 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    // ------------------------------------------------------------- §4.1 flat
    // button recipes. The control (FlatButton) paints; these lookups carry the
    // recipe's state tables so the state-to-color mapping lives with the
    // tokens, not inside the painter.

    /// <summary>The three flat button variants of recipe 4.1.</summary>
    public enum FlatButtonVariant
    {
        /// <summary>Filled accent — one per screen region, the main action.</summary>
        Primary,

        /// <summary>Cancel/auxiliary — transparent fill, accent label.</summary>
        Secondary,

        /// <summary>Delete/confirm-destroy — filled danger.</summary>
        Destructive,
    }

    /// <summary>The interaction states the recipes define for flat buttons.</summary>
    public enum FlatButtonState
    {
        Default,
        Hover,
        Pressed,
        Focus,
        Disabled,
    }

    /// <summary>Recipe 4.1: the fill of one variant in one state.</summary>
    public static Color FlatButtonFill(FlatButtonVariant variant, FlatButtonState state)
    {
        switch (variant)
        {
            case FlatButtonVariant.Secondary:
                return state switch
                {
                    FlatButtonState.Hover => Fill,
                    FlatButtonState.Pressed => FillPressed,
                    _ => Surface, // transparent on the white cards/dialogs it sits on
                };

            case FlatButtonVariant.Destructive:
                return state switch
                {
                    FlatButtonState.Hover => DangerHover,
                    FlatButtonState.Pressed => DangerPressed,
                    FlatButtonState.Disabled => DangerDisabled,
                    _ => Danger,
                };

            default: // Primary
                return state switch
                {
                    FlatButtonState.Hover => AccentHover,
                    FlatButtonState.Pressed => AccentPressed,
                    FlatButtonState.Disabled => AccentDisabled,
                    _ => Accent,
                };
        }
    }

    /// <summary>Recipe 4.1: the label color of one variant in one state.</summary>
    public static Color FlatButtonLabel(FlatButtonVariant variant, FlatButtonState state)
    {
        switch (variant)
        {
            case FlatButtonVariant.Secondary:
                return state == FlatButtonState.Disabled ? TextTertiary : Accent;

            default: // Primary and Destructive keep their on-accent label
                return TextOnAccent;
        }
    }
}
