// =============================================================================
// CodeQuest — Phase P8: Topic & Quiz Management
// Recipe 4.10 of the design of record: the status badge (Draft / Approved).
// Look at: Draw — a 20px-tall chip, 4px radius, 8px horizontal padding,
//          CaptionSemibold text. A badge is always a text label plus its color
//          (never color alone, §2.6): Draft = Fill background + TextPrimary,
//          Approved = SuccessTint background + SuccessText. One static painter
//          serves both the grid's status column (drawn inside
//          <see cref="IosListGrid"/>) and the standalone control, so the
//          recipe exists exactly once (spec section 8).
// =============================================================================

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CodeQuest.Models;

namespace CodeQuest.UI;

/// <summary>
/// The status badge of recipe 4.10: one painter for the Draft/Approved chip,
/// reused by the quiz grid and the drill-in headers. Accessibility contract
/// (F2, §4.10): role StaticText, name = the badge's text ("Draft" / "Approved"
/// — the recipe always paints the text, so the name equals the visible label
/// by construction); non-interactive, so no focusable or toggle state. The
/// grid's painted badges carry the same contract through
/// <see cref="GridBadgeCell"/>.
/// </summary>
public static class StatusBadgePainter
{
    /// <summary>The chip height of the recipe (standard-DPI pixels).</summary>
    public const int BadgeHeight = 20;

    /// <summary>Draws the badge chip for one quiz status.</summary>
    /// <param name="graphics">The surface to draw on.</param>
    /// <param name="bounds">The cell area the chip is centered in.</param>
    /// <param name="dpiReference">The control that supplies the DPI scale.</param>
    /// <param name="status">The quiz status the badge shows.</param>
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

    /// <summary>The width one status badge needs, including its padding.</summary>
    public static int Measure(Control dpiReference, QuizStatus status)
    {
        int textWidth = TextRenderer.MeasureText(status.ToString(), UITheme.CaptionSemibold).Width;
        return textWidth + UITheme.Scaled(dpiReference, UITheme.Spacing8) * 2;
    }
}

/// <summary>
/// The badge as a standalone control — the drill-in headers (question editor,
/// review screen) carry the quiz's status next to the title (spec 5.5).
/// </summary>
public sealed class StatusBadge : Control
{
    private QuizStatus _status = QuizStatus.Draft;

    /// <summary>Creates the badge sized to its status text.</summary>
    public StatusBadge()
    {
        SetStyle(ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.ResizeRedraw, true);
        BackColor = UITheme.Surface; // the headers the badge sits on are Surface cards
        TabStop = false; // a badge is non-interactive (§4.10)
    }

    /// <summary>The quiz status this badge shows.</summary>
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

    /// <summary>Sizes the control to the chip it draws.</summary>
    public void ResizeToContent()
    {
        Height = UITheme.Scaled(this, StatusBadgePainter.BadgeHeight);
        Width = StatusBadgePainter.Measure(this, _status);
    }

    /// <summary>Drawing is delegated to the one badge painter.</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        StatusBadgePainter.Draw(e.Graphics, ClientRectangle, this, _status);
    }

    /// <summary>The accessible object of recipe 4.10's accessibility contract (F2).</summary>
    protected override AccessibleObject CreateAccessibilityInstance() => new StatusBadgeAccessibleObject(this);

    /// <summary>StaticText role, badge-text name, non-interactive state (§4.10).</summary>
    private sealed class StatusBadgeAccessibleObject : Control.ControlAccessibleObject
    {
        private readonly StatusBadge _owner;

        public StatusBadgeAccessibleObject(StatusBadge owner) : base(owner)
        {
            _owner = owner;
        }

        public override AccessibleRole Role => AccessibleRole.StaticText;

        public override string? Name => _owner.Status.ToString();

        public override AccessibleStates State => AccessibleStates.ReadOnly; // no focusable, no toggle (§4.10)
    }
}
