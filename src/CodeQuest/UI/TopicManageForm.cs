// =============================================================================
// CodeQuest — Phase P8: Topic & Quiz Management
// The topic management screen of the design of record (spec 5.3), hosted as
// the shell's "Topics" module panel (§5.2 — panels persist, the shell swaps).
// Look at: the layout — toolbar row (8px gaps): the search box ("Search
//          topics…", 240px) and the trailing "New topic" primary button;
//          16px; one full-height grid card (recipe 4.6) with the columns
//          Name · Category · Quizzes · Created and the trailing Edit/Delete
//          text actions. The search filters live while typing (no submit
//          button). The topic form is a modal card ("New topic" / "Edit
//          topic") with the Name input and the category ComboBox — exactly
//          the two values Programming / Cybersecurity, DropDownList, never
//          free text (R-2 #7).
// Delete flow (the CORRECTED two-branch flow of spec 5.3): the schema keeps
//          quizzes.topic_id ON DELETE RESTRICT, so the flow branches BEFORE
//          any dialog — a topic with no quizzes gets the confirm dialog
//          ("Delete this topic?" / "This topic will be removed. This cannot
//          be undone.", Delete destructive + Cancel) and then "Topic
//          deleted." in the strip; a topic WITH quizzes is blocked with the
//          strip message and no dialog — never a raw SQLite error.
// R-2 obligations carried here: #5 (duplicate topic name → the spec's exact
//          friendly message, pre-checked inline AND caught as
//          DuplicateEntityException — never raw), #7 (the two-value category
//          dropdown), F8 (the empty-name guard runs before the DB).
// =============================================================================

using CodeQuest.Data;
using CodeQuest.Models;
using System.Drawing;
using System.Windows.Forms;

namespace CodeQuest.UI;

/// <summary>
/// The topic management screen (spec 5.3): search, list, create/edit/delete.
/// </summary>
public sealed class TopicManageForm : ModulePanel
{
    /// <summary>The two category values the schema allows (R-2 #7) — the dropdown's whole world.</summary>
    public static readonly string[] Categories = { "Programming", "Cybersecurity" };

    // The design of record's exact copy (§4.11, §5.3 states and feedback).
    private const string EmptyTopicNameMessage = "Enter a topic name.";
    private const string DuplicateTopicMessage = "A topic with this name already exists.";
    private const string TopicHasQuizzesMessage = "This topic still has quizzes. Delete its quizzes first.";
    private const string DeleteTopicDialogTitle = "Delete this topic?";
    private const string DeleteTopicDialogBody = "This topic will be removed. This cannot be undone.";
    private const string SavedMessage = "Topic saved.";
    private const string UpdatedMessage = "Topic updated.";
    private const string DeletedMessage = "Topic deleted.";
    private const string EmptyTitle = "No topics yet";
    private const string EmptyCaption = "Choose New topic to add the first one.";
    private const string NoMatchTitle = "No topics match your search.";

    private const string EditAction = "Edit";
    private const string DeleteAction = "Delete";

    private readonly ITopicRepository _topics;
    private readonly IQuizRepository _quizzes;
    private readonly InputField _search;
    private readonly FlatButton _newTopic;
    private readonly ListCardGrid _list;
    private Dictionary<int, int> _quizCounts = new();

    /// <summary>Creates the screen for the topic and quiz repositories.</summary>
    public TopicManageForm(ITopicRepository topics, IQuizRepository quizzes)
    {
        ArgumentNullException.ThrowIfNull(topics);
        ArgumentNullException.ThrowIfNull(quizzes);
        _topics = topics;
        _quizzes = quizzes;

        BackColor = UITheme.Background;
        Dock = DockStyle.Fill;
        Visible = false;

        int toolbarHeight = UITheme.Scaled(this, 32);

        _search = new InputField(accessibleName: "Search topics")
        {
            Name = "TopicsSearchField",
            PlaceholderText = "Search topics…",
            Location = new Point(0, 0),
            Size = new Size(UITheme.Scaled(this, 240), toolbarHeight),
        };
        _search.TextChanged += static (sender, e) =>
        {
            if (sender is InputField field && field.Parent is TopicManageForm form)
            {
                form.ApplyFilter();
            }
        };

        _newTopic = new FlatButton
        {
            Name = "NewTopicButton",
            Text = "New topic",
            Location = new Point(Width - UITheme.Scaled(this, 110), 0),
            Size = new Size(UITheme.Scaled(this, 110), toolbarHeight),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        _newTopic.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.Parent is TopicManageForm form)
            {
                form.NewTopic();
            }
        };

        _list = new ListCardGrid
        {
            Name = "TopicsListCard",
            Location = new Point(0, toolbarHeight + UITheme.Scaled(this, UITheme.Spacing16)),
            Size = new Size(Width, Height - toolbarHeight - UITheme.Scaled(this, UITheme.Spacing16)),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
        };
        _list.Grid.Name = "TopicsGrid";
        _list.Grid.AccessibleName = "Topics";
        _list.Grid.AddTextColumn("TopicName", "Name", 30f, extraLeftInset: UITheme.Scaled(this, UITheme.Spacing8));
        _list.Grid.AddTextColumn("TopicCategory", "Category", 20f, minimumWidth: 110);
        _list.Grid.AddTextColumn("TopicQuizCount", "Quizzes", 12f, minimumWidth: 78);
        _list.Grid.AddTextColumn("TopicCreated", "Created", 18f, minimumWidth: 94);
        _list.Grid.AddActionColumn("TopicEdit", EditAction, GridActionKind.Secondary);
        _list.Grid.AddActionColumn("TopicDelete", DeleteAction, GridActionKind.Destructive);
        _list.Grid.ActionClicked += static (sender, e) =>
        {
            if (sender is IosListGrid grid && grid.Parent is ListCardGrid card && card.Parent is TopicManageForm form)
            {
                form.HandleRowAction(e);
            }
        };
        _list.EmptyActionClicked += static (sender, e) =>
        {
            if (sender is ListCardGrid card && card.Parent is TopicManageForm form)
            {
                form._search.ClearText(); // "Clear search" resets the live filter
            }
        };

        Controls.Add(_search);
        Controls.Add(_newTopic);
        Controls.Add(_list);
    }

    /// <inheritdoc />
    public override void RefreshData()
    {
        // The quiz counts only change on CRUD, not per keystroke — one read
        // per topic per refresh, then the live search reuses them.
        Dictionary<int, int> counts = new();
        foreach (Topic topic in _topics.GetAll())
        {
            counts[topic.TopicId] = _quizzes.GetByTopic(topic.TopicId).Count;
        }

        _quizCounts = counts;
        ApplyFilter();
    }

    /// <summary>Applies the live search to the grid (spec 5.3: filter while typing).</summary>
    private void ApplyFilter()
    {
        string searchText = _search.Text;
        IReadOnlyList<Topic> topics = searchText.Length == 0
            ? _topics.GetAll()
            : _topics.Search(searchText); // the P4 live search (name and description)

        _list.Grid.SuspendLayout();
        _list.Grid.Rows.Clear();

        foreach (Topic topic in topics)
        {
            int count = _quizCounts.TryGetValue(topic.TopicId, out int value) ? value : 0;
            int index = _list.Grid.Rows.Add(
                topic.Name,
                topic.Category,
                count.ToString(),
                UiFormat.FormatDate(topic.CreatedAt),
                EditAction,
                DeleteAction);
            _list.Grid.Rows[index].Tag = topic;
        }

        _list.Grid.ResumeLayout();

        // The states of spec 5.3: the grid never renders its own message.
        if (_list.Grid.Rows.Count == 0)
        {
            if (searchText.Length == 0)
            {
                _list.ShowEmptyState(EmptyTitle, EmptyCaption);
            }
            else
            {
                _list.ShowEmptyState(NoMatchTitle, string.Empty, "Clear search");
            }
        }
        else
        {
            _list.HideEmptyState();
        }
    }

    /// <summary>Routes a row action click to its handler.</summary>
    private void HandleRowAction(GridActionEventArgs e)
    {
        Topic? topic = _list.Grid.Rows[e.RowIndex].Tag as Topic;
        if (topic is null)
        {
            return;
        }

        if (e.ActionName == EditAction)
        {
            EditTopic(topic);
        }
        else if (e.ActionName == DeleteAction)
        {
            DeleteTopic(topic);
        }
    }

    /// <summary>Opens the "New topic" modal card and stores the topic on Save.</summary>
    private void NewTopic()
    {
        using var dialog = new TopicFormDialog("New topic")
        {
            ValidateInput = name => ValidateTopicName(name, ignoreTopicId: null),
        };

        if (ShowDialogOnOwner(dialog) != DialogResult.OK)
        {
            return;
        }

        var topic = new Topic
        {
            Name = dialog.TopicName,
            Category = dialog.TopicCategory,
            Description = string.Empty, // the schema requires a value; the form has no description field
            CreatedAt = DateTimeOffset.UtcNow,
        };

        try
        {
            _topics.Add(topic);
        }
        catch (DuplicateEntityException)
        {
            // Lost the race against another window — the friendly duplicate
            // message instead of a raw SQLite error (R-2 #5).
            ReportStatus(DuplicateTopicMessage, false);
            return;
        }

        RefreshData();
        ReportStatus(SavedMessage, true);
    }

    /// <summary>Opens the "Edit topic" modal card and saves the changes on Save.</summary>
    private void EditTopic(Topic topic)
    {
        using var dialog = new TopicFormDialog("Edit topic")
        {
            ValidateInput = name => ValidateTopicName(name, ignoreTopicId: topic.TopicId),
        };
        dialog.TopicName = topic.Name;
        dialog.TopicCategory = topic.Category;

        if (ShowDialogOnOwner(dialog) != DialogResult.OK)
        {
            return;
        }

        // The spec's form edits Name and Category only — the description and
        // creation timestamp are preserved.
        var updated = new Topic
        {
            TopicId = topic.TopicId,
            Name = dialog.TopicName,
            Category = dialog.TopicCategory,
            Description = topic.Description,
            CreatedAt = topic.CreatedAt,
        };

        try
        {
            _topics.Update(updated);
        }
        catch (DuplicateEntityException)
        {
            ReportStatus(DuplicateTopicMessage, false);
            return;
        }

        RefreshData();
        ReportStatus(UpdatedMessage, true);
    }

    /// <summary>
    /// The CORRECTED two-branch delete flow of spec 5.3: branch on the quiz
    /// count BEFORE any dialog, then confirm, then delete.
    /// </summary>
    private void DeleteTopic(Topic topic)
    {
        if (_quizzes.GetByTopic(topic.TopicId).Count > 0)
        {
            // Blocked: no dialog, the strip explains the block (§5.3).
            ReportStatus(TopicHasQuizzesMessage, false);
            return;
        }

        if (!DialogForm.ShowConfirm(DeleteTopicDialogTitle, DeleteTopicDialogBody, "Delete"))
        {
            return;
        }

        try
        {
            if (_topics.Delete(topic.TopicId))
            {
                ReportStatus(DeletedMessage, true);
            }
        }
        catch (ReferentialIntegrityException)
        {
            // The topic gained a quiz between the check and the delete — the
            // same friendly block, never a raw SQLite error.
            ReportStatus(TopicHasQuizzesMessage, false);
        }

        RefreshData();
    }

    /// <summary>The empty-name and duplicate guards of the topic form (F8: before the DB).</summary>
    private string? ValidateTopicName(string? name, int? ignoreTopicId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return EmptyTopicNameMessage;
        }

        if (_topics.ExistsByName(name.Trim(), ignoreTopicId))
        {
            return DuplicateTopicMessage;
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
/// The topic modal card of spec 5.3: Name input + the two-value category
/// dropdown (R-2 #7), Save primary / Cancel secondary. All validation runs
/// through <see cref="ValidateInput"/> before the dialog closes, so the
/// inline error line of recipe 4.4 carries the failure.
/// </summary>
public sealed class TopicFormDialog : Form
{
    private const int ContentInset = UITheme.Spacing24;
    private const int FieldGap = UITheme.Spacing12;
    private const int ButtonGap = UITheme.Spacing8;

    private readonly InputField _name;
    private readonly ThemedComboBox _category;
    private readonly FlatButton _save;
    private readonly FlatButton _cancel;

    /// <summary>Creates the dialog. <paramref name="title"/> is "New topic" or "Edit topic".</summary>
    public TopicFormDialog(string title)
    {
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
            Name = "TopicDialogTitle",
        };

        var nameLabel = new Label
        {
            Text = "Name",
            Font = UITheme.BodySemibold,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Name = "TopicNameLabel",
        };

        _name = new InputField(accessibleName: "Topic name")
        {
            Name = "TopicNameField",
            PlaceholderText = "Topic name",
        };
        _name.TextChanged += static (sender, e) =>
        {
            if (sender is InputField field)
            {
                field.ClearError(); // the error line persists until the value validates (§4.4)
            }
        };

        var categoryLabel = new Label
        {
            Text = "Category",
            Font = UITheme.BodySemibold,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Name = "TopicCategoryLabel",
        };

        _category = new ThemedComboBox
        {
            Name = "TopicCategoryCombo",
            AccessibleName = "Category",
        };
        foreach (string category in TopicManageForm.Categories)
        {
            _category.Items.Add(category);
        }
        _category.SelectedIndex = 0; // a fresh topic defaults to the first category

        _save = new FlatButton { Name = "SaveTopicButton", Text = "Save", Variant = UITheme.FlatButtonVariant.Primary };
        _save.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.Parent is TopicFormDialog dialog)
            {
                dialog.HandleSave();
            }
        };

        _cancel = new FlatButton
        {
            Name = "CancelTopicButton",
            Text = "Cancel",
            Variant = UITheme.FlatButtonVariant.Secondary,
            DialogResult = DialogResult.Cancel,
        };

        Controls.Add(titleLabel);
        Controls.Add(nameLabel);
        Controls.Add(_name);
        Controls.Add(categoryLabel);
        Controls.Add(_category);
        Controls.Add(_save);
        Controls.Add(_cancel);

        AcceptButton = _save;
        CancelButton = _cancel;

        LayoutDialog(titleLabel, nameLabel, categoryLabel);
    }

    /// <summary>Runs before the dialog closes; returns the inline error message or null.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<string?, string?>? ValidateInput { get; init; }

    /// <summary>The entered (trimmed) topic name; the preset for the edit case.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string TopicName
    {
        get => _name.Text.Trim();
        set => _name.Text = value;
    }

    /// <summary>The selected category — one of <see cref="TopicManageForm.Categories"/> by construction.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string TopicCategory
    {
        get => _category.SelectedItem as string ?? TopicManageForm.Categories[0];
        set
        {
            int index = _category.Items.IndexOf(value);
            _category.SelectedIndex = index >= 0 ? index : 0;
        }
    }

    /// <summary>Save runs the caller's validation; an error keeps the dialog open, inline.</summary>
    private void HandleSave()
    {
        string? error = ValidateInput?.Invoke(_name.Text);

        if (error is not null)
        {
            _name.ShowError(error);
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
    private void LayoutDialog(Control titleLabel, Control nameLabel, Control categoryLabel)
    {
        int titleHeight = UITheme.Scaled(this, 22);
        int labelHeight = UITheme.Scaled(this, 18);
        int buttonHeight = UITheme.Scaled(this, 32);
        int fieldHeight = UITheme.Scaled(this, 52); // the 32px field + the reserved error slot
        int comboSlot = UITheme.Scaled(this, 32);
        int width = UITheme.Scaled(this, 440);
        int inset = UITheme.Scaled(this, ContentInset);
        int contentWidth = width - inset * 2;

        int saveWidth = TextRenderer.MeasureText(_save.Text, UITheme.Body).Width + UITheme.Scaled(this, UITheme.Spacing16) * 2;
        int cancelWidth = TextRenderer.MeasureText(_cancel.Text, UITheme.Body).Width + UITheme.Scaled(this, UITheme.Spacing16) * 2;

        int titleTop = inset;
        int nameTop = titleTop + titleHeight + UITheme.Scaled(this, UITheme.Spacing16);
        int nameFieldTop = nameTop + labelHeight + UITheme.Scaled(this, UITheme.Spacing4);
        int categoryTop = nameFieldTop + fieldHeight + FieldGap;
        int categoryFieldTop = categoryTop + labelHeight + UITheme.Scaled(this, UITheme.Spacing4);
        int buttonsTop = categoryFieldTop + comboSlot + UITheme.Scaled(this, UITheme.Spacing24);
        int height = buttonsTop + buttonHeight + inset;

        titleLabel.Location = new Point(inset, titleTop);
        titleLabel.Size = new Size(contentWidth, titleHeight);

        nameLabel.Location = new Point(inset, nameTop);
        nameLabel.Size = new Size(contentWidth, labelHeight);

        _name.Location = new Point(inset, nameFieldTop);
        _name.Size = new Size(contentWidth, fieldHeight);

        categoryLabel.Location = new Point(inset, categoryTop);
        categoryLabel.Size = new Size(contentWidth, labelHeight);

        _category.Location = new Point(inset, categoryFieldTop);
        _category.Width = contentWidth; // the combo's height follows its font (native flat chrome)

        _save.Size = new Size(saveWidth, buttonHeight);
        _save.Location = new Point(width - inset - saveWidth, buttonsTop);
        _cancel.Size = new Size(cancelWidth, buttonHeight);
        _cancel.Location = new Point(width - inset - saveWidth - ButtonGap - cancelWidth, buttonsTop);

        ClientSize = new Size(width, height);
    }
}
