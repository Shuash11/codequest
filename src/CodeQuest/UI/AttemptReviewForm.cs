// Shows the answers of a finished attempt.

using CodeQuest.Data.Repositories;
using CodeQuest.Models;
using System.Drawing;
using System.Windows.Forms;

namespace CodeQuest.UI;

public sealed class AttemptReviewForm : UserControl
{
    private const string NoExplanationText = "(No explanation was provided.)";
    private const string NotAnsweredText = "Not answered";
    private const string CorrectAnswerLabel = "Correct answer";
    private const string YourAnswerLabel = "Your answer";

    private readonly AttemptRepository _attempts;
    private readonly QuizRepository _quizzes;
    private readonly SubmittedAnswerRepository _answers;

    private readonly FlatButton _backButton = null!;
    private readonly Label _reviewTitle = null!;
    private readonly Label _scoreLine = null!;
    private readonly Panel _body = null!;
    private Quiz? _openQuiz;
    private IReadOnlyList<SubmittedAnswer> _openSubmitted = Array.Empty<SubmittedAnswer>();
    private int _lastBodyWidth;

    public event EventHandler? BackRequested;

    public AttemptReviewForm(
        AttemptRepository attempts,
        QuizRepository quizzes,
        SubmittedAnswerRepository answers)
    {
        ArgumentNullException.ThrowIfNull(attempts);
        ArgumentNullException.ThrowIfNull(quizzes);
        ArgumentNullException.ThrowIfNull(answers);
        _attempts = attempts;
        _quizzes = quizzes;
        _answers = answers;

        BackColor = UITheme.Background;
        Dock = DockStyle.Fill;

        var header = new Panel
        {
            Name = "ReviewHeader",
            Dock = DockStyle.Top,
            Height = UITheme.Scaled(this, 64),
            BackColor = UITheme.Background,
        };
        _backButton = new FlatButton
        {
            Name = "ReviewBackButton",
            Text = "Back",
            Variant = UITheme.FlatButtonVariant.Secondary,
            Location = new Point(0, 0),
            Size = new Size(UITheme.Scaled(this, 90), UITheme.Scaled(this, 32)),
        };
        _backButton.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<AttemptReviewForm>() is { } review)
            {
                review.BackRequested?.Invoke(review, EventArgs.Empty);
            }
        };
        _reviewTitle = new Label
        {
            Name = "ReviewQuizTitle",
            Font = UITheme.BodySemibold,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Background,
            AutoSize = false,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Location = new Point(UITheme.Scaled(this, 90) + UITheme.Scaled(this, UITheme.Spacing12), 0),
            Size = new Size(UITheme.Scaled(this, 400), UITheme.Scaled(this, 24)),
        };
        _scoreLine = new Label
        {
            Name = "ReviewScoreLine",
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Background,
            AutoSize = false,
            Location = new Point(UITheme.Scaled(this, 90) + UITheme.Scaled(this, UITheme.Spacing12), UITheme.Scaled(this, 28)),
            Size = new Size(UITheme.Scaled(this, 400), UITheme.Scaled(this, 18)),
        };
        header.Controls.Add(_backButton);
        header.Controls.Add(_reviewTitle);
        header.Controls.Add(_scoreLine);

        _body = new Panel
        {
            Name = "ReviewBody",
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = UITheme.Background,
        };

        Controls.Add(_body);
        Controls.Add(header);
    }

    public void Open(int attemptId)
    {
        QuizAttempt? attempt = _attempts.GetById(attemptId);
        if (attempt is null)
        {
            return;
        }

        Quiz? quiz = _quizzes.GetById(attempt.QuizId);
        if (quiz is null)
        {
            return;
        }

        IReadOnlyList<SubmittedAnswer> submitted = _answers.GetByAttempt(attemptId);

        int total = quiz.Questions.Count;
        double percent = total == 0
            ? 0d
            : Math.Round(attempt.Score * 100d / total, MidpointRounding.AwayFromZero);

        _reviewTitle.Text = quiz.Title;
        _scoreLine.Text = $"{attempt.Score}/{total} \u00B7 {percent:0}% \u00B7 {UiFormat.FormatDate(attempt.AttemptedAt)}";

        _openQuiz = quiz;
        _openSubmitted = submitted;
        BuildQuestionCards(quiz, submitted);
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);

        int width = _body.ClientSize.Width;
        if (_openQuiz is not null && width != _lastBodyWidth)
        {
            _lastBodyWidth = width;
            BuildQuestionCards(_openQuiz, _openSubmitted);
        }
    }

    private void BuildQuestionCards(Quiz quiz, IReadOnlyList<SubmittedAnswer> submitted)
    {
        _body.SuspendLayout();
        foreach (Control existing in _body.Controls)
        {
            existing.Dispose();
        }
        _body.Controls.Clear();

        int inset = UITheme.Scaled(this, UITheme.Spacing16);
        int cardWidth = Math.Max(_body.ClientSize.Width - inset * 2, 1);
        int top = 0;

        for (int i = 0; i < quiz.Questions.Count; i++)
        {
            Question question = quiz.Questions[i];
            SubmittedAnswer? chosen = submitted.FirstOrDefault(answer => answer.QuestionId == question.QuestionId);
            Choice? chosenChoice = chosen is null
                ? null
                : question.Choices.FirstOrDefault(choice => choice.ChoiceId == chosen.ChosenChoiceId);
            bool correct = question.IsCorrectChoice(chosenChoice);
            Choice? correctChoice = question.GetCorrectChoice();

            int cardHeight = MeasureCardHeight(question, cardWidth, correct);
            var card = new CardPanel
            {
                Name = $"ReviewCard{i}",
                Location = new Point(inset, top),
                Size = new Size(cardWidth, cardHeight),
            };
            FillQuestionCard(card, i, question, chosenChoice, correct, correctChoice);
            _body.Controls.Add(card);
            top += cardHeight + UITheme.Scaled(this, UITheme.Spacing12);
        }

        _body.ResumeLayout();
    }

    private int MeasureCardHeight(Question question, int cardWidth, bool correct)
    {
        int inset = UITheme.Scaled(this, UITheme.Spacing16);
        int contentWidth = Math.Max(cardWidth - inset * 2, 1);
        int textHeight = TextRenderer.MeasureText(
            question.QuestionText, UITheme.Body, new Size(contentWidth, int.MaxValue), TextFormatFlags.WordBreak).Height;

        int height = inset
            + UITheme.Scaled(this, 18)
            + UITheme.Scaled(this, UITheme.Spacing8)
            + textHeight
            + UITheme.Scaled(this, UITheme.Spacing12)
            + UITheme.Scaled(this, 22);

        if (!correct && question.GetCorrectChoice() is not null)
        {
            height += UITheme.Scaled(this, 22) + UITheme.Scaled(this, UITheme.Spacing4);
        }

        height += UITheme.Scaled(this, UITheme.Spacing12);
        string explanation = question.Explanation.Length == 0 ? NoExplanationText : question.Explanation;
        int explanationHeight = TextRenderer.MeasureText(
            explanation,
            UITheme.Body,
            new Size(Math.Max(contentWidth - UITheme.Scaled(this, UITheme.Spacing12) * 2, 1), int.MaxValue),
            TextFormatFlags.WordBreak).Height;
        height += explanationHeight + UITheme.Scaled(this, 40) + inset;

        return height;
    }

    private void FillQuestionCard(
        CardPanel card,
        int index,
        Question question,
        Choice? chosenChoice,
        bool correct,
        Choice? correctChoice)
    {
        int inset = UITheme.Scaled(this, UITheme.Spacing16);
        int contentWidth = Math.Max(card.Width - inset * 2, 1);

        var number = new Label
        {
            Name = $"ReviewQuestionNumber{index}",
            Text = $"Question {index + 1}",
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(inset, inset),
            Size = new Size(UITheme.Scaled(this, 90), UITheme.Scaled(this, 18)),
        };
        Color verdictColor = correct ? UITheme.SuccessText : UITheme.DangerText;
        var markGlyph = new Label
        {
            Name = $"ReviewMarkGlyph{index}",
            Text = correct ? "\uE73E" : "\uE711",
            Font = UITheme.NavGlyph,
            ForeColor = verdictColor,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(contentWidth - UITheme.Scaled(this, 110), inset),
            Size = new Size(UITheme.Scaled(this, 20), UITheme.Scaled(this, 18)),
            TextAlign = ContentAlignment.MiddleRight,
        };
        var markWord = new Label
        {
            Name = $"ReviewMarkWord{index}",
            Text = correct ? "Correct" : "Incorrect",
            Font = UITheme.Body,
            ForeColor = verdictColor,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(contentWidth - UITheme.Scaled(this, 88), inset),
            Size = new Size(UITheme.Scaled(this, 88), UITheme.Scaled(this, 18)),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        card.Controls.Add(number);
        card.Controls.Add(markGlyph);
        card.Controls.Add(markWord);

        int textHeight = TextRenderer.MeasureText(
            question.QuestionText, UITheme.Body, new Size(contentWidth, int.MaxValue), TextFormatFlags.WordBreak).Height;
        var questionText = new Label
        {
            Name = $"ReviewQuestionText{index}",
            Text = question.QuestionText,
            Font = UITheme.Body,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(inset, inset + UITheme.Scaled(this, 18) + UITheme.Scaled(this, UITheme.Spacing8)),
            Size = new Size(contentWidth, textHeight),
        };

        int top = inset + UITheme.Scaled(this, 18) + UITheme.Scaled(this, UITheme.Spacing8) + textHeight
            + UITheme.Scaled(this, UITheme.Spacing12);

        var chosenCaption = new Label
        {
            Name = $"ReviewChosenCaption{index}",
            Text = YourAnswerLabel,
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(inset, top),
            Size = new Size(UITheme.Scaled(this, 90), UITheme.Scaled(this, 22)),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        var chosenValue = new Label
        {
            Name = $"ReviewChosenValue{index}",
            Text = chosenChoice is null ? NotAnsweredText : chosenChoice.ChoiceText,
            Font = UITheme.Body,
            ForeColor = chosenChoice is null ? UITheme.TextTertiary : UITheme.TextPrimary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            AutoEllipsis = true,
            Location = new Point(inset + UITheme.Scaled(this, 90) + UITheme.Scaled(this, UITheme.Spacing8), top),
            Size = new Size(Math.Max(contentWidth - UITheme.Scaled(this, 90) - UITheme.Scaled(this, 40), 1), UITheme.Scaled(this, 22)),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        var chosenMark = new Label
        {
            Name = $"ReviewChosenMark{index}",
            Text = chosenChoice is null ? string.Empty : correct ? "\uE73E" : "\uE711",
            Font = UITheme.NavGlyph,
            ForeColor = correct ? UITheme.SuccessText : UITheme.DangerText,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(card.Width - inset - UITheme.Scaled(this, 28), top),
            Size = new Size(UITheme.Scaled(this, 28), UITheme.Scaled(this, 22)),
            TextAlign = ContentAlignment.MiddleRight,
        };
        card.Controls.Add(chosenCaption);
        card.Controls.Add(chosenValue);
        card.Controls.Add(chosenMark);
        top += UITheme.Scaled(this, 22);

        if (!correct && correctChoice is not null)
        {
            top += UITheme.Scaled(this, UITheme.Spacing4);
            var correctCaption = new Label
            {
                Name = $"ReviewCorrectCaption{index}",
                Text = CorrectAnswerLabel,
                Font = UITheme.Caption,
                ForeColor = UITheme.TextSecondary,
                BackColor = UITheme.Surface,
                AutoSize = false,
                Location = new Point(inset, top),
                Size = new Size(UITheme.Scaled(this, 110), UITheme.Scaled(this, 22)),
                TextAlign = ContentAlignment.MiddleLeft,
            };
            var correctValue = new Label
            {
                Name = $"ReviewCorrectValue{index}",
                Text = correctChoice.ChoiceText,
                Font = UITheme.Body,
                ForeColor = UITheme.SuccessText,
                BackColor = UITheme.Surface,
                AutoSize = false,
                AutoEllipsis = true,
                Location = new Point(inset + UITheme.Scaled(this, 110) + UITheme.Scaled(this, UITheme.Spacing8), top),
                Size = new Size(Math.Max(contentWidth - UITheme.Scaled(this, 110) - UITheme.Scaled(this, 40), 1), UITheme.Scaled(this, 22)),
                TextAlign = ContentAlignment.MiddleLeft,
            };
            var correctMark = new Label
            {
                Name = $"ReviewCorrectMark{index}",
                Text = "\uE73E",
                Font = UITheme.NavGlyph,
                ForeColor = UITheme.SuccessText,
                BackColor = UITheme.Surface,
                AutoSize = false,
                Location = new Point(card.Width - inset - UITheme.Scaled(this, 28), top),
                Size = new Size(UITheme.Scaled(this, 28), UITheme.Scaled(this, 22)),
                TextAlign = ContentAlignment.MiddleRight,
            };
            card.Controls.Add(correctCaption);
            card.Controls.Add(correctValue);
            card.Controls.Add(correctMark);
            top += UITheme.Scaled(this, 22);
        }

        top += UITheme.Scaled(this, UITheme.Spacing12);
        string explanation = question.Explanation.Length == 0 ? NoExplanationText : question.Explanation;
        int explanationHeight = TextRenderer.MeasureText(
            explanation,
            UITheme.Body,
            new Size(Math.Max(contentWidth - UITheme.Scaled(this, UITheme.Spacing12) * 2, 1), int.MaxValue),
            TextFormatFlags.WordBreak).Height;
        int boxHeight = explanationHeight + UITheme.Scaled(this, 40);

        var explanationBox = new Panel
        {
            Name = $"ReviewExplanation{index}",
            Location = new Point(inset, top),
            Size = new Size(contentWidth, boxHeight),
            BackColor = UITheme.Fill,
        };
        var explanationCaption = new Label
        {
            Name = $"ReviewExplanationCaption{index}",
            Text = "Explanation",
            Font = UITheme.CaptionSemibold,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Fill,
            AutoSize = false,
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing12), UITheme.Scaled(this, UITheme.Spacing12)),
            Size = new Size(Math.Max(contentWidth - UITheme.Scaled(this, UITheme.Spacing12) * 2, 1), UITheme.Scaled(this, 16)),
        };
        var explanationText = new Label
        {
            Name = $"ReviewExplanationText{index}",
            Text = explanation,
            Font = UITheme.Body,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Fill,
            AutoSize = false,
            Location = new Point(
                UITheme.Scaled(this, UITheme.Spacing12),
                UITheme.Scaled(this, UITheme.Spacing12) + UITheme.Scaled(this, 16)),
            Size = new Size(Math.Max(contentWidth - UITheme.Scaled(this, UITheme.Spacing12) * 2, 1), explanationHeight),
        };
        explanationBox.Controls.Add(explanationCaption);
        explanationBox.Controls.Add(explanationText);
        card.Controls.Add(explanationBox);
    }
}
