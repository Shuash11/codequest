// =============================================================================
// CodeQuest — Phase I3: Report Data Services
// OOP rubric: ABSTRACTION + ENCAPSULATION — the concrete IReportService.
// Look at: Build() — it reads the user's attempts through the data layer,
//          groups them by the quiz's topic (or difficulty) and computes the
//          aggregates with one shared rule: the percentage of an attempt is
//          score ÷ (the quiz's own question count), rounded half away from
//          zero — the same math QuizScoreResult uses (R-2 #1).
// Note:    a topic with zero attempts gets the defined empty row (zeros, no
//          practice flag) — the UI renders "—", never an exception (P11
//          acceptance). The practice threshold is a named constant so the
//          instructor can ask exactly when a topic "needs practice".
// =============================================================================

using CodeQuest.Data;
using CodeQuest.Data.Repositories;
using CodeQuest.Models;

namespace CodeQuest.Services;

/// <summary>
/// The report builder behind <see cref="IReportService"/>: aggregates the
/// user's attempts per topic, per difficulty, and overall.
/// </summary>
public sealed class ReportService : IReportService
{
    /// <summary>
    /// A topic whose average sits below this percentage is flagged "needs
    /// practice" (the report's only judgement — everything else is a number).
    /// </summary>
    public const double PracticeThresholdPercent = 70d;

    private readonly IAttemptRepository _attempts;
    private readonly IQuizRepository _quizzes;
    private readonly ITopicRepository _topics;

    /// <summary>Creates the report builder over the repositories it reads.</summary>
    public ReportService(IAttemptRepository attempts, IQuizRepository quizzes, ITopicRepository topics)
    {
        ArgumentNullException.ThrowIfNull(attempts);
        ArgumentNullException.ThrowIfNull(quizzes);
        ArgumentNullException.ThrowIfNull(topics);
        _attempts = attempts;
        _quizzes = quizzes;
        _topics = topics;
    }

    /// <inheritdoc />
    public PracticeReport Build(int userId, Difficulty? difficulty = null)
    {
        // The topic names first (one read), so every row carries the display name.
        Dictionary<int, string> topicNames = new();
        foreach (Topic topic in _topics.GetAll())
        {
            topicNames[topic.TopicId] = topic.Name;
        }

        // The user's attempts, each joined to its quiz (the quiz carries the
        // topic and the question count that is the score's denominator).
        List<(QuizAttempt Attempt, Quiz Quiz)> rows = new();
        foreach (QuizAttempt attempt in _attempts.GetByUser(userId))
        {
            Quiz? quiz = _quizzes.GetById(attempt.QuizId);
            if (quiz is null)
            {
                continue; // unreachable — the FK keeps a quiz alive while attempts exist
            }

            if (difficulty is { } wanted && quiz.Difficulty != wanted)
            {
                continue; // the optional difficulty filter narrows the data
            }

            rows.Add((attempt, quiz));
        }

        List<TopicReport> topicReports = BuildTopicReports(rows, topicNames);
        List<DifficultyReport> difficultyReports = BuildDifficultyReports(rows);
        OverallReport overall = BuildOverall(rows);

        // The weak topics: averages below the threshold, weakest first, ties
        // broken by name so the order is deterministic for the tests.
        List<TopicReport> weak = topicReports
            .Where(report => report.NeedsPractice)
            .OrderBy(report => report.AveragePercent)
            .ThenBy(report => report.TopicName, StringComparer.Ordinal)
            .ToList();

        return new PracticeReport(overall, topicReports, difficultyReports, weak);
    }

    /// <summary>One row per topic, name-ordered; zero attempts is a defined empty row.</summary>
    private List<TopicReport> BuildTopicReports(
        List<(QuizAttempt Attempt, Quiz Quiz)> rows,
        Dictionary<int, string> topicNames)
    {
        var reports = new List<TopicReport>();

        foreach ((int topicId, string name) in topicNames.OrderBy(pair => pair.Value, StringComparer.Ordinal))
        {
            List<(QuizAttempt Attempt, Quiz Quiz)> topicRows = rows
                .Where(row => row.Quiz.TopicId == topicId)
                .ToList();

            if (topicRows.Count == 0)
            {
                // The defined empty row: zeros, no flag — the UI shows "—".
                reports.Add(new TopicReport(topicId, name, 0, 0d, 0d, 0d, NeedsPractice: false));
                continue;
            }

            List<double> percentages = topicRows.Select(row => AttemptPercent(row)).ToList();
            double average = Math.Round(percentages.Average(), MidpointRounding.AwayFromZero);
            double best = percentages.Max();
            (QuizAttempt LatestAttempt, Quiz LatestQuiz) newest =
                topicRows.OrderByDescending(row => row.Attempt.AttemptedAt).First();
            double latest = AttemptPercent((newest.LatestAttempt, newest.LatestQuiz));

            reports.Add(new TopicReport(
                topicId,
                name,
                topicRows.Count,
                average,
                best,
                latest,
                needsPractice: average < PracticeThresholdPercent));
        }

        return reports;
    }

    /// <summary>One row per difficulty in declaration order; zero attempts is a defined empty row.</summary>
    private List<DifficultyReport> BuildDifficultyReports(
        List<(QuizAttempt Attempt, Quiz Quiz)> rows)
    {
        var reports = new List<DifficultyReport>();
        var difficulties = new[] { Difficulty.Easy, Difficulty.Medium, Difficulty.Hard };

        foreach (Difficulty difficulty in difficulties)
        {
            List<double> percentages = rows
                .Where(row => row.Quiz.Difficulty == difficulty)
                .Select(row => AttemptPercent(row))
                .ToList();

            reports.Add(new DifficultyReport(
                difficulty,
                percentages.Count,
                percentages.Count == 0 ? 0d : Math.Round(percentages.Average(), MidpointRounding.AwayFromZero)));
        }

        return reports;
    }

    /// <summary>The overall summary: totals and the average over every attempt.</summary>
    private OverallReport BuildOverall(List<(QuizAttempt Attempt, Quiz Quiz)> rows)
    {
        if (rows.Count == 0)
        {
            return new OverallReport(0, 0, 0d); // zeros are never presented as data — the UI swaps in an empty state
        }

        double average = Math.Round(rows.Average(row => AttemptPercent(row)), MidpointRounding.AwayFromZero);
        int quizzesTaken = rows.Select(row => row.Quiz.QuizId).Distinct().Count();

        return new OverallReport(rows.Count, quizzesTaken, average);
    }

    /// <summary>
    /// The percentage of one attempt: score ÷ the quiz's own question count
    /// (the R-2 denominator), rounded half away from zero; zero when the quiz
    /// has no questions (a defined empty result, never a division by zero).
    /// </summary>
    private static double AttemptPercent((QuizAttempt Attempt, Quiz Quiz) row)
    {
        int total = row.Quiz.Questions.Count;
        return total == 0 ? 0d : Math.Round(row.Attempt.Score * 100d / total, MidpointRounding.AwayFromZero);
    }
}
