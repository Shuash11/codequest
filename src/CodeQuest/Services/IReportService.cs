// The report contract. (Abstraction)

using CodeQuest.Data.Repositories;
using CodeQuest.Models;

namespace CodeQuest.Services;

public sealed class TopicReport
{
    public TopicReport(
        int topicId,
        string topicName,
        int attempts,
        double averagePercent,
        double bestPercent,
        double latestPercent,
        bool needsPractice)
    {
        TopicId = topicId;
        TopicName = topicName;
        Attempts = attempts;
        AveragePercent = averagePercent;
        BestPercent = bestPercent;
        LatestPercent = latestPercent;
        NeedsPractice = needsPractice;
    }

    public int TopicId { get; }

    public string TopicName { get; }

    public int Attempts { get; }

    public double AveragePercent { get; }

    public double BestPercent { get; }

    public double LatestPercent { get; }

    public bool NeedsPractice { get; }
}

public sealed class DifficultyReport
{
    public DifficultyReport(Difficulty difficulty, int attempts, double averagePercent)
    {
        Difficulty = difficulty;
        Attempts = attempts;
        AveragePercent = averagePercent;
    }

    public Difficulty Difficulty { get; }

    public int Attempts { get; }

    public double AveragePercent { get; }
}

public sealed class OverallReport
{
    public OverallReport(int totalAttempts, int quizzesTaken, double averagePercent)
    {
        TotalAttempts = totalAttempts;
        QuizzesTaken = quizzesTaken;
        AveragePercent = averagePercent;
    }

    public int TotalAttempts { get; }

    public int QuizzesTaken { get; }

    public double AveragePercent { get; }
}

public sealed class PracticeReport
{
    public PracticeReport(
        OverallReport overall,
        IReadOnlyList<TopicReport> topics,
        IReadOnlyList<DifficultyReport> difficulties,
        IReadOnlyList<TopicReport> weakTopics)
    {
        Overall = overall;
        Topics = topics;
        Difficulties = difficulties;
        WeakTopics = weakTopics;
    }

    public OverallReport Overall { get; }

    public IReadOnlyList<TopicReport> Topics { get; }

    public IReadOnlyList<DifficultyReport> Difficulties { get; }

    public IReadOnlyList<TopicReport> WeakTopics { get; }
}

public interface IReportService
{
    PracticeReport Build(int userId, Difficulty? difficulty = null);
}
