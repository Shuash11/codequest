// The update dialog.

using CodeQuest.Services;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace CodeQuest.UI;

public sealed class UpdateDialog : Form
{
    private const string Title = "Update required";
    private const string NotNowButton = "Not now";
    private const string UpdateNowButton = "Update now";
    private const string WhatIsNewLabel = "What's new";
    private const string NotesFallback = "Bug fixes and improvements.";

    private readonly UpdateCheckResult _result;

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

        AcceptButton = download;
    }

    private void UpdateNow()
    {
        if (_result.InstallerUrl is null)
        {
            OpenInBrowser();
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void OpenInBrowser()
    {
        if (!_result.DownloadUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return; // the fallback obeys the same https rule as the in-app path (security review P1)
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _result.DownloadUrl,
                UseShellExecute = true,
            });
        }
        catch
        {
        }
    }
}
