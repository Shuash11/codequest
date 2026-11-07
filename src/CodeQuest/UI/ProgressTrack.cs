// =============================================================================
// CodeQuest — Quiz Taking (P9): the determinate progress track of recipe 4.13
// (spec 5.6 moment 2). Track: 8px tall, Fill fill, 4px radius (pill); the fill
// segment is Accent with the value answered ÷ total. Unlike the generation
// busy bar (P13's indeterminate marquee) this progress is genuinely known:
// the run counts its own answered questions.
// Look at: OnPaint — the owner-drawn pill; Value (0..1) is the only state.
// =============================================================================

using System.Drawing;
using System.Windows.Forms;

namespace CodeQuest.UI;

/// <summary>
/// The determinate progress track of recipe 4.13 as spec 5.6's run progress:
/// an 8px pill whose Accent segment shows the answered fraction.
/// </summary>
public sealed class ProgressTrack : Control
{
    private double _value;

    /// <summary>Creates the track at the recipe's 8px height.</summary>
    public ProgressTrack()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.ResizeRedraw, true);
        BackColor = UITheme.Background;
        Height = UITheme.Scaled(this, 8);
        TabStop = false; // a read-only indicator is not a Tab stop
    }

    /// <summary>
    /// The answered fraction (0..1). Values outside the range are clamped —
    /// the run computes the fraction from its own counts, so the clamp is a
    /// display guarantee, never a data correction.
    /// </summary>
    public double Value
    {
        get => _value;
        set => _value = Math.Clamp(value, 0d, 1d);
    }

    /// <summary>Paints the Fill pill and the Accent segment (recipe 4.13).</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (Width <= 0 || Height <= 0)
        {
            return;
        }

        Rectangle track = new(0, 0, Width, Height);
        using (var trackBrush = new SolidBrush(UITheme.Fill))
        {
            DrawPill(e.Graphics, trackBrush, track);
        }

        int fillWidth = (int)Math.Round(Width * _value);
        if (fillWidth >= UITheme.Scaled(this, 1))
        {
            Rectangle fill = new(0, 0, fillWidth, Height);
            using var fillBrush = new SolidBrush(UITheme.Accent);
            DrawPill(e.Graphics, fillBrush, fill);
        }
    }

    /// <summary>Draws one pill-shaped rectangle with the recipe's 4px radius.</summary>
    private void DrawPill(Graphics graphics, Brush brush, Rectangle bounds)
    {
        int radius = UITheme.Scaled(this, 4);
        using GraphicsPath path = UITheme.RoundedPath(bounds, radius);
        graphics.FillPath(brush, path);
    }

    /// <summary>The track is a read-only indicator: name it for UIA (§2.6).</summary>
    protected override AccessibleObject CreateAccessibilityInstance() => new ProgressTrackAccessibleObject(this);

    /// <summary>Progress-bar role; the state lives in the run header's question count.</summary>
    private sealed class ProgressTrackAccessibleObject : Control.ControlAccessibleObject
    {
        public ProgressTrackAccessibleObject(ProgressTrack owner) : base(owner)
        {
        }

        public override AccessibleRole Role => AccessibleRole.ProgressBar;
    }
}
