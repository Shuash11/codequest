// The topics screen.

using CodeQuest.Data;
using CodeQuest.Models;
using CodeQuest.Services;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CodeQuest.UI;

public sealed class TopicManageForm : ModulePanel
{
    public static readonly string[] Categories = { "Programming", "Cybersecurity" };

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
    private const string ListCaption = "Topics group your quizzes; every quiz belongs to one.";
    private const string NewTopicLabel = "+  New topic";

    private readonly ITopicRepository _topics;
    private readonly IQuizRepository _quizzes;
    private readonly ITopicGenerationService _topicGeneration;
    private readonly Label _listCaption;
    private readonly InputField _search;
    private readonly FlatButton _newTopic;
    private readonly ListCardGrid _list;
    private Dictionary<int, int> _quizCounts = new();

    public TopicManageForm(ITopicRepository topics, IQuizRepository quizzes, ITopicGenerationService topicGeneration)
    {
        ArgumentNullException.ThrowIfNull(topics);
        ArgumentNullException.ThrowIfNull(quizzes);
        ArgumentNullException.ThrowIfNull(topicGeneration);
        _topics = topics;
        _quizzes = quizzes;
        _topicGeneration = topicGeneration;

        BackColor = UITheme.Background;
        Dock = DockStyle.Fill;
        Visible = false;

        int captionHeight = UITheme.Scaled(this, 20);
        _listCaption = new Label
        {
            Name = "TopicsListCaption",
            Text = ListCaption,
            Font = UITheme.Body,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Background,
            AutoSize = false,
            Location = new Point(0, 0),
            Size = new Size(Math.Max(Width, 1), captionHeight),
        };

        int toolbarHeight = UITheme.Scaled(this, 32);
        int toolbarTop = captionHeight + UITheme.Scaled(this, UITheme.Spacing16);

        _search = new InputField(accessibleName: "Search topics…")
        {
            Name = "TopicsSearchField",
            PlaceholderText = "Search topics…",
            Location = new Point(0, toolbarTop),
            Size = new Size(UITheme.Scaled(this, 240), toolbarHeight),
        };
        _search.TextChanged += static (sender, e) =>
        {
            if (sender is InputField field && field.FindAncestor<TopicManageForm>() is { } form)
            {
                form.ApplyFilter();
            }
        };

        _newTopic = new FlatButton
        {
            Name = "NewTopicButton",
            Text = NewTopicLabel,
            AccessibleName = "New topic",
            Location = new Point(Width - UITheme.Scaled(this, 124), toolbarTop),
            Size = new Size(UITheme.Scaled(this, 124), toolbarHeight),
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
            Location = new Point(0, toolbarTop + toolbarHeight + UITheme.Scaled(this, UITheme.Spacing16)),
            Size = new Size(Width, Height - toolbarTop - toolbarHeight - UITheme.Scaled(this, UITheme.Spacing16)),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
        };
        _list.Grid.Name = "TopicsGrid";
        _list.Grid.AccessibleName = "Topics";
        _list.Grid.AddTextColumn("TopicName", "Name", 30f, extraLeftInset: UITheme.Scaled(this, UITheme.Spacing8));
        _list.Grid.AddTextColumn("TopicCategory", "Category", 20f, minimumWidth: 110);
        _list.Grid.AddTextColumn("TopicQuizCount", "Quizzes", 12f, minimumWidth: 78);
        _list.Grid.AddTextColumn("TopicCreated", "Created", 18f, minimumWidth: 94);
        _list.Grid.AddActionColumn(EditAction, EditAction, GridActionKind.Secondary);
        _list.Grid.AddActionColumn(DeleteAction, DeleteAction, GridActionKind.Destructive);
        _list.Grid.ActionClicked += static (sender, e) =>
        {
            if (sender is IosListGrid grid && grid.FindAncestor<TopicManageForm>() is { } form)
            {
                form.HandleRowAction(e);
            }
        };
        _list.EmptyActionClicked += static (sender, e) =>
        {
            if (sender is ListCardGrid card && card.Parent is TopicManageForm form)
            {
                form._search.ClearText();
            }
        };

        Controls.Add(_search);
        Controls.Add(_newTopic);
        Controls.Add(_list);
        Controls.Add(_listCaption);
    }

    public override void RefreshData()
    {
        Dictionary<int, int> counts = new();
        foreach (Topic topic in _topics.GetAll())
        {
            counts[topic.TopicId] = _quizzes.GetByTopic(topic.TopicId).Count;
        }

        _quizCounts = counts;
        _listCaption.Width = Math.Max(Width, 1);
        ApplyFilter();
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        _listCaption.Width = Math.Max(Width, 1);
    }

    private void ApplyFilter()
    {
        string searchText = _search.Text;
        IReadOnlyList<Topic> topics = searchText.Length == 0
            ? _topics.GetAll()
            : _topics.Search(searchText);

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

    private void NewTopic()
    {
        using var dialog = new TopicFormDialog(_topicGeneration)
        {
            ValidateInput = name => ValidateTopicName(name, ignoreTopicId: null),
        };

        if (ShowDialogOnOwner(dialog) != DialogResult.OK)
        {
            return;
        }

        PersistNewTopic(dialog);
    }

    internal bool PersistNewTopic(TopicFormDialog dialog)
    {
        var topic = new Topic
        {
            Name = dialog.TopicName,
            Category = dialog.TopicCategory,
            Description = dialog.TopicDescription,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        try
        {
            _topics.Add(topic);
        }
        catch (DuplicateEntityException)
        {
            ReportStatus(DuplicateTopicMessage, false);
            return false;
        }

        RefreshData();
        ReportStatus(SavedMessage, true);
        return true;
    }

    private void EditTopic(Topic topic)
    {
        using var dialog = new EditTopicDialog("Edit topic")
        {
            ValidateInput = name => ValidateTopicName(name, ignoreTopicId: topic.TopicId),
        };
        dialog.TopicName = topic.Name;
        dialog.TopicCategory = topic.Category;

        if (ShowDialogOnOwner(dialog) != DialogResult.OK)
        {
            return;
        }

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

    private void DeleteTopic(Topic topic)
    {
        if (_quizzes.GetByTopic(topic.TopicId).Count > 0)
        {
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
            ReportStatus(TopicHasQuizzesMessage, false);
        }

        RefreshData();
    }

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

    private DialogResult ShowDialogOnOwner(Form dialog)
    {
        Form? owner = FindForm();
        return owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
    }
}

public abstract class TopicDialogShell : Form
{
    protected TopicDialogShell(string title)
    {
        FormBorderStyle = FormBorderStyle.None;
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        BackColor = UITheme.SurfaceRaised;
        KeyPreview = true;

        Paint += static (sender, e) =>
        {
            if (sender is Form form)
            {
                using var borderPen = new Pen(UITheme.BorderHairline);
                using GraphicsPath borderPath = UITheme.RoundedPath(
                    new Rectangle(0, 0, form.Width - 1, form.Height - 1), UITheme.RadiusCard);
                e.Graphics.DrawPath(borderPen, borderPath);
            }
        };
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.ClassStyle |= 0x20000;
            return parameters;
        }
    }

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
}

public sealed class TopicFormDialog : TopicDialogShell
{
    private const string DialogTitle = "New topic";
    private const string DescribeLabelText = "Describe your topic";
    private const string DescribePlaceholder = "e.g. Common SQL injection attacks and how to prevent them";
    private const string DescribeRangeMessage = "Use between 5 and 300 characters.";
    private const string GenerateButtonLabel = "Generate with AI";
    private const string GeneratingButtonLabel = "Generating…";
    private const string ConnectionNote = "Needs an internet connection.";
    private const string GeneratingStatus = "Generating topic…";
    private const string CancelLabel = "Cancel";
    private const string PreviewHeaderText = "AI suggestion — edit anything before saving";
    private const string BannerTitleText = "Topic generation is unavailable right now.";
    private const string BannerManualLine = "You can still fill in the fields manually and save.";

    private const string NoProviderMessage = "No AI provider is configured.";
    private const string AuthMessage = "The AI provider rejected the request. Check the provider configuration.";
    private const string QuotaMessage = "The AI provider reached its request limit. Try again later.";
    private const string NetworkMessage = "Couldn't reach the AI provider. Check the internet connection.";
    private const string HttpMessage = "The AI provider returned an error. Try again later.";
    private const string BadOutputMessage = "The AI provider returned an unusable response. Try again.";
    private const string GenericFailureMessage = "Topic generation failed. Try again.";

    private const int ContentInset = UITheme.Spacing24;
    private const int MinDialogWidth = 480;
    private const int DescribeAreaHeight = 90;
    private const int PreviewAreaHeight = 90;
    private const int BusyTrackWidth = 240;

    private readonly ITopicGenerationService _generation;
    private readonly Label _titleLabel;
    private readonly Label _describeLabel;
    private readonly InputArea _describe;
    private readonly FlatButton _generate;
    private readonly Label _note;
    private readonly Panel _busyBlock;
    private readonly ProgressTrack _busyTrack;
    private readonly Label _busyStatus;
    private readonly FlatButton _busyCancel;
    private readonly Panel _banner;
    private readonly Label _bannerTitle;
    private readonly Label _bannerReason;
    private readonly Label _bannerManual;
    private readonly Label _previewHeader;
    private readonly Label _nameLabel;
    private readonly InputField _name;
    private readonly Label _categoryLabel;
    private readonly ThemedComboBox _category;
    private readonly Label _descriptionLabel;
    private readonly InputArea _description;
    private readonly FlatButton _save;
    private readonly FlatButton _cancel;
    private readonly System.Windows.Forms.Timer _elapsedTimer = new() { Interval = 1000 };

    private bool _busy;
    private int _generationId;
    private DateTimeOffset _startedAt;

    public TopicFormDialog(ITopicGenerationService generation)
        : base(DialogTitle)
    {
        ArgumentNullException.ThrowIfNull(generation);
        _generation = generation;

        _titleLabel = new Label
        {
            Text = DialogTitle,
            Name = "TopicDialogTitle",
            Font = UITheme.BodySemibold,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.SurfaceRaised,
            AutoSize = false,
        };

        _describeLabel = MakeFieldLabel(DescribeLabelText, "TopicDescribeLabel");
        _describe = new InputArea(accessibleName: DescribeLabelText)
        {
            Name = "TopicDescribeField",
            PlaceholderText = DescribePlaceholder,
        };
        _describe.LabeledBy = _describeLabel;
        _describe.TextChanged += static (sender, e) =>
        {
            if (sender is InputArea area)
            {
                area.ClearError();
            }
        };

        _generate = new FlatButton
        {
            Name = "TopicGenerateButton",
            Text = GenerateButtonLabel,
            Variant = UITheme.FlatButtonVariant.Primary,
        };
        _generate.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<TopicFormDialog>() is { } dialog)
            {
                dialog.StartGeneration();
            }
        };

        _note = new Label
        {
            Name = "TopicGenerateNote",
            Text = ConnectionNote,
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.SurfaceRaised,
            AutoSize = false,
        };

        _busyBlock = new Panel
        {
            Name = "TopicBusyBlock",
            BackColor = UITheme.SurfaceRaised,
            Visible = false,
        };
        _busyTrack = new ProgressTrack
        {
            Name = "TopicBusyTrack",
        };
        _busyStatus = new Label
        {
            Name = "TopicBusyStatus",
            Text = GeneratingStatus,
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.SurfaceRaised,
            AutoSize = false,
        };
        _busyCancel = new FlatButton
        {
            Name = "TopicBusyCancel",
            Text = CancelLabel,
            Variant = UITheme.FlatButtonVariant.Secondary,
        };
        _busyCancel.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<TopicFormDialog>() is { } dialog)
            {
                dialog.CancelGeneration();
            }
        };
        _busyBlock.Controls.Add(_busyTrack);
        _busyBlock.Controls.Add(_busyStatus);
        _busyBlock.Controls.Add(_busyCancel);

        _banner = new Panel
        {
            Name = "TopicFailureBanner",
            BackColor = UITheme.WarningTint,
            Visible = false,
        };
        _banner.Paint += static (sender, e) =>
        {
            if (sender is Panel panel)
            {
                using var pen = new Pen(UITheme.Warning);
                using GraphicsPath path = UITheme.RoundedPath(
                    new Rectangle(0, 0, panel.Width - 1, panel.Height - 1), UITheme.Scaled(panel, UITheme.RadiusControl));
                e.Graphics.DrawPath(pen, path);
            }
        };
        _bannerTitle = new Label
        {
            Name = "TopicBannerTitle",
            Text = BannerTitleText,
            Font = UITheme.BodySemibold,
            ForeColor = UITheme.WarningText,
            BackColor = UITheme.WarningTint,
            AutoSize = false,
        };
        _bannerReason = new Label
        {
            Name = "TopicBannerReason",
            Font = UITheme.Body,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.WarningTint,
            AutoSize = false,
        };
        _bannerManual = new Label
        {
            Name = "TopicBannerManual",
            Text = BannerManualLine,
            Font = UITheme.Body,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.WarningTint,
            AutoSize = false,
        };
        _banner.Controls.Add(_bannerTitle);
        _banner.Controls.Add(_bannerReason);
        _banner.Controls.Add(_bannerManual);

        _previewHeader = new Label
        {
            Name = "TopicPreviewHeader",
            Text = PreviewHeaderText,
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.SurfaceRaised,
            AutoSize = false,
            Visible = false,
        };

        _nameLabel = MakeFieldLabel("Name", "TopicNameLabel");
        _name = new InputField(reserveErrorLine: true, accessibleName: "Topic name")
        {
            Name = "TopicNameField",
            PlaceholderText = "Topic name",
        };
        _name.LabeledBy = _nameLabel;
        _name.TextChanged += static (sender, e) =>
        {
            if (sender is InputField field)
            {
                field.ClearError();
            }
        };

        _categoryLabel = MakeFieldLabel("Category", "TopicCategoryLabel");
        _category = new ThemedComboBox
        {
            Name = "TopicCategoryCombo",
            AccessibleName = "Category",
        };
        foreach (string category in TopicManageForm.Categories)
        {
            _category.Items.Add(category);
        }
        _category.SelectedIndex = 0;

        _descriptionLabel = MakeFieldLabel("Description", "TopicDescriptionLabel");
        _description = new InputArea(accessibleName: "Topic description")
        {
            Name = "TopicDescriptionField",
            PlaceholderText = "Topic description",
        };
        _description.LabeledBy = _descriptionLabel;

        _save = new FlatButton { Name = "SaveTopicButton", Text = "Save", Variant = UITheme.FlatButtonVariant.Primary };
        _save.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<TopicFormDialog>() is { } dialog)
            {
                dialog.HandleSave();
            }
        };

        _cancel = new FlatButton
        {
            Name = "CancelTopicButton",
            Text = CancelLabel,
            Variant = UITheme.FlatButtonVariant.Secondary,
            DialogResult = DialogResult.Cancel,
        };

        Controls.Add(_titleLabel);
        Controls.Add(_describeLabel);
        Controls.Add(_describe);
        Controls.Add(_generate);
        Controls.Add(_note);
        Controls.Add(_busyBlock);
        Controls.Add(_banner);
        Controls.Add(_previewHeader);
        Controls.Add(_nameLabel);
        Controls.Add(_name);
        Controls.Add(_categoryLabel);
        Controls.Add(_category);
        Controls.Add(_descriptionLabel);
        Controls.Add(_description);
        Controls.Add(_save);
        Controls.Add(_cancel);

        AcceptButton = _save;
        CancelButton = _cancel;

        _elapsedTimer.Tick += static (sender, e) =>
        {
            if (sender is System.Windows.Forms.Timer timer && timer.Tag is TopicFormDialog dialog)
            {
                double seconds = (DateTimeOffset.UtcNow - dialog._startedAt).TotalSeconds;
                dialog._busyStatus.Text = $"{GeneratingStatus} {seconds:0} s";
            }
        };
        _elapsedTimer.Tag = this;

        LayoutDialog();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _elapsedTimer.Stop();
            _elapsedTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<string?, string?>? ValidateInput { get; init; }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string TopicName
    {
        get => _name.Text.Trim();
        set => _name.Text = value;
    }

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

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string TopicDescription
    {
        get => _description.Text.Trim();
        set => _description.Text = value;
    }

    public bool IsBusy => _busy;

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

    private void StartGeneration()
    {
        if (_busy)
        {
            return;
        }

        string describeText = _describe.Text.Trim();

        if (describeText.Length < TopicGenerationRequest.MinDescriptionLength
            || describeText.Length > TopicGenerationRequest.MaxDescriptionLength)
        {
            _describe.ShowError(DescribeRangeMessage);
            return;
        }

        var request = new TopicGenerationRequest(describeText);

        _busy = true;
        _generationId++;
        int generationId = _generationId;
        _startedAt = DateTimeOffset.UtcNow;

        SetBusyState(busy: true);
        _busyTrack.Marquee = true;
        _busyBlock.BringToFront();
        _elapsedTimer.Start();
        Refresh();

        Task.Run(() => _generation.GenerateTopic(request))
            .ContinueWith(
                task => HandleOutcome(task.Result, generationId),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void HandleOutcome(TopicGenerationOutcome outcome, int generationId)
    {
        if (IsDisposed || generationId != _generationId)
        {
            return;
        }

        _elapsedTimer.Stop();
        _busyTrack.Marquee = false;
        _busy = false;
        SetBusyState(busy: false);

        if (outcome.IsSuccess && outcome.Draft is not null)
        {
            _banner.Visible = false;
            _previewHeader.Visible = true;

            TopicDraft draft = outcome.Draft;
            _name.Text = draft.Name;
            TopicCategory = draft.Category;
            _description.Text = draft.Description;
            LayoutDialog();
            return;
        }

        _bannerReason.Text = DescribeFailure(outcome.ProviderReasons);
        _banner.AccessibleName = $"{BannerTitleText} {_bannerReason.Text} {BannerManualLine}";
        _banner.Visible = true;
        LayoutDialog();
    }

    private void CancelGeneration()
    {
        if (!_busy)
        {
            return;
        }

        _generationId++;
        _elapsedTimer.Stop();
        _busyTrack.Marquee = false;
        _busy = false;
        SetBusyState(busy: false);
    }

    private void SetBusyState(bool busy)
    {
        _generate.Enabled = !busy;
        _generate.Text = busy ? GeneratingButtonLabel : GenerateButtonLabel;
        _describe.Enabled = !busy;
        _save.Enabled = !busy;
        _note.Visible = !busy;
        _busyBlock.Visible = busy;

        if (busy)
        {
            _busyStatus.Text = GeneratingStatus;
        }
    }

    private static string DescribeFailure(IReadOnlyList<string> reasons)
    {
        // Same rule as the quiz banner: a skipped tier (empty key) must not
        // mask a tier that really tried and failed.
        string first = reasons.FirstOrDefault(reason =>
            !reason.Contains(":not-configured", StringComparison.Ordinal)
            && !reason.Contains("empty-provider-list", StringComparison.Ordinal)) ?? string.Empty;

        if (first.Length == 0)
        {
            return NoProviderMessage;
        }

        string detail = first.Contains(":auth-failed", StringComparison.Ordinal) ? AuthMessage
            : first.Contains(":quota", StringComparison.Ordinal) ? QuotaMessage
            : first.Contains(":timeout", StringComparison.Ordinal) ? NetworkMessage
            : first.Contains(":http-error", StringComparison.Ordinal) ? HttpMessage
            : first.Contains(":bad-output", StringComparison.Ordinal) ? BadOutputMessage
            : GenericFailureMessage;

        return detail;
    }

    private Label MakeFieldLabel(string text, string name)
    {
        return new Label
        {
            Text = text,
            Name = name,
            Font = UITheme.BodySemibold,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.SurfaceRaised,
            AutoSize = false,
        };
    }

    private void LayoutDialog()
    {
        int inset = UITheme.Scaled(this, ContentInset);
        int titleHeight = UITheme.Scaled(this, 20);
        int labelHeight = UITheme.Scaled(this, 18);
        int captionHeight = UITheme.Scaled(this, 16);
        int buttonHeight = UITheme.Scaled(this, 32);
        int trackHeight = UITheme.Scaled(this, 8);
        int comboSlot = UITheme.Scaled(this, 32);
        int width = UITheme.Scaled(this, MinDialogWidth);
        int contentWidth = width - inset * 2;
        int gap8 = UITheme.Scaled(this, UITheme.Spacing8);
        int gap12 = UITheme.Scaled(this, UITheme.Spacing12);
        int gap16 = UITheme.Scaled(this, UITheme.Spacing16);
        int gap24 = UITheme.Scaled(this, UITheme.Spacing24);

        int generateWidth = TextRenderer.MeasureText(GeneratingButtonLabel, UITheme.Body).Width
            + UITheme.Scaled(this, UITheme.Spacing16) * 2;
        int busyCancelWidth = TextRenderer.MeasureText(CancelLabel, UITheme.Body).Width
            + UITheme.Scaled(this, UITheme.Spacing16) * 2;
        int saveWidth = TextRenderer.MeasureText(_save.Text, UITheme.Body).Width
            + UITheme.Scaled(this, UITheme.Spacing16) * 2;
        int cancelWidth = TextRenderer.MeasureText(_cancel.Text, UITheme.Body).Width
            + UITheme.Scaled(this, UITheme.Spacing16) * 2;

        int x = inset;
        int y = inset;

        _titleLabel.SetBounds(x, y, contentWidth, titleHeight);
        y += titleHeight + gap16;

        _describeLabel.SetBounds(x, y, contentWidth, labelHeight);
        y += labelHeight + UITheme.Scaled(this, UITheme.Spacing4);
        _describe.SetBounds(x, y, contentWidth, DescribeAreaHeight);
        y += DescribeAreaHeight + gap12;

        _generate.SetBounds(x, y, generateWidth, buttonHeight);
        y += buttonHeight + gap8;

        int busyBlockHeight = buttonHeight + UITheme.Scaled(this, UITheme.Spacing4) + captionHeight;
        if (_busyBlock.Visible)
        {
            _busyBlock.SetBounds(x, y, contentWidth, busyBlockHeight);
            _busyTrack.SetBounds(0, (buttonHeight - trackHeight) / 2, UITheme.Scaled(this, BusyTrackWidth), trackHeight);
            _busyCancel.SetBounds(UITheme.Scaled(this, BusyTrackWidth) + gap8, 0, busyCancelWidth, buttonHeight);
            _busyStatus.SetBounds(0, buttonHeight + UITheme.Scaled(this, UITheme.Spacing4), contentWidth, captionHeight);
            y += busyBlockHeight;
        }
        else
        {
            _note.SetBounds(x, y, contentWidth, captionHeight);
            y += captionHeight;
        }

        if (_banner.Visible)
        {
            int bannerHeight = LayoutBanner(contentWidth);
            _banner.SetBounds(x, y + gap8, contentWidth, bannerHeight);
            y += gap8 + bannerHeight + gap8;
        }
        else
        {
            y += gap24;
        }

        if (_previewHeader.Visible)
        {
            _previewHeader.SetBounds(x, y, contentWidth, captionHeight);
            y += captionHeight + gap8;
        }

        _nameLabel.SetBounds(x, y, contentWidth, labelHeight);
        y += labelHeight + UITheme.Scaled(this, UITheme.Spacing4);
        _name.SetBounds(x, y, contentWidth, UITheme.Scaled(this, 56));
        y += _name.Height + gap16;

        _categoryLabel.SetBounds(x, y, contentWidth, labelHeight);
        y += labelHeight + UITheme.Scaled(this, UITheme.Spacing4);
        _category.SetBounds(x, y, contentWidth, comboSlot);
        y += comboSlot + gap16;

        _descriptionLabel.SetBounds(x, y, contentWidth, labelHeight);
        y += labelHeight + UITheme.Scaled(this, UITheme.Spacing4);
        _description.SetBounds(x, y, contentWidth, PreviewAreaHeight);
        y += PreviewAreaHeight + gap24;

        _save.SetBounds(width - inset - saveWidth, y, saveWidth, buttonHeight);
        _cancel.SetBounds(width - inset - saveWidth - gap8 - cancelWidth, y, cancelWidth, buttonHeight);
        y += buttonHeight;

        ClientSize = new Size(width, y + inset);
    }

    private int LayoutBanner(int contentWidth)
    {
        int padding = UITheme.Scaled(this, UITheme.Spacing12);
        int innerWidth = contentWidth - padding * 2;
        int titleHeight = UITheme.Scaled(this, 20);
        int manualHeight = UITheme.Scaled(this, 20);
        int reasonHeight = TextRenderer.MeasureText(
            _bannerReason.Text,
            UITheme.Body,
            new Size(innerWidth, int.MaxValue),
            TextFormatFlags.WordBreak).Height;

        _bannerTitle.SetBounds(padding, padding, innerWidth, titleHeight);

        int reasonTop = padding + titleHeight + UITheme.Scaled(this, UITheme.Spacing4);
        _bannerReason.SetBounds(padding, reasonTop, innerWidth, reasonHeight);

        int manualTop = reasonTop + reasonHeight + UITheme.Scaled(this, UITheme.Spacing4);
        _bannerManual.SetBounds(padding, manualTop, innerWidth, manualHeight);

        return manualTop + manualHeight + padding;
    }
}

public sealed class EditTopicDialog : TopicDialogShell
{
    private const int ContentInset = UITheme.Spacing24;
    private const int FieldGap = UITheme.Spacing12;
    private const int ButtonGap = UITheme.Spacing8;

    private readonly InputField _name;
    private readonly ThemedComboBox _category;
    private readonly FlatButton _save;
    private readonly FlatButton _cancel;

    public EditTopicDialog(string title)
        : base(title)
    {
        var titleLabel = new Label
        {
            Text = title,
            Font = UITheme.Title,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.SurfaceRaised,
            AutoSize = false,
            Name = "TopicDialogTitle",
        };

        var nameLabel = new Label
        {
            Text = "Name",
            Font = UITheme.BodySemibold,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.SurfaceRaised,
            AutoSize = false,
            Name = "TopicNameLabel",
        };

        _name = new InputField(accessibleName: "Topic name")
        {
            Name = "TopicNameField",
            PlaceholderText = "Topic name",
        };
        _name.LabeledBy = nameLabel;
        _name.TextChanged += static (sender, e) =>
        {
            if (sender is InputField field)
            {
                field.ClearError();
            }
        };

        var categoryLabel = new Label
        {
            Text = "Category",
            Font = UITheme.BodySemibold,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.SurfaceRaised,
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
        _category.SelectedIndex = 0;

        _save = new FlatButton { Name = "SaveTopicButton", Text = "Save", Variant = UITheme.FlatButtonVariant.Primary };
        _save.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<EditTopicDialog>() is { } dialog)
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

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<string?, string?>? ValidateInput { get; init; }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string TopicName
    {
        get => _name.Text.Trim();
        set => _name.Text = value;
    }

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

    private void LayoutDialog(Control titleLabel, Control nameLabel, Control categoryLabel)
    {
        int titleHeight = UITheme.Scaled(this, 30);
        int labelHeight = UITheme.Scaled(this, 18);
        int buttonHeight = UITheme.Scaled(this, 32);
        int fieldHeight = UITheme.Scaled(this, 52);
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
        _category.Width = contentWidth;

        _save.Size = new Size(saveWidth, buttonHeight);
        _save.Location = new Point(width - inset - saveWidth, buttonsTop);
        _cancel.Size = new Size(cancelWidth, buttonHeight);
        _cancel.Location = new Point(width - inset - saveWidth - ButtonGap - cancelWidth, buttonsTop);

        ClientSize = new Size(width, height);
    }
}
