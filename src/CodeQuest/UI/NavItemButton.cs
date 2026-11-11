// A sidebar button.

using System.Windows.Forms;

namespace CodeQuest.UI;

public sealed class NavItemButton : Button
{
    private bool _hovering;
    private bool _pressed;

    public NavItemButton()
    {
        SetStyle(ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;

        Font = UITheme.Body;
        Height = UITheme.Scaled(this, 40);
        BackColor = UITheme.Background;
        UseVisualStyleBackColor = false;
        TabStop = true;
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Glyph { get; set; } = string.Empty;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Active
    {
        get => _active;
        set
        {
            _active = value;
            Invalidate();
        }
    }

    private bool _active;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool SecondaryAction { get; set; }

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

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        int radius = UITheme.Scaled(this, UITheme.RadiusControl);
        System.Drawing.Rectangle bounds = new(0, 0, Width - 1, Height - 1);

        System.Drawing.Color surface = UITheme.ContainerSurface(this);

        using (var backgroundBrush = new System.Drawing.SolidBrush(surface))
        {
            e.Graphics.FillRectangle(backgroundBrush, ClientRectangle);
        }

        Color fillColor = ResolveFill();
        Color glyphColor;
        Color labelColor;

        if (!Enabled)
        {
            glyphColor = UITheme.TextTertiary;
            labelColor = UITheme.TextTertiary;
        }
        else if (Active)
        {
            glyphColor = UITheme.AccentText;
            labelColor = UITheme.TextPrimary;
        }
        else if (SecondaryAction)
        {
            glyphColor = UITheme.TextSecondary;
            labelColor = UITheme.AccentText;
        }
        else
        {
            glyphColor = UITheme.TextSecondary;
            labelColor = UITheme.TextPrimary;
        }

        if (fillColor != surface)
        {
            using (var fillPath = UITheme.RoundedPath(bounds, radius))
            {
                using var fillBrush = new System.Drawing.SolidBrush(fillColor);
                e.Graphics.FillPath(fillBrush, fillPath);
            }
        }

        if (Focused && Enabled)
        {
            bounds.Inflate(-1, -1);
            using var outlinePath = UITheme.RoundedPath(bounds, radius);
            using var outlinePen = new System.Drawing.Pen(UITheme.AccentText);
            e.Graphics.DrawPath(outlinePen, outlinePath);
        }

        int textX = UITheme.Scaled(this, UITheme.Spacing16);

        if (!SecondaryAction && !string.IsNullOrEmpty(Glyph))
        {
            int glyphWidth = TextRenderer.MeasureText(Glyph, UITheme.NavGlyph).Width;
            int glyphHeight = TextRenderer.MeasureText(Glyph, UITheme.NavGlyph).Height;
            var glyphRect = new System.Drawing.Rectangle(
                textX,
                (Height - glyphHeight) / 2,
                glyphWidth,
                glyphHeight);

            TextRenderer.DrawText(
                e.Graphics,
                Glyph,
                UITheme.NavGlyph,
                glyphRect,
                glyphColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

            textX += glyphWidth + UITheme.Scaled(this, UITheme.Spacing8);
        }

        var labelRect = new System.Drawing.Rectangle(
            textX,
            0,
            Width - textX - UITheme.Scaled(this, UITheme.Spacing16),
            Height);

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Active ? UITheme.BodySemibold : UITheme.Body,
            labelRect,
            labelColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
    }

    private System.Drawing.Color ResolveFill()
    {
        if (!Enabled)
        {
            return UITheme.Background;
        }

        if (_pressed)
        {
            return UITheme.NavItemPressed;
        }

        if (_hovering)
        {
            return UITheme.Fill;
        }

        return Active ? UITheme.AccentTint : UITheme.Background;
    }
}
