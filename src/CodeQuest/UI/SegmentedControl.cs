// The two-tab switch control.

using System.Drawing;
using System.Windows.Forms;

namespace CodeQuest.UI;

public sealed class SegmentedControl : Control
{
    private string[] _items = Array.Empty<string>();
    private int _selectedIndex;
    private int _hoveredSegment = -1;
    private Control? _labeledBy;

    public SegmentedControl()
    {
        SetStyle(ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.SupportsTransparentBackColor, false);

        BackColor = UITheme.Surface;
        Font = UITheme.Body;
        Height = UITheme.Scaled(this, 32);
        TabStop = true;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(UITheme.ContainerSurface(this));
    }

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

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Control? LabeledBy
    {
        get => _labeledBy;
        set => _labeledBy = value;
    }

    public event EventHandler? SelectionChanged;

    private void OnSelectionChanged(EventArgs eventArgs) => SelectionChanged?.Invoke(this, eventArgs);

    private int SegmentHeight => UITheme.Scaled(this, 32);

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);

        int segment = SegmentAt(e.Location);
        if (segment >= 0)
        {
            SelectedIndex = segment;
        }
    }

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

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hoveredSegment = -1;
        Invalidate();
    }

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

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

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
                segmentBounds.Width = Width - segmentBounds.X;
            }

            bool selected = i == _selectedIndex;

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

            if (selected && Focused && Enabled)
            {
                var outlineBounds = new Rectangle(
                    segmentBounds.X + 1,
                    1,
                    segmentBounds.Width - 2,
                    Height - 2);

                using var outlinePath = UITheme.RoundedPath(outlineBounds, UITheme.Scaled(this, UITheme.RadiusControl));
                using var outlinePen = new Pen(UITheme.AccentText);
                e.Graphics.DrawPath(outlinePen, outlinePath);
            }
        }
    }

    private int SegmentAt(Point location)
    {
        if (_items.Length == 0 || location.X < 0 || location.X >= Width)
        {
            return -1;
        }

        int segment = location.X * _items.Length / Width;
        return segment >= _items.Length ? _items.Length - 1 : segment;
    }

    private string GroupAccessibleName => _labeledBy?.Text ?? AccessibleName ?? string.Empty;

    private Rectangle SegmentBounds(int index)
    {
        int width = _items.Length == 0 ? 0 : Width / _items.Length;
        int x = index * width;
        if (index == _items.Length - 1)
        {
            width = Width - x;
        }

        return new Rectangle(x, 0, width, Height);
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new SegmentGroupAccessibleObject(this);

    private sealed class SegmentGroupAccessibleObject : Control.ControlAccessibleObject
    {
        private readonly SegmentedControl _owner;

        public SegmentGroupAccessibleObject(SegmentedControl owner) : base(owner)
        {
            _owner = owner;
        }

        public override AccessibleRole Role => AccessibleRole.Grouping;

        public override string? Name => _owner.GroupAccessibleName;

        public override int GetChildCount() => _owner.Items.Count;

        public override AccessibleObject? GetChild(int index)
            => index >= 0 && index < _owner.Items.Count ? new SegmentAccessibleObject(_owner, index) : null;
    }

    private sealed class SegmentAccessibleObject : AccessibleObject
    {
        private readonly SegmentedControl _owner;
        private readonly int _index;

        public SegmentAccessibleObject(SegmentedControl owner, int index)
        {
            _owner = owner;
            _index = index;
        }

        public override AccessibleRole Role => AccessibleRole.RadioButton;

        public override string? Name => _owner.Items[_index];

        public override AccessibleStates State
            => _owner.SelectedIndex == _index ? AccessibleStates.Selected : AccessibleStates.None;

        public override AccessibleObject? Parent => _owner.AccessibilityObject;

        public override Rectangle Bounds => _owner.RectangleToScreen(_owner.SegmentBounds(_index));
    }
}
