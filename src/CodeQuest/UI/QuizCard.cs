// The quiz card on the home screen.

using CodeQuest.Models;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CodeQuest.UI;

public sealed class QuizCard : Panel
{
    private const string PlayGlyph = "\u25B6";
    private const string LockedGlyph = "\uE72E";

    private readonly Label _title = new();
    private readonly Label _meta = new();
    private readonly Label _chip = new();
    private readonly FlatButton _play = new();
    private readonly int _quizId;
    private readonly bool _approved;

    public event EventHandler<int>? PlayClicked;

    public QuizCard(Quiz quiz, string topicName, int questionCount)
    {
        _quizId = quiz.QuizId;
        _approved = quiz.Status == QuizStatus.Approved;

        DoubleBuffered = true;
        Size = new Size(UITheme.Scaled(this, 330), UITheme.Scaled(this, 150));
        Margin = new Padding(0, 0, UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, UITheme.Spacing16));
        BackColor = UITheme.Surface;

        Paint += static (sender, e) =>
        {
            if (sender is Panel panel)
            {
                using var pen = new Pen(UITheme.BorderHairline);
                using GraphicsPath path = UITheme.RoundedPath(
                    new Rectangle(0, 0, panel.Width - 1, panel.Height - 1), UITheme.Scaled(panel, 14));
                e.Graphics.DrawPath(pen, path);
            }
        };

        (string chipText, Color chipTextColor, Color chipBack) = _approved
            ? (quiz.Difficulty.ToString(), LevelTextColor(quiz.Difficulty), LevelTint(quiz.Difficulty))
            : ("Pending review", UITheme.TextTertiary, UITheme.Fill);

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
            Size = new Size(Width - UITheme.Scaled(this, UITheme.Spacing32), UITheme.Scaled(this, 24)),
        };

        _meta = new Label
        {
            Name = "QuizCardMeta",
            Text = $"{topicName}  \u00B7  {questionCount} questions",
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, UITheme.Spacing16) + UITheme.Scaled(this, 28)),
            Size = new Size(Width - UITheme.Scaled(this, UITheme.Spacing32), UITheme.Scaled(this, 18)),
        };

        _chip = new Label
        {
            Name = "QuizCardChip",
            Text = chipText,
            Font = UITheme.CaptionSemibold,
            ForeColor = chipTextColor,
            BackColor = chipBack,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, 56)),
            Size = new Size(UITheme.Scaled(this, 110), UITheme.Scaled(this, 22)),
        };

        _play = new FlatButton
        {
            Name = "QuizCardPlay",
            Text = _approved ? PlayGlyph + "  PLAY" : LockedGlyph + "  LOCKED",
            AccessibleName = _approved ? "Play " + quiz.Title : quiz.Title + " is pending review",
            Enabled = _approved,
            Variant = _approved ? UITheme.FlatButtonVariant.Primary : UITheme.FlatButtonVariant.Secondary,
            Location = new Point(Width - UITheme.Scaled(this, 124), Height - UITheme.Scaled(this, 46)),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Size = new Size(UITheme.Scaled(this, 108), UITheme.Scaled(this, 32)),
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
        Controls.Add(_chip);
        Controls.Add(_play);

        AccessibleName = _approved
            ? quiz.Title + ", " + quiz.Difficulty + ", " + questionCount + " questions"
            : quiz.Title + ", pending review";
    }

    private static Color LevelTextColor(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => UITheme.LevelEasy,
        Difficulty.Medium => UITheme.LevelMedium,
        Difficulty.Insane => UITheme.InsaneText,
        _ => UITheme.LevelHard,
    };

    private static Color LevelTint(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => UITheme.SuccessTint,
        Difficulty.Medium => UITheme.WarningTint,
        Difficulty.Insane => UITheme.InsaneTint,
        _ => UITheme.DangerTint,
    };
}
