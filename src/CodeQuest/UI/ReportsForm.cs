// =============================================================================
// CodeQuest — Performance Reports (I3): the reports screen of spec 5.10.
// Layout: filter row — topic filter (§4.5 combo, "All topics"), difficulty
// filter (§4.5 combo, "All difficulties"); summary card — three inline stat
// groups (number in Title over a Caption label): Attempts, Quizzes taken,
// Average score; per-topic table card (§4.6): Topic — Attempts — Average % —
// Best % — Needs practice (a quiet WarningText flag, word plus color, §2.6);
// needs-practice list card, header "Topics that need more practice", rows
// sorted ascending by average (weakest first).
// States: empty (no attempts) — the summary card, table, and list are all
// replaced by one friendly empty state: "No quiz attempts yet" + "Take a quiz
// and your results will appear here." Zeros are never presented as data; a
// zero-attempt topic row renders "—" in its numeric columns and no flag.
// The panel reloads from IReportService each time it becomes visible, so
// finishing a quiz updates it on return. Read-only — no strip messages.
// =============================================================================

using CodeQuest.Data.Repositories;
using CodeQuest.Models;
using CodeQuest.Services;
using System.Drawing;
using System.Windows.Forms;

namespace CodeQuest.UI;

/// <summary>
/// The reports module (spec 5.10): summary, per-topic table, and the
/// needs-practice list over the tested report service.
/// </summary>
public sealed class ReportsForm : ModulePanel
{
    private const string EmptyTitle = "No quiz attempts yet";
    private const string EmptyCaption = "Take a quiz and your results will appear here.";
    private const string AllTopicsLabel = "All topics";
    private const string AllDifficultiesLabel = "All difficulties";
    private const string WeakListHeader = "Topics that need more practice";
    private const string WeakListEmpty = "No topic needs practice right now.";

    private readonly UserAccount _user;
    private readonly IReportService _reports;

    private readonly ThemedComboBox _topicFilter = null!;
    private readonly ThemedComboBox _difficultyFilter = null!;
    private readonly Label _summaryAttempts = null!;
    private readonly Label _summaryQuizzes = null!;
    private readonly Label _summaryAverage = null!;
    private readonly ListCardGrid _table = null!;
    private readonly Panel _weakCard = null!;
    private readonly Panel _emptyView = null!;
    private bool _rebuildingFilters; // guards the SelectedIndexChanged feedback loop while the combos rebuild

    /// <summary>Creates the reports module over the report service.</summary>
    public ReportsForm(UserAccount user, IReportService reports)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(reports);
        _user = user;
        _reports = reports;

        AutoScroll = true;
        Dock = DockStyle.Fill;
        BackColor = UITheme.Background;

        int filterHeight = UITheme.Scaled(this, 32);
        int comboWidth = UITheme.Scaled(this, 200);

        _topicFilter = new ThemedComboBox
        {
            Name = "ReportsTopicFilter",
            Location = new Point(0, 0),
            Size = new Size(comboWidth, filterHeight),
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        _topicFilter.SelectedIndexChanged += static (sender, e) =>
        {
            if (sender is ThemedComboBox combo && combo.FindAncestor<ReportsForm>() is { } form)
            {
                form.ApplyFilter();
            }
        };

        _difficultyFilter = new ThemedComboBox
        {
            Name = "ReportsDifficultyFilter",
            Location = new Point(comboWidth + UITheme.Scaled(this, UITheme.Spacing8), 0),
            Size = new Size(comboWidth, filterHeight),
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        _difficultyFilter.SelectedIndexChanged += static (sender, e) =>
        {
            if (sender is ThemedComboBox combo && combo.FindAncestor<ReportsForm>() is { } form)
            {
                form.ApplyFilter();
            }
        };

        int cardWidth = UITheme.Scaled(this, 640);

        // The summary card: three stat groups (number in Title, label in Caption).
        var summaryCard = new CardPanel
        {
            Name = "ReportsSummaryCard",
            Location = new Point(0, filterHeight + UITheme.Scaled(this, UITheme.Spacing16)),
            Size = new Size(cardWidth, UITheme.Scaled(this, 96)),
        };
        _summaryAttempts = StatLabel("ReportsAttemptsValue", 0);
        _summaryQuizzes = StatLabel("ReportsQuizzesValue", 1);
        _summaryAverage = StatLabel("ReportsAverageValue", 2);
        summaryCard.Controls.Add(_summaryAttempts);
        summaryCard.Controls.Add(_summaryQuizzes);
        summaryCard.Controls.Add(_summaryAverage);
        summaryCard.Controls.Add(StatCaption("ReportsAttemptsCaption", "Attempts", 0));
        summaryCard.Controls.Add(StatCaption("ReportsQuizzesCaption", "Quizzes taken", 1));
        summaryCard.Controls.Add(StatCaption("ReportsAverageCaption", "Average score", 2));

        // The per-topic table card (§4.6 list).
        _table = new ListCardGrid
        {
            Name = "ReportsTableCard",
            Location = new Point(0, summaryCard.Bottom + UITheme.Scaled(this, UITheme.Spacing16)),
            Size = new Size(cardWidth, UITheme.Scaled(this, 300)),
        };
        _table.Grid.Name = "ReportsGrid";
        _table.Grid.AccessibleName = "Performance by topic";
        _table.Grid.AddTextColumn("ReportsTopic", "Topic", 30f, extraLeftInset: UITheme.Scaled(this, UITheme.Spacing8), minimumWidth: 120);
        _table.Grid.AddTextColumn("ReportsTopicAttempts", "Attempts", 12f, minimumWidth: 84);
        _table.Grid.AddTextColumn("ReportsTopicAverage", "Average %", 14f, minimumWidth: 96);
        _table.Grid.AddTextColumn("ReportsTopicBest", "Best %", 12f, minimumWidth: 84);
        _table.Grid.AddTextColumn("ReportsTopicFlag", "Needs practice", 20f, minimumWidth: 120);

        // The needs-practice list card.
        _weakCard = new Panel
        {
            Name = "ReportsWeakCard",
            Location = new Point(0, _table.Bottom + UITheme.Scaled(this, UITheme.Spacing16)),
            Size = new Size(cardWidth, UITheme.Scaled(this, 120)),
            BackColor = UITheme.Surface,
        };

        // The empty view replaces every report element when no attempt exists.
        _emptyView = new Panel
        {
            Name = "ReportsEmptyView",
            Dock = DockStyle.Fill,
            Visible = false,
            BackColor = UITheme.Background,
        };
        var emptyCard = new CardPanel
        {
            Name = "ReportsEmptyCard",
            Size = new Size(UITheme.Scaled(this, 520), UITheme.Scaled(this, 140)),
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
        };
        var emptyTitle = new Label
        {
            Name = "ReportsEmptyTitle",
            Text = EmptyTitle,
            Font = UITheme.BodySemibold,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, UITheme.Spacing16)),
            Size = new Size(UITheme.Scaled(this, 488), UITheme.Scaled(this, 22)),
        };
        var emptyCaption = new Label
        {
            Name = "ReportsEmptyCaption",
            Text = EmptyCaption,
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, UITheme.Spacing16) + UITheme.Scaled(this, 26)),
            Size = new Size(UITheme.Scaled(this, 488), UITheme.Scaled(this, 18)),
        };
        emptyCard.Controls.Add(emptyTitle);
        emptyCard.Controls.Add(emptyCaption);
        _emptyView.Controls.Add(emptyCard);

        // The report elements are flow-positioned (the panel scrolls when the
        // window is short); the empty view fills the whole module.
        Controls.Add(_weakCard);
        Controls.Add(_table);
        Controls.Add(summaryCard);
        Controls.Add(_topicFilter);
        Controls.Add(_difficultyFilter);
        Controls.Add(_emptyView);
    }

    /// <summary>One stat number of the summary card (Title font, column-positioned).</summary>
    private Label StatLabel(string name, int column)
    {
        return new Label
        {
            Name = name,
            Font = UITheme.Title,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing16) + column * UITheme.Scaled(this, 200), UITheme.Scaled(this, UITheme.Spacing16)),
            Size = new Size(UITheme.Scaled(this, 180), UITheme.Scaled(this, 36)),
        };
    }

    /// <summary>One stat caption of the summary card (Caption font under its number).</summary>
    private Label StatCaption(string name, string text, int column)
    {
        return new Label
        {
            Name = name,
            Text = text,
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(
                UITheme.Scaled(this, UITheme.Spacing16) + column * UITheme.Scaled(this, 200),
                UITheme.Scaled(this, UITheme.Spacing16) + UITheme.Scaled(this, 40)),
            Size = new Size(UITheme.Scaled(this, 180), UITheme.Scaled(this, 18)),
        };
    }

    /// <inheritdoc />
    public override void RefreshData()
    {
        // The difficulty items never change (three members); the topic items
        // follow the topic CRUD, rebuilt by NAME so the selection survives.
        if (_difficultyFilter.Items.Count == 0)
        {
            _difficultyFilter.Items.Add(AllDifficultiesLabel);
            _difficultyFilter.Items.Add(Difficulty.Easy.ToString());
            _difficultyFilter.Items.Add(Difficulty.Medium.ToString());
            _difficultyFilter.Items.Add(Difficulty.Hard.ToString());
        }

        ApplyFilter(rebuildTopicItems: true);
    }

    /// <summary>
    /// Rebuilds the report with the current filters. The topic combo is
    /// rebuilt from the report's own topic rows (name-preserved); the guard
    /// stops the SelectedIndexChanged feedback loop while it does.
    /// </summary>
    private void ApplyFilter(bool rebuildTopicItems = false)
    {
        if (_rebuildingFilters)
        {
            return;
        }

        Difficulty? difficulty = _difficultyFilter.SelectedIndex switch
        {
            1 => Difficulty.Easy,
            2 => Difficulty.Medium,
            3 => Difficulty.Hard,
            _ => null,
        };

        PracticeReport report = _reports.Build(_user.UserId, difficulty);

        _rebuildingFilters = true;
        if (rebuildTopicItems)
        {
            string? previous = _topicFilter.SelectedIndex is > 0 ? _topicFilter.Items[_topicFilter.SelectedIndex]?.ToString() : null;
            _topicFilter.Items.Clear();
            _topicFilter.Items.Add(AllTopicsLabel);
            foreach (TopicReport row in report.Topics)
            {
                _topicFilter.Items.Add(row.TopicName);
            }

            int index = previous is null ? 0 : _topicFilter.Items.IndexOf(previous);
            _topicFilter.SelectedIndex = index >= 0 ? index : 0;
        }

        string? topicFilterName = _topicFilter.SelectedIndex is > 0 ? _topicFilter.Items[_topicFilter.SelectedIndex]?.ToString() : null;
        _rebuildingFilters = false;

        if (report.Overall.TotalAttempts == 0)
        {
            // §5.10: zeros are never presented as data — one friendly empty state.
            foreach (Control child in Controls)
            {
                child.Visible = child == _emptyView;
            }

            return;
        }

        foreach (Control child in Controls)
        {
            child.Visible = child != _emptyView;
        }

        // The summary card.
        _summaryAttempts.Text = report.Overall.TotalAttempts.ToString();
        _summaryQuizzes.Text = report.Overall.QuizzesTaken.ToString();
        _summaryAverage.Text = $"{report.Overall.AveragePercent:0}%";

        // The per-topic table, narrowed by the topic filter (a pure view filter).
        _table.Grid.SuspendLayout();
        _table.Grid.Rows.Clear();
        foreach (TopicReport row in report.Topics)
        {
            if (topicFilterName is not null && !string.Equals(row.TopicName, topicFilterName, StringComparison.Ordinal))
            {
                continue;
            }

            int index = _table.Grid.Rows.Add(
                row.TopicName,
                row.Attempts == 0 ? "\u2014" : row.Attempts.ToString(), // zero-attempt rows render "—", never zeros-as-data
                row.Attempts == 0 ? "\u2014" : $"{row.AveragePercent:0}%",
                row.Attempts == 0 ? "\u2014" : $"{row.BestPercent:0}%",
                row.NeedsPractice ? "Needs practice" : "\u2014");
            DataGridViewRow gridRow = _table.Grid.Rows[index];
            gridRow.Cells["ReportsTopicFlag"].Style.ForeColor = row.NeedsPractice ? UITheme.WarningText : UITheme.TextTertiary;
        }
        _table.Grid.ResumeLayout();

        // The needs-practice list, weakest first (the service's order).
        BuildWeakList(report);
    }

    /// <summary>Builds the needs-practice list card's rows (§5.10, ascending by average).</summary>
    private void BuildWeakList(PracticeReport report)
    {
        _weakCard.SuspendLayout();
        foreach (Control existing in _weakCard.Controls)
        {
            existing.Dispose();
        }
        _weakCard.Controls.Clear();

        int inset = UITheme.Scaled(this, UITheme.Spacing16);
        var header = new Label
        {
            Name = "ReportsWeakHeader",
            Text = WeakListHeader,
            Font = UITheme.BodySemibold,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(inset, inset),
            Size = new Size(Math.Max(_weakCard.Width - inset * 2, 1), UITheme.Scaled(this, 22)),
        };
        _weakCard.Controls.Add(header);

        int top = inset + UITheme.Scaled(this, 22) + UITheme.Scaled(this, UITheme.Spacing8);

        if (report.WeakTopics.Count == 0)
        {
            var quiet = new Label
            {
                Name = "ReportsWeakEmpty",
                Text = WeakListEmpty,
                Font = UITheme.Caption,
                ForeColor = UITheme.TextSecondary,
                BackColor = UITheme.Surface,
                AutoSize = false,
                Location = new Point(inset, top),
                Size = new Size(Math.Max(_weakCard.Width - inset * 2, 1), UITheme.Scaled(this, 18)),
            };
            _weakCard.Controls.Add(quiet);
            top += UITheme.Scaled(this, 18);
        }
        else
        {
            for (int i = 0; i < report.WeakTopics.Count; i++)
            {
                TopicReport weak = report.WeakTopics[i];
                var name = new Label
                {
                    Name = $"ReportsWeakName{i}",
                    Text = weak.TopicName,
                    Font = UITheme.Body,
                    ForeColor = UITheme.TextPrimary,
                    BackColor = UITheme.Surface,
                    AutoSize = false,
                    Location = new Point(inset, top),
                    Size = new Size(UITheme.Scaled(this, 220), UITheme.Scaled(this, 26)),
                    TextAlign = ContentAlignment.MiddleLeft,
                };
                var average = new Label
                {
                    Name = $"ReportsWeakAverage{i}",
                    Text = $"{weak.AveragePercent:0}%",
                    Font = UITheme.Body,
                    ForeColor = UITheme.WarningText,
                    BackColor = UITheme.Surface,
                    AutoSize = false,
                    Location = new Point(inset + UITheme.Scaled(this, 220), top),
                    Size = new Size(UITheme.Scaled(this, 80), UITheme.Scaled(this, 26)),
                    TextAlign = ContentAlignment.MiddleLeft,
                };
                var attempts = new Label
                {
                    Name = $"ReportsWeakAttempts{i}",
                    Text = $"{weak.Attempts} attempt{(weak.Attempts == 1 ? string.Empty : "s")}",
                    Font = UITheme.Caption,
                    ForeColor = UITheme.TextSecondary,
                    BackColor = UITheme.Surface,
                    AutoSize = false,
                    Location = new Point(Math.Max(_weakCard.Width - inset - UITheme.Scaled(this, 120), 1), top),
                    Size = new Size(UITheme.Scaled(this, 120), UITheme.Scaled(this, 26)),
                    TextAlign = ContentAlignment.MiddleRight,
                };
                _weakCard.Controls.Add(name);
                _weakCard.Controls.Add(average);
                _weakCard.Controls.Add(attempts);
                top += UITheme.Scaled(this, 26);

                if (i < report.WeakTopics.Count - 1)
                {
                    var separator = new Panel
                    {
                        Name = $"ReportsWeakSeparator{i}",
                        Location = new Point(inset, top),
                        Size = new Size(Math.Max(_weakCard.Width - inset * 2, 1), UITheme.Scaled(this, 1)),
                        BackColor = UITheme.SeparatorHairline,
                    };
                    _weakCard.Controls.Add(separator);
                    top += UITheme.Scaled(this, 1);
                }
            }
        }

        _weakCard.ResumeLayout();
        _weakCard.Height = top + inset;
    }
}
