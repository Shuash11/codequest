// =============================================================================
// CodeQuest — Gamification UI (plan WP5, spec §5.14): the ACHIEVEMENTS screen.
// The catalog is code-defined (plan FR4 — the definitions live with the
// evaluator in Services/AchievementEvaluator.cs); the DATABASE records what
// the player has actually earned (user_achievements, plan A5). This screen is
// where the owner's "the app records what's the achievement" becomes visible:
// ten cards, locked until earned, the unlock date shown once recorded — and
// a re-completed achievement never changes its date (PK (user_id, key)).
// Look at: the layout — the §5.12 explaining caption over a wrapping card
//          grid (FlowLayoutPanel, 16px card↔card gaps via 8px margin + 8px
//          padding, AutoScroll). Card anatomy (§5.14): glyph tile 40×40
//          (RadiusControl 8) + the title beside it (BodySemibold), 8px, the
//          description (Caption, two lines), 8px, the status line.
//          Unlocked = possession → the ACCENT family (AccentTint tile,
//          AccentText glyph, 5.83:1) — deliberately NOT success-green
//          (success stays a quiz verdict; ten green cards would be semantic
//          color as decoration, §1.6 of the D4 amendment). Locked = the ink
//          ramp does the muting (Fill tile, lock glyph, TextTertiary status;
//          the description STAYS TextSecondary — it tells the player what to
//          do, and must be readable, 6.88:1). No card-level opacity tricks,
//          no lock overlay, no recolor.
// States:  no empty state by construction (the catalog always renders its ten
//          cards — Lead-confirmed); no per-card interaction (display-only:
//          no hover, no focus, no tab stops); read error → §4.11 dialog.
// =============================================================================

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CodeQuest.Data;
using CodeQuest.Models;
using CodeQuest.Services;

namespace CodeQuest.UI;

/// <summary>
/// The achievements screen (spec §5.14): the full catalog as a wrapping card
/// grid, locked states muted by the ink ramp, unlocks recorded with dates.
/// </summary>
public sealed class AchievementsForm : ModulePanel
{
    /// <summary>The §5.14 explaining caption (the §5.12 pattern).</summary>
    private const string ListCaption = "Unlocks are recorded as you play.";
    private const string LockedStatus = "Locked";
    private const string UnlockedPrefix = "Unlocked ";
    private const string LockGlyph = "\uE72E"; // Segoe MDL2 Assets "Lock" (§5.14, verified)

    /// <summary>The §5.14 card size (standard-DPI; content-fit at the 16px inset).</summary>
    private const int CardWidth = 240;
    private const int CardHeight = 134;

    private readonly UserAccount _user;
    private readonly IGamificationRepository _gamification;
    private readonly IAchievementEvaluator _evaluator;
    private readonly Label _listCaption;
    private readonly FlowLayoutPanel _flow;
    private readonly List<AchievementCard> _cards;

    /// <summary>Creates the screen for one signed-in account over the catalog and the unlock records.</summary>
    public AchievementsForm(UserAccount user, IGamificationRepository gamification, IAchievementEvaluator evaluator)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(gamification);
        ArgumentNullException.ThrowIfNull(evaluator);
        _user = user;
        _gamification = gamification;
        _evaluator = evaluator;

        BackColor = UITheme.Background;
        Dock = DockStyle.Fill;
        Visible = false;

        int captionHeight = UITheme.Scaled(this, 20);
        _listCaption = new Label
        {
            Name = "AchievementsCaption",
            Text = ListCaption,
            Font = UITheme.Body,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Background,
            AutoSize = false,
            Location = new Point(0, 0),
            Size = new Size(Math.Max(Width, 1), captionHeight),
        };

        // The wrapping card grid: 8px padding + 8px margins = the spec's 16px
        // card↔card gaps and screen-edge inset; AutoScroll carries overflow.
        _flow = new FlowLayoutPanel
        {
            Name = "AchievementsFlow",
            Location = new Point(0, captionHeight + UITheme.Scaled(this, UITheme.Spacing16)),
            Size = new Size(Math.Max(Width, 1), Math.Max(Height - captionHeight - UITheme.Scaled(this, UITheme.Spacing16), 1)),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = UITheme.Background,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = true,
            Padding = new Padding(UITheme.Scaled(this, UITheme.Spacing8)),
            TabStop = false,
        };

        // The cards are built ONCE from the catalog (the definitions never
        // change at runtime); RefreshData only re-states locked/unlocked.
        _cards = new List<AchievementCard>();
        foreach (AchievementDefinition definition in _evaluator.Catalog)
        {
            AchievementCard card = new(definition);
            _flow.Controls.Add(card.Card);
            _cards.Add(card);
        }

        Controls.Add(_flow);
        Controls.Add(_listCaption);
    }

    /// <inheritdoc />
    public override void RefreshData()
    {
        // One read: every unlock record of this user. The catalog order is
        // preserved — the screen always shows all ten, locked until earned.
        IReadOnlyList<UnlockedAchievement> unlocked = _gamification.GetAchievements(_user.UserId);
        Dictionary<string, DateTimeOffset> unlockedAtByKey = new();
        foreach (UnlockedAchievement achievement in unlocked)
        {
            unlockedAtByKey[achievement.Key] = achievement.UnlockedAt; // one row per key (table PK)
        }

        foreach (AchievementCard card in _cards)
        {
            card.SetState(unlockedAtByKey.TryGetValue(card.Definition.Key, out DateTimeOffset unlockedAt), unlockedAt);
        }
    }

    /// <summary>Keeps the caption full-width as the module resizes (M6).</summary>
    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        _listCaption.Width = Math.Max(Width, 1);
    }

    /// <summary>
    /// One achievement card of the §5.14 grid: the §4.2 card composed with the
    /// glyph tile, the title, the description and the status line.
    /// </summary>
    private sealed class AchievementCard
    {
        /// <summary>Creates the card for one catalog definition (starts locked).</summary>
        public AchievementCard(AchievementDefinition definition)
        {
            Definition = definition;

            int inset = UITheme.Spacing16;

            var card = new CardPanel
            {
                Name = "AchievementCard" + definition.Key,
            };
            card.Margin = new Padding(UITheme.Scaled(card, UITheme.Spacing8));
            card.Size = new Size(UITheme.Scaled(card, CardWidth), UITheme.Scaled(card, CardHeight));

            var tile = new GlyphTilePanel
            {
                Name = "AchievementTile" + definition.Key,
                Glyph = definition.Glyph,
            };
            tile.Location = new Point(UITheme.Scaled(tile, inset), UITheme.Scaled(tile, inset));
            tile.Size = new Size(UITheme.Scaled(tile, 40), UITheme.Scaled(tile, 40));

            var title = new Label
            {
                Name = "AchievementTitle" + definition.Key,
                Text = definition.Title,
                Font = UITheme.BodySemibold,
                ForeColor = UITheme.TextSecondary, // the locked default; SetState re-inks
                BackColor = UITheme.Surface,
                AutoSize = false,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            title.Location = new Point(UITheme.Scaled(tile, inset + 40 + UITheme.Spacing8), UITheme.Scaled(tile, inset));
            title.Size = new Size(UITheme.Scaled(tile, CardWidth - inset - 40 - UITheme.Spacing8 - inset), UITheme.Scaled(tile, 40));

            var description = new Label
            {
                Name = "AchievementDescription" + definition.Key,
                Text = definition.Description,
                Font = UITheme.Caption,
                ForeColor = UITheme.TextSecondary,
                BackColor = UITheme.Surface,
                AutoSize = false,
            };
            description.Location = new Point(UITheme.Scaled(tile, inset), UITheme.Scaled(tile, inset + 40 + UITheme.Spacing8));
            description.Size = new Size(UITheme.Scaled(tile, CardWidth - inset * 2), UITheme.Scaled(tile, 30));

            var status = new Label
            {
                Name = "AchievementStatus" + definition.Key,
                Text = LockedStatus,
                Font = UITheme.Caption,
                ForeColor = UITheme.TextTertiary,
                BackColor = UITheme.Surface,
                AutoSize = false,
            };
            status.Location = new Point(UITheme.Scaled(tile, inset), UITheme.Scaled(tile, CardHeight - inset - 16));
            status.Size = new Size(UITheme.Scaled(tile, CardWidth - inset * 2), UITheme.Scaled(tile, 16));

            card.Controls.Add(tile);
            card.Controls.Add(title);
            card.Controls.Add(description);
            card.Controls.Add(status);

            Card = card;
            Tile = tile;
            Title = title;
            Description = description;
            Status = status;
        }

        /// <summary>The catalog definition this card displays (order fixed at build).</summary>
        public AchievementDefinition Definition { get; }

        /// <summary>The composed §4.2 card.</summary>
        public CardPanel Card { get; }

        private GlyphTilePanel Tile { get; }

        private Label Title { get; }

        private Label Description { get; }

        private Label Status { get; }

        /// <summary>
        /// Re-states the card for one unlock record: the §5.14 ink rules —
        /// unlocked = AccentTint tile + AccentText glyph + TextPrimary title +
        /// the dated status line; locked = Fill tile + lock glyph + muted ink,
        /// description kept readable (TextSecondary).
        /// </summary>
        public void SetState(bool unlocked, DateTimeOffset unlockedAt)
        {
            Tile.Unlocked = unlocked;
            Tile.Glyph = unlocked ? Definition.Glyph : LockGlyph;

            Title.ForeColor = unlocked ? UITheme.TextPrimary : UITheme.TextSecondary;

            Status.Text = unlocked
                ? UnlockedPrefix + UiFormat.FormatDate(unlockedAt)
                : LockedStatus;
            Status.ForeColor = unlocked ? UITheme.TextSecondary : UITheme.TextTertiary;
        }
    }

    /// <summary>
    /// The §5.14 glyph tile: a 40×40 rounded square (RadiusControl 8) whose
    /// fill is AccentTint (unlocked) or Fill (locked) and whose glyph renders
    /// in AccentText / TextTertiary respectively — the corner cutouts resolve
    /// the card's Surface at paint time (D4 audit rule #1).
    /// </summary>
    private sealed class GlyphTilePanel : Panel
    {
        private bool _unlocked;

        public GlyphTilePanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.ResizeRedraw, true);
            TabStop = false; // display-only: no focus, no tab stops (§5.14 states)
        }

        /// <summary>The Segoe MDL2 glyph drawn centered (the achievement's own, or the lock).</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string Glyph { get; set; } = LockGlyph;

        /// <summary>Whether the achievement is earned — flips the tile's fill and ink.</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool Unlocked
        {
            get => _unlocked;
            set
            {
                if (_unlocked == value)
                {
                    return;
                }

                _unlocked = value;
                Invalidate(); // the tile paints its own state — a flip must repaint
            }
        }

        /// <summary>The corner cutouts show the card's Surface, never the tile fill.</summary>
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(UITheme.ContainerSurface(this));
        }

        /// <summary>Paints the rounded tile and its centered glyph.</summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (Width <= 0 || Height <= 0)
            {
                return;
            }

            var bounds = new Rectangle(0, 0, Width, Height);
            using GraphicsPath path = UITheme.RoundedPath(bounds, UITheme.Scaled(this, UITheme.RadiusControl));
            using var brush = new SolidBrush(_unlocked ? UITheme.AccentTint : UITheme.Fill);
            e.Graphics.FillPath(brush, path);

            TextRenderer.DrawText(
                e.Graphics,
                Glyph,
                UITheme.AchievementGlyph,
                bounds,
                _unlocked ? UITheme.AccentText : UITheme.TextTertiary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }
}
