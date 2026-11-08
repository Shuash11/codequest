// =============================================================================
// CodeQuest — Phase P7: UI Shell & Navigation
// Recipe 4.1 of the design of record: the custom flat button.
// Look at: OnPaint — one flat rounded fill per interaction state (the state
//          tables live in UITheme.FlatButtonFill/FlatButtonLabel), a 1px
//          focus outline inset 1px when keyboard-focused (§2.6 "focus is
//          visible"; D4 three-case ring: TextPrimary over filled accent/
//          danger, AccentText everywhere else), and the centered Body label.
//          No borders, no bevels, no gradients anywhere (§4 preamble). The
//          three variants (primary / secondary / destructive) are one class —
//          spec section 8: one implementation per recipe, reused on every
//          screen. The secondary variant's default is transparent: the
//          container surface shows through (fills resolve the actual parent
//          via UITheme.ContainerSurface — D4 audit rule #1).
// Inheritance note: this extends WinForms' Button (not for behavior, but so
//          the control keeps Button's keyboard activation, tab stop and
//          UIAutomation Invoke pattern for free) while painting all of its
//          chrome itself.
// =============================================================================

using System.Windows.Forms;

namespace CodeQuest.UI;

/// <summary>
/// The flat button of recipe 4.1: 8px radius, 32px height, Body label,
/// three variants, five interaction states.
/// </summary>
public sealed class FlatButton : Button
{
    private UITheme.FlatButtonVariant _variant = UITheme.FlatButtonVariant.Primary;
    private bool _hovering;
    private bool _pressed;

    /// <summary>Creates the button in the primary variant at the recipe height.</summary>
    public FlatButton()
    {
        SetStyle(ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat; // recipe 4.1 assumes FlatStyle.Flat (§4 preamble)

        Font = UITheme.Body;
        Height = UITheme.Scaled(this, 32);
        // The themed default button face must be off, or it shows through the
        // rounded corner cutouts (the paint fills the recipe colors itself).
        // BackColor is only the parentless fallback: at paint time the corner
        // cutouts read UITheme.ContainerSurface(this) — the surface the
        // button actually sits on (cards, dialogs, module backgrounds differ;
        // D4 audit rule #1: never assume a fixed token).
        BackColor = UITheme.Surface;
        UseVisualStyleBackColor = false;
    }

    /// <summary>Which of the three recipe-4.1 variants this button is.</summary>
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

        // Paint the button background ourselves — ButtonBase's own background
        // pass leaves the rounded corner cutouts unpainted (black specks).
        // The cutout color is the CONTAINER's surface (D4 audit rule #1):
        // a fixed token shows square corners on any other elevation.
        using (var backgroundBrush = new System.Drawing.SolidBrush(UITheme.ContainerSurface(this)))
        {
            e.Graphics.FillRectangle(backgroundBrush, ClientRectangle);
        }

        System.Drawing.Rectangle bounds = new(0, 0, Width - 1, Height - 1);
        if (!fill.IsEmpty) // Secondary default is transparent (§4.1) — skip the fill pass
        {
            using (var fillPath = UITheme.RoundedPath(bounds, UITheme.Scaled(this, UITheme.RadiusControl)))
            {
                using var fillBrush = new System.Drawing.SolidBrush(fill);
                e.Graphics.FillPath(fillBrush, fillPath);
            }
        }

        if (Focused)
        {
            // §2.6 focus ring (D4 three-case rule): 1px, inset 1px, never a
            // glow — TextPrimary over the filled accent/danger variants
            // (accent-on-accent is 2.0:1 on dark, invisible), AccentText
            // everywhere else.
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

    /// <summary>Maps the live control state to the recipe's state table.</summary>
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
