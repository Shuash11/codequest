// =============================================================================
// CodeQuest — Phase P8: Topic & Quiz Management
// Recipe 4.9 of the design of record: the segmented control.
// Look at: OnPaint — one 8px-radius container filled Fill, equal-width
//          segments; the selected segment paints Surface with a BodySemibold
//          label (the iOS segmented style), unselected segments are
//          transparent with a Body label, hover on unselected paints the
//          recipe's quiet step (UITheme.SegmentHover, §4.9). The group is ONE
//          Tab stop; arrow keys move the selection within it (§2.6) and the
//          selected segment carries the standard 1px accent outline inset 1px
//          when the control is keyboard-focused. Disabled: labels TextTertiary,
//          the selected segment keeps its Surface fill so the stored value
//          stays legible, no hover response.
// Users:   the difficulty picker of the quiz form, the status filter of the
//          quiz management screen (All / Draft / Approved) and the question
//          type picker of the question form (spec 5.4, 5.5) — never free text,
//          never a different pattern per screen.
// =============================================================================

using System.Drawing;
using System.Windows.Forms;

namespace CodeQuest.UI;

/// <summary>
/// The segmented control of recipe 4.9: 2–4 fixed, equal options in one group.
/// </summary>
public sealed class SegmentedControl : Control
{
    private string[] _items = Array.Empty<string>();
    private int _selectedIndex;
    private int _hoveredSegment = -1;

    /// <summary>Creates the control at the recipe's 32px height.</summary>
    public SegmentedControl()
    {
        SetStyle(ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.SupportsTransparentBackColor, false);

        BackColor = UITheme.Surface; // the cards the control sits on
        Font = UITheme.Body;
        Height = UITheme.Scaled(this, 32);
        TabStop = true; // the group is one Tab stop (recipe 4.9)
    }

    /// <summary>The fixed option labels of the group, in order.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<string> Items
    {
        get => _items;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _items = value.ToArray();
            if (_items.Length == 0)
            {
                throw new ArgumentException("A segmented control needs at least one item.", nameof(value));
            }
            _selectedIndex = 0;
            _hoveredSegment = -1;
            Invalidate();
        }
    }

    /// <summary>The index of the selected segment.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (value < 0 || value >= _items.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            if (_selectedIndex == value)
            {
                return;
            }

            _selectedIndex = value;
            Invalidate();
            OnSelectionChanged(EventArgs.Empty);
        }
    }

    /// <summary>Raised after the selection changes (mouse click or arrow keys).</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Raises the <see cref="SelectionChanged"/> event.</summary>
    private void OnSelectionChanged(EventArgs eventArgs) => SelectionChanged?.Invoke(this, eventArgs);

    /// <summary>The recipe's segment height (standard-DPI pixels).</summary>
    private int SegmentHeight => UITheme.Scaled(this, 32);

    /// <summary>Sets the selection from the pointer and repaints.</summary>
    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);

        int segment = SegmentAt(e.Location);
        if (segment >= 0)
        {
            SelectedIndex = segment;
        }
    }

    /// <summary>Tracks the hovered segment for the recipe's hover paint.</summary>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        int segment = SegmentAt(e.Location);
        if (_hoveredSegment != segment)
        {
            _hoveredSegment = segment;
            Invalidate();
        }
    }

    /// <summary>Clears the hover state when the pointer leaves.</summary>
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hoveredSegment = -1;
        Invalidate();
    }

    /// <summary>Arrow keys move the selection within the group (recipe 4.9).</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.KeyCode == Keys.Left && _selectedIndex > 0)
        {
            SelectedIndex = _selectedIndex - 1;
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Right && _selectedIndex < _items.Length - 1)
        {
            SelectedIndex = _selectedIndex + 1;
            e.Handled = true;
        }
    }

    /// <summary>Repaints when the disabled state changes (labels go TextTertiary).</summary>
    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    /// <summary>The recipe's segment paints, one segment at a time.</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        // The container: 8px radius, Fill fill (recipe 4.9).
        var containerBounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var containerPath = UITheme.RoundedPath(containerBounds, UITheme.Scaled(this, UITheme.RadiusControl)))
        {
            using var containerBrush = new SolidBrush(UITheme.Fill);
            e.Graphics.FillPath(containerBrush, containerPath);
        }

        int segmentWidth = Width / _items.Length;

        for (int i = 0; i < _items.Length; i++)
        {
            var segmentBounds = new Rectangle(i * segmentWidth, 0, segmentWidth, Height);
            if (i == _items.Length - 1)
            {
                segmentBounds.Width = Width - segmentBounds.X; // the last segment takes the rounding remainder
            }

            bool selected = i == _selectedIndex;

            // Selected segment = Surface fill + BodySemibold label; the fill is
            // inset so the container's rounded frame stays visible around it.
            if (selected)
            {
                var fillBounds = new Rectangle(
                    segmentBounds.X + UITheme.Scaled(this, UITheme.Spacing4),
                    UITheme.Scaled(this, UITheme.Spacing4),
                    segmentBounds.Width - UITheme.Scaled(this, UITheme.Spacing4) * 2,
                    SegmentHeight - UITheme.Scaled(this, UITheme.Spacing4) * 2 - 1);

                using var segmentPath = UITheme.RoundedPath(fillBounds, UITheme.Scaled(this, UITheme.RadiusControl));
                using var segmentBrush = new SolidBrush(UITheme.Surface);
                e.Graphics.FillPath(segmentBrush, segmentPath);
            }
            else if (Enabled && i == _hoveredSegment)
            {
                // Hover on unselected: the recipe's quiet step (§4.9).
                var hoverBounds = new Rectangle(
                    segmentBounds.X + UITheme.Scaled(this, UITheme.Spacing4),
                    UITheme.Scaled(this, UITheme.Spacing4),
                    segmentBounds.Width - UITheme.Scaled(this, UITheme.Spacing4) * 2,
                    SegmentHeight - UITheme.Scaled(this, UITheme.Spacing4) * 2 - 1);

                using var hoverPath = UITheme.RoundedPath(hoverBounds, UITheme.Scaled(this, UITheme.RadiusControl));
                using var hoverBrush = new SolidBrush(UITheme.SegmentHover);
                e.Graphics.FillPath(hoverBrush, hoverPath);
            }

            Color label = !Enabled ? UITheme.TextTertiary : UITheme.TextPrimary;

            TextRenderer.DrawText(
                e.Graphics,
                _items[i],
                selected && Enabled ? UITheme.BodySemibold : UITheme.Body,
                segmentBounds,
                label,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

            // Focus (keyboard): the selected segment carries the standard 1px
            // accent outline inset 1px, over its selected paint (recipe 4.9).
            if (selected && Focused && Enabled)
            {
                var outlineBounds = new Rectangle(
                    segmentBounds.X + 1,
                    1,
                    segmentBounds.Width - 2,
                    Height - 2);

                using var outlinePath = UITheme.RoundedPath(outlineBounds, UITheme.Scaled(this, UITheme.RadiusControl));
                using var outlinePen = new Pen(UITheme.Accent);
                e.Graphics.DrawPath(outlinePen, outlinePath);
            }
        }
    }

    /// <summary>The segment index under a point, or -1.</summary>
    private int SegmentAt(Point location)
    {
        if (_items.Length == 0 || location.X < 0 || location.X >= Width)
        {
            return -1;
        }

        int segment = location.X * _items.Length / Width;
        return segment >= _items.Length ? _items.Length - 1 : segment;
    }
}
