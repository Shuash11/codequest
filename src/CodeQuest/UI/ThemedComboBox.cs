// =============================================================================
// CodeQuest — Phase P8: Topic & Quiz Management
// Recipe 4.5 of the design of record: the combo box.
// Look at: DropDownStyle = DropDownList ALWAYS — free text is impossible by
//          construction, which is the R-2 #7 lock for the topic category
//          (exactly the two items Programming / Cybersecurity, never a third
//          value). The dropdown rows are owner-drawn per the recipe: Surface
//          rows with hairline separators, Fill hover row, AccentTint selected
//          row, Body text. The closed face uses the native flat chrome of the
//          platform (the recorded tradeoff the spec itself makes for grid
//          scrollbars, §4.6: native platform chrome stays native in GDI+).
// Users:   the topic category dropdown (two values), the quiz form's topic
//          picker and the quiz management topic filter (spec 5.3, 5.4).
// =============================================================================

using System.Drawing;
using System.Windows.Forms;

namespace CodeQuest.UI;

/// <summary>
/// The bounded-choice dropdown of recipe 4.5: DropDownList always, owner-drawn
/// rows, Body text.
/// </summary>
public sealed class ThemedComboBox : ComboBox
{
    /// <summary>Creates the combo with the recipe's locked dropdown style.</summary>
    public ThemedComboBox()
    {
        DropDownStyle = ComboBoxStyle.DropDownList; // recipe 4.5: never free text
        FlatStyle = FlatStyle.Flat;
        DrawMode = DrawMode.OwnerDrawFixed;
        Font = UITheme.Body;
        BackColor = UITheme.Surface;
        ForeColor = UITheme.TextPrimary;
        ItemHeight = UITheme.Scaled(this, 24);
    }

    /// <summary>The dropdown rows of recipe 4.5.</summary>
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        base.OnDrawItem(e);

        if (e.Index < 0 || e.Index >= Items.Count)
        {
            return;
        }

        bool selected = (e.State & DrawItemState.Selected) != 0;

        // Row fill: AccentTint when selected/hot, otherwise the Surface row.
        Color rowBack = selected ? UITheme.AccentTint : UITheme.Surface;
        using (var rowBrush = new SolidBrush(rowBack))
        {
            e.Graphics.FillRectangle(rowBrush, e.Bounds);
        }

        // Hairline separator under every row (recipe 4.5: Surface rows with
        // hairline separators).
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

    /// <summary>The closed face repaints with the selection so DPI stays crisp.</summary>
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        ItemHeight = UITheme.Scaled(this, 24);
    }
}
