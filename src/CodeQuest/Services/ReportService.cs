// Builds the player's reports. (Polymorphism)

using CodeQuest.Data;
using CodeQuest.Data.Repositories;
using CodeQuest.Models;

namespace CodeQuest.Services;

public sealed class ReportService : IReportService
{
    public const double PracticeThresholdPercent = 70d;

    private readonly IAttemptRepository _attempts;
    private readonly IQuizRepository _quizzes;
    private readonly ITopicRepository _topics;

    public ReportService(IAttemptRepository attempts, IQuizRepository quizzes, ITopicRepository topics)
    {
        ArgumentNullException.ThrowIfNull(attempts);
        ArgumentNullException.ThrowIfNull(quizzes);
        ArgumentNullException.ThrowIfNull(topics);
        _attempts = attempts;
        _quizzes = quizzes;
        _topics = topics;
    }

    public PracticeReport Build(int userId, Difficulty? difficulty = null)
    {
        Dictionary<int, string> topicNames = new();
        foreach (Topic topic in _topics.GetAll())
        {
            topicNames[topic.TopicId] = topic.Name;
        }

        List<(QuizAttempt Attempt, Quiz Quiz)> rows = new();
        foreach (QuizAttempt attempt in _attempts.GetByUser(userId))
        {
            Quiz? quiz = _quizzes.GetById(attempt.QuizId);
            if (quiz is null)
            {
                continue;
            }

            if (difficulty is { } wanted && quiz.Difficulty != wanted)
            {
                continue;
            }

            rows.Add((attempt, quiz));
        }

        List<TopicReport> topicReports = BuildTopicReports(rows, topicNames);
        List<DifficultyReport> difficultyReports = BuildDifficultyReports(rows);
        OverallReport overall = BuildOverall(rows);

        List<TopicReport> weak = topicReports
            .Where(report => report.NeedsPractice)
            .OrderBy(report => report.AveragePercent)
            .ThenBy(report => report.TopicName, StringComparer.Ordinal)
            .ToList();

        return new PracticeReport(overall, topicReports, difficultyReports, weak);
    }

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
                reports.Add(new TopicReport(topicId, name, 0, 0d, 0d, 0d, needsPractice: false));
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

    private OverallReport BuildOverall(List<(QuizAttempt Attempt, Quiz Quiz)> rows)
    {
        if (rows.Count == 0)
        {
            return new OverallReport(0, 0, 0d);
        }

        double average = Math.Round(rows.Average(row => AttemptPercent(row)), MidpointRounding.AwayFromZero);
        int quizzesTaken = rows.Select(row => row.Quiz.QuizId).Distinct().Count();

        return new OverallReport(rows.Count, quizzesTaken, average);
    }

    private static double AttemptPercent((QuizAttempt Attempt, Quiz Quiz) row)
    {
        int total = row.Quiz.Questions.Count;
        return total == 0 ? 0d : Math.Round(row.Attempt.Score * 100d / total, MidpointRounding.AwayFromZero);
    }
}
