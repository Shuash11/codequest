// The AI quiz creation screen.

using CodeQuest.Data.Repositories;
using CodeQuest.Models;
using CodeQuest.Services;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CodeQuest.UI;

public sealed class GenerateQuizForm : ModulePanel
{
    private const string GenerateButton = "Generate";
    private const string ConnectionNote = "Needs an internet connection. Generating usually takes up to a minute.";
    private const string GeneratingStatus = "Generating quiz\u2026";
    private const string CancelButton = "Cancel";
    private const string CancelledLateMessage = "A generation finished after you cancelled. It was not saved.";
    private const string ReadyMessage = "Quiz generated and ready to play!";
    private const string TakeNowButton = "Take it now";
    private const string BannerTitle = "Quiz generation is unavailable right now.";
    private const string NoProviderMessage = "No AI provider is configured.";
    private const string AuthMessage = "The AI provider rejected the request. Check the provider configuration.";
    private const string QuotaMessage = "The AI provider reached its request limit. Try again later.";
    private const string NetworkMessage = "Couldn't reach the AI provider. Check the internet connection.";
    private const string HttpMessage = "The AI provider returned an error. Try again later.";
    private const string BadOutputMessage = "The AI provider returned an unusable response. Try again.";
    private const string GenericFailureMessage = "Quiz generation failed. Try again.";
    private const string SaveFailedMessage = "The quiz could not be saved. Please try again.";
    private const string OfflinePointer = "You can still take any quizzes already saved on this computer.";
    private const string NoTopicsTitle = "Create a topic first.";
    private const string GoToTopicsButton = "Go to topics";
    private const string TryAgainButton = "Try again";
    private const string BrowseQuizzesButton = "Browse quizzes";

    private readonly QuizRepository _quizzes;
    private readonly TopicRepository _topics;
    private readonly QuizGenerationService _generation;
    private readonly UserAccount _user;

    private readonly ThemedComboBox _topicFilter = null!;
    private readonly SegmentedControl _difficulty = null!;
    private readonly ThemedComboBox _count = null!;
    private readonly CardPanel _optionsCard = null!;
    private readonly FlatButton _generate = null!;
    private readonly FlatButton _takeNow = null!;
    private readonly Panel _busyBlock = null!;
    private readonly ProgressTrack _busyTrack = null!;
    private readonly Label _busyStatus = null!;
    private readonly Panel _banner = null!;
    private readonly Label _bannerDetail = null!;
    private readonly Panel _emptyTopics = null!;
    private readonly Label _topicLabel = null!;
    private readonly Label _difficultyLabel = null!;
    private readonly Label _countLabel = null!;
    private readonly Panel _actionCard = null!;
    private readonly System.Windows.Forms.Timer _elapsedTimer = new() { Interval = 1000 };

    private List<Topic> _topicsCache = new();
    private bool _busy;
    private bool _cancelPending;
    private int _generationId;
    private DateTimeOffset _startedAt;
    private int _savedQuizId;

    public GenerateQuizForm(
        QuizRepository quizzes,
        TopicRepository topics,
        QuizGenerationService generation,
        UserAccount user)
    {
        ArgumentNullException.ThrowIfNull(quizzes);
        ArgumentNullException.ThrowIfNull(topics);
        ArgumentNullException.ThrowIfNull(generation);
        ArgumentNullException.ThrowIfNull(user);
        _quizzes = quizzes;
        _topics = topics;
        _generation = generation;
        _user = user;

        AutoScroll = true;
        Dock = DockStyle.Fill;
        BackColor = UITheme.Background;

        int cardWidth = UITheme.Scaled(this, 560);

        _optionsCard = new CardPanel
        {
            Name = "GenerateOptionsCard",
            Location = new Point(0, 0),
            Size = new Size(cardWidth, 0),
        };

        _banner = new Panel
        {
            Name = "GenerateFailureBanner",
            Size = new Size(cardWidth - UITheme.Scaled(this, UITheme.Spacing32), UITheme.Scaled(this, 110)),
            BackColor = UITheme.WarningTint,
            Visible = false,
        };
        _banner.Paint += static (sender, e) =>
        {
            if (sender is Panel panel)
            {
                using var pen = new Pen(UITheme.Warning);
                using GraphicsPath path = UITheme.RoundedPath(
                    new Rectangle(0, 0, panel.Width - 1, panel.Height - 1), UITheme.Scaled(panel, UITheme.RadiusControl));
                e.Graphics.DrawPath(pen, path);
            }
        };
        var bannerTitle = new Label
        {
            Name = "GenerateBannerTitle",
            Text = BannerTitle,
            Font = UITheme.BodySemibold,
            ForeColor = UITheme.WarningText,
            BackColor = UITheme.WarningTint,
            AutoSize = false,
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing12), UITheme.Scaled(this, UITheme.Spacing12)),
            Size = new Size(_banner.Width - UITheme.Scaled(this, UITheme.Spacing24), UITheme.Scaled(this, 20)),
        };
        _bannerDetail = new Label
        {
            Name = "GenerateBannerDetail",
            Font = UITheme.Body,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.WarningTint,
            AutoSize = false,
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing12), UITheme.Scaled(this, UITheme.Spacing12) + UITheme.Scaled(this, 24)),
            Size = new Size(_banner.Width - UITheme.Scaled(this, UITheme.Spacing24), UITheme.Scaled(this, 20)),
        };
        var tryAgain = new FlatButton
        {
            Name = "GenerateTryAgain",
            Text = TryAgainButton,
            Variant = UITheme.FlatButtonVariant.Secondary,
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing12), _banner.Height - UITheme.Scaled(this, 44)),
            Size = new Size(UITheme.Scaled(this, 100), UITheme.Scaled(this, 30)),
        };
        tryAgain.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<GenerateQuizForm>() is { } form)
            {
                form.HideBanner();
            }
        };
        var browseQuizzes = new FlatButton
        {
            Name = "GenerateBrowseQuizzes",
            Text = BrowseQuizzesButton,
            Variant = UITheme.FlatButtonVariant.Secondary,
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing12) + UITheme.Scaled(this, 110), _banner.Height - UITheme.Scaled(this, 44)),
            Size = new Size(UITheme.Scaled(this, 140), UITheme.Scaled(this, 30)),
        };
        browseQuizzes.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<GenerateQuizForm>() is { } form)
            {
                form.NavigateHome();
            }
        };
        _banner.Controls.Add(bannerTitle);
        _banner.Controls.Add(_bannerDetail);
        _banner.Controls.Add(tryAgain);
        _banner.Controls.Add(browseQuizzes);

        _topicLabel = new Label
        {
            Name = "GenerateTopicLabel",
            Text = "Topic",
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Size = new Size(UITheme.Scaled(this, 200), UITheme.Scaled(this, 18)),
        };
        _topicFilter = new ThemedComboBox
        {
            Name = "GenerateTopicPicker",
            Size = new Size(UITheme.Scaled(this, 300), UITheme.Scaled(this, 32)),
            DropDownStyle = ComboBoxStyle.DropDownList,
        };

        _difficultyLabel = new Label
        {
            Name = "GenerateDifficultyLabel",
            Text = "Difficulty",
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Size = new Size(UITheme.Scaled(this, 200), UITheme.Scaled(this, 18)),
        };
        _difficulty = new SegmentedControl
        {
            Name = "GenerateDifficultyPicker",
            Size = new Size(UITheme.Scaled(this, 300), UITheme.Scaled(this, 32)),
            LabeledBy = _difficultyLabel,
        };
        _difficulty.Items = new[] { Difficulty.Easy.ToString(), Difficulty.Medium.ToString(), Difficulty.Hard.ToString() };
        _difficulty.SelectedIndex = 0;

        _countLabel = new Label
        {
            Name = "GenerateCountLabel",
            Text = "Questions",
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Size = new Size(UITheme.Scaled(this, 200), UITheme.Scaled(this, 18)),
        };
        _count = new ThemedComboBox
        {
            Name = "GenerateCountPicker",
            Size = new Size(UITheme.Scaled(this, 120), UITheme.Scaled(this, 32)),
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        for (int count = 5; count <= 20; count++)
        {
            _count.Items.Add(count.ToString());
        }
        _count.SelectedIndex = 0;

        _actionCard = new CardPanel
        {
            Name = "GenerateActionCard",
            Size = new Size(cardWidth, UITheme.Scaled(this, 120)),
        };
        _generate = new FlatButton
        {
            Name = "GenerateButton",
            Text = GenerateButton,
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, UITheme.Spacing16)),
            Size = new Size(UITheme.Scaled(this, 160), UITheme.Scaled(this, 32)),
        };
        _generate.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<GenerateQuizForm>() is { } form)
            {
                form.StartGeneration();
            }
        };
        var note = new Label
        {
            Name = "GenerateNote",
            Text = ConnectionNote,
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, UITheme.Spacing16) + UITheme.Scaled(this, 40)),
            Size = new Size(cardWidth - UITheme.Scaled(this, UITheme.Spacing32), UITheme.Scaled(this, 18)),
        };
        _takeNow = new FlatButton
        {
            Name = "GenerateTakeNow",
            Text = TakeNowButton,
            Variant = UITheme.FlatButtonVariant.Secondary,
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing16) + UITheme.Scaled(this, 170), UITheme.Scaled(this, UITheme.Spacing16)),
            Size = new Size(UITheme.Scaled(this, 120), UITheme.Scaled(this, 32)),
            Visible = false,
        };
        _takeNow.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<GenerateQuizForm>() is { } form)
            {
                form.OpenTakeNow();
            }
        };
        _actionCard.Controls.Add(_generate);
        _actionCard.Controls.Add(_takeNow);
        _actionCard.Controls.Add(note);

        _busyBlock = new Panel
        {
            Name = "GenerateBusyBlock",
            Size = new Size(cardWidth, UITheme.Scaled(this, 120)),
            BackColor = UITheme.Surface,
            Visible = false,
        };
        _busyTrack = new ProgressTrack
        {
            Name = "GenerateBusyTrack",
            Size = new Size(cardWidth - UITheme.Scaled(this, UITheme.Spacing32), UITheme.Scaled(this, 8)),
        };
        _busyStatus = new Label
        {
            Name = "GenerateBusyStatus",
            Text = GeneratingStatus,
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, UITheme.Spacing16) + UITheme.Scaled(this, 16)),
            Size = new Size(UITheme.Scaled(this, 300), UITheme.Scaled(this, 18)),
        };
        var cancelButton = new FlatButton
        {
            Name = "GenerateCancel",
            Text = CancelButton,
            Variant = UITheme.FlatButtonVariant.Secondary,
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, UITheme.Spacing16) + UITheme.Scaled(this, 44)),
            Size = new Size(UITheme.Scaled(this, 100), UITheme.Scaled(this, 30)),
        };
        cancelButton.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<GenerateQuizForm>() is { } form)
            {
                form.CancelGeneration();
            }
        };
        _busyBlock.Controls.Add(_busyTrack);
        _busyBlock.Controls.Add(_busyStatus);
        _busyBlock.Controls.Add(cancelButton);

        _emptyTopics = new Panel
        {
            Name = "GenerateNoTopics",
            Dock = DockStyle.Fill,
            Visible = false,
            BackColor = UITheme.Background,
        };
        var emptyCard = new CardPanel
        {
            Name = "GenerateNoTopicsCard",
            Size = new Size(UITheme.Scaled(this, 440), UITheme.Scaled(this, 130)),
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
        };
        var emptyTitle = new Label
        {
            Name = "GenerateNoTopicsTitle",
            Text = NoTopicsTitle,
            Font = UITheme.BodySemibold,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, UITheme.Spacing16)),
            Size = new Size(UITheme.Scaled(this, 408), UITheme.Scaled(this, 22)),
        };
        var goToTopics = new FlatButton
        {
            Name = "GenerateGoToTopics",
            Text = GoToTopicsButton,
            Variant = UITheme.FlatButtonVariant.Secondary,
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, UITheme.Spacing16) + UITheme.Scaled(this, 30)),
            Size = new Size(UITheme.Scaled(this, 130), UITheme.Scaled(this, 32)),
        };
        goToTopics.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<GenerateQuizForm>() is { } form)
            {
                form.NavigateToTopics();
            }
        };
        emptyCard.Controls.Add(emptyTitle);
        emptyCard.Controls.Add(goToTopics);
        _emptyTopics.Controls.Add(emptyCard);

        _optionsCard.Controls.Add(_banner);
        _optionsCard.Controls.Add(_topicLabel);
        _optionsCard.Controls.Add(_topicFilter);
        _optionsCard.Controls.Add(_difficultyLabel);
        _optionsCard.Controls.Add(_difficulty);
        _optionsCard.Controls.Add(_countLabel);
        _optionsCard.Controls.Add(_count);

        _elapsedTimer.Tick += static (sender, e) =>
        {
            if (sender is System.Windows.Forms.Timer timer && timer.Tag is GenerateQuizForm form)
            {
                double seconds = (DateTimeOffset.UtcNow - form._startedAt).TotalSeconds;
                form._busyStatus.Text = $"{GeneratingStatus} {seconds:0} s";
            }
        };
        _elapsedTimer.Tag = this;

        _optionsCard.Resize += static (sender, e) =>
        {
            if (sender is CardPanel card && card.FindAncestor<GenerateQuizForm>() is { } form)
            {
                form.LayoutGenerateLayout();
            }
        };

        Controls.Add(_busyBlock);
        Controls.Add(_actionCard);
        Controls.Add(_optionsCard);
        Controls.Add(_emptyTopics);
        LayoutGenerateLayout();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _elapsedTimer.Stop();
            _elapsedTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void LayoutGenerateLayout()
    {
        int inset = UITheme.Scaled(this, UITheme.Spacing16);
        int y = inset;

        if (_banner.Visible)
        {
            _banner.Location = new Point(inset, y);
            y += _banner.Height + UITheme.Scaled(this, UITheme.Spacing12);
        }

        _topicLabel.Location = new Point(inset, y);
        y += UITheme.Scaled(this, 18) + UITheme.Scaled(this, UITheme.Spacing4);
        _topicFilter.Location = new Point(inset, y);
        y += UITheme.Scaled(this, 32) + UITheme.Scaled(this, UITheme.Spacing16);

        _difficultyLabel.Location = new Point(inset, y);
        y += UITheme.Scaled(this, 18) + UITheme.Scaled(this, UITheme.Spacing4);
        _difficulty.Location = new Point(inset, y);
        y += UITheme.Scaled(this, 32) + UITheme.Scaled(this, UITheme.Spacing16);

        _countLabel.Location = new Point(inset, y);
        y += UITheme.Scaled(this, 18) + UITheme.Scaled(this, UITheme.Spacing4);
        _count.Location = new Point(inset, y);

        int contentBottom = Math.Max(_count.Bottom, _banner.Visible ? _banner.Bottom : 0);
        _optionsCard.Height = contentBottom + inset;

        int blockTop = _optionsCard.Bottom + UITheme.Scaled(this, UITheme.Spacing16);
        _actionCard.Location = new Point(0, blockTop);
        _busyBlock.Location = new Point(0, blockTop);
    }

    public override void RefreshData()
    {
        _topicsCache = _topics.GetAll().OrderBy(topic => topic.Name, StringComparer.Ordinal).ToList();

        _topicFilter.Items.Clear();
        foreach (Topic topic in _topicsCache)
        {
            _topicFilter.Items.Add(topic.Name);
        }
        _topicFilter.SelectedIndex = _topicsCache.Count > 0 ? 0 : -1;

        bool hasTopics = _topicsCache.Count > 0;
        _optionsCard.Visible = hasTopics;
        _busyBlock.Visible = false;
        _emptyTopics.Visible = !hasTopics;
        if (!hasTopics)
        {
            foreach (Control child in Controls)
            {
                child.Visible = child == _emptyTopics;
            }
        }
        else
        {
            foreach (Control child in Controls)
            {
                child.Visible = child != _emptyTopics;
            }

            _busyBlock.Visible = false;
        }
    }

    private void StartGeneration()
    {
        if (_busy || _topicFilter.SelectedIndex < 0 || _topicFilter.SelectedIndex >= _topicsCache.Count)
        {
            return;
        }

        Topic topic = _topicsCache[_topicFilter.SelectedIndex];
        var difficulty = _difficulty.SelectedIndex switch
        {
            1 => Difficulty.Medium,
            2 => Difficulty.Hard,
            _ => Difficulty.Easy,
        };
        int questionCount = 5 + Math.Max(_count.SelectedIndex, 0);

        _busy = true;
        _cancelPending = false;
        _generationId++;
        int generationId = _generationId;
        _startedAt = DateTimeOffset.UtcNow;
        _savedQuizId = 0;

        SetFormEnabled(enabled: false);
        HideBanner();
        _busyStatus.Text = GeneratingStatus;
        _busyBlock.Visible = true;
        _busyTrack.Marquee = true;
        _busyBlock.BringToFront();
        _elapsedTimer.Start();
        this.FindAncestor<MainForm>()?.SetSidebarEnabled(enabled: false);
        Refresh();

        Task.Run(() => _generation.GenerateQuiz(topic, difficulty, questionCount))
            .ContinueWith(
                task => HandleOutcome(topic, task.Result, generationId),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void HandleOutcome(Topic topic, QuizGenerationOutcome outcome, int generationId)
    {
        if (generationId != _generationId)
        {
            if (_cancelPending && !_busy)
            {
                _cancelPending = false;
                ReportStatus(CancelledLateMessage, success: false);
            }

            return;
        }

        _elapsedTimer.Stop();

        if (!outcome.IsSuccess || outcome.QuizDraft is null)
        {
            ShowBanner(outcome.ProviderReasons);
            EndBusy(readyOnly: true);
            return;
        }

        Quiz draft = outcome.QuizDraft;
        draft.TopicId = topic.TopicId;
        string baseTitle = draft.Title;
        int suffix = 2;
        while (_quizzes.ExistsByTitle(topic.TopicId, draft.Title))
        {
            draft.Title = $"{baseTitle} ({suffix})";
            suffix++;
        }

        try
        {
            _savedQuizId = _quizzes.Add(draft);
        }
        catch (Exception)
        {
            EndBusy(readyOnly: true);
            ReportStatus(SaveFailedMessage, success: false);
            return;
        }

        EndBusy(readyOnly: false);
        ReportStatus(ReadyMessage, success: true);
        _takeNow.Visible = true;
    }

    private void CancelGeneration()
    {
        if (!_busy)
        {
            return;
        }

        _cancelPending = true;
        _generationId++;
        EndBusy(readyOnly: true);
    }

    private void EndBusy(bool readyOnly)
    {
        _busy = false;
        _busyTrack.Marquee = false;
        _busyBlock.Visible = false;
        _elapsedTimer.Stop();
        SetFormEnabled(enabled: true);
        this.FindAncestor<MainForm>()?.SetSidebarEnabled(enabled: true);

        if (readyOnly)
        {
            _takeNow.Visible = false;
        }
    }

    private void SetFormEnabled(bool enabled)
    {
        _topicFilter.Enabled = enabled;
        _difficulty.Enabled = enabled;
        _count.Enabled = enabled;
        _generate.Enabled = enabled;
        _takeNow.Enabled = enabled;
    }

    private void ShowBanner(IReadOnlyList<string> reasons)
    {
        _bannerDetail.Text = DescribeFailure(reasons);
        _banner.Visible = true;
        LayoutGenerateLayout();
        _banner.BringToFront();
    }

    private void HideBanner()
    {
        _banner.Visible = false;
        LayoutGenerateLayout();
    }

    private static string DescribeFailure(IReadOnlyList<string> reasons)
    {
        string first = reasons.Count > 0 ? reasons[0] : string.Empty;
        string detail = first.Contains("empty-provider-list", StringComparison.Ordinal) ? NoProviderMessage
            : first.Contains(":not-configured", StringComparison.Ordinal) ? NoProviderMessage
            : first.Contains(":auth-failed", StringComparison.Ordinal) ? AuthMessage
            : first.Contains(":quota", StringComparison.Ordinal) ? QuotaMessage
            : first.Contains(":timeout", StringComparison.Ordinal) ? NetworkMessage
            : first.Contains(":http-error", StringComparison.Ordinal) ? HttpMessage
            : first.Contains(":bad-output", StringComparison.Ordinal) ? BadOutputMessage
            : GenericFailureMessage;

        return $"{detail} {OfflinePointer}";
    }

    private void OpenTakeNow()
    {
        if (_savedQuizId == 0)
        {
            return;
        }

        this.FindAncestor<MainForm>()?.NavigateToNewQuiz(_savedQuizId);
    }

    private void NavigateHome()
    {
        this.FindAncestor<MainForm>()?.NavigateToModule("TakeQuiz");
    }

    private void NavigateToTopics()
    {
        this.FindAncestor<MainForm>()?.NavigateToModule("Topics");
    }
}
