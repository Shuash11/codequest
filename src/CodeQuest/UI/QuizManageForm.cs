// =============================================================================
// CodeQuest — Phase P8: Topic & Quiz Management
// The quiz management screen of the design of record (spec 5.4), hosted as
// the shell's "Quiz management" module panel (§5.2).
// Look at: the layout — toolbar: search ("Search quizzes…"), the topic filter
//          combo ("All topics" + one per topic), the status segmented control
//          (All / Draft / Approved, recipe 4.9) and the trailing "New quiz"
//          primary button; the grid card carries Title · Topic · Difficulty ·
//          Questions · Status (the Draft/Approved badge of recipe 4.10
//          inline) · Created, with trailing actions (Questions, Edit, Delete).
//          The search narrows by title and the filters by topic and status,
//          live while the user types or switches (QuizListFilter).
// Quiz form: modal card — Title (§4.4), Topic (§4.5 combo of existing
//          topics), Difficulty (§4.9 segmented, Easy/Medium/Hard).
// Delete flow (the CORRECTED two-branch flow of spec 5.4): only an unblocked
//          delete cascades the quiz's questions and their choices (questions
//          / choices ON DELETE CASCADE), while quiz_attempts.quiz_id is ON
//          DELETE RESTRICT — the flow branches on the attempts BEFORE any
//          dialog: no attempts → confirm ("Delete this quiz?" / "Its
//          questions will also be removed. This cannot be undone.", Delete
//          destructive + Cancel) → "Quiz deleted."; attempts recorded →
//          blocked, no dialog, the strip reads "This quiz has recorded
//          attempts and cannot be deleted." — never a raw SQLite error.
// Status:  the badges are the P13-mandated surface (Draft/Approved always
//          visible here); the Approve affordance itself belongs to §5.12
//          (P13) — a zero-question quiz cannot be approved THERE, this
//          screen only shows the state.
// R-2 obligations carried here: #5 (duplicate title per topic → the spec's
//          exact message, pre-checked inline AND caught as
//          DuplicateEntityException — never raw), F8 (empty-title guard
//          before the DB).
// =============================================================================

using CodeQuest.Data;
using CodeQuest.Models;
using System.Drawing;
using System.Windows.Forms;

namespace CodeQuest.UI;

/// <summary>
/// The quiz management screen (spec 5.4): search + filters, list,
/// create/edit/delete, and the entry to the question editor (§5.5).
/// </summary>
public sealed class QuizManageForm : ModulePanel
{
    // The design of record's exact copy (§4.11, §5.4 states and feedback).
    private const string EmptyQuizTitleMessage = "Enter a quiz title.";
    private const string DuplicateQuizMessage = "A quiz with this title already exists in this topic.";
    private const string NoTopicMessage = "Create a topic first.";
    private const string QuizHasAttemptsMessage = "This quiz has recorded attempts and cannot be deleted.";
    private const string DeleteQuizDialogTitle = "Delete this quiz?";
    private const string DeleteQuizDialogBody = "Its questions will also be removed. This cannot be undone.";
    private const string SavedMessage = "Quiz saved.";
    private const string DeletedMessage = "Quiz deleted.";
    private const string EmptyTitle = "No quizzes yet";
    private const string EmptyCaption = "Generate one with AI or create one manually.";
    private const string NoMatchTitle = "No quizzes match your search.";
    private const string AllTopicsLabel = "All topics";

    private const string QuestionsAction = "Questions";
    private const string EditAction = "Edit";
    private const string DeleteAction = "Delete";

    private readonly IQuizRepository _quizzes;
    private readonly ITopicRepository _topics;
    private readonly IQuestionRepository _questions;
    private readonly IChoiceRepository _choices;
    private readonly IAttemptRepository _attempts;
    private readonly ISubmittedAnswerRepository _answers;
    private readonly InputField _search;
    private readonly ThemedComboBox _topicFilter;
    private readonly SegmentedControl _statusFilter;
    private readonly FlatButton _newQuiz;
    private readonly ListCardGrid _list;

    private readonly List<int?> _topicFilterIds = new(); // parallel to the combo items; null = all topics
    private Dictionary<int, string> _topicNames = new();
    private Dictionary<int, int> _questionCounts = new();

    /// <summary>Creates the screen for the repositories it reads and writes.</summary>
    public QuizManageForm(
        IQuizRepository quizzes,
        ITopicRepository topics,
        IQuestionRepository questions,
        IChoiceRepository choices,
        IAttemptRepository attempts,
        ISubmittedAnswerRepository answers)
    {
        ArgumentNullException.ThrowIfNull(quizzes);
        ArgumentNullException.ThrowIfNull(topics);
        ArgumentNullException.ThrowIfNull(questions);
        ArgumentNullException.ThrowIfNull(choices);
        ArgumentNullException.ThrowIfNull(attempts);
        ArgumentNullException.ThrowIfNull(answers);
        _quizzes = quizzes;
        _topics = topics;
        _questions = questions;
        _choices = choices;
        _attempts = attempts;
        _answers = answers;

        BackColor = UITheme.Background;
        Dock = DockStyle.Fill;
        Visible = false;

        int toolbarHeight = UITheme.Scaled(this, 32);
        int gap = UITheme.Scaled(this, UITheme.Spacing8);

        _search = new InputField(accessibleName: "Search quizzes")
        {
            Name = "QuizSearchField",
            PlaceholderText = "Search quizzes…",
            Location = new Point(0, 0),
            Size = new Size(UITheme.Scaled(this, 180), toolbarHeight),
        };
        _search.TextChanged += static (sender, e) =>
        {
            if (sender is InputField field && field.Parent is QuizManageForm form)
            {
                form.ApplyFilter();
            }
        };

        _topicFilter = new ThemedComboBox
        {
            Name = "QuizTopicFilter",
            AccessibleName = "Topic filter",
            Location = new Point(_search.Right + gap, 0),
            Width = UITheme.Scaled(this, 170),
        };
        _topicFilter.SelectedIndexChanged += static (sender, e) =>
        {
            if (sender is ThemedComboBox combo && combo.Parent is QuizManageForm form)
            {
                form.ApplyFilter();
            }
        };

        _statusFilter = new SegmentedControl
        {
            Name = "QuizStatusFilter",
            AccessibleName = "Status filter",
            Location = new Point(_topicFilter.Right + gap, 0),
            Size = new Size(UITheme.Scaled(this, 228), toolbarHeight),
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
        };
        _statusFilter.Items = new[] { "All", "Draft", "Approved" };
        _statusFilter.SelectionChanged += static (sender, e) =>
        {
            if (sender is SegmentedControl segment && segment.Parent is QuizManageForm form)
            {
                form.ApplyFilter();
            }
        };

        _newQuiz = new FlatButton
        {
            Name = "NewQuizButton",
            Text = "New quiz",
            Location = new Point(Width - UITheme.Scaled(this, 104), 0),
            Size = new Size(UITheme.Scaled(this, 104), toolbarHeight),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        _newQuiz.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.Parent is QuizManageForm form)
            {
                form.NewQuiz();
            }
        };

        _list = new ListCardGrid
        {
            Name = "QuizListCard",
            Location = new Point(0, toolbarHeight + UITheme.Scaled(this, UITheme.Spacing16)),
            Size = new Size(Width, Height - toolbarHeight - UITheme.Scaled(this, UITheme.Spacing16)),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
        };
        _list.Grid.Name = "QuizGrid";
        _list.Grid.AccessibleName = "Quizzes";
        _list.Grid.AddTextColumn("QuizTitle", "Title", 34f, extraLeftInset: UITheme.Scaled(this, UITheme.Spacing8));
        _list.Grid.AddTextColumn("QuizTopic", "Topic", 16f, minimumWidth: 82);
        _list.Grid.AddTextColumn("QuizDifficulty", "Difficulty", 12f, minimumWidth: 74);
        _list.Grid.AddTextColumn("QuizQuestionCount", "Questions", 11f, minimumWidth: 74);
        _list.Grid.AddBadgeColumn("QuizStatus", "Status", 96);
        _list.Grid.AddTextColumn("QuizCreated", "Created", 15f, minimumWidth: 108);
        _list.Grid.AddActionColumn("QuizQuestions", QuestionsAction, GridActionKind.Secondary);
        _list.Grid.AddActionColumn("QuizEdit", EditAction, GridActionKind.Secondary);
        _list.Grid.AddActionColumn("QuizDelete", DeleteAction, GridActionKind.Destructive);
        _list.Grid.ActionClicked += static (sender, e) =>
        {
            if (sender is IosListGrid grid && grid.Parent is ListCardGrid card && card.Parent is QuizManageForm form)
            {
                form.HandleRowAction(e);
            }
        };
        _list.EmptyActionClicked += static (sender, e) =>
        {
            if (sender is ListCardGrid card && card.Parent is QuizManageForm form)
            {
                form.ClearFilters(); // the filtered-empty state's "Clear filters"
            }
        };

        Controls.Add(_search);
        Controls.Add(_topicFilter);
        Controls.Add(_statusFilter);
        Controls.Add(_newQuiz);
        Controls.Add(_list);
    }

    /// <inheritdoc />
    public override void RefreshData()
    {
        // The topic names and per-quiz question counts only change on CRUD —
        // one read each per refresh; the live filters reuse them.
        Dictionary<int, string> names = new();
        foreach (Topic topic in _topics.GetAll())
        {
            names[topic.TopicId] = topic.Name;
        }
        _topicNames = names;

        Dictionary<int, int> counts = new();
        IReadOnlyList<Quiz> quizzes = _quizzes.GetAll();
        foreach (Quiz quiz in quizzes)
        {
            counts[quiz.QuizId] = _questions.GetByQuiz(quiz.QuizId).Count;
        }
        _questionCounts = counts;

        RebuildTopicFilter();
        ApplyFilter();
    }

    /// <summary>Rebuilds the topic filter items ("All topics" + one per topic).</summary>
    private void RebuildTopicFilter()
    {
        int previous = _topicFilter.SelectedIndex;

        _topicFilter.Items.Clear();
        _topicFilterIds.Clear();
        _topicFilter.Items.Add(AllTopicsLabel);
        _topicFilterIds.Add(null);

        foreach ((int topicId, string name) in _topicNames.OrderBy(pair => pair.Value))
        {
            _topicFilter.Items.Add(name);
            _topicFilterIds.Add(topicId);
        }

        _topicFilter.SelectedIndex = previous >= 0 && previous < _topicFilter.Items.Count ? previous : 0;
    }

    /// <summary>Applies the live search and the topic/status filters to the grid.</summary>
    private void ApplyFilter()
    {
        string searchText = _search.Text;
        int? topicId = _topicFilter.SelectedIndex > 0 && _topicFilter.SelectedIndex < _topicFilterIds.Count
            ? _topicFilterIds[_topicFilter.SelectedIndex]
            : null;
        QuizStatus? status = _statusFilter.SelectedIndex switch
        {
            1 => QuizStatus.Draft,
            2 => QuizStatus.Approved,
            _ => null,
        };

        IReadOnlyList<Quiz> quizzes = QuizListFilter.Apply(_quizzes.GetAll(), searchText, topicId, status);

        _list.Grid.SuspendLayout();
        _list.Grid.Rows.Clear();

        foreach (Quiz quiz in quizzes)
        {
            string topicName = _topicNames.TryGetValue(quiz.TopicId, out string? name) ? name : string.Empty;
            int questionCount = _questionCounts.TryGetValue(quiz.QuizId, out int count) ? count : 0;

            int index = _list.Grid.Rows.Add(
                quiz.Title,
                topicName,
                quiz.Difficulty.ToString(),
                questionCount.ToString(),
                quiz.Status.ToString(), // the badge column paints this as a §4.10 chip
                UiFormat.FormatDate(quiz.CreatedAt),
                QuestionsAction,
                EditAction,
                DeleteAction);
            _list.Grid.Rows[index].Tag = quiz;
        }

        _list.Grid.ResumeLayout();

        // The states of spec 5.4: empty (nothing at all) vs filtered-empty.
        bool unfiltered = searchText.Trim().Length == 0 && topicId is null && status is null;
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
        _statusFilter.SelectedIndex = 0;
        ApplyFilter();
    }

    /// <summary>Routes a row action click to its handler.</summary>
    private void HandleRowAction(GridActionEventArgs e)
    {
        Quiz? quiz = _list.Grid.Rows[e.RowIndex].Tag as Quiz;
        if (quiz is null)
        {
            return;
        }

        switch (e.ActionName)
        {
            case QuestionsAction:
                OpenQuestionEditor(quiz);
                break;
            case EditAction:
                EditQuiz(quiz);
                break;
            case DeleteAction:
                DeleteQuiz(quiz);
                break;
        }
    }

    /// <summary>Opens the question editor of spec 5.5 for one quiz.</summary>
    private void OpenQuestionEditor(Quiz quiz)
    {
        using var editor = new QuestionEditForm(_quizzes, _questions, _choices, _answers, quiz.QuizId);
        editor.ShowDialog(FindForm());

        if (editor.ChangesSaved)
        {
            RefreshData(); // question counts may have changed
        }
    }

    /// <summary>Opens the "New quiz" modal card and stores the quiz on Save.</summary>
    private void NewQuiz()
    {
        List<Topic> topics = _topics.GetAll().OrderBy(topic => topic.Name).ToList();
        using var dialog = new QuizFormDialog("New quiz", topics)
        {
            ValidateInput = (title, topicId) => ValidateQuizTitle(title, topicId, ignoreQuizId: null),
        };

        if (ShowDialogOnOwner(dialog) != DialogResult.OK)
        {
            return;
        }

        var quiz = new Quiz
        {
            Title = dialog.QuizTitle,
            TopicId = dialog.QuizTopicId,
            Difficulty = dialog.QuizDifficulty,
            Status = QuizStatus.Draft, // a hand-made quiz is born Draft like a generated one (R-2 #4)
            CreatedAt = DateTimeOffset.UtcNow,
        };

        try
        {
            _quizzes.Add(quiz);
        }
        catch (DuplicateEntityException)
        {
            // Lost the race — the friendly duplicate message, never raw (R-2 #5).
            ReportStatus(DuplicateQuizMessage, false);
            return;
        }

        RefreshData();
        ReportStatus(SavedMessage, true);
    }

    /// <summary>Opens the quiz modal card and saves the changed metadata on Save.</summary>
    private void EditQuiz(Quiz quiz)
    {
        List<Topic> topics = _topics.GetAll().OrderBy(topic => topic.Name).ToList();
        using var dialog = new QuizFormDialog("Edit quiz", topics)
        {
            ValidateInput = (title, topicId) => ValidateQuizTitle(title, topicId, ignoreQuizId: quiz.QuizId),
        };
        dialog.QuizTitle = quiz.Title;
        dialog.QuizTopicId = quiz.TopicId;
        dialog.QuizDifficulty = quiz.Difficulty;

        if (ShowDialogOnOwner(dialog) != DialogResult.OK)
        {
            return;
        }

        // Only the metadata the form edits changes; status and timestamp are
        // preserved (status flips belong to §5.12, R-2 #4).
        quiz.Title = dialog.QuizTitle;
        quiz.TopicId = dialog.QuizTopicId;
        quiz.Difficulty = dialog.QuizDifficulty;

        try
        {
            _quizzes.Update(quiz);
        }
        catch (DuplicateEntityException)
        {
            ReportStatus(DuplicateQuizMessage, false);
            return;
        }

        RefreshData();
        ReportStatus(SavedMessage, true);
    }

    /// <summary>
    /// The CORRECTED two-branch delete flow of spec 5.4: branch on the
    /// attempts BEFORE any dialog, then confirm, then cascade delete.
    /// </summary>
    private void DeleteQuiz(Quiz quiz)
    {
        if (_attempts.GetByQuiz(quiz.QuizId).Count > 0)
        {
            // Blocked: no dialog, the strip explains the block (§5.4).
            ReportStatus(QuizHasAttemptsMessage, false);
            return;
        }

        if (!DialogForm.ShowConfirm(DeleteQuizDialogTitle, DeleteQuizDialogBody, "Delete"))
        {
            return;
        }

        try
        {
            if (_quizzes.Delete(quiz.QuizId))
            {
                // The unblocked delete cascades: the quiz's questions and
                // their choices go with it (schema ON DELETE CASCADE).
                ReportStatus(DeletedMessage, true);
            }
        }
        catch (ReferentialIntegrityException)
        {
            // The quiz gained an attempt between the check and the delete —
            // the same friendly block, never a raw SQLite error.
            ReportStatus(QuizHasAttemptsMessage, false);
        }

        RefreshData();
    }

    /// <summary>The empty-title, no-topic and duplicate guards (F8: before the DB).</summary>
    private string? ValidateQuizTitle(string? title, int topicId, int? ignoreQuizId)
    {
        if (topicId <= 0)
        {
            return NoTopicMessage;
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return EmptyQuizTitleMessage;
        }

        if (_quizzes.ExistsByTitle(topicId, title.Trim(), ignoreQuizId))
        {
            return DuplicateQuizMessage;
        }

        return null;
    }

    /// <summary>Shows a modal dialog over the window this panel lives in.</summary>
    private DialogResult ShowDialogOnOwner(Form dialog)
    {
        Form? owner = FindForm();
        return owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
    }
}

/// <summary>
/// The quiz modal card of spec 5.4: Title input, Topic combo (existing
/// topics), Difficulty segmented control — Save primary / Cancel secondary.
/// Validation runs through <see cref="ValidateInput"/> before the dialog
/// closes, so the inline error line of recipe 4.4 carries the failure.
/// </summary>
public sealed class QuizFormDialog : Form
{
    private const int ContentInset = UITheme.Spacing24;
    private const int FieldGap = UITheme.Spacing12;
    private const int ButtonGap = UITheme.Spacing8;

    private static readonly string[] Difficulties = { "Easy", "Medium", "Hard" };

    private readonly InputField _title;
    private readonly ThemedComboBox _topic;
    private readonly SegmentedControl _difficulty;
    private readonly FlatButton _save;
    private readonly FlatButton _cancel;
    private readonly List<int> _topicIds = new();

/// <summary>Creates the dialog with the given topics to pick from.</summary>
public QuizFormDialog(string title, IReadOnlyList<Topic> topics)
{
    ArgumentNullException.ThrowIfNull(topics);
    Text = title;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        BackColor = UITheme.Surface;
        KeyPreview = true;

        var titleLabel = new Label
        {
            Text = title,
            Font = UITheme.BodySemibold,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Name = "QuizDialogTitle",
        };

        var titleFieldLabel = new Label
        {
            Text = "Title",
            Font = UITheme.BodySemibold,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Name = "QuizTitleLabel",
        };

        _title = new InputField(accessibleName: "Quiz title")
        {
            Name = "QuizTitleField",
            PlaceholderText = "Quiz title",
        };
        _title.TextChanged += static (sender, e) =>
        {
            if (sender is InputField field)
            {
                field.ClearError(); // the error line persists until the value validates (§4.4)
            }
        };

        var topicLabel = new Label
        {
            Text = "Topic",
            Font = UITheme.BodySemibold,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Name = "QuizTopicLabel",
        };

        _topic = new ThemedComboBox
        {
            Name = "QuizTopicCombo",
            AccessibleName = "Topic",
        };
        foreach (Topic topic in topics)
        {
            _topic.Items.Add(topic.Name);
            _topicIds.Add(topic.TopicId);
        }
        if (_topicIds.Count > 0)
        {
            _topic.SelectedIndex = 0; // a fresh quiz defaults to the first topic
        }

        var difficultyLabel = new Label
        {
            Text = "Difficulty",
            Font = UITheme.BodySemibold,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Name = "QuizDifficultyLabel",
        };

        _difficulty = new SegmentedControl
        {
            Name = "QuizDifficultySelector",
            AccessibleName = "Difficulty",
        };
        _difficulty.Items = Difficulties;

        _save = new FlatButton { Name = "SaveQuizButton", Text = "Save", Variant = UITheme.FlatButtonVariant.Primary };
        _save.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.Parent is QuizFormDialog dialog)
            {
                dialog.HandleSave();
            }
        };

        _cancel = new FlatButton
        {
            Name = "CancelQuizButton",
            Text = "Cancel",
            Variant = UITheme.FlatButtonVariant.Secondary,
            DialogResult = DialogResult.Cancel,
        };

        Controls.Add(titleLabel);
        Controls.Add(titleFieldLabel);
        Controls.Add(_title);
        Controls.Add(topicLabel);
        Controls.Add(_topic);
        Controls.Add(difficultyLabel);
        Controls.Add(_difficulty);
        Controls.Add(_save);
        Controls.Add(_cancel);

        AcceptButton = _save;
        CancelButton = _cancel;

        LayoutDialog(titleLabel, titleFieldLabel, topicLabel, difficultyLabel);
    }

    /// <summary>Runs before the dialog closes; returns the inline error or null. Gets title and topic.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<string?, int, string?>? ValidateInput { get; init; }

    /// <summary>The entered (trimmed) quiz title; the preset for the edit case.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string QuizTitle
    {
        get => _title.Text.Trim();
        set => _title.Text = value;
    }

    /// <summary>The selected topic's id, or -1 when no topic exists to pick.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int QuizTopicId
    {
        get => _topic.SelectedIndex >= 0 && _topic.SelectedIndex < _topicIds.Count
            ? _topicIds[_topic.SelectedIndex]
            : -1;
        set
        {
            int index = _topicIds.IndexOf(value);
            _topic.SelectedIndex = index >= 0 ? index : _topicIds.Count > 0 ? 0 : -1;
        }
    }

    /// <summary>The selected difficulty.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Difficulty QuizDifficulty
    {
        get => _difficulty.SelectedIndex switch
        {
            1 => Difficulty.Medium,
            2 => Difficulty.Hard,
            _ => Difficulty.Easy,
        };
        set => _difficulty.SelectedIndex = value switch
        {
            Difficulty.Medium => 1,
            Difficulty.Hard => 2,
            _ => 0,
        };
    }

    /// <summary>Save runs the caller's validation; an error keeps the dialog open, inline.</summary>
    private void HandleSave()
    {
        string? error = ValidateInput?.Invoke(_title.Text, QuizTopicId);

        if (error is not null)
        {
            _title.ShowError(error);
            return;
        }

        DialogResult = DialogResult.OK;
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

    /// <summary>Sizes the card and places its content (§4.11 dialog anatomy).</summary>
    private void LayoutDialog(Control titleLabel, Control titleFieldLabel, Control topicLabel, Control difficultyLabel)
    {
        int titleHeight = UITheme.Scaled(this, 22);
        int labelHeight = UITheme.Scaled(this, 18);
        int buttonHeight = UITheme.Scaled(this, 32);
        int fieldHeight = UITheme.Scaled(this, 52); // the 32px field + the reserved error slot
        int comboSlot = UITheme.Scaled(this, 32);
        int width = UITheme.Scaled(this, 460);
        int inset = UITheme.Scaled(this, ContentInset);
        int contentWidth = width - inset * 2;

        int saveWidth = TextRenderer.MeasureText(_save.Text, UITheme.Body).Width + UITheme.Scaled(this, UITheme.Spacing16) * 2;
        int cancelWidth = TextRenderer.MeasureText(_cancel.Text, UITheme.Body).Width + UITheme.Scaled(this, UITheme.Spacing16) * 2;

        int titleTop = inset;
        int fieldLabelTop = titleTop + titleHeight + UITheme.Scaled(this, UITheme.Spacing16);
        int titleFieldTop = fieldLabelTop + labelHeight + UITheme.Scaled(this, UITheme.Spacing4);
        int topicTop = titleFieldTop + fieldHeight + FieldGap;
        int topicFieldTop = topicTop + labelHeight + UITheme.Scaled(this, UITheme.Spacing4);
        int difficultyTop = topicFieldTop + comboSlot + FieldGap;
        int difficultyFieldTop = difficultyTop + labelHeight + UITheme.Scaled(this, UITheme.Spacing4);
        int buttonsTop = difficultyFieldTop + comboSlot + UITheme.Scaled(this, UITheme.Spacing24);
        int height = buttonsTop + buttonHeight + inset;

        titleLabel.Location = new Point(inset, titleTop);
        titleLabel.Size = new Size(contentWidth, titleHeight);

        titleFieldLabel.Location = new Point(inset, fieldLabelTop);
        titleFieldLabel.Size = new Size(contentWidth, labelHeight);

        _title.Location = new Point(inset, titleFieldTop);
        _title.Size = new Size(contentWidth, fieldHeight);

        topicLabel.Location = new Point(inset, topicTop);
        topicLabel.Size = new Size(contentWidth, labelHeight);

        _topic.Location = new Point(inset, topicFieldTop);
        _topic.Width = contentWidth; // the combo's height follows its font (native flat chrome)

        difficultyLabel.Location = new Point(inset, difficultyTop);
        difficultyLabel.Size = new Size(contentWidth, labelHeight);

        _difficulty.Location = new Point(inset, difficultyFieldTop);
        _difficulty.Size = new Size(contentWidth, comboSlot);

        _save.Size = new Size(saveWidth, buttonHeight);
        _save.Location = new Point(width - inset - saveWidth, buttonsTop);
        _cancel.Size = new Size(cancelWidth, buttonHeight);
        _cancel.Location = new Point(width - inset - saveWidth - ButtonGap - cancelWidth, buttonsTop);

        ClientSize = new Size(width, height);
    }
}
