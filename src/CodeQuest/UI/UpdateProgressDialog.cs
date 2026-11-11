// =============================================================================
// CodeQuest — In-App Update (task 2): the "Updating CodeQuest" dialog.
// UX contract (owner directive): clicking "Update now" must NOT hand the user
// to GitHub — this dialog IS the update experience. It walks the three phases
// of SelfUpdateService (Downloading with a percent, Verifying, Installing)
// and closes the app when the installer is applying; the apply script
// relaunches CodeQuest by itself. Failure is a friendly inline line in this
// dialog with "Try again"/"Close" — never a raw error, never a browser.
// "Cancel" aborts during the download and returns to the running session.
// =============================================================================

using CodeQuest.Services;
using System.Drawing;
using System.Windows.Forms;

namespace CodeQuest.UI;

/// <summary>
/// The "updating" dialog: download progress, verify, silent install — then
/// this session ends and the new version starts itself.
/// </summary>
public sealed class UpdateProgressDialog : Form
{
    private const string Title = "Updating CodeQuest";
    private const string DownloadingLabel = "Downloading update…";
    private const string VerifyingLabel = "Verifying update…";
    private const string InstallingLabel = "Installing update…";
    private const string RestartNote = "CodeQuest will restart by itself when the update finishes.";
    private const string CancelButtonText = "Cancel";
    private const string TryAgainButtonText = "Try again";
    private const string CloseButtonText = "Close";

    private readonly UpdateCheckResult _result;
    private readonly Label _status;
    private readonly ProgressTrack _track;
    private readonly FlatButton _cancel;
    private readonly FlatButton _retry;
    private readonly FlatButton _close;
    private CancellationTokenSource? _cancellation;

    /// <summary>Creates the updating dialog for one verified update.</summary>
    public UpdateProgressDialog(UpdateCheckResult result)
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
        ClientSize = new Size(UITheme.Scaled(this, 470), UITheme.Scaled(this, 210));

        int inset = UITheme.Scaled(this, UITheme.Spacing16);
        int cardWidth = ClientSize.Width - inset * 2;

        var card = new CardPanel
        {
            Name = "UpdatingCard",
            Location = new Point(inset, inset),
            Size = new Size(cardWidth, ClientSize.Height - inset * 2),
        };

        var title = new Label
        {
            Name = "UpdatingTitle",
            Text = Title,
            Font = UITheme.Title,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(inset, inset),
            Size = new Size(cardWidth - inset * 2, UITheme.Scaled(this, 32)),
        };

        _status = new Label
        {
            Name = "UpdatingStatus",
            Text = DownloadingLabel,
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(inset, inset + UITheme.Scaled(this, 40)),
            Size = new Size(cardWidth - inset * 2, UITheme.Scaled(this, 34)),
        };

        _track = new ProgressTrack
        {
            Name = "UpdatingProgress",
            AccessibleName = "Update progress",
            Location = new Point(inset, inset + UITheme.Scaled(this, 82)),
            Size = new Size(cardWidth - inset * 2, UITheme.Scaled(this, 8)),
            Marquee = true,
        };

        var note = new Label
        {
            Name = "UpdatingNote",
            Text = RestartNote,
            Font = UITheme.Caption,
            ForeColor = UITheme.TextTertiary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(inset, inset + UITheme.Scaled(this, 100)),
            Size = new Size(cardWidth - inset * 2, UITheme.Scaled(this, 18)),
        };

        card.Controls.Add(title);
        card.Controls.Add(_status);
        card.Controls.Add(_track);
        card.Controls.Add(note);

        // The action row (UpdateDialog family: primary left, secondary right).
        // "Try again"/"Close" exist only for the failure state.
        _retry = new FlatButton
        {
            Name = "UpdateRetryButton",
            Text = TryAgainButtonText,
            Location = new Point(inset, ClientSize.Height - inset - UITheme.Scaled(this, 34)),
            Size = new Size(UITheme.Scaled(this, 150), UITheme.Scaled(this, 34)),
            Visible = false,
        };
        _retry.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<UpdateProgressDialog>() is { } dialog)
            {
                dialog.Retry();
            }
        };

        _cancel = new FlatButton
        {
            Name = "UpdateCancelButton",
            Text = CancelButtonText,
            Variant = UITheme.FlatButtonVariant.Secondary,
            Location = new Point(cardWidth - inset - UITheme.Scaled(this, 110), ClientSize.Height - inset - UITheme.Scaled(this, 34)),
            Size = new Size(UITheme.Scaled(this, 110), UITheme.Scaled(this, 34)),
        };
        _cancel.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<UpdateProgressDialog>() is { } dialog)
            {
                dialog.CancelUpdate();
            }
        };

        _close = new FlatButton
        {
            Name = "UpdateCloseButton",
            Text = CloseButtonText,
            Variant = UITheme.FlatButtonVariant.Secondary,
            Location = new Point(cardWidth - inset - UITheme.Scaled(this, 110), ClientSize.Height - inset - UITheme.Scaled(this, 34)),
            Size = new Size(UITheme.Scaled(this, 110), UITheme.Scaled(this, 34)),
            Visible = false,
        };
        _close.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<UpdateProgressDialog>() is { } dialog)
            {
                dialog.DialogResult = DialogResult.Cancel;
                dialog.Close();
            }
        };

        Controls.Add(_retry);
        Controls.Add(_cancel);
        Controls.Add(_close);
        Controls.Add(card);

        CancelButton = _cancel; // Esc during the download aborts the update

        Shown += static (sender, e) =>
        {
            if (sender is UpdateProgressDialog dialog)
            {
                dialog.BeginUpdate();
            }
        };
    }

    /// <summary>Starts one download-verify-apply attempt in the background.</summary>
    private void BeginUpdate()
    {
        _cancellation?.Dispose();
        _cancellation = new CancellationTokenSource();

        _status.ForeColor = UITheme.TextSecondary;
        _status.Text = DownloadingLabel;
        _track.Value = 0;
        _track.Marquee = true;
        _retry.Visible = false;
        _close.Visible = false;
        _cancel.Visible = true;
        _cancel.Enabled = true;
        CancelButton = _cancel;

        var reporter = new Progress<SelfUpdateProgress>(ApplyProgress); // captures the UI context
        CancellationTokenSource cancellation = _cancellation;
        string? installerUrl = _result.InstallerUrl;

        Task.Run(() => SelfUpdateService.ApplyAsync(installerUrl!, _result.InstallerSha256, reporter, cancellation.Token))
            .ContinueWith(
                task => Finish(task),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>Applies one progress report from the update engine.</summary>
    private void ApplyProgress(SelfUpdateProgress progress)
    {
        switch (progress.Phase)
        {
            case SelfUpdatePhase.Downloading when progress.Fraction is double fraction:
                _track.Marquee = false;
                _track.Value = fraction;
                _status.Text = $"{DownloadingLabel} {(int)Math.Round(fraction * 100)}%";
                break;
            case SelfUpdatePhase.Downloading:
                _track.Marquee = true; // total size unknown: activity, never a fake percent
                _status.Text = DownloadingLabel;
                break;
            case SelfUpdatePhase.Verifying:
                _track.Value = 0;
                _track.Marquee = true;
                _status.Text = VerifyingLabel;
                break;
            case SelfUpdatePhase.Installing:
                _track.Value = 0;
                _track.Marquee = true;
                _status.Text = InstallingLabel;
                _cancel.Enabled = false; // the installer is launching; nothing left to cancel
                CancelButton = null;
                break;
        }
    }

    /// <summary>Ends one attempt: applied (exit for the install), cancelled, or failed.</summary>
    private void Finish(Task<SelfUpdateOutcome> task)
    {
        if (task.IsCanceled || (task.IsFaulted && task.Exception is not null))
        {
            ShowFailure("The update could not be completed. Please try again later.");
            return;
        }

        SelfUpdateOutcome outcome = task.Result;
        switch (outcome.Status)
        {
            case SelfUpdateStatus.Applied:
                DialogResult = DialogResult.OK; // the shell closes; the script installs and relaunches
                Close();
                break;
            case SelfUpdateStatus.Cancelled:
                DialogResult = DialogResult.Cancel; // the session simply continues
                Close();
                break;
            case SelfUpdateStatus.Failed:
                ShowFailure(outcome.FailureMessage);
                break;
        }
    }

    /// <summary>The failure state: friendly message inline, retry/close instead of cancel.</summary>
    private void ShowFailure(string message)
    {
        _cancellation?.Dispose();
        _cancellation = null;

        _track.Marquee = false;
        _track.Value = 0;
        _status.ForeColor = UITheme.DangerText;
        _status.Text = message;
        _cancel.Visible = false;
        _retry.Visible = true;
        _close.Visible = true;
        CancelButton = _close;
    }

    /// <summary>The user cancelled the download; the engine reports the outcome.</summary>
    private void CancelUpdate()
    {
        _cancel.Enabled = false; // one cancel is enough; the outcome closes the dialog
        _cancellation?.Cancel();
    }

    /// <summary>Another attempt after a failure.</summary>
    private void Retry() => BeginUpdate();
}
