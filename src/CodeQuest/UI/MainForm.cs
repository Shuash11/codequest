// =============================================================================
// CodeQuest — Phase P7: UI Shell & Navigation (extended in Phase P8)
// The main window of the design of record (spec 5.2): the sidebar/navigation
// shell every later module lives in.
// Look at: the layout — minimum window 1024×640 on Background; the leading
//          sidebar (220px, Background, 1px SeparatorHairline right edge, 24px
//          inset): "CodeQuest" (BodySemibold), 24px, the nav list (recipe 4.3
//          rows), 16px, the account block (an accent avatar chip with the
//          account's initial, the signed-in name in Caption TextSecondary
//          beside it, "Log out" as a secondary-style nav item beneath — not
//          pinned to the window bottom, §3.5). The trailing content area follows the
//          screen anatomy (§5): content header row, 20px status strip, then
//          the active module panel inset 24px (32px below the header block).
// Nav entries (§5.2, one per feature cluster): Take quiz (P9, home, active
//          after login), History (P10), Reports (P12), Generate quiz (P13),
//          Review (P13), Topics (P8), Quiz management (P8). One item is
//          active at a time and always matches the visible panel.
// Lossless switching (§5.2, a structural rule): module panels are created
//          ONCE and persist for the shell's lifetime — switching shows/hides,
//          it never recreates, so in-progress state survives every round trip
//          (search text and filters stay; the data reloads on show).
//          P7 hosted one spec-styled placeholder card per module ("Coming in
//          phase …"); P8 replaced the Topics and Quiz-management placeholders,
//          I2 the Take-quiz and History screens (spec 5.6-5.9), and I3/I4 the
//          Reports, Generate-quiz and Review screens (spec 5.10-5.12) — all
//          seven nav entries are real modules.
// Status strip (§4.11, §5 anatomy): module panels report success and failure
//          lines through <see cref="ShowStatus"/> — the SEMANTIC verdict tokens (F3 final, D3 re-issue): SuccessText for success, DangerText for failure/blocked - both AA on Background
//          the semantic text tokens keep their on-Surface uses. Success lines
//          auto-clear after ~4 seconds, failure/blocked lines stay until the
//          next feedback. Switching modules clears the strip (F12) so stale
//          feedback never describes another module.
// =============================================================================

using CodeQuest.Data;
using CodeQuest.Data.Repositories;
using CodeQuest.Models;
using CodeQuest.Services;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CodeQuest.UI;

/// <summary>
/// The main shell (spec 5.2): sidebar navigation plus the module panel host.
/// </summary>
public sealed class MainForm : Form
{
    /// <summary>The seven nav entries of spec 5.2, in sidebar order.</summary>
    private static readonly (string Key, string Title, string Glyph, string PhaseNote)[] Modules =
    {
        ("TakeQuiz", "Take quiz", "\uE80F", ""),
        ("History", "History", "\uE823", ""),
        ("Reports", "Reports", "\uE9D9", ""),
        ("GenerateQuiz", "Generate quiz", "\uE710", ""),
        ("Review", "Review", "\uE73E", ""),
        ("Topics", "Topics", "\uE8EC", ""),
    };

    /// <summary>How long a success strip line stays before it auto-clears (§4.11).</summary>
    private const int StatusAutoClearMilliseconds = 4000;

    private readonly UserAccount _user;
    private readonly AppConfig _config;
    private Label _contentHeader = null!;  // assigned during construction (BuildContentArea)
    private Label _statusStrip = null!;    // assigned during construction (BuildContentArea)
    private Panel _moduleHost = null!;     // assigned during construction (BuildContentArea)
    private readonly System.Windows.Forms.Timer _statusClearTimer = new() { Interval = StatusAutoClearMilliseconds };
    private readonly Dictionary<string, NavItemButton> _navItems = new();
    private NavItemButton _logoutButton = null!; // not a nav item, but it locks with them (the I4 busy state)
    private readonly Dictionary<string, Control> _modulePanels = new();
    private string _activeModule = string.Empty; // the module key currently on screen (§5.2)

    /// <summary>Creates the shell for the account that just signed in, over the application database and configuration.</summary>
    public MainForm(UserAccount user, Db database, AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(config);
        _user = user;
        _config = config;

        // The repositories of the management screens (P8). The screens talk to
        // the interfaces of the data layer — never to SQLite directly.
        var topics = new TopicRepository(database);
        var quizzes = new QuizRepository(database);
        var questions = new QuestionRepository(database);
        var choices = new ChoiceRepository(database);
        var attempts = new AttemptRepository(database);
        var answers = new SubmittedAnswerRepository(database);

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

        // The scoring of the quiz-taking screen (P9) is a pure service — the
        // UI depends on the IScoringService contract only (plan decision:
        // business logic behind interfaces). The report builder (I3) follows
        // the same pattern: the reports screen consumes IReportService only.
        // The generation chain (I4) consumes the configuration read once at
        // startup (contract 8).
        IScoringService scoring = new ScoringService();
        IReportService reports = new ReportService(attempts, quizzes, topics);
        var generation = new QuizGenerationService(_config);

        Controls.Add(BuildContentArea(topics, quizzes, questions, choices, attempts, answers, scoring, reports, generation));
        Controls.Add(BuildSidebar());
    }

    /// <summary>True when the shell closed because the user logged out (Program returns to the login screen).</summary>
    public bool ReturnedToLogin { get; private set; }

    /// <summary>Builds the leading sidebar: app name, nav list, account block (§5.2).</summary>
    private Panel BuildSidebar()
    {
        int inset = UITheme.Scaled(this, UITheme.Spacing24);
        // Nav rows span the sidebar's inner width: 24px leading inset (§5.2),
        // 16px margin to the right hairline — full-width hit areas (§4.3).
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

        // 1px SeparatorHairline right edge (§5.2).
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

        // Account block, directly beneath the nav list (§5.2, §3.5): an
        // accent avatar chip carrying the account's initial, the signed-in
        // name beside it, and "Log out" beneath — not pinned to the window's
        // bottom edge.
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
        avatar.Paint += static (sender, e) =>
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
            Size = new Size(navWidth - avatarSize - UITheme.Scaled(this, UITheme.Spacing8), UITheme.Scaled(this, 18)),
        };
        sidebar.Controls.Add(signedInName);

        _logoutButton = new NavItemButton
        {
            Name = "NavLogout",
            Text = "Log out",
            SecondaryAction = true,
            Location = new Point(inset, accountTop + avatarSize + UITheme.Scaled(this, UITheme.Spacing8)),
            Size = new Size(navWidth, rowHeight),
        };
        _logoutButton.Click += static (sender, e) =>
        {
            if (sender is NavItemButton button && button.Parent is Panel sidebarPanel && sidebarPanel.Parent is MainForm shell)
            {
                // §5.6/§5.2: the leave-confirmation applies to log out too —
                // a mid-quiz run is confirmed before the shell closes.
                if (shell._modulePanels.TryGetValue(shell._activeModule, out Control? current)
                    && current is ILeaveGuard leaving
                    && !leaving.ConfirmLeave())
                {
                    return; // Stay: the shell stays open, the run continues
                }

                shell.ReturnedToLogin = true;
                shell.Close(); // log out returns to the login screen (Program loops back)
            }
        };
        sidebar.Controls.Add(_logoutButton);

        return sidebar;
    }

    /// <summary>Builds the trailing content area: header row, status strip, module host (§5 anatomy).</summary>
    private Panel BuildContentArea(
        TopicRepository topics,
        QuizRepository quizzes,
        QuestionRepository questions,
        ChoiceRepository choices,
        AttemptRepository attempts,
        SubmittedAnswerRepository answers,
        IScoringService scoring,
        IReportService reports,
        QuizGenerationService generation)
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
            ForeColor = UITheme.SuccessText, // the semantic verdict token (F3 final: AA on Background since D3)
            BackColor = UITheme.Background,
            AutoSize = true,
            Location = new Point(inset, statusTop),
        };

        // The module host fills the content area below the header block; the
        // padding carries the §5 insets (24px sides/bottom, header block on
        // top). Dock + padding keeps the host correct at every window size —
        // anchored bounds here would capture their distances against the
        // panel's pre-layout size and distort the first resize.
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
                // Every nav entry is a real module screen now (spec 5.3-5.12);
                // the switch keeps the placeholder fallback for safety.
                "TakeQuiz" => BuildModulePanel(
                    new QuizTakeForm(quizzes, questions, topics, attempts, answers, scoring, _user), key),
                "History" => BuildModulePanel(new HistoryForm(_user, attempts, quizzes, topics, answers), key),
                "Reports" => BuildModulePanel(new ReportsForm(_user, reports), key),
                "GenerateQuiz" => BuildModulePanel(new GenerateQuizForm(quizzes, topics, generation, _user), key),
                "Review" => BuildModulePanel(
                    new QuizReviewForm(quizzes, topics, questions, choices, answers), key),
                "Topics" => BuildModulePanel(new TopicManageForm(topics, quizzes), key),
                _ => BuildPlaceholderPanel(key, moduleTitle, phaseNote),
            };

            _modulePanels[key] = module;
        }

        content.Controls.Add(_contentHeader);
        content.Controls.Add(_statusStrip);
        content.Controls.Add(_moduleHost);
        foreach (Control panel in _modulePanels.Values)
        {
            _moduleHost.Controls.Add(panel); // the host owns the modules (§5.2: panel inset 24px below the header block)
        }

        return content;
    }

    /// <summary>
    /// Wraps a real module screen as the shell's module panel: the same panel
    /// name and persistence rules as the placeholders (§5.2), wired to the
    /// shell's status strip.
    /// </summary>
    private Control BuildModulePanel(ModulePanel module, string key)
    {
        module.Name = key + "Panel";
        module.StatusRequested += HandleModuleStatus;

        return module;
    }

    /// <summary>A module's feedback line goes to the shell's status strip.</summary>
    private void HandleModuleStatus(object? sender, StatusEventArgs e)
        => ShowStatus(e.Message, e.Success);

    /// <summary>
    /// Builds one module's panel: a spec-styled placeholder card (§4.2) with
    /// the module title and its "Coming in phase …" note — the real module
    /// screens replace this content in P8–P13.
    /// </summary>
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

    /// <summary>
    /// Shows one module: exactly one nav item is active and exactly one panel
    /// is visible — the active item always matches the visible panel (§4.3).
    /// The panels persist, so the switch is lossless (§5.2); a real module
    /// screen reloads its data on show so the rows are always current.
    /// The strip clears on every switch (F12): stale feedback must never
    /// describe another module than the one now on screen.
    /// </summary>
    private void ShowModule(string key)
    {
        if (!_modulePanels.ContainsKey(key) || key == _activeModule)
        {
            return;
        }

        // The §5.6 mid-run guard: the module being LEFT may hold an
        // in-progress state (a running quiz). It confirms first — Stay
        // cancels the whole switch, leaving strip and panels untouched.
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

        _contentHeader.Text = Modules.First(module => module.Key == key).Title;
        _activeModule = key;
    }

    /// <summary>
    /// The navigation entry points the module panels use (spec 5.11's
    /// "Review now" deep-link and "Browse quizzes"): a nav-equivalent switch
    /// through the same guard rules as a sidebar click.
    /// </summary>
    internal void NavigateToModule(string key) => ShowModule(key);

    /// <summary>
    /// The §5.11 "Review now" deep-link: the Review module with one freshly
    /// generated draft already open in its review view.
    /// </summary>
    internal void NavigateToReview(int quizId)
    {
        ShowModule("Review");

        if (_modulePanels.TryGetValue("Review", out Control? panel) && panel is QuizReviewForm review)
        {
            review.OpenQuiz(quizId);
        }
    }

    /// <summary>
    /// The §5.11 busy state disables the sidebar items — one unambiguous
    /// locked state while a generation runs off the UI thread.
    /// </summary>
    internal void SetSidebarEnabled(bool enabled)
    {
        foreach (NavItemButton item in _navItems.Values)
        {
            item.Enabled = enabled;
        }

        _logoutButton.Enabled = enabled; // §5.11: the WHOLE sidebar locks — no half-locked state
    }

    /// <summary>
    /// Shows the content-header status strip line (§5 screen anatomy, §4.11 —
    /// feedback is an inline strip line, never a dialog). Semantic strip
    /// tokens (F3 final, D3 re-issue): SuccessText on success, DangerText on
    /// failure/blocked — both AA on Background since the re-issue.
    /// Success lines auto-clear after ~4 seconds; failure/blocked lines stay
    /// until the next feedback.
    /// </summary>
    internal void ShowStatus(string message, bool success = true)
    {
        _statusStrip.ForeColor = success ? UITheme.SuccessText : UITheme.DangerText; // semantic verdict tokens (F3 final: both AA on Background)
        _statusStrip.Text = message;

        _statusClearTimer.Stop();
        if (success)
        {
            _statusClearTimer.Start();
        }
    }

    /// <summary>Activates the home module ("Take quiz" is active after login, §5.2).</summary>
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ShowModule("TakeQuiz");
        StartUpdateCheck();
    }

    /// <summary>
    /// The version check (once per launch, off the UI thread): a newer public
    /// release raises the "update required" dialog with the release notes.
    /// Every failure is silent — the check never disturbs a study session.
    /// </summary>
    private void StartUpdateCheck()
    {
        Task.Run(() => UpdateCheckService.CheckAsync())
            .ContinueWith(
                task => MaybeShowUpdateDialog(task.Result),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>Shows the update dialog only for a verified newer release.</summary>
    private void MaybeShowUpdateDialog(UpdateCheckResult? result)
    {
        if (result is not { IsNewer: true })
        {
            return; // current build, offline, or a failed check — stay silent
        }

        using var dialog = new UpdateDialog(result);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            Close(); // "Download update" — the browser has the new build; this session ends
        }
    }
}
