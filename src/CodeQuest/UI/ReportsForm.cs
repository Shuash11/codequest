// The reports screen.

using CodeQuest.Data.Repositories;
using CodeQuest.Models;
using CodeQuest.Services;
using System.Drawing;
using System.Windows.Forms;

namespace CodeQuest.UI;

public sealed class ReportsForm : ModulePanel
{
    private const string EmptyTitle = "No quiz attempts yet";
    private const string EmptyCaption = "Take a quiz and your results will appear here.";
    private const string AllTopicsLabel = "All topics";
    private const string AllDifficultiesLabel = "All difficulties";
    private const string NoMatchTitle = "No topics match your filters.";
    private const string WeakListHeader = "Topics that need more practice";
    private const string WeakListEmpty = "No topic needs practice right now.";

    private readonly UserAccount _user;
    private readonly IReportService _reports;

    private readonly ThemedComboBox _topicFilter = null!;
    private readonly ThemedComboBox _difficultyFilter = null!;
    private readonly Label _summaryAttempts = null!;
    private readonly Label _summaryQuizzes = null!;
    private readonly Label _summaryAverage = null!;
    private readonly Panel _barStack = null!;
    private readonly Panel _weakCard = null!;
    private readonly Panel _emptyView = null!;
    private readonly CardPanel _summaryCard = null!;
    private PracticeReport? _lastReport;
    private string? _lastTopicFilter;
    private System.Windows.Forms.Timer _resizeTimer = null!;
    private bool _rebuildingFilters;

    private Dictionary<int, string> _topicNames = new();

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

        _summaryCard = new CardPanel
        {
            Name = "ReportsSummaryCard",
            Location = new Point(0, filterHeight + UITheme.Scaled(this, UITheme.Spacing16)),
            Size = new Size(cardWidth, UITheme.Scaled(this, 110)),
        };
        _summaryAttempts = StatLabel("ReportsAttemptsValue", 0);
        _summaryQuizzes = StatLabel("ReportsQuizzesValue", 1);
        _summaryAverage = StatLabel("ReportsAverageValue", 2);
        _summaryCard.Controls.Add(_summaryAttempts);
        _summaryCard.Controls.Add(_summaryQuizzes);
        _summaryCard.Controls.Add(_summaryAverage);
        _summaryCard.Controls.Add(StatCaption("ReportsAttemptsCaption", "Attempts", 0));
        _summaryCard.Controls.Add(StatCaption("ReportsQuizzesCaption", "Quizzes taken", 1));
        _summaryCard.Controls.Add(StatCaption("ReportsAverageCaption", "Average score", 2));

        _barStack = new Panel
        {
            Name = "ReportsBarStack",
            Location = new Point(0, _summaryCard.Bottom + UITheme.Scaled(this, UITheme.Spacing16)),
            Size = new Size(cardWidth, UITheme.Scaled(this, 100)),
            BackColor = UITheme.Surface,
        };

        _weakCard = new Panel
        {
            Name = "ReportsWeakCard",
            Location = new Point(0, _barStack.Bottom + UITheme.Scaled(this, UITheme.Spacing16)),
            Size = new Size(cardWidth, UITheme.Scaled(this, 120)),
            BackColor = UITheme.Surface,
        };

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

        Controls.Add(_weakCard);
        Controls.Add(_barStack);
        Controls.Add(_summaryCard);
        Controls.Add(_topicFilter);
        Controls.Add(_difficultyFilter);
        Controls.Add(_emptyView);

        // Responsive content (owner directive): stretch the cards with the
        // window; the bar/weak rows rebuild at the new width once the drag
        // settles (they read the card width while painting).
        _resizeTimer = new System.Windows.Forms.Timer { Interval = 250 };
        _resizeTimer.Tick += static (sender, e) =>
        {
            if (sender is System.Windows.Forms.Timer timer)
            {
                timer.Stop();
                if (timer.Tag is ReportsForm form)
                {
                    form.RebuildForWidth();
                }
            }
        };
        _resizeTimer.Tag = this;
    }

    private void RebuildForWidth()
    {
        if (_lastReport is not null)
        {
            BuildScoreBars(_lastReport, _lastTopicFilter);
            BuildWeakList(_lastReport);
        }
        else
        {
            ApplyFilter();
        }
    }

    private void LayoutReportCards()
    {
        int contentWidth = UITheme.ResponsiveWidth(this);
        int x = Math.Max((ClientSize.Width - contentWidth) / 2, 0);
        _summaryCard.Location = new Point(x, _summaryCard.Location.Y);
        _summaryCard.Width = contentWidth;
        _barStack.Location = new Point(x, _barStack.Location.Y);
        _barStack.Width = contentWidth;
        _weakCard.Location = new Point(x, _weakCard.Location.Y);
        _weakCard.Width = contentWidth;

        int inset = UITheme.Scaled(this, UITheme.Spacing16);
        int columnWidth = Math.Max((contentWidth - inset * 2) / 3, 1);
        string[] valueNames = { "ReportsAttemptsValue", "ReportsQuizzesValue", "ReportsAverageValue" };
        string[] captionNames = { "ReportsAttemptsCaption", "ReportsQuizzesCaption", "ReportsAverageCaption" };
        for (int column = 0; column < 3; column++)
        {
            int left = inset + column * columnWidth + UITheme.Scaled(this, 8);
            Control? value = _summaryCard.Controls[valueNames[column]];
            Control? caption = _summaryCard.Controls[captionNames[column]];
            if (value is not null)
            {
                value.Location = new Point(left, value.Location.Y);
                value.Width = Math.Max(columnWidth - UITheme.Scaled(this, UITheme.Spacing16), 1);
            }

            if (caption is not null)
            {
                caption.Location = new Point(left, caption.Location.Y);
                caption.Width = Math.Max(columnWidth - UITheme.Scaled(this, UITheme.Spacing16), 1);
            }
        }
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        LayoutReportCards();
        _resizeTimer.Stop();
        _resizeTimer.Start(); // rebuild the painted rows once the drag settles
    }

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

    public override void RefreshData()
    {
        if (_difficultyFilter.Items.Count == 0)
        {
            _difficultyFilter.Items.Add(AllDifficultiesLabel);
            _difficultyFilter.Items.Add(Difficulty.Easy.ToString());
            _difficultyFilter.Items.Add(Difficulty.Medium.ToString());
            _difficultyFilter.Items.Add(Difficulty.Hard.ToString());
        }

        ApplyFilter(rebuildTopicItems: true);
    }

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
        _lastReport = report;
        _lastTopicFilter = topicFilterName;

        if (report.Overall.TotalAttempts == 0)
        {
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

        _summaryAttempts.Text = report.Overall.TotalAttempts.ToString();
        _summaryQuizzes.Text = report.Overall.QuizzesTaken.ToString();
        _summaryAverage.Text = $"{report.Overall.AveragePercent:0}%";

        BuildScoreBars(report, topicFilterName);
        BuildWeakList(report);
    }

    private static Color BandColor(double percent) => percent switch
    {
        >= 80d => UITheme.LevelEasy,
        >= 50d => UITheme.LevelMedium,
        _ => UITheme.LevelHard,
    };

    private void BuildScoreBars(PracticeReport report, string? topicFilterName)
    {
        _barStack.SuspendLayout();
        foreach (Control existing in _barStack.Controls)
        {
            existing.Dispose();
        }
        _barStack.Controls.Clear();

        int inset = UITheme.Scaled(this, UITheme.Spacing16);
        int rowHeight = UITheme.Scaled(this, 64);
        int barHeight = UITheme.Scaled(this, 14);
        int top = inset;

        var rows = report.Topics
            .Where(row => topicFilterName is null || string.Equals(row.TopicName, topicFilterName, StringComparison.Ordinal))
            .ToList();

        if (rows.Count == 0)
        {
            var nothing = new Label
            {
                Name = "ReportsBarsEmpty",
                Text = NoMatchTitle,
                Font = UITheme.Caption,
                ForeColor = UITheme.TextSecondary,
                BackColor = UITheme.Surface,
                AutoSize = false,
                Location = new Point(inset, top),
                Size = new Size(Math.Max(_barStack.Width - inset * 2, 1), UITheme.Scaled(this, 20)),
            };
            _barStack.Controls.Add(nothing);
            top += UITheme.Scaled(this, 20);
        }

        foreach (TopicReport row in rows)
        {
            var name = new Label
            {
                Name = "ReportsBarName" + row.TopicId,
                Text = row.TopicName,
                Font = UITheme.BodySemibold,
                ForeColor = row.Attempts == 0 ? UITheme.TextTertiary : UITheme.TextPrimary,
                BackColor = UITheme.Surface,
                AutoSize = false,
                Location = new Point(inset, top),
                Size = new Size(Math.Max(_barStack.Width - inset * 2, 1), UITheme.Scaled(this, 20)),
            };
            _barStack.Controls.Add(name);
            top += UITheme.Scaled(this, 20);

            int trackWidth = Math.Max(_barStack.Width - inset * 2, 1);
            var track = new Panel
            {
                Name = "ReportsBarTrack" + row.TopicId,
                Location = new Point(inset, top),
                Size = new Size(trackWidth, barHeight),
                BackColor = UITheme.Fill,
            };
            if (row.Attempts > 0)
            {
                int fillWidth = Math.Max((int)Math.Round(trackWidth * row.AveragePercent / 100d), UITheme.Scaled(this, 6));
                var fillBar = new Panel
                {
                    Name = "ReportsBarFill" + row.TopicId,
                    Location = new Point(0, 0),
                    Size = new Size(fillWidth, barHeight),
                    BackColor = BandColor(row.AveragePercent),
                };
                track.Controls.Add(fillBar);
            }
            _barStack.Controls.Add(track);
            top += barHeight + UITheme.Scaled(this, UITheme.Spacing4);

            string numbers = row.Attempts == 0
                ? "\u2014 no attempts yet"
                : $"avg {row.AveragePercent:0}%  \u00B7  best {row.BestPercent:0}%  \u00B7  {row.Attempts} attempt{(row.Attempts == 1 ? string.Empty : "s")}"
                    + (row.NeedsPractice ? "  \u00B7  needs practice" : string.Empty);
            var numbersLabel = new Label
            {
                Name = "ReportsBarNumbers" + row.TopicId,
                Text = numbers,
                Font = UITheme.Caption,
                ForeColor = row.Attempts == 0 ? UITheme.TextTertiary : BandColor(row.AveragePercent),
                BackColor = UITheme.Surface,
                AutoSize = false,
                Location = new Point(inset, top),
                Size = new Size(Math.Max(_barStack.Width - inset * 2, 1), UITheme.Scaled(this, 16)),
            };
            _barStack.Controls.Add(numbersLabel);
            top += UITheme.Scaled(this, 16) + UITheme.Scaled(this, UITheme.Spacing12);
        }

        _barStack.ResumeLayout();
        _barStack.Height = top + inset;
    }

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
