// The achievements screen.

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CodeQuest.Data;
using CodeQuest.Models;
using CodeQuest.Services;

namespace CodeQuest.UI;

public sealed class AchievementsForm : ModulePanel
{
    private const string ListCaption = "Unlocks are recorded as you play.";
    private const string LockedStatus = "Locked";
    private const string UnlockedPrefix = "Unlocked ";
    private const string LockGlyph = "\uE72E";

    private const int CardWidth = 240;
    private const int CardHeight = 134;

    private readonly UserAccount _user;
    private readonly IGamificationRepository _gamification;
    private readonly IAchievementEvaluator _evaluator;
    private readonly Label _listCaption;
    private readonly FlowLayoutPanel _flow;
    private readonly List<AchievementCard> _cards;

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

    public override void RefreshData()
    {
        IReadOnlyList<UnlockedAchievement> unlocked = _gamification.GetAchievements(_user.UserId);
        Dictionary<string, DateTimeOffset> unlockedAtByKey = new();
        foreach (UnlockedAchievement achievement in unlocked)
        {
            unlockedAtByKey[achievement.Key] = achievement.UnlockedAt;
        }

        foreach (AchievementCard card in _cards)
        {
            card.SetState(unlockedAtByKey.TryGetValue(card.Definition.Key, out DateTimeOffset unlockedAt), unlockedAt);
        }
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        _listCaption.Width = Math.Max(Width, 1);
    }

    private sealed class AchievementCard
    {
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
                ForeColor = UITheme.TextSecondary,
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

        public AchievementDefinition Definition { get; }

        public CardPanel Card { get; }

        private GlyphTilePanel Tile { get; }

        private Label Title { get; }

        private Label Description { get; }

        private Label Status { get; }

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

    private sealed class GlyphTilePanel : Panel
    {
        private bool _unlocked;

        public GlyphTilePanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.ResizeRedraw, true);
            TabStop = false;
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string Glyph { get; set; } = LockGlyph;

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
                Invalidate();
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(UITheme.ContainerSurface(this));
        }

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
