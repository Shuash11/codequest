// The progress bar.

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CodeQuest.UI;

public sealed class ProgressTrack : Control
{
    private double _value;
    private bool _marquee;
    private int _marqueeX;
    private readonly System.Windows.Forms.Timer _marqueeTimer = new() { Interval = 60 };

    public ProgressTrack()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.ResizeRedraw, true);
        BackColor = UITheme.Background;
        Height = UITheme.Scaled(this, 8);
        TabStop = false;

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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _marqueeTimer.Stop();
            _marqueeTimer.Dispose();
        }

        base.Dispose(disposing);
    }

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
            Invalidate();
        }
    }

    private int _segments;

    /// <summary>
    /// When greater than 1, thin separators divide the track into N equal
    /// segments (one per question) — progress becomes countable. 0 = plain.
    /// Marquee mode ignores segments.
    /// </summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Segments
    {
        get => _segments;
        set
        {
            if (_segments == value)
            {
                return;
            }

            _segments = value;
            Invalidate();
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(UITheme.ContainerSurface(this));
    }

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

        if (_segments > 1)
        {
            // The countable variant (design spec R2.1): separators cut the
            // pill into one segment per question, in the container's surface
            // color so they read through both track and fill.
            e.Graphics.SetClip(track);
            using var separatorBrush = new SolidBrush(UITheme.ContainerSurface(this));
            int separatorWidth = UITheme.Scaled(this, 2);
            double step = (double)Width / _segments;
            for (int i = 1; i < _segments; i++)
            {
                int x = (int)Math.Round(i * step) - separatorWidth / 2;
                e.Graphics.FillRectangle(separatorBrush, x, 0, separatorWidth, Height);
            }

            e.Graphics.ResetClip();
        }
    }

    private void DrawPill(Graphics graphics, Brush brush, Rectangle bounds)
    {
        int radius = UITheme.Scaled(this, 4);
        using GraphicsPath path = UITheme.RoundedPath(bounds, radius);
        graphics.FillPath(brush, path);
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new ProgressTrackAccessibleObject(this);

    private sealed class ProgressTrackAccessibleObject : Control.ControlAccessibleObject
    {
        public ProgressTrackAccessibleObject(ProgressTrack owner) : base(owner)
        {
        }

        public override AccessibleRole Role => AccessibleRole.ProgressBar;
    }
}
