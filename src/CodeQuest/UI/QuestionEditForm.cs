// =============================================================================
// CodeQuest — Phase P8: Topic & Quiz Management
// The question editor of the design of record (spec 5.5): one quiz's
// questions and their choices, opened as a drill-in from the quiz management
// screen (§5.4's Questions row action).
// Look at: the layout — the header "Questions — {quiz title}" with the quiz's
//          status badge (recipe 4.10) and a status strip of its own (the §5
//          anatomy, §4.11 — "Question saved." / "Question deleted." live
//          here, never in a dialog); the toolbar's "Add question" primary;
//          the questions list card — each row is the position number
//          ("1", Caption TextSecondary), the question text (one line), the
//          kind tag ("Multiple choice" / "True–false"), and the trailing
//          up/down reorder chevrons (recipe 4.1 quiet glyph actions) plus
//          Edit and Delete.
// Reordering (R-2 #8): up is disabled on the first row and down on the last;
//          a move swaps the two rows' positions by sending the WHOLE new
//          order through <see cref="IQuestionRepository.Reorder"/> — the data
//          layer's transactional renumbering (lift + set in one transaction),
//          never ad-hoc position updates. Positions are never typed, so they
//          cannot collide; the renumbered list is the reorder's feedback.
// Question form: Question text (multi-line §4.4), Type (§4.9 segmented:
//          Multiple choice / True–false), the choice rows — four for multiple
//          choice, the fixed two for true–false — each a recipe-4.7 checkbox
//          (the correct marker; the editor enforces exactly one) + a text
//          input, and "Explanation (optional)" (multi-line §4.4). The
//          explanation ALWAYS reaches the model — an empty string when the
//          field is blank (R-2 #6: the column is NOT NULL).
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
/// The question editor (spec 5.5): the question list of one quiz plus the
/// add/edit/delete/reorder flows.
/// </summary>
public sealed class QuestionEditForm : Form
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
    private readonly int _quizId;
    private readonly StatusBadge _statusBadge;
    private readonly FlatButton _addQuestion;
    private readonly Label _statusStrip;
    private readonly ListCardGrid _list;
    private Quiz _quiz = null!; // assigned in LoadQuiz (called from the constructor)

    /// <summary>Creates the editor for one quiz. The quiz header is read here.</summary>
    public QuestionEditForm(
        IQuizRepository quizzes,
        IQuestionRepository questions,
        IChoiceRepository choices,
        ISubmittedAnswerRepository answers,
        int quizId)
    {
        ArgumentNullException.ThrowIfNull(quizzes);
        ArgumentNullException.ThrowIfNull(questions);
        ArgumentNullException.ThrowIfNull(choices);
        ArgumentNullException.ThrowIfNull(answers);
        _quizzes = quizzes;
        _questions = questions;
        _choices = choices;
        _answers = answers;
        _quizId = quizId;

        Text = "Questions";
        StartPosition = FormStartPosition.CenterParent;
        BackColor = UITheme.Background;
        ClientSize = new Size(UITheme.Scaled(this, 760), UITheme.Scaled(this, 560));
        MinimumSize = new Size(UITheme.Scaled(this, 640), UITheme.Scaled(this, 480));

        int inset = UITheme.Scaled(this, UITheme.Spacing24);
        int headerHeight = UITheme.Scaled(this, 30);
        int statusTop = inset + headerHeight + UITheme.Scaled(this, UITheme.Spacing8);
        int statusHeight = UITheme.Scaled(this, 20);
        int toolbarTop = statusTop + statusHeight + UITheme.Scaled(this, UITheme.Spacing8);
        int listTop = toolbarTop + UITheme.Scaled(this, 32) + UITheme.Scaled(this, UITheme.Spacing16);

        _statusBadge = new StatusBadge
        {
            Name = "QuestionEditStatusBadge",
            Location = new Point(0, 0), // placed in LayoutHeader once the quiz is known
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
        };

        _statusStrip = new Label
        {
            Name = "QuestionEditStatus",
            Text = string.Empty,
            Font = UITheme.Body,
            ForeColor = UITheme.SuccessText,
            BackColor = UITheme.Background,
            AutoSize = true,
            Location = new Point(inset, statusTop),
        };

        _addQuestion = new FlatButton
        {
            Name = "AddQuestionButton",
            Text = "Add question",
            Size = new Size(UITheme.Scaled(this, 120), UITheme.Scaled(this, 32)),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        _addQuestion.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.Parent is QuestionEditForm form)
            {
                form.AddQuestion();
            }
        };

        _list = new ListCardGrid
        {
            Name = "QuestionListCard",
            Location = new Point(inset, listTop),
            Size = new Size(ClientSize.Width - inset * 2, ClientSize.Height - listTop - inset),
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
            if (sender is IosListGrid grid && grid.Parent is ListCardGrid card && card.Parent is QuestionEditForm form)
            {
                form.HandleRowAction(e);
            }
        };

        Controls.Add(_statusBadge);
        Controls.Add(_statusStrip);
        Controls.Add(_addQuestion);
        Controls.Add(_list);

        Resize += static (sender, e) =>
        {
            if (sender is QuestionEditForm form)
            {
                form.LayoutHeader();
            }
        };

        LoadQuiz();
    }

    /// <summary>True when any question was added, edited, deleted or reordered — the caller refreshes.</summary>
    public bool ChangesSaved { get; private set; }

    /// <summary>Reads the quiz fresh from the database and binds the list.</summary>
    private void LoadQuiz()
    {
        _quiz = _quizzes.GetById(_quizId)
            ?? throw new Data.EntityNotFoundException($"There is no quiz with the id {_quizId}.");

        Text = "Questions — " + _quiz.Title;
        _statusBadge.Status = _quiz.Status;
        _statusBadge.ResizeToContent();
        LayoutHeader();

        BindList();
    }

    /// <summary>Places the header: "Questions — {quiz title}" + the status badge + the toolbar button.</summary>
    private void LayoutHeader()
    {
        int inset = UITheme.Scaled(this, UITheme.Spacing24);

        _addQuestion.Location = new Point(ClientSize.Width - inset - _addQuestion.Width, inset);

        Label headerLabel = (Label)(Controls.Find("QuestionEditHeader", searchAllChildren: false).FirstOrDefault()
            ?? new Label
            {
                Name = "QuestionEditHeader",
                Font = UITheme.Title,
                ForeColor = UITheme.TextPrimary,
                BackColor = UITheme.Background,
                AutoSize = true,
            });

        headerLabel.Text = "Questions — " + _quiz.Title;
        headerLabel.Location = new Point(inset, inset);
        if (!Controls.Contains(headerLabel))
        {
            Controls.Add(headerLabel);
        }

        _statusBadge.Location = new Point(
            Math.Min(headerLabel.Right + UITheme.Scaled(this, UITheme.Spacing8), _addQuestion.Left - _statusBadge.Width - UITheme.Scaled(this, UITheme.Spacing8)),
            inset + (headerLabel.Height - _statusBadge.Height) / 2);
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
                EditQuestion(question);
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

    /// <summary>Opens the blank question form and stores the new question on Save.</summary>
    private void AddQuestion()
    {
        using var dialog = new QuestionFormDialog("Add question");
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        int nextPosition = _quiz.Questions.Count == 0 ? 1 : _quiz.Questions.Max(question => question.Position) + 1;
        Question question = BuildQuestion(dialog, _quizId, nextPosition);

        try
        {
            _questions.Add(question); // question + choices in one transaction (P4)
        }
        catch (DuplicateEntityException)
        {
            ShowStatus(QuestionFormValidator.DuplicateChoiceMessage, success: false);
            return;
        }

        ChangesSaved = true;
        _quiz = _quizzes.GetById(_quizId)!;
        BindList();
        ShowStatus(SavedMessage, success: true);
    }

    /// <summary>Opens the prefilled question form and saves the changes on Save.</summary>
    private void EditQuestion(Question question)
    {
        using var dialog = new QuestionFormDialog("Edit question");
        dialog.LoadQuestion(question);

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            ApplyQuestionEdit(question, dialog);
        }
        catch (DuplicateEntityException)
        {
            // A transient UNIQUE collision of the in-place choice rewrite or a
            // raced edit — the spec's exact duplicate message, never raw.
            ShowStatus(QuestionFormValidator.DuplicateChoiceMessage, success: false);
            return;
        }
        catch (ReferentialIntegrityException)
        {
            // The structural part of the edit (removing a choice an answer
            // still points at) is protected by the schema.
            ShowStatus(QuestionHasAnswersMessage, success: false);
            return;
        }

        ChangesSaved = true;
        _quiz = _quizzes.GetById(_quizId)!;
        BindList();
        ShowStatus(SavedMessage, success: true);
    }

    /// <summary>
    /// Writes the edited question: the choice rows first (in-place update of
    /// the kept options, add the new ones, delete the surplus LAST so the
    /// schema-protected delete is the only risky step), then the question row.
    /// </summary>
    private void ApplyQuestionEdit(Question question, QuestionFormDialog dialog)
    {
        List<Choice> existing = question.Choices.OrderBy(choice => choice.Position).ToList();
        IReadOnlyList<string> texts = dialog.ChoiceTexts;
        int correctIndex = dialog.CorrectIndex;

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

        question.QuestionText = dialog.QuestionText;
        question.Explanation = dialog.Explanation; // always a string (R-2 #6)
        _questions.Update(question);
    }

    /// <summary>The two-branch delete of a question (§5.5 + the §5.3/§5.4 branch pattern).</summary>
    private void DeleteQuestion(Question question)
    {
        if (_answers.GetByQuestion(question.QuestionId).Count > 0)
        {
            // Blocked: no dialog, the strip explains the block.
            ShowStatus(QuestionHasAnswersMessage, success: false);
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
                ShowStatus(DeletedMessage, success: true);
                ChangesSaved = true;
            }
        }
        catch (ReferentialIntegrityException)
        {
            ShowStatus(QuestionHasAnswersMessage, success: false);
        }

        _quiz = _quizzes.GetById(_quizId)!;
        BindList();
    }

    /// <summary>Builds the concrete question kind from the dialog's values.</summary>
    private static Question BuildQuestion(QuestionFormDialog dialog, int quizId, int position)
    {
        Question question = dialog.IsTrueFalse ? new TrueFalseQuestion() : new MultipleChoiceQuestion();
        question.QuizId = quizId; // the insert stores the question in this quiz
        question.QuestionText = dialog.QuestionText;
        question.Explanation = dialog.Explanation; // always a string (R-2 #6)
        question.Position = position;
        question.CreatedAt = DateTimeOffset.UtcNow;

        for (int i = 0; i < dialog.ChoiceTexts.Count; i++)
        {
            question.AddChoice(new Choice
            {
                QuestionId = 0, // stamped together with the question row
                ChoiceText = dialog.ChoiceTexts[i],
                IsCorrect = i == dialog.CorrectIndex,
                Position = i + 1,
            });
        }

        return question;
    }

    /// <summary>Shows the editor's own status strip line (§5 anatomy).</summary>
    private void ShowStatus(string message, bool success)
    {
        _statusStrip.Text = message;
        _statusStrip.ForeColor = success ? UITheme.SuccessText : UITheme.DangerText;
    }
}

/// <summary>
/// The question modal card of spec 5.5: Question text (multi-line), Type
/// (segmented), the choice rows (recipe-4.7 checkbox + recipe-4.4 input —
/// four for multiple choice, the fixed two for true–false) and "Explanation
/// (optional)". Validation runs through <see cref="QuestionFormValidator"/>
/// before the dialog closes; the inline error lines carry the failures.
/// </summary>
public sealed class QuestionFormDialog : Form
{
    private const int ContentInset = UITheme.Spacing24;
    private const int FieldGap = UITheme.Spacing12;
    private const int ButtonGap = UITheme.Spacing8;
    private const int ChoiceRowCount = 4;

    private readonly InputArea _questionText;
    private readonly SegmentedControl _type;
    private readonly MarkedCheckBox[] _correctMarks;
    private readonly InputField[] _choiceFields;
    private readonly InputArea _explanation;
    private readonly Label _blockError;
    private readonly FlatButton _save;
    private readonly FlatButton _cancel;
    private int _correctIndex = -1;
    private bool _suppressCorrectTracking; // guards the checkbox bookkeeping

    /// <summary>Creates the question form. <paramref name="title"/> is "Add question" or "Edit question".</summary>
    public QuestionFormDialog(string title)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        BackColor = UITheme.Surface;
        KeyPreview = true;

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
            AccessibleName = "Question type",
        };
        _type.Items = new[] { "Multiple choice", "True–false" };
        _type.SelectionChanged += static (sender, e) =>
        {
            if (sender is SegmentedControl segmented && segmented.Parent is QuestionFormDialog dialog)
            {
                dialog.HandleTypeChanged();
            }
        };

        _correctMarks = new MarkedCheckBox[ChoiceRowCount];
        _choiceFields = new InputField[ChoiceRowCount];

        for (int i = 0; i < ChoiceRowCount; i++)
        {
            int index = i;

            var mark = new MarkedCheckBox
            {
                Name = "ChoiceCorrectCheck" + (i + 1),
                AccessibleName = $"Mark choice {i + 1} as the correct answer",
            };
            mark.CheckedChanged += (sender, e) =>
            {
                if (sender is MarkedCheckBox checkBox && checkBox.Parent is QuestionFormDialog dialog)
                {
                    dialog.HandleCorrectChanged(index, checkBox.IsChecked);
                }
            };
            _correctMarks[i] = mark;

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
            if (sender is FlatButton button && button.Parent is QuestionFormDialog dialog)
            {
                dialog.HandleSave();
            }
        };

        _cancel = new FlatButton
        {
            Name = "CancelQuestionButton",
            Text = "Cancel",
            Variant = UITheme.FlatButtonVariant.Secondary,
            DialogResult = DialogResult.Cancel,
        };

        Controls.Add(_questionText);
        Controls.Add(_type);
        for (int i = 0; i < ChoiceRowCount; i++)
        {
            Controls.Add(_correctMarks[i]);
            Controls.Add(_choiceFields[i]);
        }
        Controls.Add(_blockError);
        Controls.Add(_explanation);
        Controls.Add(_save);
        Controls.Add(_cancel);

        AcceptButton = _save;
        CancelButton = _cancel;

        LayoutForm();
    }

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

    /// <summary>Prefills the form from an existing question (edit).</summary>
    public void LoadQuestion(Question question)
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
        LayoutForm();
    }

    /// <summary>Save runs the pure validation; an issue keeps the dialog open, inline.</summary>
    private void HandleSave()
    {
        ClearChoiceErrors();

        QuestionFormIssue? issue = QuestionFormValidator.Validate(QuestionText, ChoiceTexts, CorrectIndex);
        if (issue is null)
        {
            DialogResult = DialogResult.OK;
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

    /// <summary>Rounds the card like the §4.11 dialog shape.</summary>
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        UpdateRegion();
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        UpdateRegion();
    }

    /// <summary>Clips the dialog to the recipe's rounded card shape.</summary>
    private void UpdateRegion()
    {
        if (Width <= 0 || Height <= 0)
        {
            return;
        }

        using var path = UITheme.RoundedPath(new Rectangle(0, 0, Width, Height), UITheme.RadiusCard);
        Region? oldRegion = Region;
        Region = new Region(path);
        oldRegion?.Dispose();
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
        int contentWidth = width - inset * 2;

        int saveWidth = TextRenderer.MeasureText(_save.Text, UITheme.Body).Width + UITheme.Scaled(this, UITheme.Spacing16) * 2;
        int cancelWidth = TextRenderer.MeasureText(_cancel.Text, UITheme.Body).Width + UITheme.Scaled(this, UITheme.Spacing16) * 2;

        var titleLabel = EnsureLabel("QuestionDialogTitle", Text);
        titleLabel.Location = new Point(inset, inset);
        titleLabel.Size = new Size(contentWidth, titleHeight);

        int questionLabelTop = inset + titleHeight + UITheme.Scaled(this, UITheme.Spacing16);
        var questionLabel = EnsureLabel("QuestionTextLabel", "Question");
        questionLabel.Location = new Point(inset, questionLabelTop);
        questionLabel.Size = new Size(contentWidth, labelHeight);

        _questionText.Location = new Point(inset, questionLabelTop + labelHeight + UITheme.Scaled(this, UITheme.Spacing4));
        _questionText.Size = new Size(contentWidth, questionAreaHeight);

        int typeLabelTop = _questionText.Bottom + FieldGap;
        var typeLabel = EnsureLabel("QuestionTypeLabel", "Type");
        typeLabel.Location = new Point(inset, typeLabelTop);
        typeLabel.Size = new Size(contentWidth, labelHeight);

        _type.Location = new Point(inset, typeLabelTop + labelHeight + UITheme.Scaled(this, UITheme.Spacing4));
        _type.Size = new Size(contentWidth, comboSlot);

        int choicesLabelTop = _type.Bottom + FieldGap;
        var choicesLabel = EnsureLabel("QuestionChoicesLabel", "Choices");
        choicesLabel.Location = new Point(inset, choicesLabelTop);
        choicesLabel.Size = new Size(contentWidth, labelHeight);

        int rowTop = choicesLabelTop + labelHeight + UITheme.Scaled(this, UITheme.Spacing4);
        int markSize = UITheme.Scaled(this, 16);
        int errorSlot = UITheme.Scaled(this, 18);
        for (int i = 0; i < ChoiceRowCount; i++)
        {
            bool visible = !IsTrueFalse || i < 2;
            _correctMarks[i].Visible = visible;
            _choiceFields[i].Visible = visible;
            if (!visible)
            {
                continue;
            }

            _correctMarks[i].Location = new Point(inset, rowTop + (choiceRowHeight - markSize) / 2);
            _correctMarks[i].Size = new Size(markSize, markSize);
            _choiceFields[i].Location = new Point(inset + markSize + UITheme.Scaled(this, UITheme.Spacing8), rowTop);
            _choiceFields[i].Size = new Size(contentWidth - markSize - UITheme.Scaled(this, UITheme.Spacing8), choiceRowHeight);
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
        explanationLabel.Size = new Size(contentWidth, labelHeight);

        _explanation.Location = new Point(inset, explanationLabelTop + labelHeight + UITheme.Scaled(this, UITheme.Spacing4));
        _explanation.Size = new Size(contentWidth, explanationAreaHeight);

        int buttonsTop = _explanation.Bottom + UITheme.Scaled(this, UITheme.Spacing24);
        _save.Size = new Size(saveWidth, buttonHeight);
        _save.Location = new Point(width - inset - saveWidth, buttonsTop);
        _cancel.Size = new Size(cancelWidth, buttonHeight);
        _cancel.Location = new Point(width - inset - saveWidth - ButtonGap - cancelWidth, buttonsTop);

        int height = buttonsTop + buttonHeight + inset + errorSlot; // headroom for the next error slot
        ClientSize = new Size(width, height);
    }

    /// <summary>Finds or creates one of the card's static labels.</summary>
    private Label EnsureLabel(string name, string text)
    {
        Control? existing = Controls.Find(name, searchAllChildren: false).FirstOrDefault();
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
        Controls.Add(created);
        return created;
    }
}
