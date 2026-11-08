// =============================================================================
// CodeQuest — Phase P8: Topic & Quiz Management (New-topic reworked by WP4)
// The topic management screen of the design of record (spec 5.3), hosted as
// the shell's "Topics" module panel (§5.2 — panels persist, the shell swaps).
// Look at: the layout — an explaining caption over the toolbar ("Topics
//          group your quizzes; every quiz belongs to one." — the same
//          caption-over-list anatomy the Review module uses), then the
//          toolbar row (8px gaps): the search box ("Search topics…", 240px)
//          and the trailing "New topic" primary button (with its "+" mark —
//          the PLAY-pill glyph language of the quiz card); 16px; one
//          full-height grid card (recipe 4.6, accent column headers) with
//          the columns Name · Category · Quizzes · Created and the trailing
//          Edit/Delete text actions. The search filters live while typing
//          (no submit button).
// WP4 — the New-topic dialog is the §5.3a AI dialog: the user DESCRIBES the
//          topic, "Generate with AI" walks the same provider chain as the
//          quiz generation (off the UI thread, busy state, typed failure
//          banner), and the result lands as an EDITABLE preview. Manual-first
//          by construction: every field starts empty and enabled, and Save
//          works without ever generating. The description the dialog carries
//          now reaches the repository (the pre-WP4 form saved string.Empty).
//          Two failure classes, two placements, never mixed (§5.3a): the
//          describe range violation is the §4.4 inline DangerText line; the
//          generation unavailability is the compact WarningTint banner. The
//          busy state reuses the app's one §4.13 pattern verbatim (the call
//          runs OFF the UI thread — R-5 #3; a frozen dialog is a review
//          defect) and its Cancel discards the late result (§5.11 semantics).
//          The Edit-topic flow is UNCHANGED and stays manual (Name + Category
//          only, description preserved) — it uses EditTopicDialog below,
//          which carries the pre-WP4 manual card verbatim.
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
using CodeQuest.Services;
using System.Drawing;
using System.Drawing.Drawing2D;
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

    /// <summary>Creates the screen for the topic and quiz repositories and the AI topic chain.</summary>
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

        _search = new InputField(accessibleName: "Search topics…") // §4.4: name from the visible placeholder
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
            AccessibleName = "New topic", // the "+" is the visual mark, not the name
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
                form._search.ClearText(); // "Clear search" resets the live filter
            }
        };

        Controls.Add(_search);
        Controls.Add(_newTopic);
        Controls.Add(_list);
        Controls.Add(_listCaption);
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
        _listCaption.Width = Math.Max(Width, 1); // the caption was built at the pre-layout width (M6)
        ApplyFilter();
    }

    /// <summary>Keeps the list caption full-width as the module resizes (M6).</summary>
    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        _listCaption.Width = Math.Max(Width, 1);
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

    /// <summary>
    /// Opens the §5.3a "New topic" AI dialog and stores the topic on Save.
    /// The dialog is manual-first: it can save without ever generating.
    /// </summary>
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

    /// <summary>
    /// The persisting phase of the New-topic flow (the seam the
    /// TopicGenerationTests drive to verify the save path without the modal
    /// loop): the dialog's name, category AND description reach the
    /// repository — the pre-WP4 form saved an empty description. Returns
    /// false when the save lost the duplicate race (R-2 #5): the strip
    /// carries the friendly message instead of a raw SQLite error.
    /// </summary>
    internal bool PersistNewTopic(TopicFormDialog dialog)
    {
        var topic = new Topic
        {
            Name = dialog.TopicName,
            Category = dialog.TopicCategory,
            Description = dialog.TopicDescription, // the §5.3a preview's description is the stored one (WP4)
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
            return false;
        }

        RefreshData();
        ReportStatus(SavedMessage, true);
        return true;
    }

    /// <summary>Opens the "Edit topic" modal card and saves the changes on Save.</summary>
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

        // The spec's form edits Name and Category only — the description and
        // creation timestamp are preserved (the Edit-topic flow is unchanged
        // by WP4: it stays manual and carries no AI affordances).
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
/// The D4 dialog-card shell both topic dialogs share (§4.11 as amended by the
/// D4 dark-theme re-issue): the one floating elevation level — SurfaceRaised
/// fill, 12px radius, 1px BorderHairline edge, and the native CS_DROPSHADOW
/// (the only shadow in the app, owner-mandated, platform-drawn). No dimmed
/// backdrop. Subclasses build their content; the shell owns the card shape.
/// </summary>
public abstract class TopicDialogShell : Form
{
    /// <summary>Creates the borderless dialog card. <paramref name="title"/> is also the window caption (UIA reads it).</summary>
    protected TopicDialogShell(string title)
    {
        FormBorderStyle = FormBorderStyle.None;
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        BackColor = UITheme.SurfaceRaised; // the floating elevation level (D4 §2)
        KeyPreview = true;

        // The D4 hairline card border (the card treatment): BorderHairline
        // keeps a crisp edge over the app window behind it.
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

    /// <summary>The native drop shadow (D4 §2): CS_DROPSHADOW class style, platform-drawn — the one shadow in the app.</summary>
    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.ClassStyle |= 0x20000; // CS_DROPSHADOW
            return parameters;
        }
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
}

/// <summary>
/// The §5.3a AI "New topic" dialog (WP4): the user describes the topic,
/// "Generate with AI" walks the topic provider chain off the UI thread, and
/// the result lands as an editable preview (Name / Category / Description).
/// Manual-first: every field starts empty and enabled and Save works without
/// ever generating. All colors, fonts and spacing come from
/// <see cref="UITheme"/> only.
/// </summary>
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

    // The typed-reason map of §5.11, same wording verbatim — a generation
    // failure is never a raw reason string, never a provider detail that
    // could leak configuration. The generic fallback is topic-worded.
    private const string NoProviderMessage = "No AI provider is configured.";
    private const string AuthMessage = "The AI provider rejected the request. Check the provider configuration.";
    private const string QuotaMessage = "The AI provider reached its request limit. Try again later.";
    private const string NetworkMessage = "Couldn't reach the AI provider. Check the internet connection.";
    private const string HttpMessage = "The AI provider returned an error. Try again later.";
    private const string BadOutputMessage = "The AI provider returned an unusable response. Try again.";
    private const string GenericFailureMessage = "Topic generation failed. Try again.";

    private const int ContentInset = UITheme.Spacing24;
    private const int MinDialogWidth = 480;
    private const int DescribeAreaHeight = 90; // the §4.4 multi-line minimum (64px) + the 8px gap + the error-line slot
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

    private bool _busy; // a generation is in flight (also the re-entrancy guard)
    private int _generationId; // increments per start — a late or cancelled result is discarded (§5.11)
    private DateTimeOffset _startedAt;

    /// <summary>Creates the AI New-topic dialog over the configured topic chain.</summary>
    public TopicFormDialog(ITopicGenerationService generation)
        : base(DialogTitle)
    {
        ArgumentNullException.ThrowIfNull(generation);
        _generation = generation;

        // --- The describe field (§5.3a) ------------------------------------
        _titleLabel = new Label
        {
            Text = DialogTitle,
            Name = "TopicDialogTitle",
            Font = UITheme.BodySemibold, // §5.3a pins the dialog title style
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.SurfaceRaised, // must equal the dialog fill (D4 audit #9)
            AutoSize = false,
        };

        _describeLabel = MakeFieldLabel(DescribeLabelText, "TopicDescribeLabel");
        _describe = new InputArea(accessibleName: DescribeLabelText)
        {
            Name = "TopicDescribeField",
            PlaceholderText = DescribePlaceholder,
        };
        _describe.LabeledBy = _describeLabel; // §4.4: the visible label is the accessible name's source (F2)
        _describe.TextChanged += static (sender, e) =>
        {
            if (sender is InputArea area)
            {
                area.ClearError(); // the error line persists until the value validates (§4.4)
            }
        };

        // --- The generate row (§5.3a) --------------------------------------
        _generate = new FlatButton
        {
            Name = "TopicGenerateButton",
            Text = GenerateButtonLabel,
            Variant = UITheme.FlatButtonVariant.Primary,
        };
        // No AccessibleName here on purpose: §5.3a announces the busy state
        // through the button's accessible name becoming "Generating…" — with
        // no override the name follows the label.
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
            BackColor = UITheme.SurfaceRaised, // must equal the dialog fill (D4 audit #9)
            AutoSize = false,
        };

        // The busy block (§4.13, the app's one busy pattern, reused verbatim):
        // the marquee track with the "Cancel" secondary beside it, and the
        // status line with the live elapsed counter beneath.
        _busyBlock = new Panel
        {
            Name = "TopicBusyBlock",
            BackColor = UITheme.SurfaceRaised, // must equal the dialog fill (D4 audit #9)
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
            BackColor = UITheme.SurfaceRaised, // must equal the dialog fill (D4 audit #9)
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

        // --- The typed-failure banner (§5.3a) -------------------------------
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
            BackColor = UITheme.WarningTint, // every label matches its wash (D4 audit #9)
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

        // --- The preview group (§5.3a) — manual-first -----------------------
        _previewHeader = new Label
        {
            Name = "TopicPreviewHeader",
            Text = PreviewHeaderText,
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.SurfaceRaised, // must equal the dialog fill (D4 audit #9)
            AutoSize = false,
            Visible = false, // appears with the first successful generate
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
                field.ClearError(); // the error line persists until the value validates (§4.4)
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
        _category.SelectedIndex = 0; // a fresh topic defaults to the first category

        _descriptionLabel = MakeFieldLabel("Description", "TopicDescriptionLabel");
        _description = new InputArea(accessibleName: "Topic description")
        {
            Name = "TopicDescriptionField",
            PlaceholderText = "Topic description",
        };
        _description.LabeledBy = _descriptionLabel;

        // --- The commit row (§4.11) -----------------------------------------
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

        // The elapsed-seconds counter ticks on the UI timer (§4.13) while the
        // chain call runs off the UI thread.
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

    /// <summary>Stops the elapsed timer when the dialog is disposed (L1).</summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _elapsedTimer.Stop();
            _elapsedTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>Runs before the dialog closes; returns the inline error message or null.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<string?, string?>? ValidateInput { get; init; }

    /// <summary>The entered (trimmed) topic name — the Save target, prefilled from the AI draft.</summary>
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

    /// <summary>
    /// The (trimmed) topic description of the preview group — the value the
    /// New-topic flow persists (the pre-WP4 form saved string.Empty).
    /// </summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string TopicDescription
    {
        get => _description.Text.Trim();
        set => _description.Text = value;
    }

    /// <summary>True while the AI chain call is in flight (the §5.3a busy state).</summary>
    public bool IsBusy => _busy;

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

    /// <summary>
    /// Starts one generation: validates the describe range FIRST (§5.3a — the
    /// violation is the §4.4 inline line, never the banner), disables the
    /// busy-state controls, and runs the blocking chain call off the UI
    /// thread. The preview fields stay enabled (manual editing is always
    /// possible, FR1).
    /// </summary>
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

        // The request's own guard agrees with the range above, so building it
        // here cannot throw.
        var request = new TopicGenerationRequest(describeText);

        _busy = true;
        _generationId++;
        int generationId = _generationId; // this request's token (the §5.11 cancellation rule)
        _startedAt = DateTimeOffset.UtcNow;

        SetBusyState(busy: true);
        _busyTrack.Marquee = true; // armed per run — never left over (M1)
        _busyBlock.BringToFront();
        _elapsedTimer.Start();
        Refresh(); // paint the busy state before the (off-thread) call starts

        // The chain call is synchronous and can block for a minute — it runs
        // off the UI thread so the marquee animates (the binding R-5 #3 rule).
        Task.Run(() => _generation.GenerateTopic(request))
            .ContinueWith(
                task => HandleOutcome(task.Result, generationId),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>
    /// The UI-thread continuation: prefill the preview, or show the typed
    /// failure banner. A result whose token no longer matches — cancelled, or
    /// the dialog already closed — is discarded; nothing is ever half-saved.
    /// </summary>
    private void HandleOutcome(TopicGenerationOutcome outcome, int generationId)
    {
        if (IsDisposed || generationId != _generationId)
        {
            return; // the §5.11 cancellation rule: the late result is discarded
        }

        _elapsedTimer.Stop();
        _busyTrack.Marquee = false;
        _busy = false;
        SetBusyState(busy: false);

        if (outcome.IsSuccess && outcome.Draft is not null)
        {
            // The banner persists until a SUCCESSFUL generate (§5.3a).
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
        _banner.AccessibleName = $"{BannerTitleText} {_bannerReason.Text} {BannerManualLine}"; // StaticText semantics
        _banner.Visible = true;
        LayoutDialog();
    }

    /// <summary>
    /// The §5.11 Cancel: the dialog returns to its ready state immediately;
    /// the in-flight call cannot be aborted through the client, so its token
    /// is invalidated — the late result is discarded (never half-saved).
    /// </summary>
    private void CancelGeneration()
    {
        if (!_busy)
        {
            return;
        }

        _generationId++; // invalidate the in-flight request's completion (§5.11: the late result is discarded)
        _elapsedTimer.Stop();
        _busyTrack.Marquee = false;
        _busy = false;
        SetBusyState(busy: false);
    }

    /// <summary>
    /// Enters or leaves the §5.3a busy state: the generate button disabled
    /// with the "Generating…" label, the describe field disabled (§4.4
    /// disabled recipe), Save disabled, and the note replaced by the §4.13
    /// busy block. The preview fields stay enabled throughout (FR1).
    /// </summary>
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

    /// <summary>
    /// Maps the typed reason strings to the §5.11 plain-language lines — never
    /// a raw reason, never a provider detail that could leak configuration.
    /// The chain-level class (contract 7.6 — a fresh clone has no config file)
    /// maps to the no-provider message. Same classification and wording as the
    /// generation screen's map; the third banner line is this dialog's own
    /// manual-fallback pointer (not the take-a-quiz offline pointer).
    /// </summary>
    private static string DescribeFailure(IReadOnlyList<string> reasons)
    {
        string first = reasons.Count > 0 ? reasons[0] : string.Empty;
        return first.Contains("empty-provider-list", StringComparison.Ordinal) ? NoProviderMessage
            : first.Contains(":not-configured", StringComparison.Ordinal) ? NoProviderMessage
            : first.Contains(":auth-failed", StringComparison.Ordinal) ? AuthMessage
            : first.Contains(":quota", StringComparison.Ordinal) ? QuotaMessage
            : first.Contains(":timeout", StringComparison.Ordinal) ? NetworkMessage
            : first.Contains(":http-error", StringComparison.Ordinal) ? HttpMessage
            : first.Contains(":bad-output", StringComparison.Ordinal) ? BadOutputMessage
            : GenericFailureMessage;
    }

    /// <summary>Creates one §4.4 field label (BodySemibold on the dialog fill).</summary>
    private Label MakeFieldLabel(string text, string name)
    {
        return new Label
        {
            Text = text,
            Name = name,
            Font = UITheme.BodySemibold,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.SurfaceRaised, // must equal the dialog fill (D4 audit #9)
            AutoSize = false,
        };
    }

    /// <summary>
    /// Sizes the card and places its content (§5.3a anatomy). Called again
    /// whenever the banner or the preview header changes visibility — the
    /// dialog grows downward, never overlaying content.
    /// </summary>
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
            + UITheme.Scaled(this, UITheme.Spacing16) * 2; // sized for the longer busy label
        int busyCancelWidth = TextRenderer.MeasureText(CancelLabel, UITheme.Body).Width
            + UITheme.Scaled(this, UITheme.Spacing16) * 2;
        int saveWidth = TextRenderer.MeasureText(_save.Text, UITheme.Body).Width
            + UITheme.Scaled(this, UITheme.Spacing16) * 2;
        int cancelWidth = TextRenderer.MeasureText(_cancel.Text, UITheme.Body).Width
            + UITheme.Scaled(this, UITheme.Spacing16) * 2;

        int x = inset;
        int y = inset;

        // Title (§5.3a: BodySemibold), 16px above the describe label.
        _titleLabel.SetBounds(x, y, contentWidth, titleHeight);
        y += titleHeight + gap16;

        // The describe field: label, 4px, the multi-line area (min 64px).
        _describeLabel.SetBounds(x, y, contentWidth, labelHeight);
        y += labelHeight + UITheme.Scaled(this, UITheme.Spacing4);
        _describe.SetBounds(x, y, contentWidth, DescribeAreaHeight);
        y += DescribeAreaHeight + gap12;

        // The generate row: the primary button leading, the note 8px below —
        // replaced in place by the §4.13 busy block while a run is in flight.
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

        // The banner sits between the generate row and the preview group with
        // 8px gaps (§5.3a); without it the section gap is 24.
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

        // The preview group: quiet header, then Name / Category / Description
        // (16px field↔field, §5.3a).
        if (_previewHeader.Visible)
        {
            _previewHeader.SetBounds(x, y, contentWidth, captionHeight);
            y += captionHeight + gap8;
        }

        _nameLabel.SetBounds(x, y, contentWidth, labelHeight);
        y += labelHeight + UITheme.Scaled(this, UITheme.Spacing4);
        _name.SetBounds(x, y, contentWidth, UITheme.Scaled(this, 56));
        y += _name.Height + gap16; // the field settles at its own reserved height (32px field + error slot)

        _categoryLabel.SetBounds(x, y, contentWidth, labelHeight);
        y += labelHeight + UITheme.Scaled(this, UITheme.Spacing4);
        _category.SetBounds(x, y, contentWidth, comboSlot); // the combo's height follows its font (native flat chrome)
        y += comboSlot + gap16;

        _descriptionLabel.SetBounds(x, y, contentWidth, labelHeight);
        y += labelHeight + UITheme.Scaled(this, UITheme.Spacing4);
        _description.SetBounds(x, y, contentWidth, PreviewAreaHeight);
        y += PreviewAreaHeight + gap24;

        // The commit row: Save primary trailing (the default), Cancel
        // secondary 8px to its left (§4.11 / §5.3a).
        _save.SetBounds(width - inset - saveWidth, y, saveWidth, buttonHeight);
        _cancel.SetBounds(width - inset - saveWidth - gap8 - cancelWidth, y, cancelWidth, buttonHeight);
        y += buttonHeight;

        ClientSize = new Size(width, y + inset);
    }

    /// <summary>
    /// Lays out the three banner lines inside the wash (12px padding; the
    /// reason line is measured, since the mapped sentences wrap at this
    /// width) and returns the banner's total height.
    /// </summary>
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

/// <summary>
/// The manual topic modal card of spec 5.3 — the Edit-topic form (carried
/// verbatim from the pre-WP4 dialog): Name input + the two-value category
/// dropdown (R-2 #7), Save primary / Cancel secondary. All validation runs
/// through <see cref="ValidateInput"/> before the dialog closes, so the
/// inline error line of recipe 4.4 carries the failure. The Edit-topic flow
/// is unchanged by WP4 (§5.3a reworks only the New-topic form); the
/// description is not part of this form and is preserved by the caller.
/// </summary>
public sealed class EditTopicDialog : TopicDialogShell
{
    private const int ContentInset = UITheme.Spacing24;
    private const int FieldGap = UITheme.Spacing12;
    private const int ButtonGap = UITheme.Spacing8;

    private readonly InputField _name;
    private readonly ThemedComboBox _category;
    private readonly FlatButton _save;
    private readonly FlatButton _cancel;

    /// <summary>Creates the dialog. <paramref name="title"/> is "Edit topic".</summary>
    public EditTopicDialog(string title)
        : base(title)
    {
        var titleLabel = new Label
        {
            Text = title,
            Font = UITheme.Title,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.SurfaceRaised, // must equal the dialog fill (D4 audit #9)
            AutoSize = false,
            Name = "TopicDialogTitle",
        };

        var nameLabel = new Label
        {
            Text = "Name",
            Font = UITheme.BodySemibold,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.SurfaceRaised, // must equal the dialog fill (D4 audit #9)
            AutoSize = false,
            Name = "TopicNameLabel",
        };

        _name = new InputField(accessibleName: "Topic name")
        {
            Name = "TopicNameField",
            PlaceholderText = "Topic name",
        };
        _name.LabeledBy = nameLabel; // §4.4: the visible label is the name's source (F2)
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
            BackColor = UITheme.SurfaceRaised, // must equal the dialog fill (D4 audit #9)
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

    /// <summary>Sizes the card and places its content (§4.11 dialog anatomy).</summary>
    private void LayoutDialog(Control titleLabel, Control nameLabel, Control categoryLabel)
    {
        int titleHeight = UITheme.Scaled(this, 30); // the Title-font header line
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
