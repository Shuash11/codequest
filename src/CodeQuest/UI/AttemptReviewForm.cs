// =============================================================================
// CodeQuest — Quiz History & Answer Review (P10): the attempt review of spec
// 5.9, as a drill-in panel shared by its two hosts (the History module and the
// quiz-taking result's "View answers"). Read-only by design.
// Layout (§5.9): the header row — "Back" secondary (leading), the quiz title,
// and the score line "8/10 · 80% · Nov 4, 2026" (Caption, TextSecondary) —
// then one card per question:
//   1. top line: "Question N" (Caption, TextSecondary) + the mark ("Correct"
//      SuccessText / "Incorrect" DangerText — glyph + word, never color alone);
//   2. the question text (Body);
//   3. the answer rows — the chosen choice labeled "Your answer" (check in
//      SuccessText when it was the correct choice, cross in DangerText when
//      not); on incorrect or unanswered questions the correct choice row
//      additionally carries the label "Correct answer" with the check, so
//      chosen vs correct are always both visible (P10 acceptance);
//   4. the explanation in a quiet inset box (Fill fill, 8px radius, 12px
//      padding, "Explanation" CaptionSemibold) — always rendered; the empty
//      string reads "(No explanation was provided.)" (R-2 #6).
// Derived data (R-2 #1): correctness is computed live from the submitted
// answers and the quiz's answer key — nothing is read from a stored column.
// The score denominator is the quiz's own question count, so the display
// stays honest even when a question was never answered (shown as
// "Not answered"). The screen is fully local — the §5.8 offline note applies.
// =============================================================================

using CodeQuest.Data.Repositories;
using CodeQuest.Models;
using System.Drawing;
using System.Windows.Forms;

namespace CodeQuest.UI;

/// <summary>
/// The attempt review of spec 5.9: one drill-in panel, hosted by the History
/// module and by the quiz-taking result.
/// </summary>
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
    private Quiz? _openQuiz; // the attempt being reviewed — rebuilt on resize (pre-layout widths)
    private IReadOnlyList<SubmittedAnswer> _openSubmitted = Array.Empty<SubmittedAnswer>();

    /// <summary>Raised when the panel's "Back" is pressed; the host restores its list state.</summary>
    public event EventHandler? BackRequested;

    /// <summary>Creates the review panel over the attempt data.</summary>
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

    /// <summary>Loads one attempt and renders its review (§5.9).</summary>
    public void Open(int attemptId)
    {
        QuizAttempt? attempt = _attempts.GetById(attemptId);
        if (attempt is null)
        {
            return; // the host only opens existing attempts; a defensive guard
        }

        Quiz? quiz = _quizzes.GetById(attempt.QuizId);
        if (quiz is null)
        {
            // Unreachable while the schema blocks quiz deletion when attempts
            // exist (the FK keeps the quiz alive); a defensive guard anyway.
            return;
        }

        IReadOnlyList<SubmittedAnswer> submitted = _answers.GetByAttempt(attemptId);

        int total = quiz.Questions.Count; // the R-2 denominator: the quiz's own question count
        double percent = total == 0
            ? 0d
            : Math.Round(attempt.Score * 100d / total, MidpointRounding.AwayFromZero);

        _reviewTitle.Text = quiz.Title;
        _scoreLine.Text = $"{attempt.Score}/{total} \u00B7 {percent:0}% \u00B7 {UiFormat.FormatDate(attempt.AttemptedAt)}";

        _openQuiz = quiz;
        _openSubmitted = submitted;
        BuildQuestionCards(quiz, submitted);
    }

    /// <summary>
    /// Rebuilds the question cards when the panel's size changes: the panel
    /// may be laid out (Dock = Fill) only after it becomes visible, so the
    /// card widths from the moment of Open() are not final.
    /// </summary>
    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);

        if (_openQuiz is not null)
        {
            BuildQuestionCards(_openQuiz, _openSubmitted);
        }
    }

    /// <summary>Builds one §5.9 card per question, stacked in the scrolling body.</summary>
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

    /// <summary>Computes one card's height from its measured text blocks.</summary>
    private int MeasureCardHeight(Question question, int cardWidth, bool correct)
    {
        int inset = UITheme.Scaled(this, UITheme.Spacing16);
        int contentWidth = Math.Max(cardWidth - inset * 2, 1);
        int textHeight = TextRenderer.MeasureText(
            question.QuestionText, UITheme.Body, new Size(contentWidth, int.MaxValue)).Height;

        int height = inset
            + UITheme.Scaled(this, 18) // the "Question N" top line
            + UITheme.Scaled(this, UITheme.Spacing8)
            + textHeight
            + UITheme.Scaled(this, UITheme.Spacing12)
            + UITheme.Scaled(this, 22); // the chosen-answer row

        // The correct-answer row rides along on incorrect questions (§5.9).
        if (!correct && question.GetCorrectChoice() is not null)
        {
            height += UITheme.Scaled(this, 22) + UITheme.Scaled(this, UITheme.Spacing4);
        }

        height += UITheme.Scaled(this, UITheme.Spacing12);
        string explanation = question.Explanation.Length == 0 ? NoExplanationText : question.Explanation;
        int explanationHeight = TextRenderer.MeasureText(
            explanation, UITheme.Body, new Size(Math.Max(contentWidth - UITheme.Scaled(this, UITheme.Spacing12) * 2, 1), int.MaxValue)).Height;
        height += explanationHeight + UITheme.Scaled(this, UITheme.Spacing12) * 2 + inset;

        return height;
    }

    /// <summary>Fills one question card per the §5.9 block order.</summary>
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
            question.QuestionText, UITheme.Body, new Size(contentWidth, int.MaxValue)).Height;
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

        // The chosen answer (§5.9 item 3): always labeled; "Not answered" is
        // the honest rendering when the question carries no submitted answer.
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

        // The correct answer row rides along on incorrect or unanswered
        // questions, so chosen vs correct are always both visible (§5.9).
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

        // The explanation inset (§5.9 item 4) — always rendered (R-2 #6).
        top += UITheme.Scaled(this, UITheme.Spacing12);
        string explanation = question.Explanation.Length == 0 ? NoExplanationText : question.Explanation;
        int explanationHeight = TextRenderer.MeasureText(
            explanation,
            UITheme.Body,
            new Size(Math.Max(contentWidth - UITheme.Scaled(this, UITheme.Spacing12) * 2, 1), int.MaxValue)).Height;
        int boxHeight = explanationHeight + UITheme.Scaled(this, UITheme.Spacing12) * 2;

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
