// =============================================================================
// CodeQuest — Quiz Taking (P9): the determinate progress track of recipe 4.13
// (spec 5.6 moment 2). Track: 8px tall, Fill fill, 4px radius (pill); the fill
// segment is Accent with the value answered ÷ total. Unlike the generation
// busy bar (P13's indeterminate marquee) this progress is genuinely known:
// the run counts its own answered questions.
// Look at: OnPaint — the owner-drawn pill; Value (0..1) is the only state.
// =============================================================================

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CodeQuest.UI;

/// <summary>
/// The determinate progress track of recipe 4.13 as spec 5.6's run progress:
/// an 8px pill whose Accent segment shows the answered fraction.
/// </summary>
public sealed class ProgressTrack : Control
{
    private double _value;
    private bool _marquee;
    private int _marqueeX;
    private readonly System.Windows.Forms.Timer _marqueeTimer = new() { Interval = 60 };

    /// <summary>Creates the track at the recipe's 8px height.</summary>
    public ProgressTrack()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.ResizeRedraw, true);
        // The pill corners show the CONTAINER's surface, resolved at paint
        // time (OnPaintBackground) — a track sits on module backgrounds AND
        // inside cards (D4 audit rule #1; §5.7a puts one on a card).
        BackColor = UITheme.Background;
        Height = UITheme.Scaled(this, 8);
        TabStop = false; // a read-only indicator is not a Tab stop

        // The §4.13 marquee: an Accent segment sweeps the track while real
        // work runs. No percent, no fake progress — the promise is activity.
        _marqueeTimer.Tick += static (sender, e) =>
        {
            if (sender is System.Windows.Forms.Timer timer && timer.Tag is ProgressTrack track)
            {
                int segmentWidth = UITheme.Scaled(track, 60);
                track._marqueeX += Math.Max(track.Width / 24, UITheme.Scaled(track, 6));
                if (track._marqueeX > track.Width + segmentWidth)
                {
                    track._marqueeX = -segmentWidth;
                }

                track.Invalidate();
            }
        };
        _marqueeTimer.Tag = this;
    }

    /// <summary>Stops the marquee timer when the control is disposed.</summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _marqueeTimer.Stop();
            _marqueeTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// The §4.13 indeterminate mode: an Accent segment sweeps while a long
    /// operation runs (generation's duration is unknowable — no percent).
    /// </summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Marquee
    {
        get => _marquee;
        set
        {
            if (_marquee == value)
            {
                return;
            }

            _marquee = value;
            if (_marquee)
            {
                _marqueeX = -UITheme.Scaled(this, 60);
                _marqueeTimer.Start();
            }
            else
            {
                _marqueeTimer.Stop();
            }

            Invalidate();
        }
    }

    /// <summary>
    /// The answered fraction (0..1). Values outside the range are clamped —
    /// the run computes the fraction from its own counts, so the clamp is a
    /// display guarantee, never a data correction. The control is
    /// owner-drawn, so a new value schedules its own repaint.
    /// </summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public double Value
    {
        get => _value;
        set
        {
            double clamped = Math.Clamp(value, 0d, 1d);
            if (_value == clamped)
            {
                return;
            }

            _value = clamped;
            Invalidate(); // the pill paints the fraction — a new value must repaint
        }
    }

    /// <summary>
    /// The corner cutouts around the pill show the CONTAINER's surface, not a
    /// fixed token (D4 audit rule #1) — the pill paints in OnPaint.
    /// </summary>
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(UITheme.ContainerSurface(this));
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

        if (_marquee)
        {
            // The sweeping segment (§4.13): activity, never a percent. The
            // clip keeps the sweep inside the pill at both edges.
            e.Graphics.SetClip(track);
            Rectangle segment = new(_marqueeX, 0, UITheme.Scaled(this, 60), Height);
            using var segmentBrush = new SolidBrush(UITheme.Accent);
            DrawPill(e.Graphics, segmentBrush, segment);
            e.Graphics.ResetClip();
            return;
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
