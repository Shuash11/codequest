// =============================================================================
// CodeQuest — The fun quiz UI: one ANSWER TILE (Kahoot-style). A quiz answer
// is not a table row — it is a big, colorful, tappable tile with a shape mark
// (▲ ● ◆ ■ per position, like every fun quiz app) and bold white text.
// States: rest (vivid fill) → hover (darken) → selected (white ring) →
// revealed: the correct tile washes green with a check, a wrong pick washes
// red with a cross. After a reveal the question locks (one honest shot per
// question — the fun-quiz rule the owner chose over exam-style re-answering).
// Accessibility: role RadioButton, name = the choice text, Selected state,
// Enter/Space selects — the F2 contract, carried over from the old row.
// =============================================================================

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CodeQuest.UI;

/// <summary>
/// One colorful answer tile of the quiz run (the fun-UI redesign).
/// </summary>
public sealed class AnswerTile : Control
{
    /// <summary>The four shape marks, one per tile position (Kahoot-style).</summary>
    public static readonly string[] ShapeGlyphs = { "\u25B2", "\u25CF", "\u25C6", "\u25A0" };

    private const string CheckGlyph = "\u2713";
    private const string CrossGlyph = "\u2717";

    private readonly Color _baseColor;
    private bool _selected;
    private bool _revealed;
    private bool _isCorrectTile;

    /// <summary>Creates one answer tile with its position color and shape.</summary>
    public AnswerTile(int positionIndex, string choiceText, Color baseColor)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, true);
        _baseColor = baseColor;
        ShapeGlyph = ShapeGlyphs[positionIndex % ShapeGlyphs.Length];
        BackColor = baseColor; // fallback only — corners resolve the container (OnPaintBackground)
        Font = UITheme.BodySemibold;
        Text = choiceText;
        Height = UITheme.Scaled(this, 54);
        Cursor = Cursors.Hand;
    }

    /// <summary>
    /// The corner cutouts around the 8px-rounded tile show the CONTAINER's
    /// surface, never the tile color (D4 audit rule #1) — the tile fill
    /// paints in OnPaint.
    /// </summary>
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(UITheme.ContainerSurface(this));
    }

    /// <summary>The choice text this tile represents.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string ChoiceText { get => Text; set { Text = value; Invalidate(); } }

    /// <summary>The shape mark drawn in the tile's left badge (▲ ● ◆ ■).</summary>
    public string ShapeGlyph { get; }

    /// <summary>The choice id this tile maps to (set by the run, read by the click handler).</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int ChoiceId { get; set; }

    /// <summary>Whether the tile is the picked answer before the reveal.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool IsSelected
    {
        get => _selected;
        set { if (_selected != value) { _selected = value; Invalidate(); } }
    }

    /// <summary>Whether the question is revealed — tiles lock and show the verdict.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool IsRevealed
    {
        get => _revealed;
        set { if (_revealed != value) { _revealed = value; Invalidate(); } }
    }

    /// <summary>Whether this tile is the answer key (draws the green wash on reveal).</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool IsCorrectTile
    {
        get => _isCorrectTile;
        set { if (_isCorrectTile != value) { _isCorrectTile = value; Invalidate(); } }
    }

    /// <summary>Whether this tile was the picked one on a wrong reveal (draws the red wash).</summary>
    public bool IsWrongPick
    {
        get => _revealed && _selected && !_isCorrectTile;
    }

    /// <summary>Paints the tile: vivid fill, shape badge, bold white text, verdict state.</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        Rectangle bounds = new(0, 0, Width - 1, Height - 1);
        Color fill = _baseColor;
        if (_revealed && _isCorrectTile)
        {
            fill = UITheme.CorrectWash; // the answer key turns green on reveal
        }
        else if (IsWrongPick)
        {
            fill = UITheme.WrongWash; // a wrong pick turns red on reveal
        }
        else if (_selected || ClientRectangle.Contains(PointToClient(Cursor.Position)))
        {
            fill = UITheme.TileHover(_baseColor);
        }

        using (var fillBrush = new SolidBrush(fill))
        {
            using GraphicsPath path = UITheme.RoundedPath(bounds, UITheme.Scaled(this, UITheme.RadiusControl));
            e.Graphics.FillPath(fillBrush, path);
        }

        if (Focused && !_revealed)
        {
            using var ringPen = new Pen(UITheme.TextOnAccent, UITheme.Scaled(this, 2));
            using GraphicsPath ringPath = UITheme.RoundedPath(
                new Rectangle(2, 2, Width - 5, Height - 5), UITheme.Scaled(this, UITheme.RadiusControl));
            e.Graphics.DrawPath(ringPen, ringPath);
        }

        int inset = UITheme.Scaled(this, UITheme.Spacing12);
        int badge = UITheme.Scaled(this, 30);
        var badgeRect = new Rectangle(inset, (Height - badge) / 2, badge, badge);

        // The shape badge: a translucent light circle with the shape mark.
        using (var badgeBrush = new SolidBrush(Color.FromArgb(52, UITheme.TextOnAccent)))
        {
            e.Graphics.FillEllipse(badgeBrush, badgeRect);
        }

        // The verdict mark replaces the shape badge on reveal.
        string verdictMark = _revealed
            ? (_isCorrectTile ? CheckGlyph : IsWrongPick ? CrossGlyph : string.Empty)
            : ShapeGlyph;
        TextRenderer.DrawText(
            e.Graphics,
            verdictMark,
            UITheme.NavGlyph,
            badgeRect,
            UITheme.TextOnAccent,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        int textX = inset + badge + UITheme.Scaled(this, UITheme.Spacing12);
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            new Rectangle(textX, 0, Width - textX - inset, Height),
            UITheme.TextOnAccent,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    /// <summary>Enter and Space select the focused tile (the F2 keyboard contract).</summary>
    protected override bool IsInputKey(Keys keyData)
        => keyData is Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    /// <summary>Raises Click for Enter/Space so keyboard and mouse share one path.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Enter or Keys.Space)
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    /// <summary>Hover repaint (the tile darkens).</summary>
    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        if (!_revealed) { Invalidate(); }
    }

    /// <summary>Hover repaint (the tile returns to its vivid fill).</summary>
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        Invalidate();
    }

    /// <summary>The accessible object: RadioButton semantics on the tile (F2 contract).</summary>
    protected override AccessibleObject CreateAccessibilityInstance() => new AnswerTileAccessibleObject(this);

    /// <summary>RadioButton role, visible-text name, Selected state (§2.6/F2). The
    /// legacy default action (DoDefaultAction) triggers the tile for MSAA clients;
    /// keyboard users activate with Tab + Enter/Space.</summary>
    private sealed class AnswerTileAccessibleObject : Control.ControlAccessibleObject
    {
        private readonly AnswerTile _owner;

        public AnswerTileAccessibleObject(AnswerTile owner) : base(owner)
        {
            _owner = owner;
        }

        public override AccessibleRole Role => AccessibleRole.RadioButton;

        public override string? Name => _owner.ChoiceText;

        public override AccessibleStates State
        {
            get
            {
                AccessibleStates state = AccessibleStates.Focusable;
                if (_owner.Focused)
                {
                    state |= AccessibleStates.Focused;
                }

                if (_owner.IsSelected || (_owner.IsRevealed && _owner.IsCorrectTile))
                {
                    state |= AccessibleStates.Selected;
                }

                return state;
            }
        }

        public override void DoDefaultAction() => _owner.OnClick(EventArgs.Empty);
    }
}
