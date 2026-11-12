// The AI rival dialog. (Inheritance)

using CodeQuest.Models;
using System.Globalization;
using System.Drawing;
using System.Windows.Forms;

namespace CodeQuest.UI;

// The result of one rival generation attempt: the new quiz id on success, a
// friendly reason on failure (never a raw exception).
public sealed record RivalGeneration(int? QuizId, string? Failure)
{
    public static RivalGeneration Ok(int quizId) => new(quizId, null);

    public static RivalGeneration Failed(string reason) => new(null, reason);
}

public sealed class RivalDialog : Form
{
    private const string WindowTitle = "The AI strikes back";
    private const string SuccessMessageFormat = "Level {0} is ready ({1}). It is waiting for you.";
    private const string FailureMessage = "Your rival retreated for now (no connection). Try again later.";
    private const string BringItOnButton = "Bring it on";
    private const string NotYetButton = "Not yet";
    private const string PlayNowButton = "Play now";
    private const string LaterButton = "Later";
    private const string TryAgainButton = "Try again";
    private const string CloseButton = "Close";

    private readonly Func<string, Difficulty, RivalGeneration> _generate;
    private readonly string _topicName;
    private readonly int _nextLevel;
    private readonly string _offerMessage;
    private readonly Label _message;
    private string? _failureReason;
    private readonly ProgressTrack _busyTrack;
    private readonly FlatButton _primary;
    private readonly FlatButton _secondary;
    private RivalState _state = RivalState.Offer;

    public RivalDialog(string topicName, int nextLevel, int passedPercent, Func<string, Difficulty, RivalGeneration> generate)
    {
        ArgumentNullException.ThrowIfNull(topicName);
        ArgumentNullException.ThrowIfNull(generate);
        _topicName = topicName;
        _nextLevel = nextLevel;
        _generate = generate;
        _offerMessage =
            $"Your rival grins: \"You passed my level with {passedPercent:0}%\u2026 I'll have the next one waiting for you.\"";

        Text = WindowTitle;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = UITheme.Background;
        ClientSize = new Size(UITheme.Scaled(this, 470), UITheme.Scaled(this, 250));

        int inset = UITheme.Scaled(this, UITheme.Spacing16);
        int buttonHeight = UITheme.Scaled(this, 34);
        int buttonTop = ClientSize.Height - inset - buttonHeight;
        int cardWidth = ClientSize.Width - inset * 2;

        var card = new CardPanel
        {
            Name = "RivalCard",
            Location = new Point(inset, inset),
            Size = new Size(cardWidth, buttonTop - inset - UITheme.Scaled(this, UITheme.Spacing8)),
        };

        var title = new Label
        {
            Name = "RivalTitle",
            Text = WindowTitle,
            Font = UITheme.Title,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(inset, inset),
            Size = new Size(cardWidth - inset * 2, UITheme.Scaled(this, 32)),
        };

        _message = new Label
        {
            Name = "RivalMessage",
            Text = _offerMessage,
            Font = UITheme.Body,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(inset, inset + UITheme.Scaled(this, 40)),
            Size = new Size(cardWidth - inset * 2, UITheme.Scaled(this, 60)),
        };

        _busyTrack = new ProgressTrack
        {
            Name = "RivalBusyTrack",
            Size = new Size(cardWidth - inset * 2, UITheme.Scaled(this, 8)),
            Location = new Point(inset, inset + UITheme.Scaled(this, 106)),
            Visible = false,
        };

        card.Controls.Add(_busyTrack);
        card.Controls.Add(_message);
        card.Controls.Add(title);

        _primary = new FlatButton
        {
            Name = "RivalPrimaryButton",
            Text = BringItOnButton,
            Location = new Point(inset, buttonTop),
            Size = new Size(UITheme.Scaled(this, 160), buttonHeight),
        };
        _primary.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<RivalDialog>() is { } dialog)
            {
                dialog.HandlePrimaryClick();
            }
        };

        _secondary = new FlatButton
        {
            Name = "RivalSecondaryButton",
            Text = NotYetButton,
            Variant = UITheme.FlatButtonVariant.Secondary,
            Location = new Point(ClientSize.Width - inset - UITheme.Scaled(this, 110), buttonTop),
            Size = new Size(UITheme.Scaled(this, 110), buttonHeight),
        };
        _secondary.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<RivalDialog>() is { } dialog)
            {
                dialog.HandleSecondaryClick();
            }
        };

        Controls.Add(_primary);
        Controls.Add(_secondary);
        Controls.Add(card);

        AcceptButton = _primary;
        ApplyState(RivalState.Offer);
    }

    public int? GeneratedQuizId { get; private set; }

    private void HandlePrimaryClick()
    {
        switch (_state)
        {
            case RivalState.Offer:
            case RivalState.Failure:
                StartGeneration();
                break;

            case RivalState.Success:
                DialogResult = DialogResult.OK;
                Close();
                break;
        }
    }

    private void HandleSecondaryClick()
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void StartGeneration()
    {
        ApplyState(RivalState.Busy);

        Task.Run(GenerateSafely)
            .ContinueWith(
                task => HandleGenerationResult(task.IsCompletedSuccessfully ? task.Result : null),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.FromCurrentSynchronizationContext());
    }

    private RivalGeneration? GenerateSafely()
    {
        try
        {
            // The next level's difficulty follows the campaign ladder.
            return _generate(_topicName, Campaign.DifficultyForLevel(_nextLevel));
        }
        catch
        {
            return null; // the dialog never throws to the UI
        }
    }

    private void HandleGenerationResult(RivalGeneration? generation)
    {
        if (IsDisposed || Disposing)
        {
            return; // the user closed the dialog while the rival was writing
        }

        if (generation?.QuizId is { } id)
        {
            GeneratedQuizId = id;
            ApplyState(RivalState.Success);
        }
        else
        {
            _failureReason = generation?.Failure;
            ApplyState(RivalState.Failure);
        }
    }

    private void ApplyState(RivalState state)
    {
        _state = state;

        switch (state)
        {
            case RivalState.Offer:
                _message.Text = _offerMessage;
                _primary.Text = BringItOnButton;
                _secondary.Text = NotYetButton;
                SetButtonsEnabled(enabled: true);
                SetBusy(visible: false);
                break;

            case RivalState.Busy:
                _message.Text = string.Format(
                    CultureInfo.InvariantCulture,
                    "Your rival is writing Level {0}\u2026",
                    _nextLevel);
                SetButtonsEnabled(enabled: false);
                SetBusy(visible: true);
                break;

            case RivalState.Success:
                _message.Text = string.Format(
                    CultureInfo.InvariantCulture,
                    SuccessMessageFormat,
                    _nextLevel,
                    Campaign.DifficultyForLevel(_nextLevel));
                _primary.Text = PlayNowButton;
                _secondary.Text = LaterButton;
                SetButtonsEnabled(enabled: true);
                SetBusy(visible: false);
                break;

            case RivalState.Failure:
                // The typed reason when the generator supplied one — "no
                // connection" alone used to hide quota/auth/service errors.
                _message.Text = string.IsNullOrWhiteSpace(_failureReason)
                    ? FailureMessage
                    : "Your rival stumbled: " + _failureReason;
                _primary.Text = TryAgainButton;
                _secondary.Text = CloseButton;
                SetButtonsEnabled(enabled: true);
                SetBusy(visible: false);
                break;
        }

        ControlBox = state != RivalState.Busy; // the close box stays dead while the rival writes
    }

    private void SetButtonsEnabled(bool enabled)
    {
        _primary.Enabled = enabled;
        _secondary.Enabled = enabled;
    }

    private void SetBusy(bool visible)
    {
        _busyTrack.Visible = visible;
        _busyTrack.Marquee = visible;
    }

    private enum RivalState
    {
        Offer,

        Busy,

        Success,

        Failure,
    }
}
