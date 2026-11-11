// A rounded card. (Inheritance)

using System.Drawing;
using System.Windows.Forms;

namespace CodeQuest.UI;

public sealed class CardPanel : Panel
{
    public CardPanel()
    {
        SetStyle(ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = UITheme.Surface;
    }

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
