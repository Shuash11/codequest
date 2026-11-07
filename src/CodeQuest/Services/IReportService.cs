// =============================================================================
// CodeQuest — Phase I3: Report Data Services
// OOP rubric: ABSTRACTION — IReportService is the contract the reports screen
//              depends on; the concrete ReportService composes the repository
//              layer and computes the aggregates.
// Look at: Build() — the one operation that produces the whole performance
//          report: per-topic averages (average, best, latest), per-difficulty
//          performance, the topics that need more practice, and the overall
//          summary. Nothing is stored (R-2 #1: reports are derived data —
//          the score denominator is the quiz's own question count).
// Note:    the aggregation is plain C# over repository reads (all reads go
//          through the parameterized data layer — no SQL is written here).
//          A local SQLite with a handful of attempts makes in-memory
//          aggregation the simplest explainable choice.
// =============================================================================

using CodeQuest.Data.Repositories;
using CodeQuest.Models;

namespace CodeQuest.Services;

/// <summary>One topic's performance row of the report.</summary>
public sealed class TopicReport
{
    /// <summary>Creates one topic report row.</summary>
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

    /// <summary>The topic id (a filter handle for the UI).</summary>
    public int TopicId { get; }

    /// <summary>The topic's display name.</summary>
    public string TopicName { get; }

    /// <summary>How many attempts this topic has (zero is a defined empty row).</summary>
    public int Attempts { get; }

    /// <summary>The average percentage over the attempts (0 for zero attempts).</summary>
    public double AveragePercent { get; }

    /// <summary>The best single attempt percentage (0 for zero attempts).</summary>
    public double BestPercent { get; }

    /// <summary>The most recent attempt's percentage (0 for zero attempts).</summary>
    public double LatestPercent { get; }

    /// <summary>True when the average sits below the practice threshold (never for zero attempts).</summary>
    public bool NeedsPractice { get; }
}

/// <summary>One difficulty's aggregate performance of the report.</summary>
public sealed class DifficultyReport
{
    /// <summary>Creates one difficulty report row.</summary>
    public DifficultyReport(Difficulty difficulty, int attempts, double averagePercent)
    {
        Difficulty = difficulty;
        Attempts = attempts;
        AveragePercent = averagePercent;
    }

    /// <summary>The difficulty this row aggregates.</summary>
    public Difficulty Difficulty { get; }

    /// <summary>How many attempts this difficulty has (zero is a defined empty row).</summary>
    public int Attempts { get; }

    /// <summary>The average percentage over the attempts (0 for zero attempts).</summary>
    public double AveragePercent { get; }
}

/// <summary>The overall summary numbers of the report.</summary>
public sealed class OverallReport
{
    /// <summary>Creates the overall summary.</summary>
    public OverallReport(int totalAttempts, int quizzesTaken, double averagePercent)
    {
        TotalAttempts = totalAttempts;
        QuizzesTaken = quizzesTaken;
        AveragePercent = averagePercent;
    }

    /// <summary>All completed attempts of the user.</summary>
    public int TotalAttempts { get; }

    /// <summary>How many distinct quizzes those attempts touched.</summary>
    public int QuizzesTaken { get; }

    /// <summary>The average percentage over every answered attempt (0 with no attempts).</summary>
    public double AveragePercent { get; }
}

/// <summary>The whole performance report — the reports screen's data.</summary>
public sealed class PracticeReport
{
    /// <summary>Creates the report.</summary>
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

    /// <summary>The overall summary (totals + average).</summary>
    public OverallReport Overall { get; }

    /// <summary>One row per topic, name-ordered.</summary>
    public IReadOnlyList<TopicReport> Topics { get; }

    /// <summary>One row per difficulty, declaration-ordered.</summary>
    public IReadOnlyList<DifficultyReport> Difficulties { get; }

    /// <summary>The topics below the practice threshold, weakest first.</summary>
    public IReadOnlyList<TopicReport> WeakTopics { get; }
}

/// <summary>
/// Builds the performance report of one user. The screen depends on this
/// interface only, so the aggregation can change without touching a form.
/// </summary>
public interface IReportService
{
    /// <summary>
    /// Builds the report over the user's attempts. An optional difficulty
    /// narrows the data to quizzes of that level (null = everything).
    /// </summary>
    PracticeReport Build(int userId, Difficulty? difficulty = null);
}
