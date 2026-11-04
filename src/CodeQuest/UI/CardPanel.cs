// =============================================================================
// CodeQuest — Phase P7: UI Shell & Navigation
// Recipe 4.2 of the design of record: the rounded panel / card.
// Look at: UpdateRegion — the card is a Surface panel whose Region is the
//          12px rounded path of the spec (§2.4 large-container radius), so
//          the Background shows through the corners. No border and no shadow
//          by default — the white-on-Background contrast does the separation
//          (§4.2; §3.6: borders instead of shadows). Groups of controls
//          inside one card get 8px internal gaps and a 16px inset from the
//          card edge (the forms place their children with those values).
// =============================================================================

using System.Windows.Forms;

namespace CodeQuest.UI;

/// <summary>
/// The card of recipe 4.2: Surface fill, 12px radius, no border, no shadow.
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
