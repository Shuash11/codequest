// =============================================================================
// CodeQuest — Phase P8: Topic & Quiz Management (P8.1 fix round)
// The question editor of the design of record (spec 5.5): one quiz's
// questions and their choices, opened as a drill-in from the quiz management
// screen (§5.4's "Edit questions" row action).
// Surface (F5, §5 preamble/§4.11/§5.5): the editor is a PANEL STATE of the
//          quiz-management module — never a window, never a modal. No window
//          chrome, no duplicated title: the drill-in's in-content header
//          (leading "Back" secondary returning to the quiz list WITH its
//          search/filter state intact) is the only title. The shell is
//          unchanged while it is open — the sidebar stays enabled, "Quiz
//          management" stays the active nav item — and the module's feedback
//          still reports to the shell's status strip (§5.2) through
//          <see cref="StatusRequested"/>.
// Look at: the layout — the header "Questions — {quiz title}" with the quiz's
//          status badge (recipe 4.10); the toolbar's "Add question" primary;
//          the questions list card — each row is the position number
//          ("1", Caption TextSecondary), the question text (one line), the
//          kind tag ("Multiple choice" / "True–false"), and the trailing
//          up/down reorder chevrons (recipe 4.1 quiet glyph actions) plus
//          Edit and Delete. The list card and the form card swap (list ↔
//          form state, §5.5) — the form never becomes a second window.
// Reordering (R-2 #8): up is disabled on the first row and down on the last;
//          a move swaps the two rows' positions by sending the WHOLE new
//          order through <see cref="IQuestionRepository.Reorder"/> — the data
//          layer's transactional renumbering (lift + set in one transaction),
//          never ad-hoc position updates. Positions are never typed, so they
//          cannot collide; the renumbered list is the reorder's feedback.
// Question form (<see cref="QuestionFormPanel"/>): a state of the drill-in —
//          the list card swaps for the form card. Question text (multi-line
//          §4.4), Type (§4.9 segmented: Multiple choice / True–false), the
//          choice rows — four for multiple choice, the fixed two for
//          true–false — each a recipe-4.7 checkbox with its VISIBLE label
//          "Mark choice n as the correct answer" (§5.5; the label toggles
//          the box on click and is the source of §4.7's accessible name)
//          + a text input, and "Explanation (optional)" (multi-line §4.4).
//          The explanation ALWAYS reaches the model — an empty string when
//          the field is blank (R-2 #6: the column is NOT NULL).
// R-2 #3 at write time: <see cref="QuestionFormValidator"/> rejects zero or
//          multiple correct choices (the exact spec message) BEFORE the
//          repository call; the checkbox group itself can never hold two
//          marks. R-2 #5: duplicate choice text → the spec's exact inline
//          message, pre-checked AND caught as DuplicateEntityException.
// Delete:  the two-branch flow — a question that a recorded answer points at
//          (submitted_answers.question_id ON DELETE RESTRICT) is blocked
//          with the strip message, no dialog; otherwise the §4.11 confirm.
// =============================================================================

using CodeQuest.Data;
using CodeQuest.Models;
using System.Drawing;
using System.Windows.Forms;

namespace CodeQuest.UI;

/// <summary>
/// The question editor (spec 5.5) as the shell-hosted drill-in panel: the
/// question list of one quiz plus the add/edit/delete/reorder flows.
/// </summary>
public sealed class QuestionEditForm : UserControl
{
    // The design of record's exact copy (§5.5 feedback) plus the two-branch
    // delete message in the §5.3/§5.4 blocked-branch pattern.
    private const string QuestionHasAnswersMessage = "This question already has recorded answers and cannot be deleted.";
    private const string DeleteQuestionDialogTitle = "Delete this question?";
    private const string DeleteQuestionDialogBody = "This question will be removed. This cannot be undone.";
    private const string SavedMessage = "Question saved.";
    private const string DeletedMessage = "Question deleted.";
    private const string EmptyListTitle = "No questions yet";
    private const string EmptyListCaption = "Add the first question.";
    private const string MultipleChoiceTag = "Multiple choice";
    private const string TrueFalseTag = "True–false";

    private const string UpAction = "Up";
    private const string DownAction = "Down";
    private const string EditAction = "Edit";
    private const string DeleteAction = "Delete";

    private readonly IQuizRepository _quizzes;
    private readonly IQuestionRepository _questions;
    private readonly IChoiceRepository _choices;
    private readonly ISubmittedAnswerRepository _answers;
    private readonly FlatButton _back;
    private readonly Label _header;
    private readonly StatusBadge _statusBadge;
    private readonly FlatButton _addQuestion;
    private readonly ListCardGrid _list;
    private readonly QuestionFormPanel _form;
    private Quiz _quiz = null!; // assigned by Open → LoadQuiz
    private int _quizId;
    private Question? _editingQuestion; // null while the form state adds a new question

    /// <summary>Creates the drill-in panel for the repositories it reads and writes.</summary>
    public QuestionEditForm(
        IQuizRepository quizzes,
        IQuestionRepository questions,
        IChoiceRepository choices,
        ISubmittedAnswerRepository answers)
    {
        ArgumentNullException.ThrowIfNull(quizzes);
        ArgumentNullException.ThrowIfNull(questions);
        ArgumentNullException.ThrowIfNull(choices);
        ArgumentNullException.ThrowIfNull(answers);
        _quizzes = quizzes;
        _questions = questions;
        _choices = choices;
        _answers = answers;

        BackColor = UITheme.Background;
        Dock = DockStyle.Fill;
        Visible = false;

        int headerHeight = UITheme.Scaled(this, 32);
        int listTop = (headerHeight + UITheme.Scaled(this, UITheme.Spacing16)) * 2; // header row, toolbar row

        _back = new FlatButton
        {
            Name = "QuestionEditorBack",
            Text = "Back",
            Variant = UITheme.FlatButtonVariant.Secondary,
            Location = new Point(0, 0),
            Size = new Size(
                TextRenderer.MeasureText("Back", UITheme.Body).Width + UITheme.Scaled(this, UITheme.Spacing16) * 2,
                headerHeight),
        };
        _back.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<QuestionEditForm>() is { } form)
            {
                form.BackRequested?.Invoke(form, EventArgs.Empty);
            }
        };

        _header = new Label
        {
            Name = "QuestionEditHeader",
            Font = UITheme.Title,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Background,
            AutoSize = true,
        };

        _statusBadge = new StatusBadge
        {
            Name = "QuestionEditStatusBadge",
        };

        _addQuestion = new FlatButton
        {
            Name = "AddQuestionButton",
            Text = "Add question",
            Size = new Size(UITheme.Scaled(this, 120), headerHeight),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        _addQuestion.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<QuestionEditForm>() is { } form)
            {
                form.OpenQuestionForm(questionToEdit: null);
            }
        };

        _list = new ListCardGrid
        {
            Name = "QuestionListCard",
            Location = new Point(0, listTop),
            Size = new Size(Width, Height - listTop),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
        };
        _list.Grid.Name = "QuestionsGrid";
        _list.Grid.AccessibleName = "Questions";
        _list.Grid.AddTextColumn("QuestionPosition", "#", 6f, extraLeftInset: 0, minimumWidth: 40);
        _list.Grid.AddTextColumn("QuestionText", "Question", 56f, extraLeftInset: UITheme.Scaled(this, UITheme.Spacing8), minimumWidth: 200);
        _list.Grid.AddTextColumn("QuestionKind", "Type", 18f, minimumWidth: 104);
        _list.Grid.AddActionColumn(UpAction, "\uE70E", GridActionKind.Glyph); // Segoe MDL2 chevron up (§6: reorder chevrons)
        _list.Grid.AddActionColumn(DownAction, "\uE70D", GridActionKind.Glyph); // Segoe MDL2 chevron down
        _list.Grid.AddActionColumn(EditAction, EditAction, GridActionKind.Secondary);
        _list.Grid.AddActionColumn(DeleteAction, DeleteAction, GridActionKind.Destructive);
        _list.Grid.IsActionEnabled = (rowIndex, actionName) => actionName switch
        {
            UpAction => rowIndex > 0, // up disabled on the first row (§5.5)
            DownAction => rowIndex < _list.Grid.Rows.Count - 1, // down disabled on the last
            _ => true,
        };
        _list.Grid.ActionClicked += static (sender, e) =>
        {
            if (sender is IosListGrid grid && grid.FindAncestor<QuestionEditForm>() is { } form)
            {
                form.HandleRowAction(e);
            }
        };

        // The form state occupies the list card's spot: the list card swaps
        // for the form card (§5.5) — never a second window.
        _form = new QuestionFormPanel
        {
            Name = "QuestionFormPanel",
            Location = _list.Location,
            Size = _list.Size,
            Anchor = _list.Anchor,
            Visible = false,
        };
        _form.Saved += HandleFormSaved;
        _form.Cancelled += static (sender, e) =>
        {
            if (sender is QuestionFormPanel panel && panel.FindAncestor<QuestionEditForm>() is { } form)
            {
                form.ShowList();
            }
        };

        Controls.Add(_back);
        Controls.Add(_statusBadge);
        Controls.Add(_header);
        Controls.Add(_addQuestion);
        Controls.Add(_list);
        Controls.Add(_form);

        Resize += static (sender, e) =>
        {
            if (sender is QuestionEditForm form)
            {
                form.LayoutHeader();
            }
        };
    }

    /// <summary>True when any question was added, edited, deleted or reordered — the host refreshes on Back.</summary>
    public bool ChangesSaved { get; private set; }

    /// <summary>Raised when "Back" returns to the quiz list (§5.5's leading affordance).</summary>
    public event EventHandler? BackRequested;

    /// <summary>The feedback this drill-in asks the shell to show in the module's status strip (§5.2).</summary>
    public event EventHandler<StatusEventArgs>? StatusRequested;

    /// <summary>Opens the editor for one quiz: reads it fresh and binds the list state.</summary>
    public void Open(int quizId)
    {
        _quizId = quizId;
        ChangesSaved = false;
        ShowList();
        LoadQuiz();
    }

    /// <summary>Reads the quiz fresh from the database and binds the list.</summary>
    private void LoadQuiz()
    {
        _quiz = _quizzes.GetById(_quizId)
            ?? throw new Data.EntityNotFoundException($"There is no quiz with the id {_quizId}.");

        _header.Text = "Questions — " + _quiz.Title;
        _statusBadge.Status = _quiz.Status;
        _statusBadge.ResizeToContent();
        LayoutHeader();

        BindList();
    }

    /// <summary>Places the header: "Back" leading, then "Questions — {quiz title}" + the status badge.</summary>
    private void LayoutHeader()
    {
        int titleGap = UITheme.Scaled(this, UITheme.Spacing12);

        _header.Location = new Point(_back.Right + titleGap, (_back.Height - _header.Height) / 2);
        _statusBadge.Location = new Point(
            Math.Min(_header.Right + UITheme.Scaled(this, UITheme.Spacing8), Width - _statusBadge.Width - UITheme.Scaled(this, UITheme.Spacing8)),
            _header.Location.Y + (_header.Height - _statusBadge.Height) / 2);

        int toolbarRow = _back.Bottom + UITheme.Scaled(this, UITheme.Spacing16);
        _addQuestion.Location = new Point(Width - _addQuestion.Width, toolbarRow);

        // The toolbar row sits between the header and the list card; the list
        // card's top follows the toolbar (both rows share the header height).
        int listTop = toolbarRow + _addQuestion.Height + UITheme.Scaled(this, UITheme.Spacing16);
        _list.SetBounds(_list.Left, listTop, _list.Width, Height - listTop);
        _form.SetBounds(_form.Left, listTop, _form.Width, Height - listTop);
    }

    /// <summary>Binds the question rows in display order (the renumbering is the reorder feedback).</summary>
    private void BindList()
    {
        _list.Grid.SuspendLayout();
        _list.Grid.Rows.Clear();

        int displayPosition = 0;
        foreach (Question question in _quiz.Questions)
        {
            displayPosition++;
            string kindTag = question is TrueFalseQuestion ? TrueFalseTag : MultipleChoiceTag;

            int index = _list.Grid.Rows.Add(
                displayPosition.ToString(),
                question.QuestionText,
                kindTag,
                UpAction,
                DownAction,
                EditAction,
                DeleteAction);
            _list.Grid.Rows[index].Tag = question;
        }

        _list.Grid.ResumeLayout();

        if (_list.Grid.Rows.Count == 0)
        {
            _list.ShowEmptyState(EmptyListTitle, EmptyListCaption); // no action button — Add question is in the toolbar
        }
        else
        {
            _list.HideEmptyState();
        }
    }

    /// <summary>The list state of the drill-in: list card visible, form card hidden (§5.5).</summary>
    private void ShowList()
    {
        _form.Visible = false;
        _list.Visible = true;
        _addQuestion.Visible = true;
    }

    /// <summary>The form state of the drill-in: the list card swaps for the form card (§5.5).</summary>
    private void ShowForm()
    {
        _list.Visible = false;
        _addQuestion.Visible = false;
        _form.Visible = true;
        _form.BringToFront();
    }

    /// <summary>Opens the blank form state (add) or the prefilled one (edit).</summary>
    private void OpenQuestionForm(Question? questionToEdit)
    {
        _editingQuestion = questionToEdit;
        if (questionToEdit is null)
        {
            _form.OpenForAdd();
        }
        else
        {
            _form.OpenForEdit(questionToEdit);
        }

        ShowForm();
    }

    /// <summary>Routes a row action click: reorder, edit or delete.</summary>
    private void HandleRowAction(GridActionEventArgs e)
    {
        Question? question = _list.Grid.Rows[e.RowIndex].Tag as Question;
        if (question is null)
        {
            return;
        }

        switch (e.ActionName)
        {
            case UpAction:
                MoveQuestion(e.RowIndex, e.RowIndex - 1);
                break;
            case DownAction:
                MoveQuestion(e.RowIndex, e.RowIndex + 1);
                break;
            case EditAction:
                OpenQuestionForm(question);
                break;
            case DeleteAction:
                DeleteQuestion(question);
                break;
        }
    }

    /// <summary>
    /// Swaps one question with its neighbour by sending the new order through
    /// the transactional reorder (R-2 #8 — never ad-hoc position updates).
    /// The visible renumbering is the (silent) feedback, §5.5.
    /// </summary>
    private void MoveQuestion(int fromIndex, int toIndex)
    {
        if (toIndex < 0 || toIndex >= _quiz.Questions.Count)
        {
            return;
        }

        List<int> order = _quiz.Questions.Select(question => question.QuestionId).ToList();
        (order[fromIndex], order[toIndex]) = (order[toIndex], order[fromIndex]);

        _questions.Reorder(_quizId, order);
        ChangesSaved = true;

        _quiz = _quizzes.GetById(_quizId)!; // the positions in the model follow the transaction
        BindList();
    }

    /// <summary>
    /// The form state's Save: the add path inserts the built question (in one
    /// transaction, P4), the edit path rewrites it in place. A typed failure
    /// keeps the form open with the strip line; success returns to the list
    /// with the strip line (§5.5).
    /// </summary>
    private void HandleFormSaved(object? sender, EventArgs e)
    {
        try
        {
            if (_editingQuestion is null)
            {
                SaveNewQuestion();
            }
            else
            {
                ApplyQuestionEdit(_editingQuestion, _form);
            }
        }
        catch (DuplicateEntityException)
        {
            // A transient UNIQUE collision of the in-place choice rewrite or
            // the insert — the spec's exact duplicate message, never raw.
            Report(QuestionFormValidator.DuplicateChoiceMessage, success: false);
            return;
        }
        catch (ReferentialIntegrityException)
        {
            // The structural part of the edit (removing a choice an answer
            // still points at) is protected by the schema.
            Report(QuestionHasAnswersMessage, success: false);
            return;
        }

        ChangesSaved = true;
        _quiz = _quizzes.GetById(_quizId)!;
        BindList();
        ShowList();
        Report(SavedMessage, success: true);
    }

    /// <summary>Builds and stores the new question from the form state.</summary>
    private void SaveNewQuestion()
    {
        int nextPosition = _quiz.Questions.Count == 0 ? 1 : _quiz.Questions.Max(question => question.Position) + 1;
        Question question = BuildQuestion(_form, _quizId, nextPosition);

        _questions.Add(question); // question + choices in one transaction (P4)
    }

    /// <summary>
    /// Writes the edited question: the choice rows first (in-place update of
    /// the kept options, add the new ones, delete the surplus LAST so the
    /// schema-protected delete is the only risky step), then the question row.
    /// </summary>
    private void ApplyQuestionEdit(Question question, QuestionFormPanel form)
    {
        List<Choice> existing = question.Choices.OrderBy(choice => choice.Position).ToList();
        IReadOnlyList<string> texts = form.ChoiceTexts;
        int correctIndex = form.CorrectIndex;

        for (int i = 0; i < texts.Count; i++)
        {
            if (i < existing.Count)
            {
                // In-place update: the choice keeps its id, so recorded answers
                // keep pointing at a real option (P10 review stays intact).
                Choice choice = existing[i];
                choice.ChoiceText = texts[i];
                choice.IsCorrect = i == correctIndex;
                choice.Position = i + 1;
                _choices.Update(choice);
            }
            else
            {
                _choices.Add(new Choice
                {
                    QuestionId = question.QuestionId,
                    ChoiceText = texts[i],
                    IsCorrect = i == correctIndex,
                    Position = i + 1,
                });
            }
        }

        // The surplus choices go LAST: a failed delete leaves the question
        // valid, and the typed error surfaces as a friendly message.
        for (int i = texts.Count; i < existing.Count; i++)
        {
            _choices.Delete(existing[i].ChoiceId);
        }

        question.QuestionText = form.QuestionText;
        question.Explanation = form.Explanation; // always a string (R-2 #6)
        _questions.Update(question);
    }

    /// <summary>The two-branch delete of a question (§5.5 + the §5.3/§5.4 branch pattern).</summary>
    private void DeleteQuestion(Question question)
    {
        if (_answers.GetByQuestion(question.QuestionId).Count > 0)
        {
            // Blocked: no dialog, the strip explains the block.
            Report(QuestionHasAnswersMessage, success: false);
            return;
        }

        if (!DialogForm.ShowConfirm(DeleteQuestionDialogTitle, DeleteQuestionDialogBody, "Delete"))
        {
            return;
        }

        try
        {
            if (_questions.Delete(question.QuestionId))
            {
                // The choices go with the question (choices.question_id ON DELETE CASCADE).
                Report(DeletedMessage, success: true);
                ChangesSaved = true;
            }
        }
        catch (ReferentialIntegrityException)
        {
            Report(QuestionHasAnswersMessage, success: false);
        }

        _quiz = _quizzes.GetById(_quizId)!;
        BindList();
    }

    /// <summary>Builds the concrete question kind from the form's values.</summary>
    private static Question BuildQuestion(QuestionFormPanel form, int quizId, int position)
    {
        Question question = form.IsTrueFalse ? new TrueFalseQuestion() : new MultipleChoiceQuestion();
        question.QuizId = quizId; // the insert stores the question in this quiz
        question.QuestionText = form.QuestionText;
        question.Explanation = form.Explanation; // always a string (R-2 #6)
        question.Position = position;
        question.CreatedAt = DateTimeOffset.UtcNow;

        for (int i = 0; i < form.ChoiceTexts.Count; i++)
        {
            question.AddChoice(new Choice
            {
                QuestionId = 0, // stamped together with the question row
                ChoiceText = form.ChoiceTexts[i],
                IsCorrect = i == form.CorrectIndex,
                Position = i + 1,
            });
        }

        return question;
    }

    /// <summary>Reports one feedback line to the module's status strip (§5.2, §5.5 feedback).</summary>
    private void Report(string message, bool success)
        => StatusRequested?.Invoke(this, new StatusEventArgs(message, success));
}

/// <summary>
/// The question form as a STATE of the drill-in panel (§5.5): the list card
/// swaps for this form card — never a modal, never a window. Question text
/// (multi-line §4.4), Type (segmented), the choice rows (recipe-4.7 checkbox
/// with its §5.5 visible label "Mark choice n as the correct answer" +
/// recipe-4.4 input) and "Explanation (optional)". Validation runs through
/// <see cref="QuestionFormValidator"/> before <see cref="Saved"/> fires; the
/// inline error lines carry the failures.
/// </summary>
public sealed class QuestionFormPanel : Panel
{
    private const int ContentInset = UITheme.Spacing24;
    private const int FieldGap = UITheme.Spacing12;
    private const int ButtonGap = UITheme.Spacing8;
    private const int ChoiceRowCount = 4;
    private const int ErrorSlot = 16; // F13: the 8px-scale error slot (§2.3), replacing the off-scale 18px

    private static readonly string CorrectMarkLabelFormat = "Mark choice {0} as the correct answer";

    private readonly CardPanel _card;
    private readonly InputArea _questionText;
    private readonly SegmentedControl _type;
    private readonly MarkedCheckBox[] _correctMarks;
    private readonly Label[] _correctMarkLabels;
    private readonly InputField[] _choiceFields;
    private readonly InputArea _explanation;
    private readonly Label _blockError;
    private readonly FlatButton _save;
    private readonly FlatButton _cancel;
    private readonly int _markLabelWidth;
    private int _correctIndex = -1;
    private bool _suppressCorrectTracking; // guards the checkbox bookkeeping

    /// <summary>Creates the form card.</summary>
    public QuestionFormPanel()
    {
        BackColor = UITheme.Background; // the drill-in area behind the card
        AutoScroll = true; // small windows scroll the tall form card (platform chrome, §4.6)

        _questionText = new InputArea(accessibleName: "Question text")
        {
            Name = "QuestionTextField",
            PlaceholderText = "Question text",
        };
        _questionText.TextChanged += static (sender, e) =>
        {
            if (sender is InputArea area)
            {
                area.ClearError();
            }
        };

        _type = new SegmentedControl
        {
            Name = "QuestionTypeSelector",
        };
        _type.Items = new[] { "Multiple choice", "True–false" };
        _type.SelectionChanged += static (sender, e) =>
        {
            if (sender is SegmentedControl segmented && segmented.FindAncestor<QuestionFormPanel>() is { } panel)
            {
                panel.HandleTypeChanged();
            }
        };

        _correctMarks = new MarkedCheckBox[ChoiceRowCount];
        _correctMarkLabels = new Label[ChoiceRowCount];
        _choiceFields = new InputField[ChoiceRowCount];
        _markLabelWidth = TextRenderer.MeasureText(
            string.Format(CorrectMarkLabelFormat, ChoiceRowCount), UITheme.Body).Width;

        for (int i = 0; i < ChoiceRowCount; i++)
        {
            int index = i;

            // The §5.5 visible label: click-to-toggle, and the SOURCE of the
            // checkbox's accessible name (§4.7's association rule).
            var markLabel = new Label
            {
                Name = "ChoiceMarkLabel" + (i + 1),
                Text = string.Format(CorrectMarkLabelFormat, i + 1),
                Font = UITheme.Body,
                ForeColor = UITheme.TextPrimary,
                BackColor = UITheme.Surface,
                AutoSize = false,
            };

            var mark = new MarkedCheckBox
            {
                Name = "ChoiceCorrectCheck" + (i + 1),
            };
            mark.LabeledBy = markLabel;
            mark.CheckedChanged += (sender, e) =>
            {
                if (sender is MarkedCheckBox checkBox && checkBox.FindAncestor<QuestionFormPanel>() is { } panel)
                {
                    panel.HandleCorrectChanged(index, checkBox.IsChecked);
                }
            };
            _correctMarks[i] = mark;
            _correctMarkLabels[i] = markLabel;

            var field = new InputField(accessibleName: $"Choice {i + 1}")
            {
                Name = "ChoiceField" + (i + 1),
                PlaceholderText = "Choice " + (i + 1),
            };
            field.TextChanged += static (sender, e) =>
            {
                if (sender is InputField input)
                {
                    input.ClearError();
                }
            };
            _choiceFields[i] = field;
        }

        _explanation = new InputArea(accessibleName: "Explanation")
        {
            Name = "ExplanationField",
            PlaceholderText = "Explanation",
        };

        _blockError = new Label
        {
            Name = "QuestionBlockError",
            Font = UITheme.Caption,
            ForeColor = UITheme.DangerText,
            BackColor = UITheme.Surface,
            AutoSize = true,
            Visible = false,
        };

        _save = new FlatButton { Name = "SaveQuestionButton", Text = "Save", Variant = UITheme.FlatButtonVariant.Primary };
        _save.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<QuestionFormPanel>() is { } panel)
            {
                panel.HandleSave();
            }
        };

        _cancel = new FlatButton
        {
            Name = "CancelQuestionButton",
            Text = "Cancel",
            Variant = UITheme.FlatButtonVariant.Secondary,
        };
        _cancel.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<QuestionFormPanel>() is { } panel)
            {
                panel.Cancelled?.Invoke(panel, EventArgs.Empty);
            }
        };

        _card = new CardPanel
        {
            Name = "QuestionFormCard",
            Location = new Point(0, 0),
        };

        // Card content, in tab order: title, Question, Type, the choice rows
        // (mark → label → input per row), the block error, Explanation, buttons.
        _card.Controls.Add(_questionText);
        _card.Controls.Add(_type);
        for (int i = 0; i < ChoiceRowCount; i++)
        {
            _card.Controls.Add(_correctMarks[i]);
            _card.Controls.Add(_correctMarkLabels[i]);
            _card.Controls.Add(_choiceFields[i]);

            int row = i;
            _correctMarkLabels[i].Click += (sender, e) =>
            {
                if (sender is Label label && label.FindAncestor<QuestionFormPanel>() is { } panel)
                {
                    panel._correctMarks[row].IsChecked = !panel._correctMarks[row].IsChecked;
                }
            };

        }
        _card.Controls.Add(_blockError);
        _card.Controls.Add(_explanation);
        _card.Controls.Add(_save);
        _card.Controls.Add(_cancel);

        // The §4.4 label associations (F2): the visible labels are the source
        // of their inputs' accessible names.
        _type.LabeledBy = EnsureLabel("QuestionTypeLabel", "Type");
        _questionText.LabeledBy = EnsureLabel("QuestionTextLabel", "Question");
        _explanation.LabeledBy = EnsureLabel("QuestionExplanationLabel", "Explanation (optional)");
        _ = EnsureLabel("QuestionDialogTitle", "Add question"); // set per state in OpenForAdd/OpenForEdit
        _ = EnsureLabel("QuestionChoicesLabel", "Choices");

        Controls.Add(_card);

        LayoutForm();
    }

    /// <summary>Raised on Save when the form's values validate (the host performs the write).</summary>
    public event EventHandler? Saved;

    /// <summary>Raised on Cancel — the host returns to the list without saving (§5.5).</summary>
    public event EventHandler? Cancelled;

    /// <summary>The entered (trimmed) question text.</summary>
    public string QuestionText => _questionText.Text.Trim();

    /// <summary>The entered explanation — always a string, empty when blank (R-2 #6).</summary>
    public string Explanation => _explanation.Text.Trim();

    /// <summary>Whether the segmented type selector sits on True–false.</summary>
    public bool IsTrueFalse => _type.SelectedIndex == 1;

    /// <summary>The entered choice texts, top row first (two rows for true–false).</summary>
    public IReadOnlyList<string> ChoiceTexts
    {
        get
        {
            int count = IsTrueFalse ? 2 : ChoiceRowCount;
            List<string> texts = new(count);
            for (int i = 0; i < count; i++)
            {
                texts.Add(_choiceFields[i].Text.Trim());
            }

            return texts;
        }
    }

    /// <summary>The marked correct row, or -1 when none is marked.</summary>
    public int CorrectIndex => _correctIndex;

    /// <summary>Opens the blank form for a new question ("Add question").</summary>
    public void OpenForAdd()
    {
        EnsureLabel("QuestionDialogTitle", "Add question").Text = "Add question";
        ResetFields();
        LayoutForm();
    }

    /// <summary>Opens the form prefilled from an existing question ("Edit question").</summary>
    public void OpenForEdit(Question question)
    {
        EnsureLabel("QuestionDialogTitle", "Edit question").Text = "Edit question";
        ResetFields();
        LoadQuestion(question);
        LayoutForm();
    }

    /// <summary>Clears every field to the add-question state.</summary>
    private void ResetFields()
    {
        _questionText.Text = string.Empty;
        _explanation.Text = string.Empty;
        _type.SelectedIndex = 0;
        _correctIndex = -1;
        _suppressCorrectTracking = true;
        for (int i = 0; i < ChoiceRowCount; i++)
        {
            _choiceFields[i].Text = string.Empty;
            _correctMarks[i].IsChecked = false;
        }
        _suppressCorrectTracking = false;
        ClearChoiceErrors();
        ApplyTypeState();
    }

    /// <summary>Prefills the form from an existing question (edit).</summary>
    private void LoadQuestion(Question question)
    {
        _questionText.Text = question.QuestionText;
        _explanation.Text = question.Explanation;
        _type.SelectedIndex = question is TrueFalseQuestion ? 1 : 0;

        _suppressCorrectTracking = true;
        for (int i = 0; i < ChoiceRowCount; i++)
        {
            bool present = i < question.Choices.Count;
            _choiceFields[i].Text = present ? question.Choices[i].ChoiceText : string.Empty;
            _correctMarks[i].IsChecked = present && question.Choices[i].IsCorrect;
            if (present && question.Choices[i].IsCorrect)
            {
                _correctIndex = i;
            }
        }
        _suppressCorrectTracking = false;

        ApplyTypeState();
    }

    /// <summary>Save runs the pure validation; an issue keeps the form open, inline.</summary>
    private void HandleSave()
    {
        ClearChoiceErrors();

        QuestionFormIssue? issue = QuestionFormValidator.Validate(QuestionText, ChoiceTexts, CorrectIndex);
        if (issue is null)
        {
            Saved?.Invoke(this, EventArgs.Empty);
            return;
        }

        switch (issue.Field)
        {
            case QuestionFormField.Choice:
                _choiceFields[issue.ChoiceIndex].ShowError(issue.Message);
                break;
            case QuestionFormField.CorrectMark:
                _blockError.Text = issue.Message;
                _blockError.Visible = true;
                break;
            default:
                _questionText.ShowError(issue.Message);
                break;
        }

        LayoutForm(); // the shown error claims its inline slot (§4.4)
    }

    /// <summary>Exactly one correct choice: checking a row unchecks the others (the radio behavior).</summary>
    private void HandleCorrectChanged(int index, bool isChecked)
    {
        if (_suppressCorrectTracking)
        {
            return;
        }

        if (isChecked)
        {
            _correctIndex = index;
            _suppressCorrectTracking = true;
            for (int i = 0; i < ChoiceRowCount; i++)
            {
                if (i != index && _correctMarks[i].IsChecked)
                {
                    _correctMarks[i].IsChecked = false;
                }
            }
            _suppressCorrectTracking = false;
        }
        else if (_correctIndex == index)
        {
            _correctIndex = -1; // unmarked — Save refuses with the spec's message (R-2 #3)
        }
    }

    /// <summary>Switching the type swaps between four editable rows and the fixed true/false pair.</summary>
    private void HandleTypeChanged()
    {
        ApplyTypeState();
        LayoutForm();
    }

    /// <summary>Applies the type to the choice rows: the fixed two for true–false.</summary>
    private void ApplyTypeState()
    {
        if (IsTrueFalse)
        {
            // The kind is defined by the True/False option set (the P4 rule),
            // so the two texts are fixed and read-only — no kind drift.
            _choiceFields[0].Text = "True";
            _choiceFields[1].Text = "False";
            _choiceFields[0].Enabled = false;
            _choiceFields[1].Enabled = false;
            _choiceFields[2].Visible = false;
            _choiceFields[3].Visible = false;
            _correctMarks[2].Visible = false;
            _correctMarks[3].Visible = false;
            _correctMarkLabels[2].Visible = false;
            _correctMarkLabels[3].Visible = false;

            if (_correctIndex is not 0 and not 1)
            {
                _correctIndex = -1;
            }
        }
        else
        {
            _choiceFields[0].Enabled = true;
            _choiceFields[1].Enabled = true;
            _choiceFields[2].Visible = true;
            _choiceFields[3].Visible = true;
            _correctMarks[2].Visible = true;
            _correctMarks[3].Visible = true;
            _correctMarkLabels[2].Visible = true;
            _correctMarkLabels[3].Visible = true;
        }
    }

    /// <summary>Clears every choice row's inline error and the block error line.</summary>
    private void ClearChoiceErrors()
    {
        foreach (InputField field in _choiceFields)
        {
            field.ClearError();
        }

        _blockError.Visible = false;
    }

    /// <summary>Places every field of the card (the layout follows the type and the error state).</summary>
    private void LayoutForm()
    {
        int titleHeight = UITheme.Scaled(this, 22);
        int labelHeight = UITheme.Scaled(this, 18);
        int buttonHeight = UITheme.Scaled(this, 32);
        int questionAreaHeight = UITheme.Scaled(this, 96); // 64px editor + the reserved error slot (§4.4 min 64)
        int explanationAreaHeight = UITheme.Scaled(this, 80);
        int choiceRowHeight = UITheme.Scaled(this, 32);
        int comboSlot = UITheme.Scaled(this, 32);
        int width = UITheme.Scaled(this, 600);
        int inset = UITheme.Scaled(this, ContentInset);
        int markSize = UITheme.Scaled(this, 16);
        int markGap = UITheme.Scaled(this, UITheme.Spacing8);
        int markLabelWidth = TextRenderer.MeasureText(
            string.Format(CorrectMarkLabelFormat, ChoiceRowCount), UITheme.Body).Width;
        int errorSlot = UITheme.Scaled(this, ErrorSlot); // F13: the 8px-scale step

        int saveWidth = TextRenderer.MeasureText(_save.Text, UITheme.Body).Width + UITheme.Scaled(this, UITheme.Spacing16) * 2;
        int cancelWidth = TextRenderer.MeasureText(_cancel.Text, UITheme.Body).Width + UITheme.Scaled(this, UITheme.Spacing16) * 2;

        var titleLabel = EnsureLabel("QuestionDialogTitle", "Add question");
        titleLabel.Location = new Point(inset, inset);
        titleLabel.Size = new Size(width - inset * 2, titleHeight);

        int questionLabelTop = inset + titleHeight + UITheme.Scaled(this, UITheme.Spacing16);
        var questionLabel = EnsureLabel("QuestionTextLabel", "Question");
        questionLabel.Location = new Point(inset, questionLabelTop);
        questionLabel.Size = new Size(width - inset * 2, labelHeight);

        _questionText.Location = new Point(inset, questionLabelTop + labelHeight + UITheme.Scaled(this, UITheme.Spacing4));
        _questionText.Size = new Size(width - inset * 2, questionAreaHeight);

        int typeLabelTop = _questionText.Bottom + FieldGap;
        var typeLabel = EnsureLabel("QuestionTypeLabel", "Type");
        typeLabel.Location = new Point(inset, typeLabelTop);
        typeLabel.Size = new Size(width - inset * 2, labelHeight);

        _type.Location = new Point(inset, typeLabelTop + labelHeight + UITheme.Scaled(this, UITheme.Spacing4));
        _type.Size = new Size(width - inset * 2, comboSlot);

        int choicesLabelTop = _type.Bottom + FieldGap;
        var choicesLabel = EnsureLabel("QuestionChoicesLabel", "Choices");
        choicesLabel.Location = new Point(inset, choicesLabelTop);
        choicesLabel.Size = new Size(width - inset * 2, labelHeight);

        int rowTop = choicesLabelTop + labelHeight + UITheme.Scaled(this, UITheme.Spacing4);
        for (int i = 0; i < ChoiceRowCount; i++)
        {
            bool visible = !IsTrueFalse || i < 2;
            _correctMarks[i].Visible = visible;
            _correctMarkLabels[i].Visible = visible;
            _choiceFields[i].Visible = visible;
            if (!visible)
            {
                continue;
            }

            // The §5.5 choice row: mark · "Mark choice n as the correct
            // answer" (click toggles the mark) · the choice text input.
            _correctMarks[i].Location = new Point(inset, rowTop + (choiceRowHeight - markSize) / 2);
            _correctMarks[i].Size = new Size(markSize, markSize);
            _correctMarkLabels[i].Location = new Point(inset + markSize + markGap, rowTop + (choiceRowHeight - labelHeight) / 2);
            _correctMarkLabels[i].Size = new Size(markLabelWidth, labelHeight);
            int fieldLeft = inset + markSize + markGap + markLabelWidth + markGap;
            _choiceFields[i].Location = new Point(fieldLeft, rowTop);
            _choiceFields[i].Size = new Size(width - inset - fieldLeft, choiceRowHeight);
            rowTop += choiceRowHeight + UITheme.Scaled(this, UITheme.Spacing8);

            // A shown inline error claims its slot below the failing row (§4.4).
            if (_choiceFields[i].HasError)
            {
                rowTop += errorSlot;
            }
        }

        // The correct-mark error of the §5.5 message is a block-level line
        // under the choice rows ("Mark exactly one correct answer.").
        if (_blockError.Visible)
        {
            _blockError.Location = new Point(inset, rowTop);
            rowTop += _blockError.Height + UITheme.Scaled(this, UITheme.Spacing4);
        }

        int explanationLabelTop = rowTop + UITheme.Scaled(this, UITheme.Spacing8);
        var explanationLabel = EnsureLabel("QuestionExplanationLabel", "Explanation (optional)");
        explanationLabel.Location = new Point(inset, explanationLabelTop);
        explanationLabel.Size = new Size(width - inset * 2, labelHeight);

        _explanation.Location = new Point(inset, explanationLabelTop + labelHeight + UITheme.Scaled(this, UITheme.Spacing4));
        _explanation.Size = new Size(width - inset * 2, explanationAreaHeight);

        int buttonsTop = _explanation.Bottom + UITheme.Scaled(this, UITheme.Spacing24);
        _save.Size = new Size(saveWidth, buttonHeight);
        _save.Location = new Point(width - inset - saveWidth, buttonsTop);
        _cancel.Size = new Size(cancelWidth, buttonHeight);
        _cancel.Location = new Point(width - inset - saveWidth - ButtonGap - cancelWidth, buttonsTop);

        int height = buttonsTop + buttonHeight + inset + errorSlot; // headroom for the next error slot
        _card.Size = new Size(width, height);
    }

    /// <summary>Finds or creates one of the card's static labels.</summary>
    private Label EnsureLabel(string name, string text)
    {
        Control? existing = _card.Controls.Find(name, searchAllChildren: false).FirstOrDefault();
        if (existing is Label label)
        {
            return label;
        }

        var created = new Label
        {
            Name = name,
            Text = text,
            Font = UITheme.BodySemibold,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Surface,
            AutoSize = false,
        };
        _card.Controls.Add(created);
        return created;
    }
}
