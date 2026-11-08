// =============================================================================
// CodeQuest — Gamification UI (plan WP5, spec §5.13): the SCOREBOARD screen.
// The leaderboard of the users' past scores (owner directive): every account
// on this machine, ranked by total XP — and XP is DERIVED, never stored
// (score × difficulty weight per attempt, plan A3 — the repository owns the
// math; this screen only displays it).
// Look at: the layout — the personal summary card (§4.2 + hairline) over the
//          leaderboard table card (recipe 4.6, no action column, selection
//          disabled — the §5.13 interaction rule: this screen is read-only).
//          Summary card: display name (BodySemibold) + the §4.14 level chip;
//          8px below: the 8px progress track (Fill track, Accent fill) with
//          the remaining-XP caption right-aligned on the same row; the
//          trailing block is the total XP (Title) over the "total XP" caption.
//          Table: Rank · Player · Level · XP · Quizzes — headers are the
//          grid's own AccentText Caption labels (§4.6 D4 rule); Rank/XP/
//          Quizzes right-aligned, Player/Level left-aligned (§5.13).
//          The signed-in row is triple-coded, never color alone (§5.13):
//          AccentTint full-row fill + the name in BodySemibold + a "You"
//          chip (Surface fill, AccentText CaptionSemibold — 7.18:1) painted
//          into the Player cell through the one shared ChipPainter.
// Ordering: XP desc, then more quizzes, then display name A..Z (plan FR3) —
//          the repository returns the rows in exactly that order; the rank is
//          the 1-based display position.
// States:  empty — the §5.13 empty-state copy (a guard: with at least one
//          account the leaderboard always has a row, so this is defensive);
//          read error — the §4.11 unexpected dialog (like every module: no
//          local catch, the shell owns unexpected failures).
// =============================================================================

using System.Drawing;
using System.Windows.Forms;
using CodeQuest.Data;
using CodeQuest.Models;
using CodeQuest.Services;

namespace CodeQuest.UI;

/// <summary>
/// The scoreboard screen (spec §5.13): the signed-in player's level progress
/// above the leaderboard of every account.
/// </summary>
public sealed class ScoreboardForm : ModulePanel
{
    /// <summary>The §5.13 empty-state copy (defensive: the table may never be empty).</summary>
    private const string EmptyTitle = "No quizzes taken yet";
    private const string EmptyCaption = "Take a quiz and the scoreboard will fill in.";
    private const string TotalXpCaption = "total XP";
    private const string YouChipText = "You";

    /// <summary>The standard-DPI height of the summary card (§5.13 anatomy).</summary>
    private const int SummaryCardHeight = 80;

    private readonly UserAccount _user;
    private readonly IGamificationRepository _gamification;
    private readonly CardPanel _summaryCard;
    private readonly Label _playerName;
    private readonly LevelChip _levelChip;
    private readonly ProgressTrack _xpBar;
    private readonly Label _xpCaption;
    private readonly Label _totalXp;
    private readonly Label _totalCaption;
    private readonly ListCardGrid _list;
    private int _signedInRowIndex = -1;
    private int _playerColumnIndex = -1;

    /// <summary>Creates the screen for one signed-in account over the gamification aggregates.</summary>
    public ScoreboardForm(UserAccount user, IGamificationRepository gamification)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(gamification);
        _user = user;
        _gamification = gamification;

        BackColor = UITheme.Background;
        Dock = DockStyle.Fill;
        Visible = false;

        _summaryCard = new CardPanel
        {
            Name = "ScoreboardSummaryCard",
            Location = new Point(0, 0),
            Size = new Size(Math.Max(Width, 1), UITheme.Scaled(this, SummaryCardHeight)),
        };

        _playerName = new Label
        {
            Name = "ScoreboardPlayerName",
            Font = UITheme.BodySemibold,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            AutoEllipsis = true,
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, 14)),
            Size = new Size(UITheme.Scaled(this, 120), UITheme.Scaled(this, 22)),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _levelChip = new LevelChip
        {
            Name = "ScoreboardLevelChip",
            Location = new Point(UITheme.Scaled(this, 144), UITheme.Scaled(this, 15)),
        };

        _xpBar = new ProgressTrack
        {
            Name = "ScoreboardXpBar",
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, 46)),
            Size = new Size(UITheme.Scaled(this, 160), UITheme.Scaled(this, 8)),
            AccessibleName = "Level progress",
        };

        _xpCaption = new Label
        {
            Name = "ScoreboardXpCaption",
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(UITheme.Scaled(this, 184), UITheme.Scaled(this, 42)),
            Size = new Size(UITheme.Scaled(this, 120), UITheme.Scaled(this, 16)),
            TextAlign = ContentAlignment.MiddleRight,
        };

        _totalXp = new Label
        {
            Name = "ScoreboardTotalXp",
            Font = UITheme.Title,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(UITheme.Scaled(this, 320), UITheme.Scaled(this, 12)),
            Size = new Size(UITheme.Scaled(this, 120), UITheme.Scaled(this, 30)),
            TextAlign = ContentAlignment.MiddleRight,
        };

        _totalCaption = new Label
        {
            Name = "ScoreboardTotalCaption",
            Text = TotalXpCaption,
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(UITheme.Scaled(this, 320), UITheme.Scaled(this, 44)),
            Size = new Size(UITheme.Scaled(this, 120), UITheme.Scaled(this, 16)),
            TextAlign = ContentAlignment.MiddleRight,
        };

        _summaryCard.Controls.Add(_playerName);
        _summaryCard.Controls.Add(_levelChip);
        _summaryCard.Controls.Add(_xpBar);
        _summaryCard.Controls.Add(_xpCaption);
        _summaryCard.Controls.Add(_totalXp);
        _summaryCard.Controls.Add(_totalCaption);

        _list = new ListCardGrid
        {
            Name = "ScoreboardListCard",
            Location = new Point(0, UITheme.Scaled(this, SummaryCardHeight + UITheme.Spacing16)),
            Size = new Size(Math.Max(Width, 1), Math.Max(Height - UITheme.Scaled(this, SummaryCardHeight + UITheme.Spacing16), 1)),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
        };
        _list.Grid.Name = "ScoreboardGrid";
        _list.Grid.AccessibleName = "Scoreboard";
        _list.Grid.TabStop = false; // §5.13: the grid is not a tab stop (read-only screen)

        _list.Grid.AddTextColumn("Rank", "Rank", 8f, extraLeftInset: UITheme.Scaled(this, UITheme.Spacing8), minimumWidth: 60);
        _list.Grid.AddTextColumn("Player", "Player", 42f, extraLeftInset: UITheme.Scaled(this, UITheme.Spacing8), minimumWidth: 140);
        _list.Grid.AddTextColumn("Level", "Level", 14f, minimumWidth: 80);
        _list.Grid.AddTextColumn("Xp", "XP", 18f, minimumWidth: 80);
        _list.Grid.AddTextColumn("Quizzes", "Quizzes", 18f, minimumWidth: 90);
        _playerColumnIndex = _list.Grid.Columns["Player"]!.Index; // exists — added above

        // §5.13 alignment rule: Rank / XP / Quizzes right-aligned, the rest left.
        _list.Grid.Columns["Rank"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _list.Grid.Columns["Xp"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _list.Grid.Columns["Quizzes"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

        // §5.13: selection disabled — a click must not leave a selection tint
        // behind (the signed-in row's AccentTint fill is a painted state, not
        // a selection).
        _list.Grid.SelectionChanged += static (sender, e) =>
        {
            if (sender is IosListGrid grid && grid.SelectedCells.Count > 0)
            {
                grid.ClearSelection();
            }
        };
        _list.Grid.CellPainting += HandleCellPainting;

        Controls.Add(_list);
        Controls.Add(_summaryCard);
    }

    /// <inheritdoc />
    public override void RefreshData()
    {
        // Two reads per refresh (the screen reloads on every show, §5.2):
        // the leaderboard and the signed-in player's progress.
        IReadOnlyList<LeaderboardEntry> leaderboard = _gamification.GetLeaderboard();
        UserProgress progress = _gamification.GetUserProgress(_user.UserId);

        UpdateSummary(progress);
        FillGrid(leaderboard);
    }

    /// <summary>Keeps the summary card full-width and lays its blocks out (M6 rule).</summary>
    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        _summaryCard.Width = Math.Max(Width, 1);
        LayoutSummary();
    }

    /// <summary>Fills the personal summary card from the derived progress.</summary>
    private void UpdateSummary(UserProgress progress)
    {
        _playerName.Text = _user.DisplayName;
        _levelChip.SetLevel(progress.Level);
        _totalXp.Text = progress.TotalXp.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _xpBar.Value = progress.ProgressFraction;

        // The §4.14 caption: the XP still missing for the next level. At the
        // top of the level range there is no next threshold (WP1's contract:
        // XpForNextLevel 0) — the caption says so instead of a nonsense 0.
        _xpCaption.Text = progress.XpForNextLevel == 0
            ? "Max level"
            : (progress.XpForNextLevel - progress.XpIntoLevel).ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " XP to Lvl " + (progress.Level + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

        LayoutSummary();
    }

    /// <summary>Fills the leaderboard rows and highlights the signed-in player.</summary>
    private void FillGrid(IReadOnlyList<LeaderboardEntry> leaderboard)
    {
        if (leaderboard.Count == 0)
        {
            _list.Grid.Rows.Clear();
            _signedInRowIndex = -1;
            _list.ShowEmptyState(EmptyTitle, EmptyCaption);
            return;
        }

        _list.HideEmptyState();
        _list.Grid.SuspendLayout();
        _list.Grid.Rows.Clear();
        _signedInRowIndex = -1;

        for (int i = 0; i < leaderboard.Count; i++)
        {
            LeaderboardEntry entry = leaderboard[i];
            int index = _list.Grid.Rows.Add(
                (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                entry.DisplayName,
                entry.Level.ToString(System.Globalization.CultureInfo.InvariantCulture),
                entry.TotalXp.ToString(System.Globalization.CultureInfo.InvariantCulture),
                entry.QuizzesTaken.ToString(System.Globalization.CultureInfo.InvariantCulture));

            if (entry.UserId == _user.UserId)
            {
                _signedInRowIndex = index;
            }
        }

        if (_signedInRowIndex >= 0)
        {
            DataGridViewRow row = _list.Grid.Rows[_signedInRowIndex];

            // §5.13's triple coding: tint fill + semibold name + the chip.
            row.DefaultCellStyle.BackColor = UITheme.AccentTint;
            row.Cells["Player"]!.Style.Font = UITheme.BodySemibold; // the column exists (added above)
        }

        _list.Grid.ResumeLayout();
        _list.Grid.CurrentCell = null; // no current cell, no focus rectangle (read-only screen)
    }

    /// <summary>
    /// Paints the "You" chip into the signed-in row's Player cell (§5.13) —
    /// the third code of the highlight, after the fill and the name weight.
    /// </summary>
    private void HandleCellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0
            || e.ColumnIndex != _playerColumnIndex
            || e.RowIndex != _signedInRowIndex
            || e.Graphics is not { } graphics)
        {
            return;
        }

        e.PaintBackground(e.ClipBounds, false);
        e.PaintContent(e.ClipBounds);

        string nameText = e.FormattedValue?.ToString() ?? string.Empty;
        Font nameFont = e.CellStyle?.Font ?? UITheme.Body;
        int textWidth = TextRenderer.MeasureText(nameText, nameFont).Width;

        int chipHeight = UITheme.Scaled(this, 18);
        int chipWidth = TextRenderer.MeasureText(YouChipText, UITheme.CaptionSemibold).Width
            + UITheme.Scaled(this, UITheme.Spacing8 * 2);
        int chipX = Math.Min(
            e.CellBounds.Left + Math.Max(e.CellStyle?.Padding.Left ?? 0, 0) + textWidth + UITheme.Scaled(this, UITheme.Spacing8),
            e.CellBounds.Right - chipWidth - UITheme.Scaled(this, 4));
        int chipY = e.CellBounds.Top + ((e.CellBounds.Height - chipHeight) / 2);

        ChipPainter.Draw(
            graphics,
            new Rectangle(chipX, chipY, chipWidth, chipHeight),
            YouChipText,
            UITheme.CaptionSemibold,
            UITheme.Surface,
            UITheme.AccentText,
            UITheme.Scaled(this, UITheme.RadiusChip));

        e.Handled = true;
    }

    /// <summary>
    /// Lays the summary card's blocks out: leading block (name + chip over the
    /// progress bar with its caption) and the trailing total-XP block, both
    /// right-aligned inside the card (§5.13 anatomy). Runs on resize and
    /// whenever a text changes — the labels are measured, never guessed.
    /// </summary>
    private void LayoutSummary()
    {
        int inset = UITheme.Scaled(this, UITheme.Spacing16);
        int cardWidth = _summaryCard.Width;

        Size titleSize = TextRenderer.MeasureText(_totalXp.Text, UITheme.Title);
        Size capSize = TextRenderer.MeasureText(_totalCaption.Text, UITheme.Caption);
        Size chipSize = _levelChip.Size;
        Size xpCaptionSize = TextRenderer.MeasureText(_xpCaption.Text, UITheme.Caption);

        int trailingWidth = Math.Max(titleSize.Width, capSize.Width);
        int leadingRight = Math.Max(cardWidth - inset - trailingWidth - UITheme.Scaled(this, UITheme.Spacing24), inset + UITheme.Scaled(this, 120));

        // Leading block, row 1: name … level chip (trailing). The name spans
        // [inset, leadingRight − chip − 8]; AutoEllipsis absorbs overflow.
        _playerName.Location = new Point(inset, UITheme.Scaled(this, 14));
        _playerName.Size = new Size(
            Math.Max(leadingRight - chipSize.Width - UITheme.Scaled(this, UITheme.Spacing8) - inset, UITheme.Scaled(this, 40)),
            UITheme.Scaled(this, 22));
        _levelChip.Location = new Point(Math.Max(leadingRight - chipSize.Width, _playerName.Right), UITheme.Scaled(this, 15));

        // Leading block, row 2: progress bar with the remaining-XP caption.
        _xpCaption.Size = new Size(Math.Max(xpCaptionSize.Width, UITheme.Scaled(this, 40)), UITheme.Scaled(this, 16));
        _xpCaption.Location = new Point(Math.Max(leadingRight - _xpCaption.Width, inset), UITheme.Scaled(this, 42));
        int barWidth = Math.Max(_xpCaption.Left - inset - UITheme.Scaled(this, UITheme.Spacing8), UITheme.Scaled(this, 40));
        _xpBar.Location = new Point(inset, UITheme.Scaled(this, 46));
        _xpBar.Size = new Size(barWidth, UITheme.Scaled(this, 8));

        // Trailing block: total XP over its caption, right-aligned.
        _totalXp.Size = new Size(Math.Max(titleSize.Width, UITheme.Scaled(this, 40)), UITheme.Scaled(this, 30));
        _totalXp.Location = new Point(cardWidth - inset - _totalXp.Width, UITheme.Scaled(this, 12));
        _totalCaption.Size = new Size(Math.Max(capSize.Width, UITheme.Scaled(this, 40)), UITheme.Scaled(this, 16));
        _totalCaption.Location = new Point(cardWidth - inset - _totalCaption.Width, UITheme.Scaled(this, 46));
    }
}
