// The app's message dialog. (Inheritance)

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CodeQuest.UI;

public sealed class DialogForm : Form
{
    private const int MinDialogWidth = 400;
    private const int ContentInset = UITheme.Spacing24;
    private const int ButtonGap = UITheme.Spacing8;
    private const int ButtonHeight = 32;
    private const int MarkSize = 40;

    private sealed record DialogMark(Color Circle, Color GlyphColor, string Glyph);

    private static readonly DialogMark UnexpectedErrorMark = new(UITheme.DangerTint, UITheme.DangerText, "\uE783");
    private static readonly DialogMark DestructiveConfirmMark = new(UITheme.DangerTint, UITheme.DangerText, "\uE7BA");
    private static readonly DialogMark StartupBlockerMark = new(UITheme.WarningTint, UITheme.WarningText, "\uE7BA");

    private DialogForm(string title)
    {
        FormBorderStyle = FormBorderStyle.None;
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        BackColor = UITheme.SurfaceRaised;
        KeyPreview = true;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.ClassStyle |= 0x20000;
            return parameters;
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        UpdateRegion();
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        UpdateRegion();
    }

    private void UpdateRegion()
    {
        if (Width <= 0 || Height <= 0)
        {
            return;
        }

        using var path = UITheme.RoundedPath(new Rectangle(0, 0, Width, Height), UITheme.RadiusCard);
        Region? oldRegion = Region;
        Region = new Region(path);
        oldRegion?.Dispose();
    }

    public static void ShowStartupBlocker()
    {
        Show(
            owner: null,
            title: "CodeQuest can't find its database",
            message: "The database file was not found next to the app. Restore codequest.db or run database/schema.sql and database/seed.sql, then start the app again.",
            primaryText: "Close",
            secondaryText: null,
            mark: StartupBlockerMark);
    }

    public static bool ShowUnexpectedError()
    {
        DialogResult result = Show(
            owner: Form.ActiveForm,
            title: "Something went wrong",
            message: "An unexpected problem stopped the last action. You can try it again; if it keeps happening, close the app and start it again.",
            primaryText: "Close",
            secondaryText: "Try again",
            mark: UnexpectedErrorMark);

        return result == DialogResult.No;
    }

    public static bool ShowConfirm(string title, string message, string confirmText, string secondaryText = "Cancel")
    {
        DialogResult result = Show(
            owner: Form.ActiveForm,
            title: title,
            message: message,
            primaryText: confirmText,
            secondaryText: secondaryText,
            primaryVariant: UITheme.FlatButtonVariant.Destructive,
            mark: DestructiveConfirmMark);

        return result == DialogResult.OK;
    }

    private static DialogResult Show(IWin32Window? owner, string title, string message, string primaryText, string? secondaryText, UITheme.FlatButtonVariant primaryVariant = UITheme.FlatButtonVariant.Primary, DialogMark? mark = null)
    {
        using var dialog = new DialogForm(title);
        var primary = new FlatButton { Text = primaryText, Variant = primaryVariant };
        var secondary = secondaryText is null ? null : new FlatButton { Text = secondaryText, Variant = UITheme.FlatButtonVariant.Secondary };

        primary.DialogResult = DialogResult.OK;
        if (secondary is not null)
        {
            secondary.DialogResult = DialogResult.No;
        }

        int inset = UITheme.Scaled(dialog, ContentInset);
        int gap = UITheme.Scaled(dialog, ButtonGap);
        int buttonHeight = UITheme.Scaled(dialog, ButtonHeight);

        int contentWidth = UITheme.Scaled(dialog, MinDialogWidth) - inset * 2;
        Size messageSize = TextRenderer.MeasureText(
            message,
            UITheme.Body,
            new Size(contentWidth, int.MaxValue),
            TextFormatFlags.WordBreak);

        int primaryWidth = TextRenderer.MeasureText(primaryText, UITheme.Body).Width + inset;
        int secondaryWidth = secondaryText is null ? 0 : TextRenderer.MeasureText(secondaryText, UITheme.Body).Width + inset;
        int buttonRowWidth = primaryWidth + (secondary is null ? 0 : secondaryWidth + gap);

        int width = Math.Max(
            UITheme.Scaled(dialog, MinDialogWidth),
            buttonRowWidth + inset * 2);

        int markSize = UITheme.Scaled(dialog, MarkSize);
        int markTop = inset;
        int titleTop = markTop + markSize + UITheme.Scaled(dialog, UITheme.Spacing16);
        int titleHeight = TextRenderer.MeasureText(title, UITheme.Title).Height;
        int bodyTop = titleTop + titleHeight + UITheme.Scaled(dialog, UITheme.Spacing8);
        int buttonsTop = bodyTop + messageSize.Height + UITheme.Scaled(dialog, UITheme.Spacing24);
        int height = buttonsTop + buttonHeight + inset;

        var titleLabel = new Label
        {
            Text = title,
            Font = UITheme.Title,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.SurfaceRaised,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(inset, titleTop),
            Size = new Size(width - inset * 2, titleHeight),
        };

        var messageLabel = new Label
        {
            Text = message,
            Font = UITheme.Body,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.SurfaceRaised,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(inset, bodyTop),
            Size = new Size(width - inset * 2, messageSize.Height),
        };

        DialogMark activeMark = mark ?? UnexpectedErrorMark;
        int markX = (width - markSize) / 2;
        dialog.Paint += (sender, e) =>
        {
            if (sender is Form form)
            {
                using var borderPen = new Pen(UITheme.BorderHairline);
                using GraphicsPath borderPath = UITheme.RoundedPath(
                    new Rectangle(0, 0, form.Width - 1, form.Height - 1), UITheme.RadiusCard);
                e.Graphics.DrawPath(borderPen, borderPath);

                using var circleBrush = new SolidBrush(activeMark.Circle);
                e.Graphics.FillEllipse(circleBrush, markX, markTop, markSize, markSize);
                TextRenderer.DrawText(
                    e.Graphics,
                    activeMark.Glyph,
                    UITheme.NavGlyph,
                    new Rectangle(markX, markTop, markSize, markSize),
                    activeMark.GlyphColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        };

        int cursorX = width - inset - primaryWidth;
        primary.SetBounds(cursorX, buttonsTop, primaryWidth, buttonHeight);
        if (secondary is not null)
        {
            cursorX -= secondaryWidth + gap;
            secondary.SetBounds(cursorX, buttonsTop, secondaryWidth, buttonHeight);
            dialog.Controls.Add(secondary);
        }

        dialog.Controls.Add(titleLabel);
        dialog.Controls.Add(messageLabel);
        dialog.Controls.Add(primary);
        dialog.AcceptButton = primary;

        dialog.ClientSize = new Size(width, height);

        if (owner is not null)
        {
            return dialog.ShowDialog(owner);
        }

        dialog.StartPosition = FormStartPosition.CenterScreen;
        return dialog.ShowDialog();
    }
}
