// =============================================================================
// CodeQuest — Phase P7: UI Shell & Navigation
// Recipe 4.2 of the design of record: the rounded panel / card.
// Look at: UpdateRegion — the card is a Surface panel whose Region is the
//          12px rounded path of the spec (§2.4 large-container radius), and
//          OnPaint — the 1px BorderHairline edge on all four sides (D4 §2:
//          on dark the hairline replaces the light theme's contrast-only
//          separation — Surface vs Background reads flatter, so the border
//          does real work). No shadow, ever (§4.2; §3.6: borders instead of
//          shadows; a card never carries both a border and a shadow — the
//          dialog is the single owner-mandated shadow exception).
//          Groups of controls inside one card get 8px internal gaps and a
//          16px inset from the card edge (the forms place their children
//          with those values).
// =============================================================================

using System.Drawing;
using System.Windows.Forms;

namespace CodeQuest.UI;

/// <summary>
/// The card of recipe 4.2: Surface fill, 12px radius, 1px BorderHairline
/// edge, no shadow.
/// </summary>
public sealed class CardPanel : Panel
{
    /// <summary>Creates the card with the recipe's Surface fill.</summary>
    public CardPanel()
    {
        SetStyle(ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = UITheme.Surface;
    }

    /// <summary>The D4 hairline edge: 1px BorderHairline on all four sides.</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        using var borderPath = UITheme.RoundedPath(
            new Rectangle(0, 0, Width - 1, Height - 1),
            UITheme.Scaled(this, UITheme.RadiusCard));
        using var borderPen = new Pen(UITheme.BorderHairline);
        e.Graphics.DrawPath(borderPen, borderPath);
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        UpdateRegion();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UpdateRegion();
    }

    /// <summary>Clips the panel (and its children) to the rounded path.</summary>
    private void UpdateRegion()
    {
        if (Width <= 0 || Height <= 0)
        {
            return;
        }

        using var path = UITheme.RoundedPath(
            new System.Drawing.Rectangle(0, 0, Width, Height),
            UITheme.Scaled(this, UITheme.RadiusCard));

        Region? oldRegion = Region;
        Region = new Region(path);
        oldRegion?.Dispose();
    }
}
