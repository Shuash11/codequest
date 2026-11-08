// =============================================================================
// CodeQuest — Gamification UI (plan WP5, spec §4.14): the level chip.
// One implementation of the chip recipe — `AccentTint` fill, `RadiusChip` 4,
// 8px horizontal padding, 20px tall, `CaptionSemibold` text in `AccentText`
// (5.83:1 on the tint, AA) — reused by the sidebar account block, the
// Scoreboard summary card and the §5.7a result reveal. Never a second chip
// design (§4.14: "one implementation, reused").
// Look at: the chip is a STATIC indicator, not a control: no hover, no focus,
//          no tab stop, role StaticText with the accessible name "Level N"
//          (§4.14 states). The corner cutouts of the rounded fill resolve the
//          CONTAINER's surface at paint time (D4 audit rule #1 — the chip sits
//          on the sidebar Background, on cards, and on the result hero card).
//          <see cref="ChipPainter"/> is the shared painter the Scoreboard's
//          "You" grid chip also uses, so every chip in the app is one shape.
// =============================================================================

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CodeQuest.UI;

/// <summary>
/// The §4.14 level chip: a static "Lvl N" badge in the gamification identity
/// (accent family). Not interactive — it reports progress, it is not a button.
/// </summary>
public sealed class LevelChip : Control
{
    private int _level = 1;

    /// <summary>Creates the chip showing level 1 (every account starts there).</summary>
    public LevelChip()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.ResizeRedraw, true);
        TabStop = false; // a static indicator is not a Tab stop (§4.14 states)
        Cursor = Cursors.Default;
        Size = MeasureFor(ChipText);
    }

    /// <summary>Sets the displayed level and resizes the chip to its text.</summary>
    public void SetLevel(int level)
    {
        if (level < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "The level cannot be below 1.");
        }

        if (_level == level)
        {
            return;
        }

        _level = level;
        Size = MeasureFor(ChipText);
        Invalidate(); // the chip paints its own text — a new level must repaint
    }

    /// <summary>The level the chip currently shows.</summary>
    public int Level => _level;

    /// <summary>The chip's text ("Lvl N", §4.14 recipe).</summary>
    private string ChipText => "Lvl " + _level.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The corner cutouts show the CONTAINER's surface, never the chip fill.</summary>
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(UITheme.ContainerSurface(this));
    }

    /// <summary>Paints the §4.14 chip through the one shared painter.</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        ChipPainter.Draw(
            e.Graphics,
            new Rectangle(0, 0, Width, Height),
            ChipText,
            UITheme.CaptionSemibold,
            UITheme.AccentTint,
            UITheme.AccentText,
            UITheme.Scaled(this, UITheme.RadiusChip));
    }

    /// <summary>Static indicator: role StaticText, name "Level N" (§4.14 states).</summary>
    protected override AccessibleObject CreateAccessibilityInstance() => new LevelChipAccessibleObject(this);

    /// <summary>Measures the chip's size for one text (8px horizontal padding, 20px tall).</summary>
    private Size MeasureFor(string text)
    {
        int textWidth = TextRenderer.MeasureText(text, UITheme.CaptionSemibold).Width;
        return new Size(
            textWidth + UITheme.Scaled(this, UITheme.Spacing8 * 2),
            UITheme.Scaled(this, 20));
    }

    /// <summary>The §4.14 accessible contract: StaticText, name "Level N".</summary>
    private sealed class LevelChipAccessibleObject : Control.ControlAccessibleObject
    {
        public LevelChipAccessibleObject(LevelChip owner) : base(owner)
        {
        }

        public override AccessibleRole Role => AccessibleRole.StaticText;

        public override string? Name
            => Owner is LevelChip chip
                ? "Level " + chip.Level.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : base.Name;
    }
}

/// <summary>
/// The one chip painter of the app: every chip (the §4.14 level chip, the
/// Scoreboard's "You" marker) is this shape — a rounded `RadiusChip` fill with
/// one centered `CaptionSemibold`-class label. The caller picks fill/ink/font;
/// the shape never varies (the anti-slop bar: one chip vocabulary).
/// </summary>
public static class ChipPainter
{
    /// <summary>
    /// Draws one chip into <paramref name="bounds"/>. The caller scales the
    /// radius against its own control (the painter has no control context).
    /// </summary>
    /// <param name="graphics">The paint surface.</param>
    /// <param name="bounds">The chip's rectangle.</param>
    /// <param name="text">The chip label, drawn centered.</param>
    /// <param name="font">The label font (chip text is CaptionSemibold-class).</param>
    /// <param name="fill">The chip fill (AccentTint for level/possession chips, Surface for "You").</param>
    /// <param name="ink">The label color (AccentText on both validated fills).</param>
    /// <param name="radiusPixels">The already-scaled corner radius (spec RadiusChip class).</param>
    public static void Draw(
        Graphics graphics,
        Rectangle bounds,
        string text,
        Font font,
        Color fill,
        Color ink,
        int radiusPixels)
    {
        using GraphicsPath path = UITheme.RoundedPath(bounds, radiusPixels);
        using var brush = new SolidBrush(fill);
        graphics.FillPath(brush, path);

        TextRenderer.DrawText(
            graphics,
            text,
            font,
            bounds,
            ink,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }
}
