// The list grid used by the screens.

using System.Drawing;
using System.Windows.Forms;
using CodeQuest.Models;

namespace CodeQuest.UI;

public enum GridActionKind
{
    Secondary,

    Destructive,

    Glyph,
}

public sealed class GridActionEventArgs : EventArgs
{
    public GridActionEventArgs(int rowIndex, string actionName)
    {
        RowIndex = rowIndex;
        ActionName = actionName;
    }

    public int RowIndex { get; }

    public string ActionName { get; }
}

public static class GridRowActions
{
    public const string HintText = "Enter activates a focused row action.";

    public static string Describe(IEnumerable<string> actionNames)
        => "Actions: " + string.Join(", ", actionNames) + ".";
}

public sealed class IosListGrid : DataGridView
{
    private readonly Dictionary<string, (GridActionKind Kind, string Text)> _actionColumns = new();
    private readonly HashSet<string> _badgeColumns = new();
    private int _hoveredActionRow = -1;
    private string? _hoveredActionColumn;
    private Point _focusedActionCell = new(-1, -1);

    public IosListGrid()
    {
        DoubleBuffered = true;
        BorderStyle = BorderStyle.None;
        BackgroundColor = UITheme.Surface;
        GridColor = UITheme.SeparatorHairline;

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

        EnableHeadersVisualStyles = false;
        ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        ColumnHeadersHeight = UITheme.Scaled(this, 32);
        ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = UITheme.Surface,
            ForeColor = UITheme.AccentText,
            Font = UITheme.Caption,
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            WrapMode = DataGridViewTriState.False,
            SelectionBackColor = UITheme.Surface,
            SelectionForeColor = UITheme.TextSecondary,
        };

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

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<int, string, bool>? IsActionEnabled { get; set; }

    public event EventHandler<GridActionEventArgs>? ActionClicked;

    internal event EventHandler? ActionColumnsChanged;

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
            CellTemplate = new GridActionCell(),
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
            CellTemplate = new GridBadgeCell(),
        };
        column.HeaderCell.Style.Padding = new Padding(
            UITheme.Scaled(this, UITheme.Spacing8), 0, UITheme.Scaled(this, UITheme.Spacing8), 0);

        _badgeColumns.Add(name);
        Columns.Add(column);
    }

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

    protected override void OnCellPainting(DataGridViewCellPaintingEventArgs e)
    {
        base.OnCellPainting(e);

        if (e.ColumnIndex < 0 && e.RowIndex < 0)
        {
            return;
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

    protected override void OnRowPostPaint(DataGridViewRowPostPaintEventArgs e)
    {
        base.OnRowPostPaint(e);

        if (e.RowIndex >= Rows.Count - 1)
        {
            return;
        }

        Rectangle rowBounds = e.RowBounds;
        int inset = UITheme.Scaled(this, UITheme.Spacing16);
        int y = rowBounds.Bottom - 1;

        using var pen = new Pen(UITheme.SeparatorHairline);
        e.Graphics.DrawLine(pen, inset, y, rowBounds.Right, y);
    }

    protected override void OnRowsRemoved(DataGridViewRowsRemovedEventArgs e)
    {
        base.OnRowsRemoved(e);
        _hoveredActionRow = -1;
        _hoveredActionColumn = null;
    }

    private bool IsActionColumn(string columnName) => _actionColumns.ContainsKey(columnName);

    private bool IsActionEnabledAt(int rowIndex, string columnName)
        => IsActionEnabled?.Invoke(rowIndex, columnName) ?? true;

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

        return true;
    }

    protected override bool ProcessDialogKey(Keys keyData)
    {
        if (TryActivateFocusedAction(keyData & Keys.KeyCode))
        {
            return true;
        }

        return base.ProcessDialogKey(keyData);
    }

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

    protected override void OnCurrentCellChanged(EventArgs e)
    {
        base.OnCurrentCellChanged(e);
        InvalidateActionCell(_focusedActionCell);
        _focusedActionCell = CurrentCellAddress;
        InvalidateActionCell(_focusedActionCell);
    }

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

    private void PaintQuietHeader(DataGridViewCellPaintingEventArgs e)
    {
        if (e.Graphics is not { } graphics)
        {
            return;
        }

        using (var backgroundBrush = new SolidBrush(UITheme.Surface))
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
                UITheme.AccentText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
    }

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
            : UITheme.AccentText;

        Font font = kind == GridActionKind.Glyph ? UITheme.NavGlyph : UITheme.Body;

        TextRenderer.DrawText(
            graphics,
            text,
            font,
            e.CellBounds,
            label,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

        if (Focused && CurrentCellAddress.X == e.ColumnIndex && CurrentCellAddress.Y == e.RowIndex)
        {
            var ring = new Rectangle(e.CellBounds.X + 1, e.CellBounds.Y + 1, e.CellBounds.Width - 3, e.CellBounds.Height - 3);
            using var ringPen = new Pen(UITheme.AccentText);
            graphics.DrawRectangle(ringPen, ring);
        }
    }

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

    private Color SelectedBackground(int rowIndex)
        => Rows[rowIndex].Selected ? UITheme.AccentTint : UITheme.Surface;

    private sealed class ActionRowTemplate : DataGridViewRow
    {
        protected override AccessibleObject CreateAccessibilityInstance() => new ActionRowAccessibleObject(this);

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

public sealed class GridActionCell : DataGridViewTextBoxCell
{
    protected override AccessibleObject CreateAccessibilityInstance() => new ActionCellAccessibleObject(this);

    private sealed class ActionCellAccessibleObject : DataGridViewCell.DataGridViewCellAccessibleObject
    {
        public ActionCellAccessibleObject(DataGridViewCell owner) : base(owner)
        {
        }

        public override string? Name => Owner is DataGridViewCell cell ? cell.FormattedValue?.ToString() : base.Name;
    }
}

public sealed class GridBadgeCell : DataGridViewTextBoxCell
{
    protected override AccessibleObject CreateAccessibilityInstance() => new BadgeCellAccessibleObject(this);

    private sealed class BadgeCellAccessibleObject : DataGridViewCell.DataGridViewCellAccessibleObject
    {
        public BadgeCellAccessibleObject(DataGridViewCell owner) : base(owner)
        {
        }

        public override AccessibleRole Role => AccessibleRole.StaticText;

        public override string? Name => Owner is DataGridViewCell cell ? cell.FormattedValue?.ToString() : base.Name;

        public override AccessibleStates State => AccessibleStates.ReadOnly;
    }
}

public sealed class ListCardGrid : Panel
{
    private readonly CardPanel _card;
    private readonly Panel _emptyPanel;
    private readonly Label _emptyTitle;
    private readonly Label _emptyCaption;
    private readonly FlatButton _emptyAction;
    private readonly Label _actionHint;

    public ListCardGrid()
    {
        BackColor = UITheme.Background;
        Padding = new Padding(1);

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
            Dock = DockStyle.Bottom,
            Padding = new Padding(0, UITheme.Scaled(this, UITheme.Spacing8), 0, 0),
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

        Controls.Add(_card);
        Controls.Add(_actionHint);

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

    public IosListGrid Grid { get; }

    public event EventHandler? EmptyActionClicked;

    public void ShowEmptyState(string title, string caption, string? actionText = null)
    {
        _emptyTitle.Text = title;
        _emptyCaption.Text = caption;
        _emptyCaption.Visible = caption.Length > 0;
        _emptyAction.Text = actionText ?? string.Empty;
        _emptyAction.Visible = actionText is not null;
        _emptyPanel.Visible = true;
        _emptyPanel.BringToFront();
        LayoutEmptyState();
    }

    public void HideEmptyState()
    {
        _emptyPanel.Visible = false;
    }

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

public static class ControlTree
{
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
