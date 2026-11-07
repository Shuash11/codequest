// =============================================================================
// CodeQuest — Phase P8: Topic & Quiz Management
// Recipe 4.6 of the design of record: the DataGridView as an iOS list.
// Look at: <see cref="IosListGrid"/> — the recipe's grid: invisible row
//          headers; quiet column headers (32px, Background fill, Caption-size
//          TextSecondary labels, EnableHeadersVisualStyles off, no 3D chrome,
//          a single 1px SeparatorHairline under the header row); 36px rows
//          with Body TextPrimary text and no alternating colors; row
//          separators as 1px SeparatorHairline lines painted per row
//          (RowPostPaint), left-inset 16px to align with the row text (§2.5);
//          FullRowSelect with SelectionBackColor AccentTint — never the
//          default blue selection chrome; empty grid renders no message of
//          its own. Action columns paint as quiet text buttons (recipe 4.1's
//          row-level text actions: Accent label, destructive text actions in
//          DangerText, disabled in TextTertiary) with the Fill hover of the
//          secondary variant; badge columns draw the §4.10 chip.
//          Keyboard-operability of row actions (F1, §4.6): the focused cell
//          moves with the arrows and Tab cell by cell through the whole grid
//          INCLUDING the action column; Enter/Space activates a focused
//          action cell (the default move-down Enter is suppressed only while
//          an action cell has focus; disabled action cells no-op); the
//          focused action cell paints §4.1's 1px Accent ring inset 1px. A
//          focused row's accessible description lists its actions in column
//          order and each action cell's accessible Name is its visible text
//          (§4.4); badge cells expose §4.10's StaticText role/name.
//          <see cref="ListCardGrid"/> is the container the recipe requires —
//          the grid sits inside a §4.2 card — plus the screen's empty-state
//          panel (§5): the grid never renders its own message.
// Users:   the topic grid and quiz grid (spec 5.3, 5.4) and the question
//          list of the question editor (spec 5.5).
// =============================================================================

using System.Drawing;
using System.Windows.Forms;
using CodeQuest.Models;

namespace CodeQuest.UI;

/// <summary>The visual kind of an action column's text (recipe 4.1 text actions).</summary>
public enum GridActionKind
{
    /// <summary>Quiet secondary text action (Accent label, Fill hover).</summary>
    Secondary,

    /// <summary>Destructive text action (DangerText label on transparent).</summary>
    Destructive,

    /// <summary>A Segoe MDL2 glyph action (the reorder chevrons of spec 5.5).</summary>
    Glyph,
}

/// <summary>The row action the user clicked.</summary>
public sealed class GridActionEventArgs : EventArgs
{
    /// <summary>Creates the event data.</summary>
    public GridActionEventArgs(int rowIndex, string actionName)
    {
        RowIndex = rowIndex;
        ActionName = actionName;
    }

    /// <summary>The row the action belongs to.</summary>
    public int RowIndex { get; }

    /// <summary>The name of the action column.</summary>
    public string ActionName { get; }
}

/// <summary>
/// The row-actions discoverability copy of recipe 4.6 (F1): the pure wording
/// a focused row's accessible description uses, and the on-screen hint line
/// the grid card shows beneath itself.
/// </summary>
public static class GridRowActions
{
    /// <summary>
    /// The hint line of recipe 4.6 (F1): shown directly beneath every grid
    /// card that carries an action column, so keyboard activation is
    /// documented on-screen, not discovered by accident.
    /// </summary>
    public const string HintText = "Enter activates a focused row action.";

    /// <summary>
    /// The accessible description of a row's actions in column order
    /// (recipe 4.6: "a screen-reader user hears what the row offers when the
    /// row takes focus").
    /// </summary>
    public static string Describe(IEnumerable<string> actionNames)
        => "Actions: " + string.Join(", ", actionNames) + ".";
}

/// <summary>
/// The restyled grid of recipe 4.6. The forms fill it with rows; this class
/// owns everything visual about the list.
/// </summary>
public sealed class IosListGrid : DataGridView
{
    private readonly Dictionary<string, (GridActionKind Kind, string Text)> _actionColumns = new();
    private readonly HashSet<string> _badgeColumns = new();
    private int _hoveredActionRow = -1;
    private string? _hoveredActionColumn;
    private Point _focusedActionCell = new(-1, -1); // the last current cell, for the §4.1 focus ring repaint

    /// <summary>Creates the grid with the recipe's full styling.</summary>
    public IosListGrid()
    {
        DoubleBuffered = true;
        BorderStyle = BorderStyle.None;
        BackgroundColor = UITheme.Surface;
        GridColor = UITheme.SeparatorHairline;

        // Recipe 4.6: no row headers, no cell borders (rows paint their own
        // hairline), no user resizing, no editing — the grid is a list. The
        // row template carries the accessible row object whose description
        // lists the row's actions (F1) — every row created from it inherits it.
        RowHeadersVisible = false;
        RowTemplate = new ActionRowTemplate();
        CellBorderStyle = DataGridViewCellBorderStyle.None;
        AllowUserToAddRows = false;
        AllowUserToDeleteRows = false;
        AllowUserToResizeRows = false;
        ReadOnly = true;
        MultiSelect = false;
        SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        EditMode = DataGridViewEditMode.EditProgrammatically;
        AutoGenerateColumns = false;
        RowTemplate.Height = UITheme.Scaled(this, 36);

        // Recipe 4.6 quiet column headers.
        EnableHeadersVisualStyles = false;
        ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        ColumnHeadersHeight = UITheme.Scaled(this, 32);
        ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = UITheme.Background,
            ForeColor = UITheme.TextSecondary,
            Font = UITheme.Caption,
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            WrapMode = DataGridViewTriState.False,
            SelectionBackColor = UITheme.Background,
            SelectionForeColor = UITheme.TextSecondary,
        };

        // Recipe 4.6 rows: Body text in TextPrimary, AccentTint selection —
        // never the default blue selection chrome (§6, sign 11).
        DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = UITheme.Surface,
            ForeColor = UITheme.TextPrimary,
            Font = UITheme.Body,
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            WrapMode = DataGridViewTriState.False,
            SelectionBackColor = UITheme.AccentTint,
            SelectionForeColor = UITheme.TextPrimary,
            Padding = new Padding(UITheme.Scaled(this, 8), 0, UITheme.Scaled(this, 8), 0),
        };
    }

    /// <summary>
    /// Which rows have which actions enabled (the reorder buttons of the
    /// question list disable at the ends of the list, spec 5.5). Gets the row
    /// index and the action column's name; null means every action is enabled.
    /// </summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<int, string, bool>? IsActionEnabled { get; set; }

    /// <summary>Raised when the user clicks an enabled action cell.</summary>
    public event EventHandler<GridActionEventArgs>? ActionClicked;

    /// <summary>
    /// Raised when an action column is registered — the card container uses
    /// it to show recipe 4.6's hint line beneath the grid card (F1).
    /// </summary>
    internal event EventHandler? ActionColumnsChanged;

    /// <summary>
    /// Adds one text column that shares the row's remaining width. A minimum
    /// width keeps short factual columns (dates, counts, the difficulty) from
    /// being squeezed below their readable width when the window is small —
    /// the leading title column absorbs the difference instead.
    /// </summary>
    /// <param name="name">The column name (tests address it).</param>
    /// <param name="headerText">The quiet header label.</param>
    /// <param name="fillWeight">The share of the remaining width.</param>
    /// <param name="extraLeftInset">Extra left padding for the leading column (§2.5 inset).</param>
    /// <param name="minimumWidth">Readable minimum width in standard-DPI pixels (0 = no minimum).</param>
    public void AddTextColumn(string name, string headerText, float fillWeight, int extraLeftInset = 0, int minimumWidth = 0)
    {
        var column = new DataGridViewTextBoxColumn
        {
            Name = name,
            HeaderText = headerText,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = fillWeight,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            Resizable = DataGridViewTriState.False,
        };

        if (minimumWidth > 0)
        {
            column.MinimumWidth = UITheme.Scaled(this, minimumWidth);
        }

        column.DefaultCellStyle.Padding = new Padding(
            UITheme.Scaled(this, UITheme.Spacing8) + extraLeftInset,
            0,
            UITheme.Scaled(this, UITheme.Spacing8),
            0);
        column.HeaderCell.Style.Padding = column.DefaultCellStyle.Padding;

        Columns.Add(column);
    }

    /// <summary>Adds one trailing action column (Edit, Delete, reorder chevrons).</summary>
    /// <param name="name">The action name the click event reports.</param>
    /// <param name="text">The action label (a glyph string for the chevrons).</param>
    /// <param name="kind">The recipe-4.1 text-action kind.</param>
    public void AddActionColumn(string name, string text, GridActionKind kind)
    {
        var column = new DataGridViewTextBoxColumn
        {
            Name = name,
            HeaderText = string.Empty,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            Resizable = DataGridViewTriState.False,
            ReadOnly = true,
            CellTemplate = new GridActionCell(), // the accessible Name = the visible text (§4.4/§4.6)
        };
        column.Width = kind == GridActionKind.Glyph
            ? UITheme.Scaled(this, 40)
            : TextRenderer.MeasureText(text, UITheme.Body).Width + UITheme.Scaled(this, UITheme.Spacing16);
        column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        column.HeaderCell.Style.Padding = new Padding(0);

        _actionColumns[name] = (kind, text);
        Columns.Add(column);
        ActionColumnsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Adds one status-badge column (recipe 4.10 chips drawn in the cell).</summary>
    public void AddBadgeColumn(string name, string headerText, int width)
    {
        var column = new DataGridViewTextBoxColumn
        {
            Name = name,
            HeaderText = headerText,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            Width = UITheme.Scaled(this, width),
            SortMode = DataGridViewColumnSortMode.NotSortable,
            Resizable = DataGridViewTriState.False,
            CellTemplate = new GridBadgeCell(), // role StaticText, name = the badge text (§4.10)
        };
        column.HeaderCell.Style.Padding = new Padding(
            UITheme.Scaled(this, UITheme.Spacing8), 0, UITheme.Scaled(this, UITheme.Spacing8), 0);

        _badgeColumns.Add(name);
        Columns.Add(column);
    }

    // Grid-level accessibility decision (P8.1 probe evidence): the grid keeps
    // the base DataGridView.DataGridViewAccessibleObject — it resolves its row
    // children through the rows' own DataGridViewRow.AccessibilityObject
    // (which the ActionRowTemplate subclasses so each row describes its
    // actions in column order, recipe 4.6/F1) and walks cleanly under UIA.
    // A custom grid-level accessible object that re-routed GetChild to the row
    // objects was probed to hang the UIA tree walk, so the grid-level tree
    // stays with the framework.

    /// <summary>Tracks the hovered action cell for the secondary hover paint.</summary>
    protected override void OnCellMouseEnter(DataGridViewCellEventArgs e)
    {
        base.OnCellMouseEnter(e);

        if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && IsActionColumn(Columns[e.ColumnIndex].Name))
        {
            InvalidateHoveredAction();
            _hoveredActionRow = e.RowIndex;
            _hoveredActionColumn = Columns[e.ColumnIndex].Name;
            InvalidateCell(e.ColumnIndex, e.RowIndex);
        }
    }

    /// <summary>Clears the hover state when the pointer leaves an action cell.</summary>
    protected override void OnCellMouseLeave(DataGridViewCellEventArgs e)
    {
        base.OnCellMouseLeave(e);

        if (_hoveredActionRow == e.RowIndex && _hoveredActionColumn == Columns[e.ColumnIndex].Name)
        {
            InvalidateCell(e.ColumnIndex, e.RowIndex);
            _hoveredActionRow = -1;
            _hoveredActionColumn = null;
        }
    }

    /// <summary>Raises the action event for an enabled action cell.</summary>
    protected override void OnCellClick(DataGridViewCellEventArgs e)
    {
        base.OnCellClick(e);

        if (e.RowIndex < 0 || e.ColumnIndex < 0)
        {
            return;
        }

        string columnName = Columns[e.ColumnIndex].Name;
        if (!IsActionColumn(columnName) || !IsActionEnabledAt(e.RowIndex, columnName))
        {
            return;
        }

        ActionClicked?.Invoke(this, new GridActionEventArgs(e.RowIndex, columnName));
    }

    /// <summary>Paints the quiet headers, the action cells and the badge cells.</summary>
    protected override void OnCellPainting(DataGridViewCellPaintingEventArgs e)
    {
        base.OnCellPainting(e);

        if (e.ColumnIndex < 0 && e.RowIndex < 0)
        {
            return; // the corner cell of the (invisible) row headers
        }

        if (e.RowIndex < 0)
        {
            PaintQuietHeader(e);
            e.Handled = true;
            return;
        }

        string columnName = Columns[e.ColumnIndex].Name;

        if (_badgeColumns.Contains(columnName))
        {
            PaintBadgeCell(e);
            e.Handled = true;
            return;
        }

        if (IsActionColumn(columnName))
        {
            PaintActionCell(e, columnName);
            e.Handled = true;
        }
    }

    /// <summary>The recipe's 1px row separators, left-inset 16px (RowPostPaint).</summary>
    protected override void OnRowPostPaint(DataGridViewRowPostPaintEventArgs e)
    {
        base.OnRowPostPaint(e);

        if (e.RowIndex >= Rows.Count - 1)
        {
            return; // the card edge closes the last row — no separator under it
        }

        Rectangle rowBounds = e.RowBounds;
        int inset = UITheme.Scaled(this, UITheme.Spacing16);
        int y = rowBounds.Bottom - 1;

        using var pen = new Pen(UITheme.SeparatorHairline);
        e.Graphics.DrawLine(pen, inset, y, rowBounds.Right, y);
    }

    /// <summary>Clears stale hover state when the rows change.</summary>
    protected override void OnRowsRemoved(DataGridViewRowsRemovedEventArgs e)
    {
        base.OnRowsRemoved(e);
        _hoveredActionRow = -1;
        _hoveredActionColumn = null;
    }

    /// <summary>Whether the column is one of the registered action columns.</summary>
    private bool IsActionColumn(string columnName) => _actionColumns.ContainsKey(columnName);

    /// <summary>Whether the row's action is enabled (the list ends disable the chevrons).</summary>
    private bool IsActionEnabledAt(int rowIndex, string columnName)
        => IsActionEnabled?.Invoke(rowIndex, columnName) ?? true;

    /// <summary>The row's actions in column order (recipe 4.6's accessible description).</summary>
    private string RowActionsDescription()
    {
        IReadOnlyList<string> names = _actionColumns.Keys
            .Select(name => (name, column: Columns[name]))
            .Where(pair => pair.column is not null)
            .OrderBy(pair => pair.column!.Index)
            .Select(pair => pair.name)
            .ToList();

        return GridRowActions.Describe(names);
    }

    /// <summary>
    /// Recipe 4.6's activation keys (F1): Enter or Space on a focused action
    /// cell invokes that row's action — Space the button idiom, Enter the
    /// list idiom, both work so no key memory is needed. Disabled action
    /// cells (§5.5's up on the first row, down on the last) make activation a
    /// no-op but still consume the key, so nothing else moves.
    /// </summary>
    private bool TryActivateFocusedAction(Keys key)
    {
        if (key is not (Keys.Enter or Keys.Space) || CurrentCellAddress.X < 0 || CurrentCellAddress.Y < 0)
        {
            return false;
        }

        string columnName = Columns[CurrentCellAddress.X].Name;
        if (!IsActionColumn(columnName))
        {
            return false;
        }

        if (IsActionEnabledAt(CurrentCellAddress.Y, columnName))
        {
            ActionClicked?.Invoke(this, new GridActionEventArgs(CurrentCellAddress.Y, columnName));
        }

        return true; // consumed whether activated or no-op — the cell keeps focus
    }

    /// <summary>
    /// Enter reaches the grid as a dialog key; while an action cell holds
    /// focus it activates instead of the grid's default move-down (recipe
    /// 4.6: the suppression applies ONLY while an action cell is focused).
    /// </summary>
    protected override bool ProcessDialogKey(Keys keyData)
    {
        if (TryActivateFocusedAction(keyData & Keys.KeyCode))
        {
            return true;
        }

        return base.ProcessDialogKey(keyData);
    }

    /// <summary>Space reaches OnKeyDown; on a focused action cell it activates (recipe 4.6).</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (TryActivateFocusedAction(e.KeyCode))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        base.OnKeyDown(e);
    }

    /// <summary>Repaints the focus ring when the focused cell moves (recipe 4.6's focus indicator).</summary>
    protected override void OnCurrentCellChanged(EventArgs e)
    {
        base.OnCurrentCellChanged(e);
        InvalidateActionCell(_focusedActionCell);
        _focusedActionCell = CurrentCellAddress;
        InvalidateActionCell(_focusedActionCell);
    }

    /// <summary>The ring follows the grid's keyboard focus state, so repaint on focus changes.</summary>
    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateActionCell(_focusedActionCell);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateActionCell(_focusedActionCell);
    }

    /// <summary>Repaints one cell when it is (or was) a focused action cell.</summary>
    private void InvalidateActionCell(Point address)
    {
        if (address.X < 0 || address.Y < 0 || address.X >= Columns.Count || address.Y >= Rows.Count)
        {
            return;
        }

        if (IsActionColumn(Columns[address.X].Name))
        {
            InvalidateCell(address.X, address.Y);
        }
    }

    /// <summary>Quiet header of recipe 4.6: Background fill, Caption TextSecondary, hairline below.</summary>
    private void PaintQuietHeader(DataGridViewCellPaintingEventArgs e)
    {
        if (e.Graphics is not { } graphics)
        {
            return;
        }

        using (var backgroundBrush = new SolidBrush(UITheme.Background))
        {
            graphics.FillRectangle(backgroundBrush, e.CellBounds);
        }

        using (var separatorPen = new Pen(UITheme.SeparatorHairline))
        {
            graphics.DrawLine(
                separatorPen,
                e.CellBounds.Left,
                e.CellBounds.Bottom - 1,
                e.CellBounds.Right,
                e.CellBounds.Bottom - 1);
        }

        string headerText = Columns[e.ColumnIndex].HeaderText;
        if (headerText.Length > 0)
        {
            TextRenderer.DrawText(
                e.Graphics,
                headerText,
                UITheme.Caption,
                e.CellBounds,
                UITheme.TextSecondary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>Action cell of recipe 4.1's row-level text actions.</summary>
    private void PaintActionCell(DataGridViewCellPaintingEventArgs e, string columnName)
    {
        if (e.Graphics is not { } graphics)
        {
            return;
        }

        using (var backgroundBrush = new SolidBrush(SelectedBackground(e.RowIndex)))
        {
            graphics.FillRectangle(backgroundBrush, e.CellBounds);
        }

        (GridActionKind kind, string text) = _actionColumns[columnName];
        bool enabled = IsActionEnabledAt(e.RowIndex, columnName);
        bool hovered = e.RowIndex == _hoveredActionRow && columnName == _hoveredActionColumn;

        if (hovered && enabled)
        {
            using var hoverBrush = new SolidBrush(UITheme.Fill);
            graphics.FillRectangle(hoverBrush, e.CellBounds);
        }

        Color label = !enabled ? UITheme.TextTertiary
            : kind == GridActionKind.Destructive ? UITheme.DangerText
            : UITheme.Accent;

        Font font = kind == GridActionKind.Glyph ? UITheme.NavGlyph : UITheme.Body;

        TextRenderer.DrawText(
            graphics,
            text,
            font,
            e.CellBounds,
            label,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

        // Recipe 4.6's focus indicator (F1): the focused action cell paints
        // §4.1's ring — a 1px Accent outline inset 1px — over its normal
        // paint, while its row keeps the AccentTint selection fill. No glow,
        // no halo — the same ring vocabulary as buttons and segments.
        if (Focused && CurrentCellAddress.X == e.ColumnIndex && CurrentCellAddress.Y == e.RowIndex)
        {
            var ring = new Rectangle(e.CellBounds.X + 1, e.CellBounds.Y + 1, e.CellBounds.Width - 3, e.CellBounds.Height - 3);
            using var ringPen = new Pen(UITheme.Accent);
            graphics.DrawRectangle(ringPen, ring);
        }
    }

    /// <summary>Badge cell of recipe 4.10: the chip centered in the cell.</summary>
    private void PaintBadgeCell(DataGridViewCellPaintingEventArgs e)
    {
        if (e.Graphics is not { } graphics)
        {
            return;
        }

        using (var backgroundBrush = new SolidBrush(SelectedBackground(e.RowIndex)))
        {
            graphics.FillRectangle(backgroundBrush, e.CellBounds);
        }

        if (e.FormattedValue is string text && Enum.TryParse(text, out QuizStatus status))
        {
            StatusBadgePainter.Draw(graphics, e.CellBounds, this, status);
        }
    }

    /// <summary>The cell background: the selection tint when the row is selected.</summary>
    private Color SelectedBackground(int rowIndex)
        => Rows[rowIndex].Selected ? UITheme.AccentTint : UITheme.Surface;

    /// <summary>A row accessible object whose description is the row's action list (recipe 4.6).</summary>
    private sealed class ActionRowTemplate : DataGridViewRow
    {
        /// <summary>The row's accessible object with the actions description (recipe 4.6, F1).</summary>
        protected override AccessibleObject CreateAccessibilityInstance() => new ActionRowAccessibleObject(this);

        /// <summary>Row role from the base, description = the row's actions in column order.</summary>
        private sealed class ActionRowAccessibleObject : DataGridViewRowAccessibleObject
        {
            private readonly DataGridViewRow _row;

            public ActionRowAccessibleObject(DataGridViewRow owner) : base(owner)
            {
                _row = owner;
            }

            public override string? Description
                => _row.DataGridView is IosListGrid grid ? grid.RowActionsDescription() : base.Description;
        }
    }

    /// <summary>Repaints the previously hovered action cell.</summary>
    private void InvalidateHoveredAction()
    {
        if (_hoveredActionRow >= 0 && _hoveredActionColumn is { } hoveredColumn && Columns.Contains(hoveredColumn))
        {
            DataGridViewColumn? column = Columns[hoveredColumn];
            if (column is not null)
            {
                InvalidateCell(column.Index, _hoveredActionRow);
            }
        }
    }
}

/// <summary>
/// The action cell of recipe 4.6's action columns (F1): its accessible Name
/// is the visible action text verbatim (§4.4 — "Edit", not "Edit button for
/// row 3"), independent of how the cell paints itself.
/// </summary>
public sealed class GridActionCell : DataGridViewTextBoxCell
{
    /// <summary>The accessible object with the §4.4 name rule.</summary>
    protected override AccessibleObject CreateAccessibilityInstance() => new ActionCellAccessibleObject(this);

    private sealed class ActionCellAccessibleObject : DataGridViewCell.DataGridViewCellAccessibleObject
    {
        public ActionCellAccessibleObject(DataGridViewCell owner) : base(owner)
        {
        }

        public override string? Name => Owner is DataGridViewCell cell ? cell.FormattedValue?.ToString() : base.Name;
    }
}

/// <summary>
/// The badge cell of recipe 4.10 (F2): a badge painted inside a §4.6 cell is
/// never a bare unnamed pane — role StaticText, name = the badge's text.
/// </summary>
public sealed class GridBadgeCell : DataGridViewTextBoxCell
{
    /// <summary>The accessible object of recipe 4.10's accessibility contract.</summary>
    protected override AccessibleObject CreateAccessibilityInstance() => new BadgeCellAccessibleObject(this);

    private sealed class BadgeCellAccessibleObject : DataGridViewCell.DataGridViewCellAccessibleObject
    {
        public BadgeCellAccessibleObject(DataGridViewCell owner) : base(owner)
        {
        }

        public override AccessibleRole Role => AccessibleRole.StaticText;

        public override string? Name => Owner is DataGridViewCell cell ? cell.FormattedValue?.ToString() : base.Name;

        public override AccessibleStates State => AccessibleStates.ReadOnly; // non-interactive: no focus, no toggle
    }
}

/// <summary>
/// The container recipe 4.6 requires: the grid sits inside a §4.2 card, and
/// the zero-rows case shows the screen's empty-state panel (§5) — the grid
/// never renders its own message. The card itself is the one §4.2
/// implementation (<see cref="CardPanel"/>); this class composes it, plus
/// recipe 4.6's discoverability hint line beneath the card when the grid has
/// an action column (F1).
/// </summary>
public sealed class ListCardGrid : Panel
{
    private readonly CardPanel _card;
    private readonly Panel _emptyPanel;
    private readonly Label _emptyTitle;
    private readonly Label _emptyCaption;
    private readonly FlatButton _emptyAction;
    private readonly Label _actionHint;

    /// <summary>Creates the card with the grid docked inside.</summary>
    public ListCardGrid()
    {
        BackColor = UITheme.Background; // the module panel behind the card
        Padding = new Padding(1); // keep the grid edge inside the rounded card

        Grid = new IosListGrid
        {
            Dock = DockStyle.Fill,
        };

        _emptyTitle = new Label
        {
            Font = UITheme.Body,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Surface,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
        };

        _emptyCaption = new Label
        {
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Surface,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
        };

        _emptyAction = new FlatButton
        {
            Variant = UITheme.FlatButtonVariant.Secondary,
            AutoSize = true,
            Visible = false,
        };
        _emptyAction.Click += (_, _) => EmptyActionClicked?.Invoke(this, EventArgs.Empty);

        _actionHint = new Label
        {
            Name = "RowActionHint",
            Text = GridRowActions.HintText,
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Background,
            AutoSize = true,
            Visible = false,
            Dock = DockStyle.Bottom, // the bottom strip of the container (§4.6); invisible docks take no space
            Padding = new Padding(0, UITheme.Scaled(this, UITheme.Spacing8), 0, 0), // 8px below the card (§4.6)
        };

        _emptyPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UITheme.Surface,
            Visible = false,
        };
        _emptyPanel.Controls.Add(_emptyTitle);
        _emptyPanel.Controls.Add(_emptyCaption);
        _emptyPanel.Controls.Add(_emptyAction);

        _card = new CardPanel
        {
            Dock = DockStyle.Fill,
        };
        _card.Controls.Add(Grid);
        _card.Controls.Add(_emptyPanel);

        // Docking lays out the LAST control first: the hint takes its bottom
        // strip, the card fills the rest above it.
        Controls.Add(_card);
        Controls.Add(_actionHint);

        // The hint appears exactly when the grid gains an action column —
        // the recipe 4.6 discoverability cue rides the registration.
        Grid.ActionColumnsChanged += static (sender, e) =>
        {
            if (sender is IosListGrid grid && grid.FindAncestor<ListCardGrid>() is { } card)
            {
                card._actionHint.Visible = true;
            }
        };

        Resize += static (sender, e) =>
        {
            if (sender is ListCardGrid card)
            {
                card.LayoutEmptyState();
            }
        };
    }

    /// <summary>The recipe-styled grid of this card.</summary>
    public IosListGrid Grid { get; }

    /// <summary>Raised when the empty state's action button ("Clear search") is clicked.</summary>
    public event EventHandler? EmptyActionClicked;

    /// <summary>Shows the empty-state panel with the screen's copy (§5 states).</summary>
    public void ShowEmptyState(string title, string caption, string? actionText = null)
    {
        _emptyTitle.Text = title;
        _emptyCaption.Text = caption;
        _emptyCaption.Visible = caption.Length > 0;
        _emptyAction.Text = actionText ?? string.Empty;
        _emptyAction.Visible = actionText is not null;
        _emptyPanel.Visible = true;
        _emptyPanel.BringToFront(); // both panels dock Fill — without this the grid paints over the empty state
        LayoutEmptyState();
    }

    /// <summary>Hides the empty-state panel (rows are shown instead).</summary>
    public void HideEmptyState()
    {
        _emptyPanel.Visible = false;
    }

    /// <summary>Centers the empty-state stack in the card (§5 centered panel).</summary>
    private void LayoutEmptyState()
    {
        if (!_emptyPanel.Visible)
        {
            return;
        }

        int gap = UITheme.Scaled(this, UITheme.Spacing4);
        int actionGap = UITheme.Scaled(this, UITheme.Spacing12);
        int captionHeight = _emptyCaption.Visible ? _emptyCaption.Height + gap : 0;
        int buttonHeight = _emptyAction.Visible ? _emptyAction.Height + actionGap : 0;
        int stackHeight = _emptyTitle.Height + captionHeight + buttonHeight;
        int top = Math.Max(UITheme.Scaled(this, UITheme.Spacing16), (_card.Height - stackHeight) / 2);

        _emptyTitle.Location = new Point((_card.Width - _emptyTitle.Width) / 2, top);
        _emptyCaption.Location = new Point((_card.Width - _emptyCaption.Width) / 2, _emptyTitle.Bottom + gap);

        if (_emptyAction.Visible)
        {
            Control above = _emptyCaption.Visible ? _emptyCaption : _emptyTitle;
            _emptyAction.Location = new Point(
                (_card.Width - _emptyAction.Width) / 2,
                above.Bottom + actionGap);
        }
    }
}

/// <summary>
/// Parent-chain resolution for control events. The recipe's containers nest
/// (the grid sits inside the §4.2 card inside the module panel), so handlers
/// resolve their owning form by walking up the chain instead of testing the
/// immediate Parent — the immediate parent is an implementation detail of the
/// composition and changes when the containers change.
/// </summary>
public static class ControlTree
{
    /// <summary>
    /// The first control of type <typeparamref name="T"/> in the parent chain
    /// starting at <paramref name="control"/> (itself included), or null.
    /// </summary>
    public static T? FindAncestor<T>(this Control? control) where T : Control
    {
        for (Control? current = control; current is not null; current = current.Parent)
        {
            if (current is T ancestor)
            {
                return ancestor;
            }
        }

        return null;
    }
}
