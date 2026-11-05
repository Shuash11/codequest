// =============================================================================
// CodeQuest — Phase P7: UI Shell & Navigation
// Recipe 4.11 of the design of record: the custom modal dialog.
// System.Windows.Forms.MessageBox is NOT used anywhere — it cannot be styled
// (spec 4.11 bans it; all confirmations and blockers run through this form).
// Look at: the layout — a Surface card (12px radius) centered over the owner
//          window, no drop shadow, no dimmed backdrop (flat modal); min width
//          400px; content inset 24px; title in BodySemibold, message in Body
//          TextPrimary; buttons in a right-aligned (trailing) row, 8px apart.
//          HIG alert rules (§3.4): the primary action is a specific verb on a
//          filled button, default on the trailing side; a "Cancel"-style
//          secondary is never the default; no "Error" titles, no exclamation
//          marks. The two P7 dialogs carry the spec's canonical copy:
//          the startup blocker (database missing) and the unexpected-error
//          dialog (plain language + retry hint, never a stack trace).
// =============================================================================

using System.Drawing;
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

    private DialogForm(string title)
    {
        FormBorderStyle = FormBorderStyle.None;
        // The title also becomes the window caption (UIA/window lists read it,
        // even though the borderless card draws no title bar).
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        BackColor = UITheme.Surface;
        KeyPreview = true;
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
            secondaryText: null);
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
            secondaryText: "Try again");

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
    public static bool ShowConfirm(string title, string message, string confirmText)
    {
        DialogResult result = Show(
            owner: Form.ActiveForm,
            title: title,
            message: message,
            primaryText: confirmText,
            secondaryText: "Cancel",
            primaryVariant: UITheme.FlatButtonVariant.Destructive);

        return result == DialogResult.OK;
    }

    /// <summary>
    /// Builds and shows one dialog card. The primary button is the filled,
    /// default (trailing) one — the recipe's primary variant unless the caller
    /// passes the destructive one (the confirmations of §4.11); the optional
    /// secondary is a plain button to its left. The secondary answers with
    /// DialogResult.No, the primary with OK, so the caller can tell the two
    /// apart without comparing captions.
    /// </summary>
    private static DialogResult Show(IWin32Window? owner, string title, string message, string primaryText, string? secondaryText, UITheme.FlatButtonVariant primaryVariant = UITheme.FlatButtonVariant.Primary)
    {
        using var dialog = new DialogForm(title);
        var primary = new FlatButton { Text = primaryText, Variant = primaryVariant };
        var secondary = secondaryText is null ? null : new FlatButton { Text = secondaryText, Variant = UITheme.FlatButtonVariant.Secondary };

        primary.DialogResult = DialogResult.OK;
        if (secondary is not null)
        {
            secondary.DialogResult = DialogResult.No;
        }

        // Measure the wrapped message and the button row, then size the card.
        int contentWidth = UITheme.Scaled(dialog, MinDialogWidth) - ContentInset * 2;
        Size messageSize = TextRenderer.MeasureText(
            message,
            UITheme.Body,
            new Size(contentWidth, int.MaxValue),
            TextFormatFlags.WordBreak);

        int primaryWidth = TextRenderer.MeasureText(primaryText, UITheme.Body).Width + ContentInset;
        int secondaryWidth = secondaryText is null ? 0 : TextRenderer.MeasureText(secondaryText, UITheme.Body).Width + ContentInset;
        int buttonRowWidth = primaryWidth + (secondary is null ? 0 : secondaryWidth + ButtonGap);

        int width = Math.Max(
            UITheme.Scaled(dialog, MinDialogWidth),
            buttonRowWidth + ContentInset * 2);

        int titleHeight = TextRenderer.MeasureText(title, UITheme.BodySemibold).Height;
        int bodyTop = ContentInset + titleHeight + UITheme.Spacing8;
        int buttonsTop = bodyTop + messageSize.Height + ContentInset;
        int height = buttonsTop + ButtonHeight + ContentInset;

        var titleLabel = new Label
        {
            Text = title,
            Font = UITheme.BodySemibold,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(ContentInset, ContentInset),
            Size = new Size(width - ContentInset * 2, titleHeight),
        };

        var messageLabel = new Label
        {
            Text = message,
            Font = UITheme.Body,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(ContentInset, bodyTop),
            Size = new Size(width - ContentInset * 2, messageSize.Height),
        };

        int cursorX = width - ContentInset - primaryWidth;
        primary.SetBounds(cursorX, buttonsTop, primaryWidth, ButtonHeight);
        if (secondary is not null)
        {
            cursorX -= secondaryWidth + ButtonGap;
            secondary.SetBounds(cursorX, buttonsTop, secondaryWidth, ButtonHeight);
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
