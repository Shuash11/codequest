// =============================================================================
// CodeQuest — Quiz Taking (P9): one choice row of spec 5.6 moment 2. A choice
// is a full-width row card (Surface, 8px radius, ≥36px tall) with a mark
// column and the choice text (Body). Tapping selects: the row gains the
// AccentTint fill and the mark fills Accent — one selection per question;
// selecting another row moves it (no toggle-off; §5.6 "changing an answer is
// allowed until submit").
// Accessibility contract (F2's rule, §2.6): role RadioButton, name = the
// choice's visible text, the Selected state on the chosen row — the
// question's choices behave as a radio group to a screen reader. Enter/Space
// selects the focused row; Tab reaches each row.
// Users: only the quiz-taking run (spec 5.6); never a management form.
// =============================================================================

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CodeQuest.UI;

/// <summary>
/// One selectable choice row of the quiz-taking run (spec 5.6 moment 2).
/// </summary>
public sealed class ChoiceRowButton : Control
{
    /// <summary>The checked mark of the selected row (recipe 4.7's glyph).</summary>
    private const string CheckGlyph = "\uE73E";

    /// <summary>The unchecked mark outline of a resting row.</summary>
    private const string CircleGlyph = "\uECC8";

    private bool _selected;

    /// <summary>Creates the row card at the recipe's minimum height.</summary>
    public ChoiceRowButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, true);
        BackColor = UITheme.Surface;
        Font = UITheme.Body;
        Height = UITheme.Scaled(this, 36); // the recipe's minimum row height (§5.6)
    }

    /// <summary>The choice this row represents (the row paints its text).</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string ChoiceText { get => Text; set => Text = value; }

    /// <summary>Whether this row is the question's current selection.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool IsSelected
    {
        get => _selected;
        set
        {
            if (_selected == value)
            {
                return;
            }

            _selected = value;
            Invalidate();
        }
    }

    /// <summary>Paints the row card: fill, mark column, choice text (§5.6).</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        Rectangle bounds = new(0, 0, Width - 1, Height - 1);
        Color back = _selected ? UITheme.AccentTint : BackColor;
        using (var backBrush = new SolidBrush(back))
        {
            using GraphicsPath path = UITheme.RoundedPath(bounds, UITheme.Scaled(this, UITheme.RadiusControl));
            e.Graphics.FillPath(backBrush, path);
        }

        if (Focused)
        {
            // The §4.1 focus ring: 1px Accent outline inset 1px (§2.6 keyboard floor).
            Rectangle ring = new(1, 1, Width - 3, Height - 3);
            using var ringPen = new Pen(UITheme.Accent);
            using GraphicsPath ringPath = UITheme.RoundedPath(ring, UITheme.Scaled(this, UITheme.RadiusControl));
            e.Graphics.DrawPath(ringPen, ringPath);
        }

        // The mark column: a filled Accent check when selected, a quiet circle
        // outline when not (§5.6: "the mark fills Accent"). The shared theme
        // fonts are never disposed — they live for the app's lifetime.
        int inset = UITheme.Scaled(this, UITheme.Spacing12);
        int markSize = UITheme.Scaled(this, 18);
        Color markColor = _selected ? UITheme.Accent : UITheme.TextTertiary;
        TextRenderer.DrawText(
            e.Graphics,
            _selected ? CheckGlyph : CircleGlyph,
            UITheme.NavGlyph,
            new Point(inset, (Height - markSize) / 2),
            markColor);

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            new Rectangle(inset + markSize + UITheme.Scaled(this, UITheme.Spacing12), 0, Width - inset * 2 - markSize * 2, Height),
            UITheme.TextPrimary,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    /// <summary>Enter and Space select the focused row (§2.6 keyboard floor).</summary>
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

    /// <summary>The accessible object of the F2 contract: RadioButton semantics.</summary>
    protected override AccessibleObject CreateAccessibilityInstance() => new ChoiceRowAccessibleObject(this);

    /// <summary>RadioButton role, visible-text name, Selected state (§2.6/F2).</summary>
    private sealed class ChoiceRowAccessibleObject : Control.ControlAccessibleObject
    {
        private readonly ChoiceRowButton _owner;

        public ChoiceRowAccessibleObject(ChoiceRowButton owner) : base(owner)
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

                if (_owner.IsSelected)
                {
                    state |= AccessibleStates.Selected;
                }

                return state;
            }
        }

        public override void DoDefaultAction() => _owner.OnClick(EventArgs.Empty);
    }
}
