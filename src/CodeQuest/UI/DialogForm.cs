// =============================================================================
// CodeQuest — Phase P7: UI Shell & Navigation
// Recipe 4.11 of the design of record: the custom modal dialog.
// System.Windows.Forms.MessageBox is NOT used anywhere — it cannot be styled
// (spec 4.11 bans it; all confirmations and blockers run through this form).
// Look at: the layout — a SurfaceRaised card (12px radius, 1px BorderHairline
//          border) centered over the owner window, no dimmed backdrop (flat
//          modal); min width 400px. D4 §2: the dialog is the app's ONLY
//          floating level — SurfaceRaised is elevation level 3 — and the one
//          element with both a hairline and a shadow: the native CS_DROPSHADOW
//          (CreateParams class style 0x20000), platform-drawn, owner-mandated;
//          no GDI+ shadow painting. The alert anatomy:
//          a centered soft-tinted mark circle (DangerTint for the unexpected
//          error and the destructive confirms, WarningTint for the startup
//          blocker — the glyph + tint are the situation, never color alone),
//          the title in the Title font, the message in Body — both centered —
//          then the buttons in a right-aligned (trailing) row, 8px apart.
//          HIG alert rules (§3.4): the primary action is a specific verb on a
//          filled button, default on the trailing side; a "Cancel"-style
//          secondary is never the default; no "Error" titles, no exclamation
//          marks. The three P7/P8 dialogs carry the spec's canonical copy:
//          the startup blocker (database missing), the unexpected-error
//          dialog (plain language + retry hint, never a stack trace) and the
//          destructive confirmations.
// =============================================================================

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CodeQuest.UI;

/// <summary>
/// The styled modal dialog of recipe 4.11. Use the two Show helpers for the
/// P7 dialogs; later phases reuse the form for their confirmations.
/// </summary>
public sealed class DialogForm : Form
{
    private const int MinDialogWidth = 400;
    private const int ContentInset = UITheme.Spacing24;
    private const int ButtonGap = UITheme.Spacing8;
    private const int ButtonHeight = 32;
    private const int MarkSize = 40;

    /// <summary>The centered mark of the alert anatomy: tint circle + status glyph.</summary>
    private sealed record DialogMark(Color Circle, Color GlyphColor, string Glyph);

    // The three marks: the tinted circle carries the situation at a glance —
    // red for errors and destructive confirms, amber for the startup blocker.
    private static readonly DialogMark UnexpectedErrorMark = new(UITheme.DangerTint, UITheme.DangerText, "\uE783");
    private static readonly DialogMark DestructiveConfirmMark = new(UITheme.DangerTint, UITheme.DangerText, "\uE7BA");
    private static readonly DialogMark StartupBlockerMark = new(UITheme.WarningTint, UITheme.WarningText, "\uE7BA");

    private DialogForm(string title)
    {
        FormBorderStyle = FormBorderStyle.None;
        // The title also becomes the window caption (UIA/window lists read it,
        // even though the borderless card draws no title bar).
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        BackColor = UITheme.SurfaceRaised; // the one floating elevation level (D4 §2)
        KeyPreview = true;
    }

    /// <summary>
    /// The dialog's native drop shadow (D4 §2, owner-mandated): the
    /// CS_DROPSHADOW class style (0x20000), drawn by the platform — the only
    /// shadow in the app, and the only chrome this form adds.
    /// </summary>
    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.ClassStyle |= 0x20000; // CS_DROPSHADOW
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

    /// <summary>Clips the dialog to the recipe's rounded card shape.</summary>
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

    /// <summary>
    /// The startup blocker of spec 4.11: the database file is missing, the app
    /// cannot proceed — the one startup dialog, with the spec's exact copy and
    /// a single "Close" button.
    /// </summary>
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

    /// <summary>
    /// The unexpected-exception dialog of spec 4.11: plain language plus a
    /// hint to retry — never a stack trace, never raw exception text.
    /// Returns true when the user chose "Try again" (the caller may continue),
    /// false for "Close".
    /// </summary>
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

    /// <summary>
    /// The confirmation dialog of spec 4.11 (used by the P8 management
    /// screens): a title that names the situation, one sentence of
    /// consequence, the confirmed action as a specific verb on the filled
    /// (destructive) button and a "Cancel" secondary that is never the
    /// default. Returns true when the user confirmed.
    /// </summary>
    /// <param name="title">The situation in one line ("Delete this topic?").</param>
    /// <param name="message">The consequence sentence ("This topic will be removed. This cannot be undone.").</param>
    /// <param name="confirmText">The specific verb ("Delete").</param>
    /// <param name="secondaryText">The dismiss verb ("Cancel"; spec 5.6's leave confirmation says "Stay").</param>
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

    /// <summary>
    /// Builds and shows one dialog card with the alert anatomy: a centered
    /// tinted mark, the title and message centered beneath it, and the button
    /// row on the trailing side. The primary button is the filled, default
    /// (trailing) one — the recipe's primary variant unless the caller passes
    /// the destructive one (the confirmations of §4.11); the optional
    /// secondary is a plain button to its left. The secondary answers with
    /// DialogResult.No, the primary with OK, so the caller can tell the two
    /// apart without comparing captions.
    /// </summary>
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

        // Measure the wrapped message and the button row, then size the card.
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

        // The alert stack: mark circle, title, message, buttons.
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
            BackColor = UITheme.SurfaceRaised, // must equal the dialog fill (D4 audit #9)
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
            BackColor = UITheme.SurfaceRaised, // must equal the dialog fill (D4 audit #9)
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(inset, bodyTop),
            Size = new Size(width - inset * 2, messageSize.Height),
        };

        // The card's chrome: the D4 hairline border (the card treatment —
        // BorderHairline per §2, not a separator) and the centered mark — a
        // soft-tinted circle with the status glyph, so the dialog's situation
        // reads at a glance (mark + words, never color alone).
        DialogMark activeMark = mark ?? UnexpectedErrorMark;
        int markX = (width - markSize) / 2;
        dialog.Paint += (sender, e) =>
        {
            if (sender is Form form)
            {
                using var borderPen = new Pen(UITheme.BorderHairline); // D4: the dialog edge is a hairline, not a separator
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

        // No owner yet (the startup blocker runs before any window exists) —
        // center on the screen instead of on a parent window.
        dialog.StartPosition = FormStartPosition.CenterScreen;
        return dialog.ShowDialog();
    }
}
