// The scoring contract. (Abstraction)

using CodeQuest.Models;

namespace CodeQuest.Services;

public sealed class QuizScoreResult
{
    public int CorrectCount { get; }

    public int TotalQuestions { get; }

    public double Percentage { get; }

    public bool IsEmpty => TotalQuestions == 0;

    public string Summary => $"{CorrectCount}/{TotalQuestions} ({Percentage:0}%)";

    public QuizScoreResult(int correctCount, int totalQuestions)
    {
        if (correctCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(correctCount),
                correctCount,
                "The number of correct answers cannot be negative.");
        }

        if (totalQuestions < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalQuestions),
                totalQuestions,
                "The number of answered questions cannot be negative.");
        }

        if (correctCount > totalQuestions)
        {
            throw new ArgumentOutOfRangeException(
                nameof(correctCount),
                correctCount,
                "There cannot be more correct answers than answered questions.");
        }

        CorrectCount = correctCount;
        TotalQuestions = totalQuestions;
        Percentage = totalQuestions == 0
            ? 0d
            : Math.Round(correctCount * 100d / totalQuestions, MidpointRounding.AwayFromZero);
    }
}

public interface IScoringService
{
    QuizScoreResult Score(Quiz quiz, IEnumerable<SubmittedAnswer> submittedAnswers);
}
