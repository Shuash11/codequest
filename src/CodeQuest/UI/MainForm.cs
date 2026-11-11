// The main window with the sidebar. (Inheritance)

using CodeQuest.Data;
using CodeQuest.Data.Repositories;
using CodeQuest.Models;
using CodeQuest.Services;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CodeQuest.UI;

public sealed class MainForm : Form
{
    private static readonly (string Key, string Title, string Glyph, string PhaseNote)[] Modules =
    {
        ("TakeQuiz", "Take quiz", "\uE80F", ""),
        ("Scoreboard", "Scoreboard", "\uE8BC", ""),
        ("Achievements", "Achievements", "\uE735", ""),
        ("History", "History", "\uE823", ""),
        ("Reports", "Reports", "\uE9D9", ""),
        ("GenerateQuiz", "Generate quiz", "\uE710", ""),
        ("Topics", "Topics", "\uE8EC", ""),
    };

    private const int StatusAutoClearMilliseconds = 4000;

    private readonly UserAccount _user;
    private readonly AppConfig _config;
    private Label _contentHeader = null!;
    private Label _statusStrip = null!;
    private Panel _moduleHost = null!;
    private readonly System.Windows.Forms.Timer _statusClearTimer = new() { Interval = StatusAutoClearMilliseconds };
    private readonly Dictionary<string, NavItemButton> _navItems = new();
    private NavItemButton _logoutButton = null!;
    private LevelChip _sidebarLevelChip = null!;
    private ProgressTrack _sidebarXpBar = null!;
    private readonly IGamificationRepository _gamification;
    private readonly Dictionary<string, Control> _modulePanels = new();
    private string _activeModule = string.Empty;

    public MainForm(UserAccount user, Db database, AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(config);
        _user = user;
        _config = config;

        var topics = new TopicRepository(database);
        var quizzes = new QuizRepository(database);
        var questions = new QuestionRepository(database);
        var choices = new ChoiceRepository(database);
        var attempts = new AttemptRepository(database);
        var answers = new SubmittedAnswerRepository(database);

        var gamification = new GamificationRepository(database);
        IAchievementEvaluator achievementEvaluator = new AchievementEvaluator();
        _gamification = gamification;

        Text = "CodeQuest";
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = UITheme.Background;
        ClientSize = new Size(UITheme.Scaled(this, 1024), UITheme.Scaled(this, 640));
        MinimumSize = new Size(UITheme.Scaled(this, 1024), UITheme.Scaled(this, 640));

        _statusClearTimer.Tick += static (sender, e) =>
        {
            if (sender is System.Windows.Forms.Timer timer)
            {
                timer.Stop();
                if (timer.Tag is MainForm shell)
                {
                    shell._statusStrip.Text = string.Empty;
                }
            }
        };
        _statusClearTimer.Tag = this;

        IScoringService scoring = new ScoringService();
        IReportService reports = new ReportService(attempts, quizzes, topics);
        var generation = new QuizGenerationService(_config);

        Controls.Add(BuildContentArea(topics, quizzes, questions, choices, attempts, answers, scoring, reports, generation, gamification, achievementEvaluator));
        Controls.Add(BuildSidebar());
    }

    public bool ReturnedToLogin { get; private set; }

    private Panel BuildSidebar()
    {
        int inset = UITheme.Scaled(this, UITheme.Spacing24);
        int navWidth = UITheme.Scaled(this, 220) - inset - UITheme.Scaled(this, UITheme.Spacing16);
        int titleTop = inset;
        int titleHeight = UITheme.Scaled(this, 22);
        int navTop = titleTop + titleHeight + UITheme.Scaled(this, UITheme.Spacing24);
        int rowHeight = UITheme.Scaled(this, 40);

        var sidebar = new Panel
        {
            Dock = DockStyle.Left,
            Width = UITheme.Scaled(this, 220),
            BackColor = UITheme.Background,
        };

        sidebar.Paint += static (sender, e) =>
        {
            if (sender is Panel panel)
            {
                using var pen = new Pen(UITheme.SeparatorHairline);
                e.Graphics.DrawLine(pen, panel.Width - 0.5f, 0f, panel.Width - 0.5f, panel.Height);
            }
        };

        var title = new Label
        {
            Text = "CodeQuest",
            Font = UITheme.BodySemibold,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Background,
            AutoSize = false,
            Location = new Point(inset, titleTop),
            Size = new Size(navWidth, titleHeight),
        };
        sidebar.Controls.Add(title);

        for (int i = 0; i < Modules.Length; i++)
        {
            (string key, string moduleTitle, string glyph, _) = Modules[i];
            var item = new NavItemButton
            {
                Name = "Nav" + key,
                Text = moduleTitle,
                Glyph = glyph,
                Location = new Point(inset, navTop + i * rowHeight),
                Size = new Size(navWidth, rowHeight),
            };
            item.Click += static (sender, e) =>
            {
                if (sender is NavItemButton nav && nav.Parent is Panel sidebarPanel)
                {
                    var shell = sidebarPanel.Parent as MainForm;
                    shell?.ShowModule(nav.Name["Nav".Length..]);
                }
            };

            _navItems[key] = item;
            sidebar.Controls.Add(item);
        }

        int accountTop = navTop + Modules.Length * rowHeight + UITheme.Scaled(this, UITheme.Spacing16);
        int avatarSize = UITheme.Scaled(this, 32);
        string avatarInitial = _user.DisplayName.Length == 0
            ? "?"
            : char.ToUpperInvariant(_user.DisplayName[0]).ToString();

        var avatar = new Panel
        {
            Name = "AccountAvatar",
            Size = new Size(avatarSize, avatarSize),
            Location = new Point(inset, accountTop),
            BackColor = UITheme.Background,
        };
        avatar.Paint += (sender, e) =>
        {
            if (sender is Panel panel)
            {
                using GraphicsPath avatarPath = UITheme.RoundedPath(
                    new Rectangle(0, 0, panel.Width - 1, panel.Height - 1), panel.Width / 2);
                using var brush = new SolidBrush(UITheme.Accent);
                e.Graphics.FillPath(brush, avatarPath);
                TextRenderer.DrawText(
                    e.Graphics,
                    avatarInitial,
                    UITheme.CaptionSemibold,
                    panel.ClientRectangle,
                    UITheme.TextOnAccent,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        };
        sidebar.Controls.Add(avatar);

        _sidebarLevelChip = new LevelChip
        {
            Name = "SidebarLevelChip",
        };
        _sidebarLevelChip.Location = new Point(
            inset + navWidth - _sidebarLevelChip.Width,
            accountTop + ((avatarSize - _sidebarLevelChip.Height) / 2));
        sidebar.Controls.Add(_sidebarLevelChip);

        var signedInName = new Label
        {
            Name = "SignedInName",
            Text = _user.DisplayName,
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Background,
            AutoSize = false,
            AutoEllipsis = true,
            Location = new Point(
                inset + avatarSize + UITheme.Scaled(this, UITheme.Spacing8),
                accountTop + ((avatarSize - UITheme.Scaled(this, 18)) / 2)),
            Size = new Size(
                navWidth - avatarSize - UITheme.Scaled(this, UITheme.Spacing8) - _sidebarLevelChip.Width - UITheme.Scaled(this, UITheme.Spacing8),
                UITheme.Scaled(this, 18)),
        };
        sidebar.Controls.Add(signedInName);

        _sidebarXpBar = new ProgressTrack
        {
            Name = "SidebarXpBar",
            AccessibleName = "Level progress",
        };
        _sidebarXpBar.Location = new Point(inset, accountTop + avatarSize + UITheme.Scaled(this, UITheme.Spacing8));
        _sidebarXpBar.Size = new Size(navWidth, UITheme.Scaled(this, 4));
        sidebar.Controls.Add(_sidebarXpBar);

        _logoutButton = new NavItemButton
        {
            Name = "NavLogout",
            Text = "Log out",
            SecondaryAction = true,
            Location = new Point(inset, accountTop + avatarSize + UITheme.Scaled(this, UITheme.Spacing8 + 4 + UITheme.Spacing16)),
            Size = new Size(navWidth, rowHeight),
        };
        _logoutButton.Click += static (sender, e) =>
        {
            if (sender is NavItemButton button && button.Parent is Panel sidebarPanel && sidebarPanel.Parent is MainForm shell)
            {
                if (shell._modulePanels.TryGetValue(shell._activeModule, out Control? current)
                    && current is ILeaveGuard leaving
                    && !leaving.ConfirmLeave())
                {
                    return;
                }

                shell.ReturnedToLogin = true;
                shell.Close();
            }
        };
        sidebar.Controls.Add(_logoutButton);

        return sidebar;
    }

    private Panel BuildContentArea(
        TopicRepository topics,
        QuizRepository quizzes,
        QuestionRepository questions,
        ChoiceRepository choices,
        AttemptRepository attempts,
        SubmittedAnswerRepository answers,
        IScoringService scoring,
        IReportService reports,
        QuizGenerationService generation,
        IGamificationRepository gamification,
        IAchievementEvaluator achievementEvaluator)
    {
        int inset = UITheme.Scaled(this, UITheme.Spacing24);
        int headerTop = inset;
        int headerHeight = UITheme.Scaled(this, 30);
        int statusTop = headerTop + headerHeight + UITheme.Scaled(this, UITheme.Spacing8);
        int statusHeight = UITheme.Scaled(this, 20);
        int hostTop = statusTop + statusHeight + UITheme.Scaled(this, UITheme.Spacing32);

        var content = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UITheme.Background,
        };

        _contentHeader = new Label
        {
            Name = "ContentHeaderLabel",
            Text = string.Empty,
            Font = UITheme.Title,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Background,
            AutoSize = true,
            Location = new Point(inset, headerTop),
        };

        _statusStrip = new LiveStatusLabel
        {
            Name = "StatusStripLabel",
            Text = string.Empty,
            Font = UITheme.Body,
            ForeColor = UITheme.SuccessText,
            BackColor = UITheme.Background,
            AutoSize = true,
            Location = new Point(inset, statusTop),
        };

        content.Padding = new Padding(inset, hostTop, inset, inset);

        _moduleHost = new Panel
        {
            Name = "ModuleHost",
            Dock = DockStyle.Fill,
            BackColor = UITheme.Background,
        };

        foreach ((string key, string moduleTitle, _, string phaseNote) in Modules)
        {
            Control module = key switch
            {
                "TakeQuiz" => BuildModulePanel(
                    new QuizTakeForm(quizzes, questions, topics, attempts, answers, scoring, _user, gamification, achievementEvaluator), key),
                "History" => BuildModulePanel(new HistoryForm(_user, attempts, quizzes, topics, answers), key),
                "Reports" => BuildModulePanel(new ReportsForm(_user, reports), key),
                "GenerateQuiz" => BuildModulePanel(new GenerateQuizForm(quizzes, topics, generation, _user), key),
                "Topics" => BuildModulePanel(new TopicManageForm(topics, quizzes, new TopicGenerationService(_config)), key),

                "Scoreboard" => BuildModulePanel(new ScoreboardForm(_user, gamification), key),
                "Achievements" => BuildModulePanel(new AchievementsForm(_user, gamification, achievementEvaluator), key),
                _ => BuildPlaceholderPanel(key, moduleTitle, phaseNote),
            };

            _modulePanels[key] = module;
        }

        content.Controls.Add(_contentHeader);
        content.Controls.Add(_statusStrip);
        content.Controls.Add(_moduleHost);
        foreach (Control panel in _modulePanels.Values)
        {
            _moduleHost.Controls.Add(panel);
        }

        return content;
    }

    private Control BuildModulePanel(ModulePanel module, string key)
    {
        module.Name = key + "Panel";
        module.StatusRequested += HandleModuleStatus;

        return module;
    }

    private void HandleModuleStatus(object? sender, StatusEventArgs e)
        => ShowStatus(e.Message, e.Success);

    private Panel BuildPlaceholderPanel(string key, string moduleTitle, string phaseNote)
    {
        var panel = new Panel
        {
            Name = key + "Panel",
            Dock = DockStyle.Fill,
            Visible = false,
            BackColor = UITheme.Background,
        };

        var card = new CardPanel
        {
            Name = key + "Card",
            Size = new Size(UITheme.Scaled(this, 560), UITheme.Scaled(this, 220)),
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
        };

        int inset = UITheme.Scaled(this, UITheme.Spacing16);
        var cardTitle = new Label
        {
            Text = moduleTitle,
            Font = UITheme.BodySemibold,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(inset, inset),
            Size = new Size(card.Width - inset * 2, UITheme.Scaled(this, 22)),
        };

        var cardNote = new Label
        {
            Name = key + "Placeholder",
            Text = phaseNote,
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(inset, inset + cardTitle.Height + UITheme.Scaled(this, UITheme.Spacing8)),
            Size = new Size(card.Width - inset * 2, UITheme.Scaled(this, 18)),
        };

        card.Controls.Add(cardTitle);
        card.Controls.Add(cardNote);
        panel.Controls.Add(card);
        return panel;
    }

    private void ShowModule(string key)
    {
        if (!_modulePanels.ContainsKey(key) || key == _activeModule)
        {
            return;
        }

        if (_activeModule.Length > 0
            && _modulePanels.TryGetValue(_activeModule, out Control? current)
            && current is ILeaveGuard leaving
            && !leaving.ConfirmLeave())
        {
            return;
        }

        _statusStrip.Text = string.Empty;
        _statusClearTimer.Stop();

        foreach ((string moduleKey, NavItemButton item) in _navItems)
        {
            item.Active = moduleKey == key;
        }

        foreach ((string moduleKey, Control panel) in _modulePanels)
        {
            bool visible = moduleKey == key;
            panel.Visible = visible;

            if (visible && panel is IModulePanel module)
            {
                module.RefreshData();
            }
        }

        UpdateSidebarLevel();

        _contentHeader.Text = Modules.First(module => module.Key == key).Title;
        _activeModule = key;
    }

    internal void UpdateSidebarLevel()
    {
        UserProgress progress = _gamification.GetUserProgress(_user.UserId);
        _sidebarLevelChip.SetLevel(progress.Level);
        _sidebarXpBar.Value = progress.ProgressFraction;
    }

    internal void NavigateToModule(string key) => ShowModule(key);

    internal void NavigateToNewQuiz(int quizId)
    {
        ShowModule("TakeQuiz");

        if (_modulePanels.TryGetValue("TakeQuiz", out Control? panel) && panel is QuizTakeForm takeQuiz)
        {
            takeQuiz.StartNewQuiz(quizId);
        }
    }

    internal void SetSidebarEnabled(bool enabled)
    {
        foreach (NavItemButton item in _navItems.Values)
        {
            item.Enabled = enabled;
        }

        _logoutButton.Enabled = enabled;
    }

    internal void ShowStatus(string message, bool success = true)
    {
        _statusStrip.ForeColor = success ? UITheme.SuccessText : UITheme.DangerText;
        _statusStrip.Text = message;

        _statusClearTimer.Stop();
        if (success)
        {
            _statusClearTimer.Start();
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ShowModule("TakeQuiz");
        StartUpdateCheck();
    }

    private void StartUpdateCheck()
    {
        Task.Run(() => UpdateCheckService.CheckAsync())
            .ContinueWith(
                task => MaybeShowUpdateDialog(task.Result),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void MaybeShowUpdateDialog(UpdateCheckResult? result)
    {
        if (result is not { IsNewer: true })
        {
            return;
        }

        using (var dialog = new UpdateDialog(result))
        {
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }
        }

        if (result.InstallerUrl is null)
        {
            Close();
            return;
        }

        using (var updating = new UpdateProgressDialog(result))
        {
            if (updating.ShowDialog(this) == DialogResult.OK)
            {
                Close();
            }
        }
    }
}
