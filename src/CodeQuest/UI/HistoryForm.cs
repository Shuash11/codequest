// =============================================================================
// CodeQuest — Quiz History & Answer Review (P10): the history list of spec
// 5.8 with its review drill-in.
// Layout (§5.8): header "History"; toolbar — search ("Search quizzes…"),
// topic filter (recipe 4.5 combo), score filter (Any score / 80% and above /
// 50–79% / Below 50%); grid card: Date — Quiz — Topic — Score ("8/10 (80%)").
// Rows open the §5.9 attempt review (trailing "Review" action or a
// double-click); the drill-in keeps the search/filter state while hidden and
// "Back" restores it (the §5.5 drill-in discipline).
// Derived data (R-2 #1): the score denominator is the quiz's own question
// count, computed live (the stored attempt row holds the correct count only).
// Offline note (proposal line 23): this screen is fully local — no network
// dependency; it must work with the cable unplugged (P10 acceptance).
// Read-only by design: no CRUD feedback lines on this screen (§5.8).
// =============================================================================

using CodeQuest.Data.Repositories;
using CodeQuest.Models;
using System.Drawing;
using System.Windows.Forms;

namespace CodeQuest.UI;

/// <summary>
/// The history module (spec 5.8): past attempts with search, filters, and the
/// answer-review drill-in.
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
    private readonly ListCardGrid _list = null!;
    private AttemptReviewForm? _reviewPanel; // the §5.9 drill-in state, created on first open

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

        BackColor = UITheme.Background;

        int toolbarHeight = UITheme.Scaled(this, 32);
        int thirdWidth = UITheme.Scaled(this, 180);
        int inset = UITheme.Scaled(this, UITheme.Spacing8);

        _search = new InputField(accessibleName: "Search history")
        {
            Name = "HistorySearch",
            PlaceholderText = "Search quizzes\u2026",
            Location = new Point(0, 0),
            Size = new Size(thirdWidth + UITheme.Scaled(this, 60), toolbarHeight),
        };
        _search.TextChanged += static (sender, e) =>
        {
            if (sender is InputField field && field.FindAncestor<HistoryForm>() is { } form)
            {
                form.ApplyFilter();
            }
        };

        _topicFilter = new ThemedComboBox
        {
            Name = "HistoryTopicFilter",
            Location = new Point(_search.Right + inset, 0),
            Size = new Size(thirdWidth, toolbarHeight),
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        _topicFilter.SelectedIndexChanged += static (sender, e) =>
        {
            if (sender is ThemedComboBox combo && combo.FindAncestor<HistoryForm>() is { } form)
            {
                form.ApplyFilter();
            }
        };

        _scoreFilter = new ThemedComboBox
        {
            Name = "HistoryScoreFilter",
            Location = new Point(_topicFilter.Right + inset, 0),
            Size = new Size(thirdWidth, toolbarHeight),
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        _scoreFilter.SelectedIndexChanged += static (sender, e) =>
        {
            if (sender is ThemedComboBox combo && combo.FindAncestor<HistoryForm>() is { } form)
            {
                form.ApplyFilter();
            }
        };
        _scoreFilter.Items.Add(AnyScoreLabel);
        _scoreFilter.Items.Add("80% and above");
        _scoreFilter.Items.Add("50\u201379%");
        _scoreFilter.Items.Add("Below 50%");
        _scoreFilter.SelectedIndex = 0;

        _list = new ListCardGrid
        {
            Name = "HistoryListCard",
            Location = new Point(0, toolbarHeight + UITheme.Scaled(this, UITheme.Spacing16)),
            Size = new Size(Width, Math.Max(Height - toolbarHeight - UITheme.Scaled(this, UITheme.Spacing16), 1)),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
        };
        _list.Grid.Name = "HistoryGrid";
        _list.Grid.AccessibleName = "Quiz history";
        _list.Grid.AddTextColumn("HistoryDate", "Date", 15f, extraLeftInset: UITheme.Scaled(this, UITheme.Spacing8), minimumWidth: 108);
        _list.Grid.AddTextColumn("HistoryQuiz", "Quiz", 34f, minimumWidth: 160);
        _list.Grid.AddTextColumn("HistoryTopic", "Topic", 16f, minimumWidth: 82);
        _list.Grid.AddTextColumn("HistoryScore", "Score", 12f, minimumWidth: 96);
        _list.Grid.AddActionColumn(ReviewAction, ReviewAction, GridActionKind.Secondary);
        _list.Grid.ActionClicked += static (sender, e) =>
        {
            if (sender is IosListGrid grid && grid.FindAncestor<HistoryForm>() is { } form)
            {
                form.OpenAttempt(e.RowIndex);
            }
        };
        _list.Grid.CellDoubleClick += static (sender, e) =>
        {
            if (sender is IosListGrid grid && grid.FindAncestor<HistoryForm>() is { } form
                && e.RowIndex >= 0)
            {
                form.OpenAttempt(e.RowIndex);
            }
        };
        _list.EmptyActionClicked += static (sender, e) =>
        {
            if (sender is ListCardGrid card && card.FindAncestor<HistoryForm>() is { } form)
            {
                form.ClearFilters(); // the filtered-empty state's "Clear filters"
            }
        };

        Controls.Add(_search);
        Controls.Add(_topicFilter);
        Controls.Add(_scoreFilter);
        Controls.Add(_list);
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

    /// <summary>Rebuilds the topic filter items ("All topics" + one per topic).</summary>
    private void RebuildTopicFilter()
    {
        int previous = _topicFilter.SelectedIndex;

        _topicFilter.Items.Clear();
        _topicFilter.Items.Add(AllTopicsLabel);
        foreach ((int _, string name) in _topicNames.OrderBy(pair => pair.Value))
        {
            _topicFilter.Items.Add(name);
        }

        _topicFilter.SelectedIndex = previous >= 0 && previous < _topicFilter.Items.Count ? previous : 0;
    }

    /// <summary>Applies the live search and the topic/score filters to the grid.</summary>
    private void ApplyFilter()
    {
        string searchText = _search.Text.Trim();
        string? topicFilterName = _topicFilter.SelectedIndex is > 0 ? _topicFilter.Items[_topicFilter.SelectedIndex].ToString() : null;
        int scoreBucket = _scoreFilter.SelectedIndex; // 0 any, 1 ≥80, 2 50–79, 3 <50

        IReadOnlyList<QuizAttempt> attempts = _attempts.GetByUser(_user.UserId);

        _list.Grid.SuspendLayout();
        _list.Grid.Rows.Clear();

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

            // §5.8's three filters: quiz-title search, topic, score bucket.
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

            int index = _list.Grid.Rows.Add(
                UiFormat.FormatDate(attempt.AttemptedAt),
                quiz.Title,
                topicName,
                $"{attempt.Score}/{total} ({percent:0}%)",
                ReviewAction);
            _list.Grid.Rows[index].Tag = attempt;
        }

        _list.Grid.ResumeLayout();

        // The states of §5.8: empty (no attempts at all) vs filtered-empty.
        bool unfiltered = searchText.Length == 0 && topicFilterName is null && scoreBucket == 0;
        if (_list.Grid.Rows.Count == 0)
        {
            if (unfiltered)
            {
                _list.ShowEmptyState(EmptyTitle, EmptyCaption);
            }
            else
            {
                _list.ShowEmptyState(NoMatchTitle, string.Empty, "Clear filters");
            }
        }
        else
        {
            _list.HideEmptyState();
        }
    }

    /// <summary>The filtered-empty state's action: reset search and filters.</summary>
    private void ClearFilters()
    {
        _search.ClearText();
        _topicFilter.SelectedIndex = 0;
        _scoreFilter.SelectedIndex = 0;
        ApplyFilter();
    }

    /// <summary>Opens the §5.9 review drill-in for the attempt of the pressed row.</summary>
    private void OpenAttempt(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _list.Grid.Rows.Count)
        {
            return;
        }

        if (_list.Grid.Rows[rowIndex].Tag is not QuizAttempt attempt)
        {
            return;
        }

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
        _reviewPanel.Open(attempt.AttemptId);
        _reviewPanel.Visible = true;
        _reviewPanel.BringToFront();
    }

    /// <summary>Shows or hides this module's list state (toolbar + grid).</summary>
    private void ShowListView(bool visible)
    {
        _search.Visible = visible;
        _topicFilter.Visible = visible;
        _scoreFilter.Visible = visible;
        _list.Visible = visible;
    }
}
