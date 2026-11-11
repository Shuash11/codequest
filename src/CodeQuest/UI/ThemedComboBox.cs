// A styled dropdown.

using System.Drawing;
using System.Windows.Forms;

namespace CodeQuest.UI;

public sealed class ThemedComboBox : ComboBox
{
    public ThemedComboBox()
    {
        DropDownStyle = ComboBoxStyle.DropDownList;
        FlatStyle = FlatStyle.Flat;
        DrawMode = DrawMode.OwnerDrawFixed;
        Font = UITheme.Body;
        BackColor = UITheme.Surface;
        ForeColor = UITheme.TextPrimary;
        ItemHeight = UITheme.Scaled(this, 24);
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        base.OnDrawItem(e);

        if (e.Index < 0 || e.Index >= Items.Count)
        {
            return;
        }

        bool selected = (e.State & DrawItemState.Selected) != 0;

        Color rowBack = selected ? UITheme.AccentTint : UITheme.Surface;
        using (var rowBrush = new SolidBrush(rowBack))
        {
            e.Graphics.FillRectangle(rowBrush, e.Bounds);
        }

        using (var separatorPen = new Pen(UITheme.SeparatorHairline))
        {
            e.Graphics.DrawLine(separatorPen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        }

        string text = GetItemText(Items[e.Index]!) ?? string.Empty;
        TextRenderer.DrawText(
            e.Graphics,
            text,
            UITheme.Body,
            new Rectangle(e.Bounds.Left + UITheme.Scaled(this, UITheme.Spacing12), e.Bounds.Top, e.Bounds.Width - UITheme.Scaled(this, UITheme.Spacing12), e.Bounds.Height),
            UITheme.TextPrimary,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        ItemHeight = UITheme.Scaled(this, 24);
    }
}
