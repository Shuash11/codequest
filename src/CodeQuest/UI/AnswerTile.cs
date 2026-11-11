// A clickable answer tile.

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CodeQuest.UI;

public sealed class AnswerTile : Control
{
    public static readonly string[] ShapeGlyphs = { "\u25B2", "\u25CF", "\u25C6", "\u25A0" };

    private const string CheckGlyph = "\u2713";
    private const string CrossGlyph = "\u2717";

    private readonly Color _baseColor;
    private bool _selected;
    private bool _revealed;
    private bool _isCorrectTile;

    public AnswerTile(int positionIndex, string choiceText, Color baseColor)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, true);
        _baseColor = baseColor;
        ShapeGlyph = ShapeGlyphs[positionIndex % ShapeGlyphs.Length];
        BackColor = baseColor;
        Font = UITheme.BodySemibold;
        Text = choiceText;
        Height = UITheme.Scaled(this, 54);
        Cursor = Cursors.Hand;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(UITheme.ContainerSurface(this));
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string ChoiceText { get => Text; set { Text = value; Invalidate(); } }

    public string ShapeGlyph { get; }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int ChoiceId { get; set; }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool IsSelected
    {
        get => _selected;
        set { if (_selected != value) { _selected = value; Invalidate(); } }
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool IsRevealed
    {
        get => _revealed;
        set { if (_revealed != value) { _revealed = value; Invalidate(); } }
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool IsCorrectTile
    {
        get => _isCorrectTile;
        set { if (_isCorrectTile != value) { _isCorrectTile = value; Invalidate(); } }
    }

    public bool IsWrongPick
    {
        get => _revealed && _selected && !_isCorrectTile;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        Rectangle bounds = new(0, 0, Width - 1, Height - 1);
        Color fill = _baseColor;
        if (_revealed && _isCorrectTile)
        {
            fill = UITheme.CorrectWash;
        }
        else if (IsWrongPick)
        {
            fill = UITheme.WrongWash;
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

        using (var badgeBrush = new SolidBrush(Color.FromArgb(52, UITheme.TextOnAccent)))
        {
            e.Graphics.FillEllipse(badgeBrush, badgeRect);
        }

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

    protected override bool IsInputKey(Keys keyData)
        => keyData is Keys.Enter or Keys.Space || base.IsInputKey(keyData);

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

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        if (!_revealed) { Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        Invalidate();
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new AnswerTileAccessibleObject(this);

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
