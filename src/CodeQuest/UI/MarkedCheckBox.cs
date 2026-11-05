// =============================================================================
// CodeQuest — Phase P8: Topic & Quiz Management
// Recipe 4.7 of the design of record: the custom checkbox.
// Look at: OnPaint — a 16px rounded-4px square: unchecked = Surface fill +
//          1px BorderControl, checked = Accent fill + the white check glyph,
//          keyboard focus = 1px Accent outline inset 1px, disabled = Fill fill
//          + TextTertiary. Used for per-choice correct-answer marking in the
//          question editor — a question has exactly one correct choice; the
//          checkbox just marks it and the editor enforces the rule (§4.7, R-2
//          #3). The control keeps Space/Enter toggling for the keyboard.
// =============================================================================

using System.Windows.Forms;

namespace CodeQuest.UI;

/// <summary>
/// The checkbox of recipe 4.7: 16px rounded square with a check glyph.
/// </summary>
public sealed class MarkedCheckBox : Control
{
    /// <summary>The Segoe MDL2 check glyph (§3.7 — the few icons that earn their place).</summary>
    private const string CheckGlyph = "\uE73E";

    private bool _checked;

    /// <summary>Creates the checkbox at the recipe's 16px size.</summary>
    public MarkedCheckBox()
    {
        SetStyle(ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.ResizeRedraw, true);

        BackColor = UITheme.Surface; // the form card the box sits on
        TabStop = true;
        Size = new System.Drawing.Size(UITheme.Scaled(this, 16), UITheme.Scaled(this, 16));
        // A 16px box is smaller than the §2.6 target size, so the accessible
        // role/name must say what the mark means (screen readers, UX F1 rule).
        AccessibleRole = System.Windows.Forms.AccessibleRole.CheckButton;
    }

    /// <summary>Whether the box is marked.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool IsChecked
    {
        get => _checked;
        set
        {
            if (_checked == value)
            {
                return;
            }

            _checked = value;
            Invalidate();
            OnCheckedChanged(EventArgs.Empty);
        }
    }

    /// <summary>Raised after the checked state changes.</summary>
    public event EventHandler? CheckedChanged;

    /// <summary>Raises the <see cref="CheckedChanged"/> event.</summary>
    private void OnCheckedChanged(EventArgs eventArgs)
    {
        CheckedChanged?.Invoke(this, eventArgs);
    }

    /// <summary>A click toggles the mark.</summary>
    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        IsChecked = !IsChecked;
    }

    /// <summary>Space and Enter toggle the mark (keyboard first-class, §2.6).</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            IsChecked = !IsChecked;
            e.Handled = true;
        }
    }

    /// <summary>Repaints when the disabled state changes.</summary>
    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    /// <summary>The recipe's box paints.</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        int boxSize = UITheme.Scaled(this, 16);
        var box = new System.Drawing.Rectangle((Width - boxSize) / 2, (Height - boxSize) / 2, boxSize, boxSize);
        int radius = UITheme.Scaled(this, UITheme.RadiusChip);

        using (var boxPath = UITheme.RoundedPath(box, radius))
        {
            Color fill = !Enabled ? UITheme.Fill
                : _checked ? UITheme.Accent
                : UITheme.Surface;

            using var fillBrush = new System.Drawing.SolidBrush(fill);
            e.Graphics.FillPath(fillBrush, boxPath);

            if (!Enabled)
            {
                // Disabled: Fill fill, quiet outline — no accent anywhere.
                using var disabledPen = new System.Drawing.Pen(UITheme.TextTertiary);
                e.Graphics.DrawPath(disabledPen, boxPath);
            }
            else if (!_checked)
            {
                using var borderPen = new System.Drawing.Pen(UITheme.BorderControl);
                e.Graphics.DrawPath(borderPen, boxPath);
            }
        }

        if (_checked && Enabled)
        {
            // The white check glyph on the accent fill (TextOnAccent).
            TextRenderer.DrawText(
                e.Graphics,
                CheckGlyph,
                UITheme.CaptionSemibold,
                box,
                UITheme.TextOnAccent,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }

        if (Focused && Enabled)
        {
            var outline = new System.Drawing.Rectangle(box.X - 1, box.Y - 1, box.Width + 2, box.Height + 2);
            using var outlinePath = UITheme.RoundedPath(outline, radius);
            using var outlinePen = new System.Drawing.Pen(UITheme.Accent);
            e.Graphics.DrawPath(outlinePen, outlinePath);
        }
    }
}
