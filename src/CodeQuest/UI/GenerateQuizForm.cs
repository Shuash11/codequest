// =============================================================================
// CodeQuest — AI Quiz Generation (I4): the generation screen of spec 5.11.
// Ready state: options card — Topic (§4.5 combo over every existing topic),
// Difficulty (§4.9 segmented Easy/Medium/Hard), Questions (the §4.12
// sanctioned ComboBox fallback prefilled 5–20) — and the action card with the
// "Generate" primary plus the quiet connection note.
// Busy state (the binding R-5 #3 obligation): the generation call runs OFF
// the UI thread (it can block ~2 minutes across the timeout tiers), so the
// §4.13 marquee actually animates, an elapsed-seconds counter ticks on the UI
// timer, the options and the sidebar disable, and Cancel returns immediately —
// a late result after a cancel is discarded, never half-saved. No percentage,
// no fake progress (§4.13: activity, not percent).
// Typed failure (the banner): every contract error class maps to one plain-
// language line (never a raw reason string), plus the offline pointer — the
// app never crashes and never half-saves a quiz (P13 acceptance).
// Success: the quiz saves as Draft (nothing auto-approves, R-2 #4), the title
// is de-duplicated against the topic's existing quizzes (the R-5 #1 insert-
// time rule), the strip announces it, and "Review now" deep-links to §5.12.
// The live path needs configured provider keys (config/config.local.json);
// without them the chain ends in the typed not-configured failure — which is
// exactly the offline path this screen is designed to show.
// =============================================================================

using CodeQuest.Data.Repositories;
using CodeQuest.Models;
using CodeQuest.Services;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CodeQuest.UI;

/// <summary>
/// The AI generation module (spec 5.11): options, busy block, typed-failure
/// banner, and the draft hand-off to the review screen.
/// </summary>
public sealed class GenerateQuizForm : ModulePanel
{
    private const string GenerateButton = "Generate";
    private const string ConnectionNote = "Needs an internet connection. Generating usually takes up to a minute.";
    private const string GeneratingStatus = "Generating quiz\u2026";
    private const string CancelButton = "Cancel";
    private const string CancelledLateMessage = "A generation finished after you cancelled. It was not saved.";
    private const string SavedMessage = "Quiz generated and saved as a draft.";
    private const string ReviewNowButton = "Review now";
    private const string BannerTitle = "Quiz generation is unavailable right now.";
    private const string NoProviderMessage = "No AI provider is configured.";
    private const string AuthMessage = "The AI provider rejected the request. Check the provider configuration.";
    private const string QuotaMessage = "The AI provider reached its request limit. Try again later.";
    private const string NetworkMessage = "Couldn't reach the AI provider. Check the internet connection.";
    private const string HttpMessage = "The AI provider returned an error. Try again later.";
    private const string BadOutputMessage = "The AI provider returned an unusable response. Try again.";
    private const string GenericFailureMessage = "Quiz generation failed. Try again.";
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
    private readonly FlatButton _reviewNow = null!;
    private readonly Panel _busyBlock = null!;
    private readonly ProgressTrack _busyTrack = null!;
    private readonly Label _busyStatus = null!;
    private readonly Panel _banner = null!;
    private readonly Label _bannerDetail = null!;
    private readonly Panel _emptyTopics = null!;
    private readonly System.Windows.Forms.Timer _elapsedTimer = new() { Interval = 1000 };

    private List<Topic> _topicsCache = new();
    private bool _busy; // the in-flight generation flag (also the re-entrancy guard)
    private bool _cancelled; // the user cancelled; the late result is discarded
    private DateTimeOffset _startedAt;
    private int _savedDraftQuizId; // the last saved draft (the "Review now" hand-off)

    /// <summary>Creates the generation module over the configured provider chain.</summary>
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
            Size = new Size(cardWidth, UITheme.Scaled(this, 260)),
        };

        // The typed-failure banner (§5.11): WarningTint, 8px radius, 12px
        // padding; hidden until a generation fails.
        _banner = new Panel
        {
            Name = "GenerateFailureBanner",
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, UITheme.Spacing16)),
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

        // The option rows.
        var topicLabel = new Label
        {
            Name = "GenerateTopicLabel",
            Text = "Topic",
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, UITheme.Spacing16)),
            Size = new Size(UITheme.Scaled(this, 200), UITheme.Scaled(this, 18)),
        };
        _topicFilter = new ThemedComboBox
        {
            Name = "GenerateTopicPicker",
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, UITheme.Spacing16) + UITheme.Scaled(this, 22)),
            Size = new Size(UITheme.Scaled(this, 300), UITheme.Scaled(this, 32)),
            DropDownStyle = ComboBoxStyle.DropDownList,
        };

        var difficultyLabel = new Label
        {
            Name = "GenerateDifficultyLabel",
            Text = "Difficulty",
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, 88)),
            Size = new Size(UITheme.Scaled(this, 200), UITheme.Scaled(this, 18)),
        };
        _difficulty = new SegmentedControl
        {
            Name = "GenerateDifficultyPicker",
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, 88) + UITheme.Scaled(this, 22)),
            Size = new Size(UITheme.Scaled(this, 300), UITheme.Scaled(this, 32)),
            LabeledBy = difficultyLabel, // §4.9: the visible label is the accessible name's source
        };
        _difficulty.Items = new[] { Difficulty.Easy.ToString(), Difficulty.Medium.ToString(), Difficulty.Hard.ToString() };
        _difficulty.SelectedIndex = 0;

        var countLabel = new Label
        {
            Name = "GenerateCountLabel",
            Text = "Questions",
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, 160)),
            Size = new Size(UITheme.Scaled(this, 200), UITheme.Scaled(this, 18)),
        };
        _count = new ThemedComboBox
        {
            Name = "GenerateCountPicker",
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, 160) + UITheme.Scaled(this, 22)),
            Size = new Size(UITheme.Scaled(this, 120), UITheme.Scaled(this, 32)),
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        for (int count = 5; count <= 20; count++)
        {
            _count.Items.Add(count.ToString()); // §4.12's sanctioned fallback: a combo prefilled 5–20
        }
        _count.SelectedIndex = 0;

        // The action card.
        var actionCard = new CardPanel
        {
            Name = "GenerateActionCard",
            Location = new Point(0, _optionsCard.Bottom + UITheme.Scaled(this, UITheme.Spacing16)),
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
        _reviewNow = new FlatButton
        {
            Name = "GenerateReviewNow",
            Text = ReviewNowButton,
            Variant = UITheme.FlatButtonVariant.Secondary,
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing16) + UITheme.Scaled(this, 170), UITheme.Scaled(this, UITheme.Spacing16)),
            Size = new Size(UITheme.Scaled(this, 120), UITheme.Scaled(this, 32)),
            Visible = false,
        };
        _reviewNow.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<GenerateQuizForm>() is { } form)
            {
                form.OpenReviewNow();
            }
        };
        actionCard.Controls.Add(_generate);
        actionCard.Controls.Add(_reviewNow);
        actionCard.Controls.Add(note);

        // The busy block (§5.11 busy state): marquee + status + Cancel.
        _busyBlock = new Panel
        {
            Name = "GenerateBusyBlock",
            Location = new Point(0, _optionsCard.Bottom + UITheme.Scaled(this, UITheme.Spacing16)),
            Size = new Size(cardWidth, UITheme.Scaled(this, 120)),
            BackColor = UITheme.Surface,
            Visible = false,
        };
        _busyTrack = new ProgressTrack
        {
            Name = "GenerateBusyTrack",
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, UITheme.Spacing16)),
            Size = new Size(cardWidth - UITheme.Scaled(this, UITheme.Spacing32), UITheme.Scaled(this, 8)),
            Marquee = true,
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

        // The no-topics empty state (§5.11).
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
        _optionsCard.Controls.Add(topicLabel);
        _optionsCard.Controls.Add(_topicFilter);
        _optionsCard.Controls.Add(difficultyLabel);
        _optionsCard.Controls.Add(_difficulty);
        _optionsCard.Controls.Add(countLabel);
        _optionsCard.Controls.Add(_count);

        // The elapsed-seconds counter ticks on the UI timer (§5.11 busy state).
        _elapsedTimer.Tick += static (sender, e) =>
        {
            if (sender is System.Windows.Forms.Timer timer && timer.Tag is GenerateQuizForm form)
            {
                double seconds = (DateTimeOffset.UtcNow - form._startedAt).TotalSeconds;
                form._busyStatus.Text = $"{GeneratingStatus} {seconds:0} s";
            }
        };
        _elapsedTimer.Tag = this;

        Controls.Add(_busyBlock);
        Controls.Add(actionCard);
        Controls.Add(_optionsCard);
        Controls.Add(_emptyTopics);
    }

    /// <inheritdoc />
    public override void RefreshData()
    {
        _topicsCache = _topics.GetAll().OrderBy(topic => topic.Name, StringComparer.Ordinal).ToList();

        _topicFilter.Items.Clear();
        foreach (Topic topic in _topicsCache)
        {
            _topicFilter.Items.Add(topic.Name);
        }
        _topicFilter.SelectedIndex = _topicsCache.Count > 0 ? 0 : -1;

        // The §5.11 empty state: without a topic there is nothing to generate for.
        bool hasTopics = _topicsCache.Count > 0;
        _optionsCard.Visible = hasTopics;
        _busyBlock.Visible = false;
        _emptyTopics.Visible = !hasTopics;
        if (!hasTopics)
        {
            // The empty state owns the module: hide the action card behind it.
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

    /// <summary>
    /// Starts one generation: reads the pickers, disables the form (and the
    /// sidebar, §5.11), and runs the blocking chain call off the UI thread.
    /// The continuation lands back on the UI thread with the typed outcome.
    /// </summary>
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
        int questionCount = 5 + Math.Max(_count.SelectedIndex, 0); // combo index 0..15 → 5..20

        _busy = true;
        _cancelled = false;
        _startedAt = DateTimeOffset.UtcNow;
        _savedDraftQuizId = 0;

        SetFormEnabled(enabled: false);
        HideBanner();
        _busyStatus.Text = GeneratingStatus;
        _busyBlock.Visible = true;
        _busyBlock.BringToFront();
        _elapsedTimer.Start();
        FindAncestor<MainForm>()?.SetSidebarEnabled(enabled: false);
        Refresh(); // paint the busy state before the (off-thread) call starts

        // The P5 chain call is synchronous and can block ~2 minutes — it runs
        // off the UI thread so the marquee animates (the binding R-5 #3 rule).
        Task.Run(() => _generation.GenerateQuiz(topic, difficulty, questionCount))
            .ContinueWith(
                task => HandleOutcome(topic, task.Result),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>The UI-thread continuation: save, or show the typed failure banner.</summary>
    private void HandleOutcome(Topic topic, QuizGenerationOutcome outcome)
    {
        _elapsedTimer.Stop();

        if (_cancelled)
        {
            // The §5.11 cancellation rule: the late result is discarded, the
            // strip notes it, and nothing is saved.
            EndBusy(readyOnly: true);
            ReportStatus(CancelledLateMessage, success: false);
            return;
        }

        if (!outcome.IsSuccess || outcome.QuizDraft is null)
        {
            ShowBanner(outcome.ProviderReasons);
            EndBusy(readyOnly: true);
            return;
        }

        // The R-5 #1 insert-time title de-dup (P13's persisting-phase duty):
        // the AI's title is unique per topic; a collision gets a numbered suffix.
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
            _savedDraftQuizId = _quizzes.Add(draft); // one atomic write — never a half-saved quiz
        }
        catch (Exception)
        {
            // A persistence failure is not a generation decision: it travels
            // to the §4.11 unexpected dialog (Program), with the busy state
            // cleanly ended first.
            EndBusy(readyOnly: true);
            throw;
        }

        EndBusy(readyOnly: false);
        ReportStatus(SavedMessage, success: true);
        _reviewNow.Visible = true; // the §5.11 "Review now" deep-link
    }

    /// <summary>
    /// The §5.11 Cancel: the form returns to its ready state immediately; the
    /// in-flight call cannot be aborted through the P5 client, so the late
    /// result is discarded (nothing is saved).
    /// </summary>
    private void CancelGeneration()
    {
        if (!_busy)
        {
            return;
        }

        _cancelled = true;
        EndBusy(readyOnly: true);
    }

    /// <summary>Restores the ready state (and the sidebar) after the busy state.</summary>
    private void EndBusy(bool readyOnly)
    {
        _busy = false;
        _busyTrack.Marquee = false;
        _busyBlock.Visible = false;
        _elapsedTimer.Stop();
        SetFormEnabled(enabled: true);
        FindAncestor<MainForm>()?.SetSidebarEnabled(enabled: true);

        if (readyOnly)
        {
            _reviewNow.Visible = false;
        }
    }

    /// <summary>Disables or re-enables every option control and the Generate button (§5.11).</summary>
    private void SetFormEnabled(bool enabled)
    {
        _topicFilter.Enabled = enabled;
        _difficulty.Enabled = enabled;
        _count.Enabled = enabled;
        _generate.Enabled = enabled;
        _reviewNow.Enabled = enabled;
    }

    /// <summary>Shows the typed-failure banner with the plain-language detail (§5.11).</summary>
    private void ShowBanner(IReadOnlyList<string> reasons)
    {
        _bannerDetail.Text = DescribeFailure(reasons);
        _banner.Visible = true;
        _banner.BringToFront();
    }

    /// <summary>Hides the banner ("Try again").</summary>
    private void HideBanner()
    {
        _banner.Visible = false;
    }

    /// <summary>
    /// Maps the typed reason strings to the §5.11 plain-language lines — never
    /// a raw reason, never a provider detail that could leak configuration.
    /// </summary>
    private static string DescribeFailure(IReadOnlyList<string> reasons)
    {
        string first = reasons.Count > 0 ? reasons[0] : string.Empty;
        string detail = first.Contains(":not-configured", StringComparison.Ordinal) ? NoProviderMessage
            : first.Contains(":auth-failed", StringComparison.Ordinal) ? AuthMessage
            : first.Contains(":quota", StringComparison.Ordinal) ? QuotaMessage
            : first.Contains(":timeout", StringComparison.Ordinal) ? NetworkMessage
            : first.Contains(":http-error", StringComparison.Ordinal) ? HttpMessage
            : first.Contains(":bad-output", StringComparison.Ordinal) ? BadOutputMessage
            : GenericFailureMessage;

        return $"{detail} {OfflinePointer}";
    }

    /// <summary>The "Review now" deep-link: the Review module with the new draft open.</summary>
    private void OpenReviewNow()
    {
        if (_savedDraftQuizId == 0)
        {
            return;
        }

        FindAncestor<MainForm>()?.NavigateToReview(_savedDraftQuizId);
    }

    /// <summary>The failure banner's "Browse quizzes": the take-quiz list (the offline path).</summary>
    private void NavigateHome()
    {
        FindAncestor<MainForm>()?.NavigateToModule("TakeQuiz");
    }

    /// <summary>The empty state's "Go to topics".</summary>
    private void NavigateToTopics()
    {
        FindAncestor<MainForm>()?.NavigateToModule("Topics");
    }
}
