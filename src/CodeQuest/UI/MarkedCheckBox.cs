// A custom checkbox.

using System.Windows.Forms;

namespace CodeQuest.UI;

public sealed class MarkedCheckBox : Control
{
    private const string CheckGlyph = "\uE73E";

    private bool _checked;
    private Control? _labeledBy;

    public MarkedCheckBox()
    {
        SetStyle(ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.ResizeRedraw, true);

        BackColor = UITheme.Surface;
        TabStop = true;
        Size = new System.Drawing.Size(UITheme.Scaled(this, 16), UITheme.Scaled(this, 16));
        AccessibleRole = System.Windows.Forms.AccessibleRole.CheckButton;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(UITheme.ContainerSurface(this));
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Control? LabeledBy
    {
        get => _labeledBy;
        set => _labeledBy = value;
    }

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

    public event EventHandler? CheckedChanged;

    private void OnCheckedChanged(EventArgs eventArgs)
    {
        CheckedChanged?.Invoke(this, eventArgs);
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        IsChecked = !IsChecked;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            IsChecked = !IsChecked;
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
            using var outlinePen = new System.Drawing.Pen(_checked ? UITheme.TextPrimary : UITheme.AccentText);
            e.Graphics.DrawPath(outlinePen, outlinePath);
        }
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new MarkedCheckBoxAccessibleObject(this);

    private sealed class MarkedCheckBoxAccessibleObject : Control.ControlAccessibleObject
    {
        private readonly MarkedCheckBox _owner;

        public MarkedCheckBoxAccessibleObject(MarkedCheckBox owner) : base(owner)
        {
            _owner = owner;
        }

        public override AccessibleRole Role => AccessibleRole.CheckButton;

        public override string? Name => _owner._labeledBy?.Text ?? base.Name;

        public override string? DefaultAction => "Toggle";

        public override AccessibleStates State
        {
            get
            {
                AccessibleStates state = AccessibleStates.Focusable;
                if (_owner.Focused)
                {
                    state |= AccessibleStates.Focused;
                }

                if (_owner.IsChecked)
                {
                    state |= AccessibleStates.Checked;
                }

                return state;
            }
        }

        public override void DoDefaultAction() => _owner.IsChecked = !_owner.IsChecked;
    }
}
