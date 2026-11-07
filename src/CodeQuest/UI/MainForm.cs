// =============================================================================
// CodeQuest — Phase P7: UI Shell & Navigation (extended in Phase P8)
// The main window of the design of record (spec 5.2): the sidebar/navigation
// shell every later module lives in.
// Look at: the layout — minimum window 1024×640 on Background; the leading
//          sidebar (220px, Background, 1px SeparatorHairline right edge, 24px
//          inset): "CodeQuest" (BodySemibold), 24px, the nav list (recipe 4.3
//          rows), 16px, the account block (signed-in name in Caption
//          TextSecondary, "Log out" as a secondary-style nav item — not pinned
//          to the window bottom, §3.5). The trailing content area follows the
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
//          I2 the Take-quiz and History screens (spec 5.6-5.9) — the Reports
//          and P13 placeholders stay until their phases.
// Status strip (§4.11, §5 anatomy): module panels report success and failure
//          lines through <see cref="ShowStatus"/> — the INTERIM §2.6/§4.11
//          rule (F3, until the R-D3 theme re-issue) renders ALL verdict lines
//          in TextPrimary on Background, so the strip is AA-compliant now;
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
        ("Reports", "Reports", "\uE9D9", "Coming in phase P12."),
        ("GenerateQuiz", "Generate quiz", "\uE710", "Coming in phase P13."),
        ("Review", "Review", "\uE73E", "Coming in phase P13."),
        ("Topics", "Topics", "\uE8EC", ""),
        ("QuizManagement", "Quiz management", "\uE70F", ""),
    };

    /// <summary>How long a success strip line stays before it auto-clears (§4.11).</summary>
    private const int StatusAutoClearMilliseconds = 4000;

    private readonly UserAccount _user;
    private Label _contentHeader = null!;  // assigned during construction (BuildContentArea)
    private Label _statusStrip = null!;    // assigned during construction (BuildContentArea)
    private Panel _moduleHost = null!;     // assigned during construction (BuildContentArea)
    private readonly System.Windows.Forms.Timer _statusClearTimer = new() { Interval = StatusAutoClearMilliseconds };
    private readonly Dictionary<string, NavItemButton> _navItems = new();
    private readonly Dictionary<string, Control> _modulePanels = new();
    private string _activeModule = string.Empty; // the module key currently on screen (§5.2)

    /// <summary>Creates the shell for the account that just signed in, over the application database.</summary>
    public MainForm(UserAccount user, Db database)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(database);
        _user = user;

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
        // business logic behind interfaces).
        IScoringService scoring = new ScoringService();

        Controls.Add(BuildContentArea(topics, quizzes, questions, choices, attempts, answers, scoring));
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

        // Account block, directly beneath the nav list (§5.2, §3.5).
        int accountTop = navTop + Modules.Length * rowHeight + UITheme.Scaled(this, UITheme.Spacing16);

        var signedInName = new Label
        {
            Name = "SignedInName",
            Text = _user.DisplayName,
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Background,
            AutoSize = false,
            Location = new Point(inset + UITheme.Scaled(this, UITheme.Spacing16), accountTop),
            Size = new Size(navWidth, UITheme.Scaled(this, 18)),
        };
        sidebar.Controls.Add(signedInName);

        var logout = new NavItemButton
        {
            Name = "NavLogout",
            Text = "Log out",
            SecondaryAction = true,
            Location = new Point(inset, accountTop + signedInName.Height + UITheme.Scaled(this, UITheme.Spacing8)),
            Size = new Size(navWidth, rowHeight),
        };
        logout.Click += static (sender, e) =>
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
        sidebar.Controls.Add(logout);

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
        IScoringService scoring)
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

        _statusStrip = new Label
        {
            Name = "StatusStripLabel",
            Text = string.Empty,
            Font = UITheme.Body,
            ForeColor = UITheme.TextPrimary, // the interim §2.6/§4.11 verdict token (F3)
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
                // The P8 management screens and the P9/P10 learning screens
                // replace their placeholders (spec 5.3-5.8); the P12/P13
                // placeholders stay until their phases.
                "TakeQuiz" => BuildModulePanel(
                    new QuizTakeForm(quizzes, questions, topics, attempts, answers, scoring, _user), key),
                "History" => BuildModulePanel(new HistoryForm(_user, attempts, quizzes, topics, answers), key),
                "Topics" => BuildModulePanel(new TopicManageForm(topics, quizzes), key),
                "QuizManagement" => BuildModulePanel(
                    new QuizManageForm(quizzes, topics, questions, choices, attempts, answers), key),
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
    /// Shows the content-header status strip line (§5 screen anatomy, §4.11 —
    /// feedback is an inline strip line, never a dialog). INTERIM strip token
    /// (F3, §2.6/§4.11): verdict lines — success AND failure/blocked alike —
    /// render in TextPrimary on Background until the R-D3 theme re-issue
    /// replaces this mapping; the wording carries the meaning in the interim.
    /// Success lines auto-clear after ~4 seconds; failure/blocked lines stay
    /// until the next feedback.
    /// </summary>
    internal void ShowStatus(string message, bool success = true)
    {
        _statusStrip.ForeColor = UITheme.TextPrimary; // the interim §2.6/§4.11 verdict token
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
    }
}
