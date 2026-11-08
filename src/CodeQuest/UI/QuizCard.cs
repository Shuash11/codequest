// =============================================================================
// CodeQuest — The fun quiz UI: one QUIZ CARD on the home screen. A takeable
// quiz is not a table row — it is a big, colorful card with the difficulty
// color as its header band, the quiz title in bold, topic + question-count
// metadata, and a wide PLAY pill. Draft quizzes render as locked cards
// ("Pending review", no play) — visible but not startable.
// Accessibility: the PLAY pill carries the accessible name "Play {title}" so
// screen readers announce the target; the card exposes a readable name too.
// =============================================================================

using CodeQuest.Models;
using System.Drawing;
using System.Windows.Forms;

namespace CodeQuest.UI;

/// <summary>
/// One colorful quiz card on the take-quiz home (the fun-UI redesign).
/// </summary>
public sealed class QuizCard : Panel
{
    private const string PlayGlyph = "\u25B6";
    private const string LockedGlyph = "\uE72E";

    private readonly Label _title = new();
    private readonly Label _meta = new();
    private readonly Label _status = new();
    private readonly FlatButton _play = new();
    private readonly int _quizId;
    private readonly bool _approved;

    /// <summary>Raised when the user presses PLAY on an approved quiz.</summary>
    public event EventHandler<int>? PlayClicked;

    /// <summary>Creates one quiz card (approved = playable, draft = locked).</summary>
    public QuizCard(Quiz quiz, string topicName, int questionCount)
    {
        _quizId = quiz.QuizId;
        _approved = quiz.Status == QuizStatus.Approved;

        DoubleBuffered = true;
        Size = new Size(UITheme.Scaled(this, 320), UITheme.Scaled(this, 168));
        Margin = new Padding(0, 0, UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, UITheme.Spacing16));
        BackColor = UITheme.Surface;

        Color bandColor = _approved ? LevelColor(quiz.Difficulty) : UITheme.TextTertiary;

        // The difficulty color band across the card's top edge.
        var band = new Panel
        {
            Name = "QuizCardBand",
            Dock = DockStyle.Top,
            Height = UITheme.Scaled(this, 6),
            BackColor = bandColor,
        };

        _title = new Label
        {
            Name = "QuizCardTitle",
            Text = quiz.Title,
            Font = UITheme.BodySemibold,
            ForeColor = _approved ? UITheme.TextPrimary : UITheme.TextTertiary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            AutoEllipsis = true,
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, UITheme.Spacing16)),
            Size = new Size(Width - UITheme.Scaled(this, UITheme.Spacing32), UITheme.Scaled(this, 40)),
        };

        _meta = new Label
        {
            Name = "QuizCardMeta",
            Text = $"{topicName}  \u00B7  {questionCount} questions",
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, 60)),
            Size = new Size(Width - UITheme.Scaled(this, UITheme.Spacing32), UITheme.Scaled(this, 18)),
        };

        var difficulty = new Label
        {
            Name = "QuizCardDifficulty",
            Text = quiz.Difficulty.ToString(),
            Font = UITheme.CaptionSemibold,
            ForeColor = _approved ? bandColor : UITheme.TextTertiary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, 84)),
            Size = new Size(UITheme.Scaled(this, 140), UITheme.Scaled(this, 18)),
        };

        _status = new Label
        {
            Name = "QuizCardStatus",
            Text = _approved ? string.Empty : "Pending review",
            Font = UITheme.Caption,
            ForeColor = UITheme.TextTertiary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, 108)),
            Size = new Size(Width - UITheme.Scaled(this, UITheme.Spacing32), UITheme.Scaled(this, 18)),
        };

        _play = new FlatButton
        {
            Name = "QuizCardPlay",
            Text = _approved ? PlayGlyph + "  PLAY" : LockedGlyph + "  LOCKED",
            AccessibleName = _approved ? "Play " + quiz.Title : quiz.Title + " is pending review",
            Enabled = _approved,
            Variant = _approved ? UITheme.FlatButtonVariant.Primary : UITheme.FlatButtonVariant.Secondary,
            Location = new Point(Width - UITheme.Scaled(this, 130), Height - UITheme.Scaled(this, 46)),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Size = new Size(UITheme.Scaled(this, 114), UITheme.Scaled(this, 32)),
        };
        _play.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<QuizCard>() is { } card && card._approved)
            {
                card.PlayClicked?.Invoke(card, card._quizId);
            }
        };

        Controls.Add(_title);
        Controls.Add(_meta);
        Controls.Add(difficulty);
        Controls.Add(_status);
        Controls.Add(_play);
        Controls.Add(band);

        AccessibleName = _approved
            ? quiz.Title + ", " + quiz.Difficulty + ", " + questionCount + " questions"
            : quiz.Title + ", pending review";
    }

    /// <summary>The difficulty's fun color (green/amber/red — E/M/H).</summary>
    private static Color LevelColor(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => UITheme.LevelEasy,
        Difficulty.Medium => UITheme.LevelMedium,
        _ => UITheme.LevelHard,
    };
}
