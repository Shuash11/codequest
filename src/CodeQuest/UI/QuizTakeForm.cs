// =============================================================================
// CodeQuest — Quiz Taking & Auto Scoring (P9): the home module of spec 5.6 and
// the result summary of spec 5.7.
// The panel has three states, hosted inside the shell like every module (§5.2):
//   1. List (§5.6 moment 1): every quiz — Approved rows selectable with a
//      trailing "Start" action, Draft rows rendered disabled (TextTertiary,
//      "Pending review" in the Questions column, Draft badge, no start) —
//      proposal line 23 enforced at the UI. When nothing is approved, the
//      screen shows the spec's drafts-only empty state instead of dead rows.
//   2. Run (§5.6 moment 2): quiz title + "Question N of M" + the determinate
//      8px progress track (answered ÷ total), the question card with one
//      ChoiceRowButton per option, Previous / Next("Submit") footer. The
//      stored selection survives Next/Previous. Leaving mid-quiz via any nav
//      item raises the §4.11 confirm dialog; Leave discards cleanly — no
//      attempt row is written (documented P9 behavior).
//   3. Result (§5.7): the hero card (score "8 / 10", percentage, topic —
//      difficulty context) and the per-question verdict list; "View answers"
//      opens the §5.9 review drill-in for this attempt, "Retake" restarts the
//      run, "Done" returns to the list with "Result saved." in the strip.
// Persistence rules (R-2 #1): the attempt row stores ONLY the correct count;
// the display denominator is the quiz's own question count, computed live.
// The one write is AttemptRepository.AddWithAnswers — one transaction, guarded
// by the data layer (R-2 #3); the submit button is disabled for its duration,
// so double submission cannot create a second attempt.
// =============================================================================

using CodeQuest.Data.Repositories;
using CodeQuest.Models;
using CodeQuest.Services;
using System.Drawing;
using System.Windows.Forms;

namespace CodeQuest.UI;

/// <summary>
/// The quiz-taking module (spec 5.6/5.7): list, run, and result in one
/// shell-hosted panel.
/// </summary>
public sealed class QuizTakeForm : ModulePanel, ILeaveGuard
{
    private const string StartAction = "Start";
    private const string EmptyTitle = "No quizzes yet.";
    private const string DraftsOnlyTitle = "Quizzes appear here once they're approved.";
    private const string ResultSavedMessage = "Result saved.";
    private const string LeaveTitle = "Leave this quiz?";
    private const string LeaveMessage = "Your answers so far will be discarded.";
    private const string LeaveConfirmText = "Leave";

    private readonly QuizRepository _quizzes;
    private readonly QuestionRepository _questions;
    private readonly TopicRepository _topics;
    private readonly AttemptRepository _attempts;
    private readonly SubmittedAnswerRepository _answers;
    private readonly IScoringService _scoring;
    private readonly UserAccount _user;

    private readonly ListCardGrid _list;
    private readonly Panel _runView;
    private readonly Label _runTitle = null!;
    private readonly Label _runProgressLabel = null!;
    private readonly ProgressTrack _runProgress = null!;
    private readonly Label _questionText = null!;
    private readonly Panel _choiceStack = null!;
    private readonly FlatButton _previousButton = null!;
    private readonly FlatButton _nextButton = null!;
    private readonly Panel _resultView;
    private readonly Label _resultScore = null!;
    private readonly Label _resultPercent = null!;
    private readonly Label _resultContext = null!;
    private readonly Panel _resultListCard = null!;
    private readonly Panel _resultActions = null!;
    private readonly FlatButton _viewAnswersButton = null!;
    private readonly FlatButton _retakeButton = null!;
    private readonly FlatButton _doneButton = null!;
    private AttemptReviewForm? _reviewPanel; // the §5.9 drill-in state, created on first open

    private Dictionary<int, string> _topicNames = new();

    // The run state (moment 2): the quiz being taken, the picked choice per
    // question index, the current question, and the submit re-entrancy guard.
    private Quiz? _runQuiz;
    private int?[] _selections = Array.Empty<int?>();
    private int _runIndex;
    private bool _submitting;
    private List<ChoiceRowButton> _choiceRows = new();

    // The result state (§5.7): what the last completed attempt produced.
    private int _resultAttemptId;
    private Quiz? _resultQuiz;
    private int _resultCorrect;
    private bool _awaitingSavedAnnouncement; // a completed attempt awaits its "Result saved." strip on the list

    /// <summary>Creates the quiz-taking module over the data layer.</summary>
    public QuizTakeForm(
        QuizRepository quizzes,
        QuestionRepository questions,
        TopicRepository topics,
        AttemptRepository attempts,
        SubmittedAnswerRepository answers,
        IScoringService scoring,
        UserAccount user)
    {
        ArgumentNullException.ThrowIfNull(quizzes);
        ArgumentNullException.ThrowIfNull(questions);
        ArgumentNullException.ThrowIfNull(topics);
        ArgumentNullException.ThrowIfNull(attempts);
        ArgumentNullException.ThrowIfNull(answers);
        ArgumentNullException.ThrowIfNull(scoring);
        ArgumentNullException.ThrowIfNull(user);
        _quizzes = quizzes;
        _questions = questions;
        _topics = topics;
        _attempts = attempts;
        _answers = answers;
        _scoring = scoring;
        _user = user;

        Dock = DockStyle.Fill;
        BackColor = UITheme.Background;

        _list = BuildListView();
        _runView = BuildRunView();
        _resultView = BuildResultView();

        Controls.Add(_list);
        Controls.Add(_runView);
        Controls.Add(_resultView);

        // The run view resizes with the module host (Dock = Fill), so its
        // internal layouts recompute on every resize.
        _runView.Resize += static (sender, e) =>
        {
            if (sender is Panel run && run.Visible && run.FindAncestor<QuizTakeForm>() is { } form)
            {
                form.LayoutRunHeader();
                form.LayoutQuestionCard();
            }
        };

        ShowList(announceCompletion: false);
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

        LoadListRows();
    }

    /// <summary>
    /// The §5.6 mid-run guard: navigation away is confirmed with the §4.11
    /// dialog; Leave discards the run cleanly (no attempt row) and allows the
    /// switch, Stay cancels it.
    /// </summary>
    public bool ConfirmLeave()
    {
        if (_runQuiz is null || _submitting)
        {
            return true; // no run in progress — leaving needs no confirmation
        }

        if (!DialogForm.ShowConfirm(LeaveTitle, LeaveMessage, LeaveConfirmText, secondaryText: "Stay"))
        {
            return false; // Stay: the run continues, the switch is cancelled
        }

        DiscardRun();
        return true;
    }

    // -------------------------------------------------------------------------
    // State 1 — the quiz list (§5.6 moment 1)
    // -------------------------------------------------------------------------

    /// <summary>Builds the list card of moment 1 (no toolbar — the take list has no filters).</summary>
    private ListCardGrid BuildListView()
    {
        var list = new ListCardGrid
        {
            Name = "TakeQuizListCard",
            Dock = DockStyle.Fill,
        };
        list.Grid.Name = "TakeQuizGrid";
        list.Grid.AccessibleName = "Quizzes";
        list.Grid.AddTextColumn("TakeTitle", "Title", 34f, extraLeftInset: UITheme.Scaled(this, UITheme.Spacing8), minimumWidth: 160);
        list.Grid.AddTextColumn("TakeTopic", "Topic", 16f, minimumWidth: 82);
        list.Grid.AddTextColumn("TakeDifficulty", "Difficulty", 12f, minimumWidth: 74);
        list.Grid.AddTextColumn("TakeQuestions", "Questions", 11f, minimumWidth: 110);
        list.Grid.AddBadgeColumn("TakeStatus", "Status", 96);
        list.Grid.AddActionColumn(StartAction, StartAction, GridActionKind.Secondary);
        list.Grid.IsActionEnabled = (rowIndex, actionName) =>
        {
            if (actionName != StartAction || rowIndex < 0 || rowIndex >= list.Grid.Rows.Count)
            {
                return false;
            }

            return list.Grid.Rows[rowIndex].Tag is Quiz quiz && quiz.Status == QuizStatus.Approved;
        };
        list.Grid.ActionClicked += static (sender, e) =>
        {
            if (sender is IosListGrid grid && grid.FindAncestor<QuizTakeForm>() is { } form)
            {
                form.HandleStart(e.RowIndex);
            }
        };

        return list;
    }

    /// <summary>Fills the list card from the database (§5.6 moment 1 states).</summary>
    private void LoadListRows()
    {
        IReadOnlyList<Quiz> quizzes = _quizzes.GetAll();
        bool anyApproved = quizzes.Any(quiz => quiz.Status == QuizStatus.Approved);

        _list.Grid.SuspendLayout();
        _list.Grid.Rows.Clear();

        if (anyApproved)
        {
            foreach (Quiz quiz in quizzes)
            {
                string topicName = _topicNames.TryGetValue(quiz.TopicId, out string? name) ? name : string.Empty;
                bool approved = quiz.Status == QuizStatus.Approved;
                int questionCount = _questions.GetByQuiz(quiz.QuizId).Count;

                int index = _list.Grid.Rows.Add(
                    quiz.Title,
                    topicName,
                    quiz.Difficulty.ToString(),
                    approved ? questionCount.ToString() : "Pending review", // §5.6: the Draft caption rides the disabled row
                    quiz.Status.ToString(), // the badge column paints this as a §4.10 chip
                    approved ? StartAction : string.Empty);
                _list.Grid.Rows[index].Tag = quiz;

                if (!approved)
                {
                    // §5.6: Draft rows render in TextTertiary and are not startable.
                    _list.Grid.Rows[index].DefaultCellStyle.ForeColor = UITheme.TextTertiary;
                }
            }
        }

        _list.Grid.ResumeLayout();

        // The states of §5.6 moment 1: empty (nothing at all) and drafts-only
        // (content exists but nothing is takeable yet).
        if (quizzes.Count == 0)
        {
            _list.ShowEmptyState(EmptyTitle, string.Empty);
        }
        else if (!anyApproved)
        {
            _list.ShowEmptyState(DraftsOnlyTitle, string.Empty);
        }
        else
        {
            _list.HideEmptyState();
        }
    }

    /// <summary>Starts the run for the quiz of the pressed row (§5.6 moment 2).</summary>
    private void HandleStart(int rowIndex)
    {
        if (_submitting || rowIndex < 0 || rowIndex >= _list.Grid.Rows.Count)
        {
            return;
        }

        if (_list.Grid.Rows[rowIndex].Tag is not Quiz listQuiz || listQuiz.Status != QuizStatus.Approved)
        {
            return; // a Draft row can never be started (the action is disabled anyway)
        }

        StartQuiz(listQuiz.QuizId);
    }

    // -------------------------------------------------------------------------
    // State 2 — the run (§5.6 moment 2)
    // -------------------------------------------------------------------------

    /// <summary>Builds the run view's controls once; each run fills them per question.</summary>
    private Panel BuildRunView()
    {
        var run = new Panel
        {
            Name = "TakeRunView",
            Dock = DockStyle.Fill,
            Visible = false,
            BackColor = UITheme.Background,
        };

        var footer = new Panel
        {
            Name = "RunFooter",
            Dock = DockStyle.Bottom,
            Height = UITheme.Scaled(this, 32),
            BackColor = UITheme.Background,
        };
        _previousButton = new FlatButton
        {
            Name = "PreviousButton",
            Text = "Previous",
            Variant = UITheme.FlatButtonVariant.Secondary,
            Location = new Point(0, 0),
            Size = new Size(UITheme.Scaled(this, 110), UITheme.Scaled(this, 32)),
        };
        _previousButton.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<QuizTakeForm>() is { } form)
            {
                form.MoveQuestion(-1);
            }
        };
        _nextButton = new FlatButton
        {
            Name = "NextButton",
            Text = "Next",
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing12), 0),
            Size = new Size(UITheme.Scaled(this, 110), UITheme.Scaled(this, 32)),
        };
        _nextButton.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<QuizTakeForm>() is { } form)
            {
                form.HandleNext();
            }
        };
        footer.Controls.Add(_previousButton);
        footer.Controls.Add(_nextButton);

        _runTitle = new Label
        {
            Name = "RunQuizTitle",
            Font = UITheme.BodySemibold,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Background,
            AutoSize = false,
        };
        _runProgressLabel = new Label
        {
            Name = "RunQuestionCount",
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Background,
            AutoSize = false,
        };
        _runProgress = new ProgressTrack { Name = "RunProgressTrack", Dock = DockStyle.Top };

        var header = new Panel
        {
            Name = "RunHeader",
            Dock = DockStyle.Top,
            Height = UITheme.Scaled(this, 52),
            BackColor = UITheme.Background,
        };
        header.Controls.Add(_runTitle);
        header.Controls.Add(_runProgressLabel);

        var card = new CardPanel { Name = "RunQuestionCard", Dock = DockStyle.Fill };
        _questionText = new Label
        {
            Name = "RunQuestionText",
            Font = UITheme.BodySemibold,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Surface,
            AutoSize = false,
        };
        _choiceStack = new Panel { Name = "RunChoiceStack", BackColor = UITheme.Surface, AutoScroll = true };
        card.Controls.Add(_questionText);
        card.Controls.Add(_choiceStack);

        // Dock layout processes the LAST control first: the footer claims the
        // bottom strip, the header the top, the progress track the next top
        // strip, and the question card fills the rest between them (§3.3: the
        // footer is part of the content flow, not the window bottom).
        run.Controls.Add(card);
        run.Controls.Add(_runProgress);
        run.Controls.Add(header);
        run.Controls.Add(footer);
        return run;
    }

    /// <summary>Lays out the run header's two labels (title leading, count under it).</summary>
    private void LayoutRunHeader()
    {
        if (_runTitle.Parent is not Panel header)
        {
            return;
        }

        int titleHeight = UITheme.Scaled(this, 22);
        _runTitle.Location = new Point(0, 0);
        _runTitle.Size = new Size(header.Width, titleHeight);
        _runProgressLabel.Location = new Point(0, titleHeight + UITheme.Scaled(this, UITheme.Spacing4));
        _runProgressLabel.Size = new Size(header.Width, UITheme.Scaled(this, 18));
    }

    /// <summary>
    /// Lays out the question card's text and choice rows (§5.6 moment 2). The
    /// choice stack is sized to the card's remaining space and the rows are
    /// positioned relative to it — an unsized container would clip the rows
    /// to the Panel's default 200×100 bounds.
    /// </summary>
    private void LayoutQuestionCard()
    {
        if (_questionText.Parent is not CardPanel card)
        {
            return;
        }

        int inset = UITheme.Scaled(this, UITheme.Spacing16);
        int contentWidth = Math.Max(card.Width - inset * 2, 1);
        int textHeight = TextRenderer.MeasureText(
            _questionText.Text.Length == 0 ? " " : _questionText.Text,
            _questionText.Font,
            new Size(contentWidth, int.MaxValue),
            TextFormatFlags.WordBreak) // multi-line questions wrap — measure wrapped (M2)
            .Height;

        _questionText.Location = new Point(inset, inset);
        _questionText.Size = new Size(contentWidth, textHeight);

        int stackTop = inset + textHeight + UITheme.Scaled(this, UITheme.Spacing12);
        _choiceStack.Location = new Point(inset, stackTop);
        _choiceStack.Size = new Size(contentWidth, Math.Max(card.Height - stackTop - inset, 1));

        int top = 0;
        int rowHeight = UITheme.Scaled(this, 36);
        int rowGap = UITheme.Scaled(this, UITheme.Spacing8);
        foreach (ChoiceRowButton row in _choiceRows)
        {
            row.Location = new Point(0, top); // stack-relative — the stack clips at the card's content edge
            row.Size = new Size(_choiceStack.Width, rowHeight);
            top += rowHeight + rowGap;
        }
    }

    /// <summary>Loads the quiz and enters the run (used by Start and Retake).</summary>
    private void StartQuiz(int quizId)
    {
        Quiz? quiz = _quizzes.GetById(quizId);
        if (quiz is null || quiz.Status != QuizStatus.Approved)
        {
            return; // unreachable from the UI (only Approved rows start); a defensive guard
        }

        if (quiz.Questions.Count == 0)
        {
            ReportStatus("This quiz has no questions yet.", success: false);
            return;
        }

        _runQuiz = quiz;
        _selections = new int?[quiz.Questions.Count];
        _runIndex = 0;

        _runTitle.Text = quiz.Title;
        ShowView(_runView);
        RenderQuestion();
    }

    /// <summary>Discards the run state (the §5.6 Leave path — no attempt row is written).</summary>
    private void DiscardRun()
    {
        _runQuiz = null;
        _selections = Array.Empty<int?>();
        _runIndex = 0;
        _choiceRows = new List<ChoiceRowButton>();
        ShowList(announceCompletion: false);
    }

    /// <summary>Renders the current question: header, progress, text, choices, buttons.</summary>
    private void RenderQuestion()
    {
        if (_runQuiz is null)
        {
            return;
        }

        Question question = _runQuiz.Questions[_runIndex];

        _runProgressLabel.Text = $"Question {_runIndex + 1} of {_runQuiz.Questions.Count}";
        UpdateRunProgress();
        _questionText.Text = question.QuestionText;

        _choiceStack.SuspendLayout();
        foreach (ChoiceRowButton existing in _choiceRows)
        {
            existing.Dispose();
        }
        _choiceStack.Controls.Clear();
        _choiceRows = new List<ChoiceRowButton>();

        foreach (Choice choice in question.Choices.OrderBy(item => item.Position))
        {
            var row = new ChoiceRowButton
            {
                Name = $"ChoiceRow{choice.ChoiceId}",
                ChoiceText = choice.ChoiceText,
                IsSelected = _selections[_runIndex] == choice.ChoiceId,
                Tag = choice.ChoiceId,
            };
            row.Click += static (sender, e) =>
            {
                if (sender is ChoiceRowButton choiceRow && choiceRow.FindAncestor<QuizTakeForm>() is { } form)
                {
                    form.SelectChoice(choiceRow);
                }
            };
            _choiceStack.Controls.Add(row);
            _choiceRows.Add(row);
        }
        _choiceStack.ResumeLayout();

        LayoutQuestionCard();

        _previousButton.Enabled = _runIndex > 0; // §5.6: disabled on question 1
        bool last = _runIndex == _runQuiz.Questions.Count - 1;
        _nextButton.Text = last ? "Submit" : "Next";
        LayoutRunHeader();
    }

    /// <summary>Updates the determinate progress from the answered count (§5.6).</summary>
    private void UpdateRunProgress()
    {
        if (_runQuiz is null)
        {
            return;
        }

        _runProgress.Value = _selections.Count(selection => selection is not null) / (double)_runQuiz.Questions.Count;
    }

    /// <summary>Moves the selection to the pressed row (§5.6: one selection, no toggle-off).</summary>
    private void SelectChoice(ChoiceRowButton row)
    {
        if (_runQuiz is null || _submitting || row.Tag is not int choiceId)
        {
            return;
        }

        _selections[_runIndex] = choiceId;
        foreach (ChoiceRowButton other in _choiceRows)
        {
            other.IsSelected = other == row;
        }

        UpdateRunProgress();
    }

    /// <summary>Previous/Next navigation; Next on the final question submits (§5.6).</summary>
    private void MoveQuestion(int delta)
    {
        if (_runQuiz is null || _submitting)
        {
            return;
        }

        int target = _runIndex + delta;
        if (target < 0 || target >= _runQuiz.Questions.Count)
        {
            return;
        }

        _runIndex = target;
        RenderQuestion();
    }

    /// <summary>Next on the final question is Submit (§5.6); otherwise it navigates.</summary>
    private void HandleNext()
    {
        if (_runQuiz is null || _submitting)
        {
            return;
        }

        if (_runIndex < _runQuiz.Questions.Count - 1)
        {
            MoveQuestion(1);
            return;
        }

        Submit();
    }

    /// <summary>
    /// The one write of the run (§5.6): score the selections, store the attempt
    /// and its answers in one transaction, and show the result (§5.7). The
    /// buttons stay disabled for the write's duration, so a double submission
    /// cannot create a second attempt; a database failure surfaces through the
    /// application's §4.11 dialog — it is not a scoring decision.
    /// </summary>
    private void Submit()
    {
        if (_runQuiz is null || _submitting)
        {
            return;
        }

        _submitting = true;
        _previousButton.Enabled = false;
        _nextButton.Enabled = false;

        Quiz quiz = _runQuiz;
        var answers = new List<SubmittedAnswer>();
        for (int i = 0; i < quiz.Questions.Count; i++)
        {
            if (_selections[i] is not { } choiceId)
            {
                continue; // an unanswered question contributes no answer row
            }

            answers.Add(new SubmittedAnswer
            {
                QuestionId = quiz.Questions[i].QuestionId,
                ChosenChoiceId = choiceId,
            });
        }

        try
        {
            QuizScoreResult score = _scoring.Score(quiz, answers);
            var attempt = new QuizAttempt
            {
                QuizId = quiz.QuizId,
                UserId = _user.UserId,
                Score = score.CorrectCount, // R-2 #1: only the correct count is stored
                AttemptedAt = DateTimeOffset.UtcNow,
            };
            _attempts.AddWithAnswers(attempt, answers);

            _resultAttemptId = attempt.AttemptId;
            _resultQuiz = quiz;
            _resultCorrect = score.CorrectCount;
            _awaitingSavedAnnouncement = true;
            _runQuiz = null; // the run is over — the leave guard no longer applies
            ShowResult();
        }
        finally
        {
            _submitting = false;
            _previousButton.Enabled = _runIndex > 0;
            _nextButton.Enabled = true;
        }
    }

    // -------------------------------------------------------------------------
    // State 3 — the result summary (§5.7)
    // -------------------------------------------------------------------------

    /// <summary>Builds the result view's controls once; each attempt fills them.</summary>
    private Panel BuildResultView()
    {
        var result = new Panel
        {
            Name = "TakeResultView",
            Dock = DockStyle.Fill,
            Visible = false,
            BackColor = UITheme.Background,
            AutoScroll = true,
        };

        int cardWidth = UITheme.Scaled(this, 560);
        var hero = new CardPanel
        {
            Name = "ResultHeroCard",
            Location = new Point(0, 0),
            Size = new Size(cardWidth, UITheme.Scaled(this, 112)),
        };
        _resultScore = new Label
        {
            Name = "ResultScoreLabel",
            Font = UITheme.Title,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Surface,
            AutoSize = true,
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, UITheme.Spacing16)),
        };
        _resultPercent = new Label
        {
            Name = "ResultPercentLabel",
            Font = UITheme.Body,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Surface,
            AutoSize = true,
        };
        _resultContext = new Label
        {
            Name = "ResultContextLabel",
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Surface,
            AutoSize = true,
            Location = new Point(
                UITheme.Scaled(this, UITheme.Spacing16),
                UITheme.Scaled(this, UITheme.Spacing16) + UITheme.Scaled(this, 52)),
        };
        hero.Controls.Add(_resultScore);
        hero.Controls.Add(_resultPercent);
        hero.Controls.Add(_resultContext);
        result.Controls.Add(hero);

        _resultListCard = new CardPanel
        {
            Name = "ResultListCard",
            Location = new Point(0, hero.Bottom + UITheme.Scaled(this, UITheme.Spacing16)),
            Size = new Size(cardWidth, UITheme.Scaled(this, 100)),
        };
        result.Controls.Add(_resultListCard);

        var actions = _resultActions = new Panel
        {
            Name = "ResultActions",
            Location = new Point(0, _resultListCard.Bottom + UITheme.Scaled(this, UITheme.Spacing16)),
            Size = new Size(cardWidth, UITheme.Scaled(this, 32)),
        };
        int buttonWidth = UITheme.Scaled(this, 110);
        int buttonHeight = UITheme.Scaled(this, 32);
        _viewAnswersButton = new FlatButton
        {
            Name = "ViewAnswersButton",
            Text = "View answers",
            Variant = UITheme.FlatButtonVariant.Secondary,
            Location = new Point(0, 0),
            Size = new Size(UITheme.Scaled(this, 140), buttonHeight),
        };
        _viewAnswersButton.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<QuizTakeForm>() is { } form)
            {
                form.OpenReview();
            }
        };
        _retakeButton = new FlatButton
        {
            Name = "RetakeButton",
            Text = "Retake",
            Location = new Point(UITheme.Scaled(this, 140) + UITheme.Scaled(this, UITheme.Spacing12), 0),
            Size = new Size(buttonWidth, buttonHeight),
        };
        _retakeButton.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<QuizTakeForm>() is { } form)
            {
                form.Retake();
            }
        };
        _doneButton = new FlatButton
        {
            Name = "DoneButton",
            Text = "Done",
            Variant = UITheme.FlatButtonVariant.Secondary,
            Location = new Point(UITheme.Scaled(this, 140) + buttonWidth + UITheme.Scaled(this, 12) + UITheme.Scaled(this, UITheme.Spacing12), 0),
            Size = new Size(buttonWidth, buttonHeight),
        };
        _doneButton.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<QuizTakeForm>() is { } form)
            {
                form.ShowList(announceCompletion: true);
            }
        };
        actions.Controls.Add(_viewAnswersButton);
        actions.Controls.Add(_retakeButton);
        actions.Controls.Add(_doneButton);
        result.Controls.Add(actions);

        return result;
    }

    /// <summary>Fills the result view from the completed attempt (§5.7).</summary>
    private void ShowResult()
    {
        if (_resultQuiz is null)
        {
            return;
        }

        int total = _resultQuiz.Questions.Count; // the R-2 denominator: the quiz's own question count
        double percent = total == 0
            ? 0d
            : Math.Round(_resultCorrect * 100d / total, MidpointRounding.AwayFromZero);
        string topicName = _topicNames.TryGetValue(_resultQuiz.TopicId, out string? name) ? name : string.Empty;

        _resultScore.Text = $"{_resultCorrect} / {total}";
        _resultPercent.Text = $"{percent:0}%";
        _resultPercent.Location = new Point(
            _resultScore.Right + UITheme.Scaled(this, UITheme.Spacing12),
            UITheme.Scaled(this, UITheme.Spacing16) + UITheme.Scaled(this, 12));
        _resultContext.Text = $"{topicName} \u2014 {_resultQuiz.Difficulty}";

        BuildResultRows(_resultQuiz);
        _resultActions.Location = new Point(0, _resultListCard.Bottom + UITheme.Scaled(this, UITheme.Spacing16));
        ShowView(_resultView);
    }

    /// <summary>Builds the §5.7 per-question verdict rows (glyph + word, never color alone).</summary>
    private void BuildResultRows(Quiz quiz)
    {
        _resultListCard.SuspendLayout();
        foreach (Control existing in _resultListCard.Controls)
        {
            existing.Dispose();
        }
        _resultListCard.Controls.Clear();

        int inset = UITheme.Scaled(this, UITheme.Spacing16);
        int rowHeight = UITheme.Scaled(this, 44);
        int top = inset;

        for (int i = 0; i < quiz.Questions.Count; i++)
        {
            Question question = quiz.Questions[i];
            bool answered = _selections.Length > i && _selections[i] is { } chosenId;
            bool correct = answered
                && question.IsCorrectChoice(question.Choices.FirstOrDefault(choice => choice.ChoiceId == chosenId));

            var row = new Panel
            {
                Name = $"ResultRow{i}",
                Location = new Point(inset, top),
                Size = new Size(Math.Max(_resultListCard.Width - inset * 2, 1), rowHeight),
                BackColor = UITheme.Surface,
            };

            Color verdictColor = correct ? UITheme.SuccessText : UITheme.DangerText;
            var mark = new Label
            {
                Name = $"ResultMark{i}",
                Text = correct ? "\uE73E" : "\uE711", // the check / cross glyph (§5.7: shape + word)
                Font = UITheme.NavGlyph,
                ForeColor = verdictColor,
                BackColor = UITheme.Surface,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 0),
                Size = new Size(UITheme.Scaled(this, 24), rowHeight),
            };
            var verdict = new Label
            {
                Name = $"ResultVerdict{i}",
                Text = correct ? "Correct" : "Incorrect",
                Font = UITheme.Body,
                ForeColor = verdictColor,
                BackColor = UITheme.Surface,
                AutoSize = true,
                Location = new Point(UITheme.Scaled(this, 24), 0),
                Size = new Size(UITheme.Scaled(this, 90), rowHeight),
                TextAlign = ContentAlignment.MiddleLeft,
            };
            var number = new Label
            {
                Name = $"ResultQuestionNumber{i}",
                Text = $"Question {i + 1}",
                Font = UITheme.Caption,
                ForeColor = UITheme.TextSecondary,
                BackColor = UITheme.Surface,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(UITheme.Scaled(this, 24) + UITheme.Scaled(this, 90), 0),
                Size = new Size(UITheme.Scaled(this, 96), rowHeight),
            };
            var detail = new Label
            {
                Name = $"ResultQuestionText{i}",
                Text = question.QuestionText,
                Font = UITheme.Body,
                ForeColor = UITheme.TextPrimary,
                BackColor = UITheme.Surface,
                AutoSize = false,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(UITheme.Scaled(this, 24) + UITheme.Scaled(this, 90) + UITheme.Scaled(this, 96), 0),
                Size = new Size(
                    Math.Max(row.Width - UITheme.Scaled(this, 24) - UITheme.Scaled(this, 90) - UITheme.Scaled(this, 96), 1),
                    rowHeight),
            };
            row.Controls.Add(mark);
            row.Controls.Add(verdict);
            row.Controls.Add(number);
            row.Controls.Add(detail);
            _resultListCard.Controls.Add(row);
            top += rowHeight;

            if (i < quiz.Questions.Count - 1)
            {
                var separator = new Panel
                {
                    Name = $"ResultSeparator{i}",
                    Location = new Point(inset, top),
                    Size = new Size(Math.Max(_resultListCard.Width - inset * 2, 1), UITheme.Scaled(this, 1)),
                    BackColor = UITheme.SeparatorHairline,
                };
                _resultListCard.Controls.Add(separator);
                top += UITheme.Scaled(this, 1);
            }
        }

        _resultListCard.ResumeLayout();
        _resultListCard.Height = top + inset;
    }

    /// <summary>Opens the §5.9 review drill-in for the completed attempt.</summary>
    private void OpenReview()
    {
        if (_resultAttemptId == 0)
        {
            return;
        }

        if (_reviewPanel is null)
        {
            _reviewPanel = new AttemptReviewForm(_attempts, _quizzes, _answers)
            {
                Name = "TakeResultReviewPanel",
                Dock = DockStyle.Fill,
                Visible = false,
            };
            _reviewPanel.BackRequested += static (sender, e) =>
            {
                if (sender is AttemptReviewForm review && review.FindAncestor<QuizTakeForm>() is { } form)
                {
                    review.Visible = false;
                    form.ShowResultFromReview();
                }
            };
            Controls.Add(_reviewPanel);
        }

        ShowDrillIn(visible: false);
        _reviewPanel.Open(_resultAttemptId);
        _reviewPanel.Visible = true;
        _reviewPanel.BringToFront();
    }

    /// <summary>Returns from the review drill-in to the result view (state intact).</summary>
    private void ShowResultFromReview()
    {
        ShowView(_resultView);
    }

    /// <summary>Restarts the run with the same quiz (§5.7's "Retake").</summary>
    private void Retake()
    {
        if (_resultQuiz is null)
        {
            return;
        }

        StartQuiz(_resultQuiz.QuizId);
    }

    // -------------------------------------------------------------------------
    // Shared state switching
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns to the list (moment 1). After a completed attempt the strip
    /// carries the spec's "Result saved." line (§5.6 moment 1 feedback).
    /// </summary>
    private void ShowList(bool announceCompletion)
    {
        bool announce = announceCompletion && _awaitingSavedAnnouncement;
        RefreshData();

        if (announce)
        {
            _awaitingSavedAnnouncement = false; // consumed — the completion's strip was shown
            ReportStatus(ResultSavedMessage, success: true);
        }

        ShowView(_list);
    }

    /// <summary>Shows exactly one of the panel's three states (list, run, result).</summary>
    private void ShowView(Control view)
    {
        foreach (Control child in Controls)
        {
            if (child is not AttemptReviewForm) // the review drill-in manages its own visibility
            {
                child.Visible = child == view;
            }
        }
    }

    /// <summary>Hides the three states while the review drill-in is open.</summary>
    private void ShowDrillIn(bool visible)
    {
        _list.Visible = visible;
        _runView.Visible = visible;
        _resultView.Visible = visible;
    }
}
