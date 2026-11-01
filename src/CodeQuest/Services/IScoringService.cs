// =============================================================================
// CodeQuest — Phase P3: Core OOP Domain Layer
// OOP rubric: ABSTRACTION — the contract of the scoring business logic.
// Look at: IScoringService below. It states the one operation every scorer has
//          to offer, and QuizScoreResult, the value that operation returns.
//          The UI (P9) works against this interface only, so the way the score
//          is computed can change without touching a single form.
// Note:    the score is NOT stored with the attempt (plan ruling R-2 #1) — it is
//          derived from the submitted answers and the answer key of the questions.
// =============================================================================

using CodeQuest.Models;

namespace CodeQuest.Services;

/// <summary>
/// The result of scoring one attempt: how many answers were right, how many
/// questions were answered, and the percentage that follows from those two.
/// </summary>
public sealed class QuizScoreResult
{
    /// <summary>How many answered questions were right (the "correct" part of correct/total).</summary>
    public int CorrectCount { get; }

    /// <summary>How many questions were answered (the "total" part of correct/total).</summary>
    public int TotalQuestions { get; }

    /// <summary>
    /// The percentage, rounded to a whole number (half away from zero). An attempt
    /// without a single answer has the percentage 0 — there is nothing to divide.
    /// </summary>
    public double Percentage { get; }

    /// <summary>True when the attempt contains no answered question at all.</summary>
    public bool IsEmpty => TotalQuestions == 0;

    /// <summary>The short result line shown in the UI, for example <c>3/5 (60%)</c>.</summary>
    public string Summary => $"{CorrectCount}/{TotalQuestions} ({Percentage:0}%)";

    /// <summary>Creates a score result.</summary>
    /// <param name="correctCount">How many answered questions were right.</param>
    /// <param name="totalQuestions">How many questions were answered.</param>
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

/// <summary>
/// Scores a finished attempt. The implementation lives in
/// <see cref="ScoringService"/>; the UI (P9) depends on this interface only.
/// </summary>
public interface IScoringService
{
    /// <summary>
    /// Scores the given answers against the answer key of the given quiz.
    /// The score is derived, never read from a stored column.
    /// </summary>
    /// <param name="quiz">The quiz that was taken, including its questions and their options.</param>
    /// <param name="submittedAnswers">The answers the student picked.</param>
    QuizScoreResult Score(Quiz quiz, IEnumerable<SubmittedAnswer> submittedAnswers);
}
