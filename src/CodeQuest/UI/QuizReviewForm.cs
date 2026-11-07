// =============================================================================
// CodeQuest — AI Quiz Review / Approve (I4): the review screen of spec 5.12.
// List view: the explaining caption, then the draft list card (§4.6): Title —
// Topic — Difficulty — Questions — Generated — Draft badge, with the trailing
// "Review" action (the Accent text action — the grid's primary style).
// Review view (drill-in): header (Back + quiz title + Draft badge) with the
// actions row — "Approve" (primary; disabled while the quiz has zero
// questions, with the inline hint), "Edit questions" (the §5.5 editor, the
// reviewer's correct-it path), "Discard" (destructive, behind the §4.11
// confirm) — then the AI's questions as cards: question text, the choice rows
// with the AI's claimed correct answer highlighted (AccentTint + the "AI's
// answer" label — the claim is highlighted, NOT validated), and the
// explanation inset ("(No explanation was provided.)" when empty, R-2 #6).
// Approve flips the status Draft → Approved ONLY (R-2 #4: no audit fields),
// the badge flips in place (the state change is the moment), the strip says
// "Quiz approved.", and the quiz becomes takeable in the take-quiz list.
// Discard permanently deletes the draft (a draft has no attempts, so the FK
// never blocks — a blocked delete still surfaces as a friendly message).
// =============================================================================

using CodeQuest.Data;
using CodeQuest.Data.Repositories;
using CodeQuest.Models;
using System.Drawing;
using System.Windows.Forms;

namespace CodeQuest.UI;

/// <summary>
/// The review module (spec 5.12): the draft list and the review/approve
/// drill-in.
/// </summary>
public sealed class QuizReviewForm : ModulePanel
{
    private const string ReviewAction = "Review";
    private const string ListCaption = "AI-generated quizzes are checked here before they can be taken.";
    private const string EmptyTitle = "No quizzes to review";
    private const string EmptyCaption = "Generated quizzes appear here as drafts.";
    private const string ApproveButton = "Approve";
    private const string EditQuestionsButton = "Edit questions";
    private const string DiscardButton = "Discard";
    private const string ZeroQuestionHint = "Add at least one question before approving.";
    private const string DiscardTitle = "Discard this quiz?";
    private const string DiscardMessage = "The quiz and all its questions will be permanently deleted.";
    private const string ApprovedMessage = "Quiz approved.";
    private const string AiAnswerLabel = "AI's answer";
    private const string NoExplanationText = "(No explanation was provided.)";

    private readonly QuizRepository _quizzes;
    private readonly TopicRepository _topics;
    private readonly QuestionRepository _questions;
    private readonly ChoiceRepository _choices;
    private readonly SubmittedAnswerRepository _answers;

    private readonly Label _listCaption = null!;
    private readonly ListCardGrid _list = null!;
    // The review view's controls are assigned in BuildReviewView (called from
    // the constructor) — readonly fields cannot be assigned from a method.
    private readonly Panel _reviewView = null!;
    private Label _reviewTitle = null!;
    private StatusBadge _reviewBadge = null!;
    private FlatButton _approve = null!;
    private Label _approveHint = null!;
    private Panel _questionStack = null!;
    private QuestionEditForm? _editor; // the §5.5 drill-in, created on first open
    private int _openQuizId;
    private int _lastStackWidth; // the width the question cards were last built at (the H1 resize rebuild)

    private Dictionary<int, string> _topicNames = new();

    /// <summary>Creates the review module over the data layer.</summary>
    public QuizReviewForm(
        QuizRepository quizzes,
        TopicRepository topics,
        QuestionRepository questions,
        ChoiceRepository choices,
        SubmittedAnswerRepository answers)
    {
        ArgumentNullException.ThrowIfNull(quizzes);
        ArgumentNullException.ThrowIfNull(topics);
        ArgumentNullException.ThrowIfNull(questions);
        ArgumentNullException.ThrowIfNull(choices);
        ArgumentNullException.ThrowIfNull(answers);
        _quizzes = quizzes;
        _topics = topics;
        _questions = questions;
        _choices = choices;
        _answers = answers;

        Dock = DockStyle.Fill;
        BackColor = UITheme.Background;

        int captionHeight = UITheme.Scaled(this, 20);
        _listCaption = new Label
        {
            Name = "ReviewListCaption",
            Text = ListCaption,
            Font = UITheme.Body,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Background,
            AutoSize = false,
            Location = new Point(0, 0),
            Size = new Size(Math.Max(Width, 1), captionHeight),
        };

        _list = new ListCardGrid
        {
            Name = "ReviewListCard",
            Location = new Point(0, captionHeight + UITheme.Scaled(this, UITheme.Spacing8)),
            Size = new Size(Math.Max(Width, 1), Math.Max(Height - captionHeight - UITheme.Scaled(this, UITheme.Spacing8), 1)),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
        };
        _list.Grid.Name = "ReviewGrid";
        _list.Grid.AccessibleName = "Drafts to review";
        _list.Grid.AddTextColumn("ReviewTitle", "Title", 32f, extraLeftInset: UITheme.Scaled(this, UITheme.Spacing8), minimumWidth: 150);
        _list.Grid.AddTextColumn("ReviewTopic", "Topic", 15f, minimumWidth: 82);
        _list.Grid.AddTextColumn("ReviewDifficulty", "Difficulty", 11f, minimumWidth: 74);
        _list.Grid.AddTextColumn("ReviewQuestions", "Questions", 10f, minimumWidth: 74);
        _list.Grid.AddTextColumn("ReviewGenerated", "Generated", 14f, minimumWidth: 108);
        _list.Grid.AddBadgeColumn("ReviewStatus", "Status", 96);
        _list.Grid.AddActionColumn(ReviewAction, ReviewAction, GridActionKind.Secondary);
        _list.Grid.ActionClicked += static (sender, e) =>
        {
            if (sender is IosListGrid grid && grid.FindAncestor<QuizReviewForm>() is { } form)
            {
                form.OpenReview(e.RowIndex);
            }
        };

        _reviewView = BuildReviewView();

        Controls.Add(_list);
        Controls.Add(_listCaption);
        Controls.Add(_reviewView);
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

        _listCaption.Width = Math.Max(Width, 1); // the caption was built at the pre-layout width (M6)

        LoadDraftRows();
    }

    /// <summary>Keeps the list caption full-width as the module resizes (M6).</summary>
    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        _listCaption.Width = Math.Max(Width, 1);
    }

    /// <summary>
    /// The generation screen's deep-link: opens the §5.12 review view for one
    /// freshly generated draft.
    /// </summary>
    internal void OpenQuiz(int quizId)
    {
        ShowListView(visible: false);
        OpenReviewView(quizId);
    }

    /// <summary>Fills the draft list from the database (spec 5.12 list view).</summary>
    private void LoadDraftRows()
    {
        IReadOnlyList<Quiz> drafts = _quizzes.GetByStatus(QuizStatus.Draft);

        _list.Grid.SuspendLayout();
        _list.Grid.Rows.Clear();

        foreach (Quiz quiz in drafts)
        {
            string topicName = _topicNames.TryGetValue(quiz.TopicId, out string? name) ? name : string.Empty;
            int questionCount = _questions.GetByQuiz(quiz.QuizId).Count;

            int index = _list.Grid.Rows.Add(
                quiz.Title,
                topicName,
                quiz.Difficulty.ToString(),
                questionCount.ToString(),
                UiFormat.FormatDate(quiz.CreatedAt),
                quiz.Status.ToString(), // the badge column paints this as a §4.10 chip
                ReviewAction);
            _list.Grid.Rows[index].Tag = quiz;
        }

        _list.Grid.ResumeLayout();

        if (_list.Grid.Rows.Count == 0)
        {
            _list.ShowEmptyState(EmptyTitle, EmptyCaption);
        }
        else
        {
            _list.HideEmptyState();
        }
    }

    /// <summary>Opens the review view for the quiz of the pressed row.</summary>
    private void OpenReview(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _list.Grid.Rows.Count)
        {
            return;
        }

        if (_list.Grid.Rows[rowIndex].Tag is not Quiz quiz)
        {
            return;
        }

        ShowListView(visible: false);
        OpenReviewView(quiz.QuizId);
    }

    /// <summary>Builds the review view's static frame once; each quiz fills it.</summary>
    private Panel BuildReviewView()
    {
        var review = new Panel
        {
            Name = "ReviewDrillInView",
            Dock = DockStyle.Fill,
            Visible = false,
            BackColor = UITheme.Background,
        };

        var header = new Panel
        {
            Name = "ReviewDrillInHeader",
            Dock = DockStyle.Top,
            Height = UITheme.Scaled(this, 58),
            BackColor = UITheme.Background,
        };
        var back = new FlatButton
        {
            Name = "ReviewDrillInBack",
            Text = "Back",
            Variant = UITheme.FlatButtonVariant.Secondary,
            Location = new Point(0, 0),
            Size = new Size(UITheme.Scaled(this, 90), UITheme.Scaled(this, 32)),
        };
        back.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<QuizReviewForm>() is { } form)
            {
                form.BackToList();
            }
        };
        _reviewTitle = new Label
        {
            Name = "ReviewDrillInTitle",
            Font = UITheme.BodySemibold,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Background,
            AutoSize = false,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Location = new Point(UITheme.Scaled(this, 90) + UITheme.Scaled(this, UITheme.Spacing12), 0),
            Size = new Size(UITheme.Scaled(this, 200), UITheme.Scaled(this, 32)),
        };
        _reviewBadge = new StatusBadge
        {
            Name = "ReviewDrillInBadge",
            Status = QuizStatus.Draft,
            Location = new Point(UITheme.Scaled(this, 90) + UITheme.Scaled(this, 200) + UITheme.Scaled(this, UITheme.Spacing12), 4),
            Size = new Size(UITheme.Scaled(this, 96), UITheme.Scaled(this, 24)),
        };
        _approve = new FlatButton
        {
            Name = "ReviewApproveButton",
            Text = ApproveButton,
            Location = new Point(Width - UITheme.Scaled(this, 340), 0),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Size = new Size(UITheme.Scaled(this, 100), UITheme.Scaled(this, 32)),
        };
        _approve.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<QuizReviewForm>() is { } form)
            {
                form.ApproveQuiz();
            }
        };
        var editQuestions = new FlatButton
        {
            Name = "ReviewEditQuestionsButton",
            Text = EditQuestionsButton,
            Variant = UITheme.FlatButtonVariant.Secondary,
            Location = new Point(Width - UITheme.Scaled(this, 230), 0),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Size = new Size(UITheme.Scaled(this, 120), UITheme.Scaled(this, 32)),
        };
        editQuestions.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<QuizReviewForm>() is { } form)
            {
                form.OpenEditor();
            }
        };
        var discard = new FlatButton
        {
            Name = "ReviewDiscardButton",
            Text = DiscardButton,
            Variant = UITheme.FlatButtonVariant.Destructive,
            Location = new Point(Width - UITheme.Scaled(this, 100), 0),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Size = new Size(UITheme.Scaled(this, 100), UITheme.Scaled(this, 32)),
        };
        discard.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<QuizReviewForm>() is { } form)
            {
                form.DiscardQuiz();
            }
        };
        _approveHint = new Label
        {
            Name = "ReviewApproveHint",
            Text = ZeroQuestionHint,
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Background,
            AutoSize = false,
            Location = new Point(Width - UITheme.Scaled(this, 344), UITheme.Scaled(this, 36)),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Size = new Size(UITheme.Scaled(this, 344), UITheme.Scaled(this, 18)),
            TextAlign = ContentAlignment.MiddleRight,
            Visible = false,
        };
        header.Controls.Add(back);
        header.Controls.Add(_reviewTitle);
        header.Controls.Add(_reviewBadge);
        header.Controls.Add(_approve);
        header.Controls.Add(editQuestions);
        header.Controls.Add(discard);
        header.Controls.Add(_approveHint);

        _questionStack = new Panel
        {
            Name = "ReviewQuestionStack",
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = UITheme.Background,
        };

        review.Controls.Add(_questionStack);
        review.Controls.Add(header);

        // The H1 fix: a Dock=Fill container is laid out only once visible, so
        // the cards build at the pre-layout width first; the width-driven
        // rebuild below corrects them the moment the real size arrives.
        _reviewView.Resize += static (sender, e) =>
        {
            if (sender is Panel view && view.Visible && view.FindAncestor<QuizReviewForm>() is { } form)
            {
                form.RebuildIfWidthChanged();
            }
        };

        return review;
    }

    /// <summary>Rebuilds the question cards when the stack's WIDTH actually changes.</summary>
    private void RebuildIfWidthChanged()
    {
        if (_openQuizId != 0
            && _quizzes.GetById(_openQuizId) is { } quiz
            && _questionStack.ClientSize.Width != _lastStackWidth)
        {
            BuildQuestionCards(quiz);
        }
    }

    /// <summary>Loads one draft and renders its review view (§5.12).</summary>
    private void OpenReviewView(int quizId)
    {
        Quiz? quiz = _quizzes.GetById(quizId);
        if (quiz is null)
        {
            BackToList();
            return; // the draft was deleted (e.g. after a discard) — back to the list
        }

        _openQuizId = quizId;
        _reviewTitle.Text = quiz.Title;
        _reviewBadge.Status = quiz.Status; // the badge shows the live status

        bool hasQuestions = quiz.Questions.Count > 0;
        _approve.Enabled = hasQuestions; // §5.12: zero questions → Approve disabled
        _approveHint.Visible = !hasQuestions;

        _reviewView.Visible = true; // visible FIRST — the stack is laid out only now (the H1 fix)
        _reviewView.BringToFront();
        BuildQuestionCards(quiz);
    }

    /// <summary>Builds one §5.12 card per AI question (claim highlighted, not validated).</summary>
    private void BuildQuestionCards(Quiz quiz)
    {
        _questionStack.SuspendLayout();
        foreach (Control existing in _questionStack.Controls)
        {
            existing.Dispose();
        }
        _questionStack.Controls.Clear();
        _lastStackWidth = _questionStack.ClientSize.Width; // the width this build used (the resize guard compares against it)

        int inset = UITheme.Scaled(this, UITheme.Spacing16);
        int cardWidth = Math.Max(_questionStack.ClientSize.Width - inset * 2, 1);
        int top = 0;

        for (int i = 0; i < quiz.Questions.Count; i++)
        {
            Question question = quiz.Questions[i];
            Choice? claimed = question.GetCorrectChoice();

            int textHeight = TextRenderer.MeasureText(
                question.QuestionText, UITheme.Body, new Size(cardWidth - inset * 2, int.MaxValue), TextFormatFlags.WordBreak).Height;
            string explanation = question.Explanation.Length == 0 ? NoExplanationText : question.Explanation;
            int explanationTextHeight = TextRenderer.MeasureText(
                explanation,
                UITheme.Body,
                new Size(Math.Max(cardWidth - inset * 2, 1), int.MaxValue),
                TextFormatFlags.WordBreak).Height;
            int explanationBlockHeight = UITheme.Scaled(this, 18) + UITheme.Scaled(this, explanationTextHeight);
            int cardHeight = inset + textHeight + UITheme.Scaled(this, UITheme.Spacing12)
                + (question.Choices.Count * UITheme.Scaled(this, 30))
                + UITheme.Scaled(this, UITheme.Spacing12)
                + explanationBlockHeight
                + UITheme.Scaled(this, 12)
                + inset;

            var card = new CardPanel
            {
                Name = $"ReviewQuestionCard{i}",
                Location = new Point(inset, top),
                Size = new Size(cardWidth, cardHeight),
            };

            var questionText = new Label
            {
                Name = $"ReviewQuestionText{i}",
                Text = question.QuestionText,
                Font = UITheme.Body,
                ForeColor = UITheme.TextPrimary,
                BackColor = UITheme.Surface,
                AutoSize = false,
                Location = new Point(inset, inset),
                Size = new Size(cardWidth - inset * 2, textHeight),
            };
            card.Controls.Add(questionText);

            int choiceTop = inset + textHeight + UITheme.Scaled(this, UITheme.Spacing12);
            foreach (Choice choice in question.Choices.OrderBy(item => item.Position))
            {
                bool claimedAnswer = claimed is not null && choice.ChoiceId == claimed.ChoiceId;
                var choiceRow = new Panel
                {
                    Name = $"ReviewChoiceRow{i}_{choice.ChoiceId}",
                    Location = new Point(inset, choiceTop),
                    Size = new Size(cardWidth - inset * 2, UITheme.Scaled(this, 30) - UITheme.Scaled(this, UITheme.Spacing4)),
                    BackColor = claimedAnswer ? UITheme.AccentTint : UITheme.Fill,
                };
                var choiceText = new Label
                {
                    Name = $"ReviewChoiceText{i}_{choice.ChoiceId}",
                    Text = choice.ChoiceText,
                    Font = UITheme.Body,
                    ForeColor = UITheme.TextPrimary,
                    BackColor = choiceRow.BackColor,
                    AutoSize = false,
                    AutoEllipsis = true,
                    Location = new Point(UITheme.Scaled(this, UITheme.Spacing8), 0),
                    Size = new Size(choiceRow.Width - UITheme.Scaled(this, UITheme.Spacing16) - UITheme.Scaled(this, 90), choiceRow.Height),
                    TextAlign = ContentAlignment.MiddleLeft,
                };
                choiceRow.Controls.Add(choiceText);

                if (claimedAnswer)
                {
                    var aiAnswer = new Label
                    {
                        Name = $"ReviewAiAnswer{i}_{choice.ChoiceId}",
                        Text = AiAnswerLabel,
                        Font = UITheme.Caption,
                        ForeColor = UITheme.TextSecondary,
                        BackColor = choiceRow.BackColor,
                        AutoSize = false,
                        Location = new Point(choiceRow.Width - UITheme.Scaled(this, 90) - UITheme.Scaled(this, UITheme.Spacing8), 0),
                        Size = new Size(UITheme.Scaled(this, 90), choiceRow.Height),
                        TextAlign = ContentAlignment.MiddleRight,
                    };
                    choiceRow.Controls.Add(aiAnswer);
                }

                card.Controls.Add(choiceRow);
                choiceTop += UITheme.Scaled(this, 30);
            }

            // The explanation inset: the AI's claimed reasoning, always rendered,
            // measured so multi-line reasoning is never clipped (M8).
            int explanationTop = choiceTop + UITheme.Scaled(this, UITheme.Spacing12);
            var explanationCaption = new Label
            {
                Name = $"ReviewExplanationCaption{i}",
                Text = "Explanation",
                Font = UITheme.CaptionSemibold,
                ForeColor = UITheme.TextSecondary,
                BackColor = UITheme.Fill,
                AutoSize = false,
                Location = new Point(inset, explanationTop),
                Size = new Size(cardWidth - inset * 2, UITheme.Scaled(this, 18)),
            };
            var explanationText = new Label
            {
                Name = $"ReviewExplanationText{i}",
                Text = explanation,
                Font = UITheme.Body,
                ForeColor = UITheme.TextPrimary,
                BackColor = UITheme.Fill,
                AutoSize = false,
                Location = new Point(inset, explanationTop + UITheme.Scaled(this, 18)),
                Size = new Size(cardWidth - inset * 2, explanationTextHeight),
            };
            card.Controls.Add(explanationCaption);
            card.Controls.Add(explanationText);

            _questionStack.Controls.Add(card);
            top += cardHeight + UITheme.Scaled(this, UITheme.Spacing12);
        }

        _questionStack.ResumeLayout();
    }

    /// <summary>Approves the open draft: the status flip Draft → Approved ONLY (R-2 #4).</summary>
    private void ApproveQuiz()
    {
        Quiz? quiz = _quizzes.GetById(_openQuizId);
        if (quiz is null || quiz.Questions.Count == 0)
        {
            return; // the button is disabled in this state; a defensive guard
        }

        quiz.Status = QuizStatus.Approved;
        _quizzes.Update(quiz); // the status flip only — no audit fields exist (R-2 #4)

        _reviewBadge.Status = QuizStatus.Approved; // the badge flips in place (the state change is the moment)
        ReportStatus(ApprovedMessage, success: true);
        LoadDraftRows(); // the list drops the approved quiz
    }

    /// <summary>Opens the §5.5 question editor for the open draft (the correct-it path).</summary>
    private void OpenEditor()
    {
        if (_openQuizId == 0)
        {
            return;
        }

        if (_editor is null)
        {
            _editor = new QuestionEditForm(_quizzes, _questions, _choices, _answers)
            {
                Name = "ReviewEditorPanel",
                Dock = DockStyle.Fill,
                Visible = false,
            };
            _editor.BackRequested += static (sender, e) =>
            {
                if (sender is QuestionEditForm editor && editor.FindAncestor<QuizReviewForm>() is { } form)
                {
                    editor.Visible = false;
                    form.EditorClosed();
                }
            };
            _editor.StatusRequested += static (sender, e) =>
            {
                if (sender is QuestionEditForm editor && editor.FindAncestor<QuizReviewForm>() is { } form)
                {
                    form.ReportStatus(e.Message, e.Success); // the editor's feedback rides the shell strip (M7)
                }
            };
            Controls.Add(_editor);
        }

        _reviewView.Visible = false;
        _editor.Open(_openQuizId);
        _editor.Visible = true;
        _editor.BringToFront();
    }

    /// <summary>The editor's "Back": the questions may have changed — rebuild the review.</summary>
    private void EditorClosed()
    {
        _reviewView.Visible = true;
        _reviewView.BringToFront();

        if (_quizzes.GetById(_openQuizId) is { } quiz)
        {
            bool hasQuestions = quiz.Questions.Count > 0;
            _approve.Enabled = hasQuestions;
            _approveHint.Visible = !hasQuestions;
            BuildQuestionCards(quiz);
        }
    }

    /// <summary>Discards the open draft after the §4.11 confirmation.</summary>
    private void DiscardQuiz()
    {
        if (_openQuizId == 0)
        {
            return;
        }

        if (!DialogForm.ShowConfirm(DiscardTitle, DiscardMessage, DiscardButton))
        {
            return;
        }

        try
        {
            _ = _quizzes.Delete(_openQuizId); // a draft has no attempts — the FK never blocks
        }
        catch (ReferentialIntegrityException)
        {
            // Defensive: an approved quiz taken meanwhile would be FK-protected.
            ReportStatus("This quiz cannot be deleted because it already has attempts.", success: false);
            BackToList();
            return;
        }

        _openQuizId = 0;
        BackToList();
    }

    /// <summary>Returns to the draft list, refreshed.</summary>
    private void BackToList()
    {
        _openQuizId = 0;
        _reviewView.Visible = false;
        ShowListView(visible: true);
        RefreshData();
    }

    /// <summary>Shows or hides this module's list state while the drill-in is open.</summary>
    private void ShowListView(bool visible)
    {
        _listCaption.Visible = visible;
        _list.Visible = visible;
    }
}
