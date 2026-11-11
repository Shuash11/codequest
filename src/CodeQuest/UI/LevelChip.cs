// The level badge in the sidebar.

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CodeQuest.UI;

public sealed class LevelChip : Control
{
    private int _level = 1;

    public LevelChip()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.ResizeRedraw, true);
        TabStop = false;
        Cursor = Cursors.Default;
        Size = MeasureFor(ChipText);
    }

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
        Invalidate();
    }

    public int Level => _level;

    private string ChipText => "Lvl " + _level.ToString(System.Globalization.CultureInfo.InvariantCulture);

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(UITheme.ContainerSurface(this));
    }

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

    protected override AccessibleObject CreateAccessibilityInstance() => new LevelChipAccessibleObject(this);

    private Size MeasureFor(string text)
    {
        int textWidth = TextRenderer.MeasureText(text, UITheme.CaptionSemibold).Width;
        return new Size(
            textWidth + UITheme.Scaled(this, UITheme.Spacing8 * 2),
            UITheme.Scaled(this, 20));
    }

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

public static class ChipPainter
{
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
