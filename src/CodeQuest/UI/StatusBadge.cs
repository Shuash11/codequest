// The Draft/Approved badge.

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CodeQuest.Models;

namespace CodeQuest.UI;

public static class StatusBadgePainter
{
    public const int BadgeHeight = 20;

    public static void Draw(Graphics graphics, Rectangle bounds, Control dpiReference, QuizStatus status)
    {
        int height = UITheme.Scaled(dpiReference, BadgeHeight);
        int chipWidth = Measure(dpiReference, status) + UITheme.Scaled(dpiReference, UITheme.Spacing8) * 2;
        var chip = new Rectangle(bounds.X, bounds.Y + (bounds.Height - height) / 2, chipWidth, height);

        if (chip.Width > bounds.Width)
        {
            chip.Width = bounds.Width;
        }

        (Color back, Color text) = status == QuizStatus.Approved
            ? (UITheme.SuccessTint, UITheme.SuccessText)
            : (UITheme.Fill, UITheme.TextPrimary);

        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var path = UITheme.RoundedPath(chip, UITheme.Scaled(dpiReference, UITheme.RadiusChip));
        using var fillBrush = new SolidBrush(back);
        graphics.FillPath(fillBrush, path);

        TextRenderer.DrawText(
            graphics,
            status.ToString(),
            UITheme.CaptionSemibold,
            chip,
            text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }

    public static int Measure(Control dpiReference, QuizStatus status)
    {
        int textWidth = TextRenderer.MeasureText(status.ToString(), UITheme.CaptionSemibold).Width;
        return textWidth + UITheme.Scaled(dpiReference, UITheme.Spacing8) * 2;
    }
}

public sealed class StatusBadge : Control
{
    private QuizStatus _status = QuizStatus.Draft;

    public StatusBadge()
    {
        SetStyle(ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.ResizeRedraw, true);
        BackColor = UITheme.Surface;
        TabStop = false;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(UITheme.ContainerSurface(this));
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public QuizStatus Status
    {
        get => _status;
        set
        {
            _status = value;
            ResizeToContent();
            Invalidate();
        }
    }

    public void ResizeToContent()
    {
        Height = UITheme.Scaled(this, StatusBadgePainter.BadgeHeight);
        Width = StatusBadgePainter.Measure(this, _status);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        StatusBadgePainter.Draw(e.Graphics, ClientRectangle, this, _status);
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new StatusBadgeAccessibleObject(this);

    private sealed class StatusBadgeAccessibleObject : Control.ControlAccessibleObject
    {
        private readonly StatusBadge _owner;

        public StatusBadgeAccessibleObject(StatusBadge owner) : base(owner)
        {
            _owner = owner;
        }

        public override AccessibleRole Role => AccessibleRole.StaticText;

        public override string? Name => _owner.Status.ToString();

        public override AccessibleStates State => AccessibleStates.ReadOnly;
    }
}
