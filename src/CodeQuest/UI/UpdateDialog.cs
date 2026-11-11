// =============================================================================
// CodeQuest — Version Update Check (I6+): the update dialog of the version
// check. UX contract: the user learns THREE things at a glance — that an
// update is required, WHICH version they would get, and WHAT changed (the
// release notes, rendered as plain readable text, never raw markdown).
// Buttons: "Update now" performs the update INSIDE the app — when the release
// carries an installer asset it opens the "Updating CodeQuest" dialog
// (UpdateProgressDialog): download, verify, silent install, self-relaunch —
// the user is never sent to GitHub. A release without an installer asset
// falls back to the old browser hand-off (the release asset opens in the
// browser) and ends this session. "Not now" continues the current session for
// people on a metered connection or in the middle of something.
// =============================================================================

using CodeQuest.Services;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace CodeQuest.UI;

/// <summary>
/// The "update required" dialog: new version, what changed, one-click
/// download.
/// </summary>
public sealed class UpdateDialog : Form
{
    private const string Title = "Update required";
    private const string NotNowButton = "Not now";
    private const string UpdateNowButton = "Update now";
    private const string WhatIsNewLabel = "What's new";
    private const string NotesFallback = "Bug fixes and improvements.";

    private readonly UpdateCheckResult _result;

    /// <summary>Creates the update dialog for one check result.</summary>
    public UpdateDialog(UpdateCheckResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _result = result;

        Text = "CodeQuest";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = UITheme.Background;
        ClientSize = new Size(UITheme.Scaled(this, 470), UITheme.Scaled(this, 430));

        int inset = UITheme.Scaled(this, UITheme.Spacing16);
        int cardWidth = ClientSize.Width - inset * 2;

        var card = new CardPanel
        {
            Name = "UpdateCard",
            Location = new Point(inset, inset),
            Size = new Size(cardWidth, ClientSize.Height - inset * 2),
        };

        var title = new Label
        {
            Name = "UpdateTitle",
            Text = Title,
            Font = UITheme.Title,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(inset, inset),
            Size = new Size(cardWidth - inset * 2, UITheme.Scaled(this, 32)),
        };

        var subtitle = new Label
        {
            Name = "UpdateSubtitle",
            Text = $"Version {result.LatestVersion} is available (you have {result.CurrentVersion}). Please update to keep everything working as expected.",
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(inset, inset + UITheme.Scaled(this, 36)),
            Size = new Size(cardWidth - inset * 2, UITheme.Scaled(this, 30)),
        };

        var whatIsNew = new Label
        {
            Name = "UpdateWhatIsNew",
            Text = WhatIsNewLabel,
            Font = UITheme.CaptionSemibold,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(inset, inset + UITheme.Scaled(this, 72)),
            Size = new Size(cardWidth - inset * 2, UITheme.Scaled(this, 18)),
        };

        // The release notes: a quiet inset box the user can scroll — the
        // UX-friendly "what changed" this feature exists for.
        var notesBox = new Panel
        {
            Name = "UpdateNotesBox",
            Location = new Point(inset, inset + UITheme.Scaled(this, 94)),
            Size = new Size(cardWidth - inset * 2, ClientSize.Height - inset * 2 - UITheme.Scaled(this, 94) - UITheme.Scaled(this, 58)),
            BackColor = UITheme.Fill,
        };
        var notes = new TextBox
        {
            Name = "UpdateNotes",
            Text = string.IsNullOrWhiteSpace(result.ReleaseNotes) ? NotesFallback : result.ReleaseNotes,
            Font = UITheme.Body,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Fill,
            BorderStyle = BorderStyle.None,
            ReadOnly = true,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
        };
        notesBox.Controls.Add(notes);
        card.Controls.Add(notesBox);
        card.Controls.Add(title);
        card.Controls.Add(subtitle);
        card.Controls.Add(whatIsNew);

        // The action row: the primary action updates INSIDE the app (the
        // "Updating CodeQuest" dialog takes over; the browser hand-off is the
        // fallback for releases without an installer asset); "Not now" stays
        // local.
        var download = new FlatButton
        {
            Name = "UpdateDownloadButton",
            Text = UpdateNowButton,
            Location = new Point(inset, ClientSize.Height - inset - UITheme.Scaled(this, 34)),
            Size = new Size(UITheme.Scaled(this, 180), UITheme.Scaled(this, 34)),
        };
        download.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<UpdateDialog>() is { } dialog)
            {
                dialog.UpdateNow();
            }
        };
        var notNow = new FlatButton
        {
            Name = "UpdateNotNowButton",
            Text = NotNowButton,
            Variant = UITheme.FlatButtonVariant.Secondary,
            Location = new Point(cardWidth - inset - UITheme.Scaled(this, 110), ClientSize.Height - inset - UITheme.Scaled(this, 34)),
            Size = new Size(UITheme.Scaled(this, 110), UITheme.Scaled(this, 34)),
        };
        notNow.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<UpdateDialog>() is { } dialog)
            {
                dialog.DialogResult = DialogResult.Cancel;
                dialog.Close();
            }
        };

        Controls.Add(download);
        Controls.Add(notNow);
        Controls.Add(card);

        AcceptButton = download; // Enter takes the required path: update
    }

    /// <summary>
    /// The primary path: when the release carries an installer asset, the
    /// "Updating CodeQuest" dialog performs the whole update (the shell
    /// closes once it reports the installer is applying). Without an
    /// installer asset, the old hand-off: the release asset opens in the
    /// browser and this session ends.
    /// </summary>
    private void UpdateNow()
    {
        if (_result.InstallerUrl is null)
        {
            OpenInBrowser();
        }

        DialogResult = DialogResult.OK; // MainForm decides what runs next
        Close();
    }

    /// <summary>
    /// The fallback hand-off: opens the release asset in the browser. Never
    /// crashes the dialog — the user can still reach the release page
    /// manually.
    /// </summary>
    private void OpenInBrowser()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _result.DownloadUrl,
                UseShellExecute = true, // the browser handles the download
            });
        }
        catch
        {
        }
    }
}
