// =============================================================================
// CodeQuest — Phase P7: UI Shell & Navigation
// Recipe 4.3 of the design of record: the sidebar nav item.
// Look at: OnPaint — the full-width 40px row with its state table: inactive
//          (transparent on Background, TextPrimary label, TextSecondary glyph),
//          hover (Fill), active (AccentTint background, Accent glyph,
//          BodySemibold label), pressed (#D6EBFF = UITheme.NavItemPressed),
//          and the 1px accent focus outline inset 1px (§2.6). Glyph and label
//          sit 8px apart behind 16px horizontal padding; the hit area is the
//          full row (the control itself).
// Inheritance note: this extends WinForms' Button for the same reason as
//          FlatButton — keyboard activation and the UIAutomation Invoke
//          pattern stay free; all chrome is painted here.
// The "Log out" row of spec section 5.2 ("a secondary-style nav item") uses
// this control with SecondaryAction = true: no glyph, accent label, hover
// Fill — the recipe's inactive/hover/pressed/focus states, action-styled.
// =============================================================================

using System.Windows.Forms;

namespace CodeQuest.UI;

/// <summary>
/// One sidebar navigation row of recipe 4.3: 40px tall, full-width hit area,
/// glyph (Segoe MDL2 Assets) + Body label.
/// </summary>
public sealed class NavItemButton : Button
{
    private bool _hovering;
    private bool _pressed;

    /// <summary>Creates a nav item with the recipe's full-row height.</summary>
    public NavItemButton()
    {
        SetStyle(ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat; // recipe 4.3 assumes FlatStyle.Flat (§4 preamble)

        Font = UITheme.Body;
        Height = UITheme.Scaled(this, 40);
        // Inactive rows are "transparent on Background" (recipe 4.3); nav items
        // only ever sit on the sidebar, whose fill is Background — same paint,
        // without transparency quirks. The themed default face must be off, or
        // it shows through the rounded corner cutouts.
        BackColor = UITheme.Background;
        UseVisualStyleBackColor = false;
        TabStop = true;
    }

    /// <summary>The Segoe MDL2 glyph codepoint (e.g. "\uE80F"); empty draws no glyph.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Glyph { get; set; } = string.Empty;

    /// <summary>True while this row is the sidebar's one active item.</summary>
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

    /// <summary>
    /// Secondary-style nav item (spec 5.2, the "Log out" row): no glyph, an
    /// accent label like recipe 4.1's secondary button.
    /// </summary>
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

        // Paint the row background ourselves — ButtonBase's own background
        // pass leaves the rounded corner cutouts unpainted (black specks).
        using (var backgroundBrush = new System.Drawing.SolidBrush(BackColor))
        {
            e.Graphics.FillRectangle(backgroundBrush, ClientRectangle);
        }

        Color fillColor = ResolveFill();
        Color glyphColor;
        Color labelColor;

        if (Active)
        {
            glyphColor = UITheme.Accent;
            labelColor = UITheme.TextPrimary;
        }
        else if (SecondaryAction)
        {
            glyphColor = UITheme.TextSecondary;
            labelColor = UITheme.Accent;
        }
        else
        {
            glyphColor = UITheme.TextSecondary;
            labelColor = UITheme.TextPrimary;
        }

        // Recipe 4.3 inactive rows are "transparent fill (on Background)" — the
        // fill equals the row background, so the fill pass is skipped entirely:
        // a same-color rounded fill would contribute nothing but antialiased
        // edge pixels, which read as a faint ghost ring on state changes.
        if (fillColor != BackColor)
        {
            using (var fillPath = UITheme.RoundedPath(bounds, radius))
            {
                using var fillBrush = new System.Drawing.SolidBrush(fillColor);
                e.Graphics.FillPath(fillBrush, fillPath);
            }
        }

        if (Focused)
        {
            // Recipe 4.3 focus row: 1px accent outline inset 1px, in addition
            // to any state (§2.6 — keyboard focus is always visible).
            bounds.Inflate(-1, -1);
            using var outlinePath = UITheme.RoundedPath(bounds, radius);
            using var outlinePen = new System.Drawing.Pen(UITheme.Accent);
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

    /// <summary>Recipe 4.3's fill states, in precedence order.</summary>
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
