// =============================================================================
// CodeQuest — Quiz History & Answer Review (the fun-UI redesign): past
// attempts as CARDS, not table rows. The toolbar keeps search ("Search
// quizzes…"), the topic filter, and the score filter (Any / ≥80% / 50–79% /
// <50%); each attempt renders as a card — date, quiz title, topic, and a
// colored score pill (green ≥80%, amber 50–79%, red <50% — pill text always
// carries the numbers, never color alone) — with a "Review" button that opens
// the §5.9 answer-review drill-in.
// Derived data (R-2 #1): the score denominator is the quiz's own question
// count, computed live. Fully local — no network dependency (proposal line
// 23). Read-only by design: no CRUD feedback lines on this screen.
// =============================================================================

using CodeQuest.Data.Repositories;
using CodeQuest.Models;
using System.Drawing;
using System.Windows.Forms;

namespace CodeQuest.UI;

/// <summary>
/// The history module (the fun-UI redesign): attempt cards with search,
/// filters, and the answer-review drill-in.
/// </summary>
public sealed class HistoryForm : ModulePanel
{
    private const string ReviewAction = "Review";
    private const string EmptyTitle = "No quizzes taken yet";
    private const string EmptyCaption = "Take a quiz and your results will appear here.";
    private const string NoMatchTitle = "No attempts match your filters.";
    private const string AllTopicsLabel = "All topics";
    private const string AnyScoreLabel = "Any score";

    private readonly UserAccount _user;
    private readonly AttemptRepository _attempts;
    private readonly QuizRepository _quizzes;
    private readonly TopicRepository _topics;
    private readonly SubmittedAnswerRepository _answers;

    private readonly InputField _search = null!;
    private readonly ThemedComboBox _topicFilter = null!;
    private readonly ThemedComboBox _scoreFilter = null!;
    private readonly FlowLayoutPanel _cardFlow = null!;
    private readonly Panel _emptyView = null!;
    private AttemptReviewForm? _reviewPanel; // the §5.9 drill-in state, created on first open
    private bool _rebuildingFilters; // guards the SelectedIndexChanged feedback loop

    private Dictionary<int, string> _topicNames = new();

    /// <summary>Creates the history module for the signed-in user's attempts.</summary>
    public HistoryForm(
        UserAccount user,
        AttemptRepository attempts,
        QuizRepository quizzes,
        TopicRepository topics,
        SubmittedAnswerRepository answers)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(attempts);
        ArgumentNullException.ThrowIfNull(quizzes);
        ArgumentNullException.ThrowIfNull(topics);
        ArgumentNullException.ThrowIfNull(answers);
        _user = user;
        _attempts = attempts;
        _quizzes = quizzes;
        _topics = topics;
        _answers = answers;

        Dock = DockStyle.Fill;
        BackColor = UITheme.Background;

        int toolbarHeight = UITheme.Scaled(this, 32);
        int comboWidth = UITheme.Scaled(this, 190);

        _search = new InputField(accessibleName: "Search history")
        {
            Name = "HistorySearch",
            PlaceholderText = "Search quizzes\u2026",
            Location = new Point(0, 0),
            Size = new Size(comboWidth + UITheme.Scaled(this, 60), toolbarHeight),
        };
        _search.TextChanged += static (sender, e) =>
        {
            if (sender is InputField field && field.FindAncestor<HistoryForm>() is { } form)
            {
                ApplyFilter();
            }
        };

        _topicFilter = new ThemedComboBox
        {
            Name = "HistoryTopicFilter",
            Location = new Point(_search.Right + UITheme.Scaled(this, UITheme.Spacing8), 0),
            Size = new Size(comboWidth, toolbarHeight),
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        _topicFilter.SelectedIndexChanged += static (sender, e) =>
        {
            if (sender is ThemedComboBox combo && combo.FindAncestor<HistoryForm>() is { } form)
            {
                ApplyFilter();
            }
        };

        _scoreFilter = new ThemedComboBox
        {
            Name = "HistoryScoreFilter",
            Location = new Point(_topicFilter.Right + UITheme.Scaled(this, UITheme.Spacing8), 0),
            Size = new Size(comboWidth, toolbarHeight),
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        _scoreFilter.SelectedIndexChanged += static (sender, e) =>
        {
            if (sender is ThemedComboBox combo && combo.FindAncestor<HistoryForm>() is { } form)
            {
                ApplyFilter();
            }
        };
        _scoreFilter.Items.Add(AnyScoreLabel);
        _scoreFilter.Items.Add("80% and above");
        _scoreFilter.Items.Add("50\u201379%");
        _scoreFilter.Items.Add("Below 50%");
        _scoreFilter.SelectedIndex = 0;

        _cardFlow = new FlowLayoutPanel
        {
            Name = "HistoryCardFlow",
            Location = new Point(0, toolbarHeight + UITheme.Scaled(this, UITheme.Spacing16)),
            Size = new Size(Math.Max(Width, 1), Math.Max(Height - toolbarHeight - UITheme.Scaled(this, UITheme.Spacing16), 1)),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            AutoScroll = true,
            BackColor = UITheme.Background,
        };

        // The empty view replaces the cards (§5.8: empty vs filtered-empty copy).
        _emptyView = new Panel
        {
            Name = "HistoryEmptyView",
            Dock = DockStyle.Fill,
            Visible = false,
            BackColor = UITheme.Background,
        };

        Controls.Add(_search);
        Controls.Add(_topicFilter);
        Controls.Add(_scoreFilter);
        Controls.Add(_cardFlow);
        Controls.Add(_emptyView);
    }

    /// <inheritdoc />
    public override void RefreshData()
    {
        Dictionary<int, string> names = new();
        foreach (Topic topic in _topics.GetAll())
        {
            names[topic.TopicId] = topic.Name;
        }
        _topicNames = names;

        RebuildTopicFilter();
        ApplyFilter();
    }

    /// <summary>Rebuilds the topic filter items, preserving the selection BY NAME.</summary>
    private void RebuildTopicFilter()
    {
        _rebuildingFilters = true;
        string? previous = _topicFilter.SelectedItem as string; // topics.name is UNIQUE

        _topicFilter.Items.Clear();
        _topicFilter.Items.Add(AllTopicsLabel);
        foreach ((int _, string name) in _topicNames.OrderBy(pair => pair.Value))
        {
            _topicFilter.Items.Add(name);
        }

        int index = previous is null ? 0 : _topicFilter.Items.IndexOf(previous);
        _topicFilter.SelectedIndex = index >= 0 ? index : 0;
        _rebuildingFilters = false;
    }

    /// <summary>Builds one attempt card: date, quiz, topic, colored score pill, Review button.</summary>
    private Control BuildAttemptCard(QuizAttempt attempt, Quiz quiz, string topicName, int total, double percent)
    {
        Color pillBack = percent switch
        {
            >= 80d => UITheme.LevelEasy,
            >= 50d => UITheme.LevelMedium,
            _ => UITheme.LevelHard,
        };

        var card = new Panel
        {
            Name = "HistoryCard" + attempt.AttemptId,
            Size = new Size(Math.Max(_cardFlow.Width - UITheme.Scaled(this, UITheme.Spacing24), 1), UITheme.Scaled(this, 84)),
            BackColor = UITheme.Surface,
            Margin = new Padding(0, 0, 0, UITheme.Scaled(this, UITheme.Spacing12)),
        };

        var date = new Label
        {
            Name = "HistoryCardDate" + attempt.AttemptId,
            Text = UiFormat.FormatDate(attempt.AttemptedAt),
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, UITheme.Spacing12)),
            Size = new Size(UITheme.Scaled(this, 120), UITheme.Scaled(this, 18)),
        };
        var title = new Label
        {
            Name = "HistoryCardTitle" + attempt.AttemptId,
            Text = quiz.Title,
            Font = UITheme.BodySemibold,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            AutoEllipsis = true,
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, UITheme.Spacing12) + UITheme.Scaled(this, 20)),
            Size = new Size(Math.Max(card.Width - UITheme.Scaled(this, UITheme.Spacing32) - UITheme.Scaled(this, 210), 1), UITheme.Scaled(this, 22)),
        };
        var topic = new Label
        {
            Name = "HistoryCardTopic" + attempt.AttemptId,
            Text = topicName,
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, UITheme.Spacing12) + UITheme.Scaled(this, 44)),
            Size = new Size(UITheme.Scaled(this, 160), UITheme.Scaled(this, 16)),
        };
        var pill = new Label
        {
            Name = "HistoryCardScore" + attempt.AttemptId,
            Text = $"{attempt.Score}/{total}  ({percent:0}%)",
            Font = UITheme.BodySemibold,
            ForeColor = UITheme.TextOnAccent,
            BackColor = pillBack,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(Math.Max(card.Width - UITheme.Scaled(this, 210), 1), (card.Height - UITheme.Scaled(this, 34)) / 2),
            Size = new Size(UITheme.Scaled(this, 120), UITheme.Scaled(this, 34)),
        };
        var review = new FlatButton
        {
            Name = "HistoryCardReview" + attempt.AttemptId,
            Text = ReviewAction,
            AccessibleName = ReviewAction + " " + quiz.Title,
            Variant = UITheme.FlatButtonVariant.Secondary,
            Location = new Point(Math.Max(card.Width - UITheme.Scaled(this, 90), 1), (card.Height - UITheme.Scaled(this, 32)) / 2),
            Size = new Size(UITheme.Scaled(this, 90), UITheme.Scaled(this, 32)),
        };
        review.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<HistoryForm>() is { } form
                && button.Tag is QuizAttempt tagAttempt)
            {
                form.OpenAttemptById(tagAttempt.AttemptId);
            }
        };
        review.Tag = attempt;
        card.Click += static (sender, e) =>
        {
            if (sender is Panel panel && panel.FindAncestor<HistoryForm>() is { } form && panel.Tag is QuizAttempt tagAttempt)
            {
                form.OpenAttemptById(tagAttempt.AttemptId);
            }
        };
        card.Tag = attempt;

        card.Controls.Add(date);
        card.Controls.Add(title);
        card.Controls.Add(topic);
        card.Controls.Add(pill);
        card.Controls.Add(review);
        return card;
    }

    /// <summary>Applies the live search and the topic/score filters to the cards.</summary>
    private void ApplyFilter()
    {
        if (_rebuildingFilters)
        {
            return;
        }

        string searchText = _search.Text.Trim();
        string? topicFilterName = _topicFilter.SelectedIndex is > 0 ? _topicFilter.Items[_topicFilter.SelectedIndex]?.ToString() : null;
        int scoreBucket = _scoreFilter.SelectedIndex; // 0 any, 1 ≥80, 2 50–79, 3 <50

        IReadOnlyList<QuizAttempt> attempts = _attempts.GetByUser(_user.UserId);

        _cardFlow.SuspendLayout();
        _cardFlow.Controls.Clear();

        foreach (QuizAttempt attempt in attempts)
        {
            Quiz? quiz = _quizzes.GetById(attempt.QuizId);
            if (quiz is null)
            {
                continue; // unreachable — the FK keeps a quiz alive while attempts exist
            }

            int total = quiz.Questions.Count; // the R-2 denominator, computed live
            double percent = total == 0
                ? 0d
                : Math.Round(attempt.Score * 100d / total, MidpointRounding.AwayFromZero);

            if (searchText.Length > 0
                && !quiz.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string topicName = _topicNames.TryGetValue(quiz.TopicId, out string? name) ? name : string.Empty;
            if (topicFilterName is not null && !string.Equals(topicName, topicFilterName, StringComparison.Ordinal))
            {
                continue;
            }

            bool inBucket = scoreBucket switch
            {
                1 => percent >= 80d,
                2 => percent is >= 50d and < 80d,
                3 => percent < 50d,
                _ => true,
            };
            if (!inBucket)
            {
                continue;
            }

            _cardFlow.Controls.Add(BuildAttemptCard(attempt, quiz, topicName, total, percent));
        }

        _cardFlow.ResumeLayout();

        // The states of §5.8: empty (no attempts at all) vs filtered-empty.
        EmptyState state = attempts.Count == 0
            ? EmptyState.AllEmpty
            : _cardFlow.Controls.Count == 0 ? EmptyState.Filtered : EmptyState.None;
        ShowState(state);
    }

    /// <summary>The two §5.8 empty states.</summary>
    private enum EmptyState
    {
        None,
        AllEmpty,
        Filtered,
    }

    /// <summary>Shows the cards, or the §5.8 empty state with its "Clear filters" action.</summary>
    private void ShowState(EmptyState state)
    {
        if (state == EmptyState.None)
        {
            _emptyView.Visible = false;
            _cardFlow.Visible = true;
            return;
        }

        _cardFlow.Visible = false;
        _emptyView.Controls.Clear();

        var card = new CardPanel
        {
            Name = "HistoryEmptyCard",
            Size = new Size(UITheme.Scaled(this, 460), UITheme.Scaled(this, 140)),
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
        };
        var title = new Label
        {
            Name = "HistoryEmptyTitle",
            Text = state == EmptyState.AllEmpty ? EmptyTitle : NoMatchTitle,
            Font = UITheme.BodySemibold,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, UITheme.Spacing16)),
            Size = new Size(UITheme.Scaled(this, 428), UITheme.Scaled(this, 22)),
        };
        card.Controls.Add(title);
        _emptyView.Controls.Add(card);

        if (state == EmptyState.AllEmpty)
        {
            var caption = new Label
            {
                Name = "HistoryEmptyCaption",
                Text = EmptyCaption,
                Font = UITheme.Caption,
                ForeColor = UITheme.TextSecondary,
                BackColor = UITheme.Surface,
                AutoSize = false,
                Location = new Point(UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, UITheme.Spacing16) + UITheme.Scaled(this, 26)),
                Size = new Size(UITheme.Scaled(this, 428), UITheme.Scaled(this, 18)),
            };
            card.Controls.Add(caption);
        }
        else
        {
            var clear = new FlatButton
            {
                Name = "HistoryClearFilters",
                Text = "Clear filters",
                Variant = UITheme.FlatButtonVariant.Secondary,
                Location = new Point(UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, UITheme.Spacing16) + UITheme.Scaled(this, 30)),
                Size = new Size(UITheme.Scaled(this, 130), UITheme.Scaled(this, 32)),
            };
            clear.Click += static (sender, e) =>
            {
                if (sender is FlatButton button && button.FindAncestor<HistoryForm>() is { } form)
                {
                    form.ClearFilters();
                }
            };
            card.Controls.Add(clear);
        }

        _emptyView.Visible = true;
        _emptyView.BringToFront();
    }

    /// <summary>The filtered-empty state's action: reset search and filters.</summary>
    private void ClearFilters()
    {
        _search.ClearText();
        _topicFilter.SelectedIndex = 0;
        _scoreFilter.SelectedIndex = 0;
        ApplyFilter();
    }

    /// <summary>Opens the §5.9 review drill-in for one attempt id.</summary>
    internal void OpenAttemptById(int attemptId)
    {
        if (_reviewPanel is null)
        {
            _reviewPanel = new AttemptReviewForm(_attempts, _quizzes, _answers)
            {
                Name = "HistoryReviewPanel",
                Dock = DockStyle.Fill,
                Visible = false,
            };
            _reviewPanel.BackRequested += static (sender, e) =>
            {
                if (sender is AttemptReviewForm review && review.FindAncestor<HistoryForm>() is { } form)
                {
                    review.Visible = false;
                    form.ShowListView(visible: true);
                }
            };
            Controls.Add(_reviewPanel);
        }

        ShowListView(visible: false);
        _reviewPanel.Open(attemptId);
        _reviewPanel.Visible = true;
        _reviewPanel.BringToFront();
    }

    /// <summary>Shows or hides this module's list state (toolbar + cards).</summary>
    private void ShowListView(bool visible)
    {
        _search.Visible = visible;
        _topicFilter.Visible = visible;
        _scoreFilter.Visible = visible;
        _cardFlow.Visible = visible;
    }
}
