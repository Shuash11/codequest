// =============================================================================
// CodeQuest — UI theme (tokens re-issued by owner directive D4, 2026-11-08:
// the whole app is DARK-ONLY — the D3 light palette is retired. Every color
// below re-issues the approved D4 amendment's validated tables
// (designs/d4-dark-theme-amendment.md §1: surfaces and neutrals, text,
// accent, semantic colors, fun set — each ratio computed with the WCAG
// relative-luminance formula; floors 4.5:1 normal text, 3:1 large/bold and
// non-text boundaries, AA-exempt for placeholder/disabled roles).)
// The design tokens of the UI design of record (designs/codequest-ui-spec.md,
// section 2 "Visual system", D4 re-issue) as constants, plus the small
// lookups that turn its section-4 control recipes into concrete colors.
// Look at: the token names below are the spec's token names (section 2.1);
//          every custom control and form reads its colors, fonts, spacing and
//          radii from here — no hard-coded colors or sizes in forms
//          (spec section 8, "Implementation handoff").
// New in D4: SurfaceRaised (the third elevation level — dialogs and anything
//          floating), BorderHairline (1px edges of passive containers — the
//          dark replacement for light's contrast-only card separation),
//          AccentText (everywhere light painted Accent as INK — links,
//          secondary labels, grid headers, focus rings; Accent itself stays a
//          FILL only), SegmentHover, the Danger hover/pressed/disabled dark
//          states, the AchievementGlyph font, and the per-tile dark hover
//          constants behind TileHover() (fills blend 15% toward black —
//          lightening on dark would break the tiles' 4.5:1 white labels).
// Values:  pinned from the amendment's tables (§2.1, D4 re-issue) — the dark
//          elevation ramp, the Segoe UI scale (§2.2, unchanged), the 8px
//          spacing grid (§2.3, unchanged) and the 12/8/4 radius classes
//          (§2.4, unchanged). The designer-derived state colors named inside
//          the recipes are tokens here too, so recipes never inline a hex.
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
    // Surfaces and neutrals — the D4 dark elevation ramp. Exactly three
    // levels (Background → Surface → SurfaceRaised), each step lighter
    // 1.08/1.09; a container nests one level per float. State fills (Fill,
    // FillPressed, SegmentHover, tints, washes) are NOT elevation — they
    // always paint on top of the current level.

    /// <summary>Window and sidebar background (elevation level 1).</summary>
    public static readonly Color Background = ColorTranslator.FromHtml("#101216");

    /// <summary>Cards, list containers, input fields, grids (elevation level 2).</summary>
    public static readonly Color Surface = ColorTranslator.FromHtml("#171A20");

    /// <summary>
    /// Dialogs and anything floating — combo dropdown panels (elevation
    /// level 3, D4 new). Never skip a level: no Raised card on Background,
    /// no Surface dialog.
    /// </summary>
    public static readonly Color SurfaceRaised = ColorTranslator.FromHtml("#1E222B");

    /// <summary>Hover/selected row fill, pressed chips, quiet control fills, progress track, disabled inputs.</summary>
    public static readonly Color Fill = ColorTranslator.FromHtml("#232833");

    /// <summary>Secondary button pressed — one step above Fill (D4: neutral fills lighten per state on dark).</summary>
    public static readonly Color FillPressed = ColorTranslator.FromHtml("#2A303D");

    /// <summary>Unselected-segment hover on the segmented control (recipe 4.9's quiet step, D4 new).</summary>
    public static readonly Color SegmentHover = ColorTranslator.FromHtml("#2C3140");

    /// <summary>
    /// Text-input/combo borders, unchecked control rings, stepper — the only
    /// borders with an AA floor: 3.40:1 on Surface, 3.10 on SurfaceRaised,
    /// 3.66 on Background (≥3.0 non-text; D4 re-issue — the plan's #4A5264
    /// measured 2.22:1 and failed its own annotation).
    /// </summary>
    public static readonly Color BorderControl = ColorTranslator.FromHtml("#646E82");

    /// <summary>
    /// 1px edges of PASSIVE containers — every card, the dialog card (D4
    /// new): decorative by classification (1.48:1 on Surface, 1.36 on
    /// SurfaceRaised), chosen to be perceivable without weight. Buttons,
    /// chips, tiles and hover states carry no border.
    /// </summary>
    public static readonly Color BorderHairline = ColorTranslator.FromHtml("#323845");

    /// <summary>1px row separators (see spec section 2.5) and the sidebar's right edge.</summary>
    public static readonly Color SeparatorHairline = ColorTranslator.FromHtml("#2A2F3A");

    // Text — the ink ramp. The first two meet AA on every surface; Tertiary
    // is reserved for AA-exempt roles (placeholders, disabled content,
    // decorative hints — never anything a user must read).

    /// <summary>Primary content (14.49:1 on Surface, 15.59 on Background).</summary>
    public static readonly Color TextPrimary = ColorTranslator.FromHtml("#E8EAF0");

    /// <summary>Metadata, captions, quiet labels (6.88:1 on Surface, 7.40 on Background).</summary>
    public static readonly Color TextSecondary = ColorTranslator.FromHtml("#9BA3B4");

    /// <summary>Placeholder text, disabled text, decorative hints (AA-exempt roles only).</summary>
    public static readonly Color TextTertiary = ColorTranslator.FromHtml("#6B7385");

    /// <summary>
    /// Text/icons on filled accent, filled danger, and the four answer tiles
    /// — white appears ONLY on those fills, never on Fill, Surface, tints or
    /// washes (§2.6 rule, re-pinned).
    /// </summary>
    public static readonly Color TextOnAccent = ColorTranslator.FromHtml("#FFFFFF");

    // Accent — exactly one, and indigo (D4). Accent is a FILL; AccentText is
    // its ink: wherever light painted Accent as text/glyph/outline, dark
    // paints AccentText (Accent as text on dark measures only 3.59:1 on
    // Surface — below the small-text floor; AccentText is 7.18).

    /// <summary>Primary action fill, progress fills — fill only (white label 4.86:1; boundary vs Surface 3.59).</summary>
    public static readonly Color Accent = ColorTranslator.FromHtml("#6C5CE7");

    /// <summary>Pointer hover on filled accent buttons (the accent family darkens per state on dark).</summary>
    public static readonly Color AccentHover = ColorTranslator.FromHtml("#5A4BD1");

    /// <summary>Button while held (darken further; white label 8.07:1).</summary>
    public static readonly Color AccentPressed = ColorTranslator.FromHtml("#493BB8");

    /// <summary>
    /// Disabled filled buttons (D4 re-issue): dim indigo well + TextTertiary
    /// label — disabled must READ as disabled (the old lavender disabled fill
    /// with a white label measured 14.42:1, i.e. it read as enabled).
    /// </summary>
    public static readonly Color AccentDisabled = ColorTranslator.FromHtml("#262938");

    /// <summary>
    /// The accent as INK (D4 new): links, secondary-button labels, grid
    /// column headers, nav active glyph, chips, the XP line, default focus
    /// rings — 7.18:1 on Surface, 7.72 on Background, 5.83 on AccentTint.
    /// </summary>
    public static readonly Color AccentText = ColorTranslator.FromHtml("#A79BF5");

    /// <summary>Selected sidebar item background, selected grid row, chips, unlocked achievement tiles.</summary>
    public static readonly Color AccentTint = ColorTranslator.FromHtml("#2A2749");

    /// <summary>Sidebar nav item pressed (recipe 4.3; AccentText ink on it 6.20:1).</summary>
    public static readonly Color NavItemPressed = ColorTranslator.FromHtml("#262244");

    // Semantic colors — fills + dark-wash tints + text variants. Tints are
    // dark washes: banner/badge backgrounds, never text. The verdict text
    // tokens meet AA on the dark strip (8.31/6.90/8.92 on Background — the
    // F3 interim all-TextPrimary strip rule is definitively discarded).

    /// <summary>Correct-answer marks, success fills.</summary>
    public static readonly Color Success = ColorTranslator.FromHtml("#2FA857");

    /// <summary>Success feedback text — 7.72:1 on Surface, 8.31 on Background, 6.30 on its tint.</summary>
    public static readonly Color SuccessText = ColorTranslator.FromHtml("#4CC27A");

    /// <summary>Success banner/badge background (dark wash).</summary>
    public static readonly Color SuccessTint = ColorTranslator.FromHtml("#16301F");

    /// <summary>
    /// Destructive buttons, incorrect marks (D4 re-issue: white label 4.99:1;
    /// the plan's #C62B25 measured 2.86:1 vs SurfaceRaised, failing the 3:1
    /// boundary floor for a destructive button in a dialog).
    /// </summary>
    public static readonly Color Danger = ColorTranslator.FromHtml("#CF3530");

    /// <summary>Error text — 6.42:1 on Surface, 6.90 on Background, 5.77 on its tint.</summary>
    public static readonly Color DangerText = ColorTranslator.FromHtml("#F07A72");

    /// <summary>Error banner/badge background (dark wash).</summary>
    public static readonly Color DangerTint = ColorTranslator.FromHtml("#3A1A18");

    /// <summary>Destructive flat button hover (D4 dark state; white label 6.42:1).</summary>
    public static readonly Color DangerHover = ColorTranslator.FromHtml("#B32A24");

    /// <summary>Destructive flat button pressed (D4 dark state; white label 8.27:1).</summary>
    public static readonly Color DangerPressed = ColorTranslator.FromHtml("#97201B");

    /// <summary>Destructive flat button disabled (D4 dark state; TextTertiary label, EXEMPT 3.19).</summary>
    public static readonly Color DangerDisabled = ColorTranslator.FromHtml("#2E2325");

    /// <summary>Generation banner border, needs-practice emphasis (border on its tint 3.84:1).</summary>
    public static readonly Color Warning = ColorTranslator.FromHtml("#B07C10");

    /// <summary>Warning text — 8.29:1 on Surface, 8.92 on Background, 6.70 on its tint.</summary>
    public static readonly Color WarningText = ColorTranslator.FromHtml("#E5A83B");

    /// <summary>Warning banner/badge background (dark wash).</summary>
    public static readonly Color WarningTint = ColorTranslator.FromHtml("#38290F");

    // ------------------------------------------------------------- The fun set
    // The learner surfaces (quiz cards, answer tiles, score bars) use the
    // vivid quiz palette — an app about playing quizzes should feel like one.
    // Tile labels are the choice text in TextOnAccent at BodySemibold 10pt —
    // SMALL text by WCAG (large = ≥14pt bold), so the floor is 4.5:1, not the
    // 3:1 large-text reading the light palette assumed. White text on every
    // pinned tile (D4 §1.5): Red 4.82:1, Blue 4.84:1, Amber 4.93:1, Green
    // 4.92:1 — all clear 4.5. (The stale light-era comment claimed 4.3/4.6/
    // 7.4/4.0 — the amber claim was off by 2×; corrected with the re-issue.)

    /// <summary>Primary action of the learner surfaces (play buttons, progress) — the accent alias.</summary>
    public static readonly Color FunPrimary = Accent;

    /// <summary>Hover state of the learner primary (the accent family darkens).</summary>
    public static readonly Color FunPrimaryHover = AccentHover;

    /// <summary>Answer tile 1 — the red tile (white label 4.82:1; kept at the light value).</summary>
    public static readonly Color TileRed = ColorTranslator.FromHtml("#D63040");

    /// <summary>Answer tile 2 — the blue tile (white label 4.84:1; kept at the light value).</summary>
    public static readonly Color TileBlue = ColorTranslator.FromHtml("#2D6BE4");

    /// <summary>Answer tile 3 — the amber tile (D4 re-issue: the light #C77700 measured 3.46:1, below the 4.5 small-text floor; #A65F00 is the brightest amber that clears it at 4.93:1).</summary>
    public static readonly Color TileAmber = ColorTranslator.FromHtml("#A65F00");

    /// <summary>Answer tile 4 — the green tile (D4 re-issue: the light #1E8E4E measured 4.17:1; #178146 clears 4.5 at 4.92:1).</summary>
    public static readonly Color TileGreen = ColorTranslator.FromHtml("#178146");

    // The per-tile dark hover constants (D4 §2/§5#4): on dark, hover DARKENS
    // a vivid fill 15% toward black — lightening would push the white labels
    // below 4.5:1 (TileRed has only ~9% headroom), darkening raises white-
    // label contrast to ≥6.2 and is clearly visible. Explicit constants
    // replace the old light-theme darken helper (system-control paint math
    // that assumed a light UI — banned in the app's painters). Each constant
    // is the tile fill with every channel scaled by 0.85 (rounded).
    private static readonly IReadOnlyDictionary<Color, Color> TileHoverMap = new Dictionary<Color, Color>
    {
        [TileRed] = ColorTranslator.FromHtml("#B62936"), // #D63040 × 0.85
        [TileBlue] = ColorTranslator.FromHtml("#265BC2"), // #2D6BE4 × 0.85
        [TileAmber] = ColorTranslator.FromHtml("#8D5100"), // #A65F00 × 0.85
        [TileGreen] = ColorTranslator.FromHtml("#146E3C"), // #178146 × 0.85
    };

    /// <summary>
    /// The dark hover fill of one answer tile: the per-tile 15%-toward-black
    /// constant for the four tile fills; any other color (defensive — the run
    /// only ever passes the four tiles) gets the same 0.85 blend computed.
    /// </summary>
    public static Color TileHover(Color tile)
        => TileHoverMap.TryGetValue(tile, out Color hover)
            ? hover
            : Color.FromArgb(
                (int)Math.Round(tile.R * 0.85),
                (int)Math.Round(tile.G * 0.85),
                (int)Math.Round(tile.B * 0.85));

    /// <summary>Correct-reveal wash: the tile keeps its hue, the verdict reads green (= TileGreen, mirroring the light relationship).</summary>
    public static readonly Color CorrectWash = ColorTranslator.FromHtml("#178146");

    /// <summary>Wrong-reveal wash (white label 6.59:1).</summary>
    public static readonly Color WrongWash = ColorTranslator.FromHtml("#B3241F");

    /// <summary>
    /// Difficulty "Easy" chip text / score-bar fill (D4 re-issue as the
    /// TEXT-variant colors — dual use: chip text on its wash 6.30:1, bar
    /// fill vs the Fill track 6.54:1).
    /// </summary>
    public static readonly Color LevelEasy = ColorTranslator.FromHtml("#4CC27A");

    /// <summary>Difficulty "Medium" chip text / score-bar fill (chip text on WarningTint 6.70:1; bar fill 7.03:1).</summary>
    public static readonly Color LevelMedium = ColorTranslator.FromHtml("#E5A83B");

    /// <summary>Difficulty "Hard" chip text / score-bar fill (chip text on DangerTint 5.77:1; bar fill 5.43:1).</summary>
    public static readonly Color LevelHard = ColorTranslator.FromHtml("#F07A72");

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

    /// <summary>The 40px achievement tiles' glyph font (Segoe MDL2 Assets 16pt — D4 new, the §5.14 display instance).</summary>
    public static readonly Font AchievementGlyph = new("Segoe MDL2 Assets", 16f);

    /// <summary>The big result score of the fun-UI result screen (Segoe UI Bold 30pt).</summary>
    public static readonly Font Display = new("Segoe UI", 28f, FontStyle.Bold);

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
    /// The surface color of the container a control actually sits on (D4
    /// painter-audit rule #1): the parent's effective BackColor — what the
    /// parent paints behind this control — or the control's own BackColor
    /// while it has no parent yet (construction time). Rounded controls'
    /// corner cutouts and below-field labels must read this at paint time;
    /// a fixed surface token shows as square-corner artifacts the moment the
    /// container sits at a different elevation (e.g. a Surface button's
    /// corners on a SurfaceRaised dialog).
    /// </summary>
    public static Color ContainerSurface(Control control)
        => control.Parent is { } parent ? parent.BackColor : control.BackColor;

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
    /// <remarks>
    /// The Secondary default is <see cref="Color.Empty"/> = transparent (the
    /// recipe's "no border, no fill" — the container surface shows through,
    /// whatever elevation it sits at). The painter skips empty fills and
    /// fills the corner cutouts with <see cref="ContainerSurface"/> instead.
    /// </remarks>
    public static Color FlatButtonFill(FlatButtonVariant variant, FlatButtonState state)
    {
        switch (variant)
        {
            case FlatButtonVariant.Secondary:
                return state switch
                {
                    FlatButtonState.Hover => Fill,
                    FlatButtonState.Pressed => FillPressed,
                    _ => Color.Empty, // transparent — the container shows through (§4.1)
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
                // The accent as INK is AccentText on dark (§2.1 D4 rule);
                // disabled labels are TextTertiary everywhere (§2.6).
                return state == FlatButtonState.Disabled ? TextTertiary : AccentText;

            default: // Primary and Destructive keep their on-accent label…
                // …except disabled: dim fill + TextTertiary label, so a
                // disabled button reads as disabled (§2.6 dark recipe).
                return state == FlatButtonState.Disabled ? TextTertiary : TextOnAccent;
        }
    }

    /// <summary>
    /// §2.6 (D4 re-issue): the one focus-ring vocabulary, case-mapped for the
    /// flat button. Over a filled variant (Primary/Destructive) the ring is
    /// <see cref="TextPrimary"/> — the light "accent outline on accent fill"
    /// rule measures 2.0:1 on dark, invisible. Everything else (Secondary —
    /// transparent, Fill hover, AccentTint selections) rings
    /// <see cref="AccentText"/>. 1px, inset 1px, never a glow.
    /// </summary>
    public static Color FlatButtonFocusRing(FlatButtonVariant variant)
        => variant == FlatButtonVariant.Secondary ? AccentText : TextPrimary;
}
