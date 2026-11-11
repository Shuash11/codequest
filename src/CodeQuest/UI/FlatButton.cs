// A styled button. (Inheritance)

using System.Windows.Forms;

namespace CodeQuest.UI;

public sealed class FlatButton : Button
{
    private UITheme.FlatButtonVariant _variant = UITheme.FlatButtonVariant.Primary;
    private bool _hovering;
    private bool _pressed;

    public FlatButton()
    {
        SetStyle(ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;

        Font = UITheme.Body;
        Height = UITheme.Scaled(this, 32);
        BackColor = UITheme.Surface;
        UseVisualStyleBackColor = false;
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public UITheme.FlatButtonVariant Variant
    {
        get => _variant;
        set
        {
            _variant = value;
            Invalidate();
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hovering = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovering = false;
        _pressed = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        _pressed = true;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _pressed = false;
        Invalidate();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        UITheme.FlatButtonState state = ResolveState();
        Color fill = UITheme.FlatButtonFill(_variant, state);
        Color label = UITheme.FlatButtonLabel(_variant, state);

        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using (var backgroundBrush = new System.Drawing.SolidBrush(UITheme.ContainerSurface(this)))
        {
            e.Graphics.FillRectangle(backgroundBrush, ClientRectangle);
        }

        System.Drawing.Rectangle bounds = new(0, 0, Width - 1, Height - 1);
        if (!fill.IsEmpty)
        {
            using (var fillPath = UITheme.RoundedPath(bounds, UITheme.Scaled(this, UITheme.RadiusControl)))
            {
                using var fillBrush = new System.Drawing.SolidBrush(fill);
                e.Graphics.FillPath(fillBrush, fillPath);
            }
        }

        if (Focused)
        {
            bounds.Inflate(-1, -1);
            using var outlinePath = UITheme.RoundedPath(bounds, UITheme.Scaled(this, UITheme.RadiusControl));
            using var outlinePen = new System.Drawing.Pen(UITheme.FlatButtonFocusRing(_variant));
            e.Graphics.DrawPath(outlinePen, outlinePath);
        }

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            UITheme.Body,
            bounds,
            label,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }

    private UITheme.FlatButtonState ResolveState()
    {
        if (!Enabled)
        {
            return UITheme.FlatButtonState.Disabled;
        }

        if (_pressed)
        {
            return UITheme.FlatButtonState.Pressed;
        }

        if (_hovering)
        {
            return UITheme.FlatButtonState.Hover;
        }

        if (Focused)
        {
            return UITheme.FlatButtonState.Focus;
        }

        return UITheme.FlatButtonState.Default;
    }
}
