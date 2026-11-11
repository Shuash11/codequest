// Level, XP and achievement records.

namespace CodeQuest.Models;

public static class DifficultyXpWeights
{
    public const int Easy = 10;

    public const int Medium = 20;

    public const int Hard = 30;

    public static int For(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => Easy,
        Difficulty.Medium => Medium,
        Difficulty.Hard => Hard,
        _ => throw new ArgumentOutOfRangeException(
            nameof(difficulty),
            difficulty,
            "The difficulty is not one of the known difficulties."),
    };
}

public sealed class LeaderboardEntry
{
    public LeaderboardEntry(int userId, string displayName, int level, long totalXp, int quizzesTaken)
    {
        if (level < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "The level cannot be below 1.");
        }

        if (totalXp < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalXp), totalXp, "The total XP cannot be negative.");
        }

        if (quizzesTaken < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quizzesTaken), quizzesTaken, "The quiz count cannot be negative.");
        }

        UserId = userId;
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Level = level;
        TotalXp = totalXp;
        QuizzesTaken = quizzesTaken;
    }

    public int UserId { get; }

    public string DisplayName { get; }

    public int Level { get; }

    public long TotalXp { get; }

    public int QuizzesTaken { get; }
}

public sealed class UserGamificationStats
{
    public UserGamificationStats(
        int userId,
        int quizzesTaken,
        long totalCorrectAnswers,
        bool hasPerfectRun,
        bool hasPerfectHardRun,
        int distinctTopicCount,
        bool hasBothCategories)
    {
        if (quizzesTaken < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quizzesTaken), quizzesTaken, "The quiz count cannot be negative.");
        }

        if (totalCorrectAnswers < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCorrectAnswers), totalCorrectAnswers, "The correct-answer count cannot be negative.");
        }

        if (distinctTopicCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(distinctTopicCount), distinctTopicCount, "The topic count cannot be negative.");
        }

        UserId = userId;
        QuizzesTaken = quizzesTaken;
        TotalCorrectAnswers = totalCorrectAnswers;
        HasPerfectRun = hasPerfectRun;
        HasPerfectHardRun = hasPerfectHardRun;
        DistinctTopicCount = distinctTopicCount;
        HasBothCategories = hasBothCategories;
    }

    public int UserId { get; }

    public int QuizzesTaken { get; }

    public long TotalCorrectAnswers { get; }

    public bool HasPerfectRun { get; }

    public bool HasPerfectHardRun { get; }

    public int DistinctTopicCount { get; }

    public bool HasBothCategories { get; }
}

public sealed class UserProgress
{
    public UserProgress(
        int userId,
        long totalXp,
        int level,
        long xpIntoLevel,
        long xpForNextLevel,
        int quizzesTaken)
    {
        if (level < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "The level cannot be below 1.");
        }

        if (totalXp < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalXp), totalXp, "The total XP cannot be negative.");
        }

        if (xpIntoLevel < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(xpIntoLevel), xpIntoLevel, "The XP inside the level cannot be negative.");
        }

        if (xpForNextLevel < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(xpForNextLevel), xpForNextLevel, "The XP window cannot be negative.");
        }

        if (quizzesTaken < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quizzesTaken), quizzesTaken, "The quiz count cannot be negative.");
        }

        UserId = userId;
        TotalXp = totalXp;
        Level = level;
        XpIntoLevel = xpIntoLevel;
        XpForNextLevel = xpForNextLevel;
        QuizzesTaken = quizzesTaken;
        ProgressFraction = xpForNextLevel <= 0
            ? 1d
            : Math.Clamp((double)xpIntoLevel / xpForNextLevel, 0d, 1d);
    }

    public int UserId { get; }

    public long TotalXp { get; }

    public int Level { get; }

    public long XpIntoLevel { get; }

    public long XpForNextLevel { get; }

    public double ProgressFraction { get; }

    public int QuizzesTaken { get; }
}

public sealed class AchievementDefinition
{
    public AchievementDefinition(string key, string title, string description, string glyph)
    {
        Key = RequireText(key, nameof(key));
        Title = RequireText(title, nameof(title));
        Description = RequireText(description, nameof(description));
        Glyph = RequireText(glyph, nameof(glyph));
    }

    public string Key { get; }

    public string Title { get; }

    public string Description { get; }

    public string Glyph { get; }

    private static string RequireText(string value, string parameterName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("The catalog text cannot be empty.", parameterName)
            : value;
}

public sealed class UnlockedAchievement
{
    public UnlockedAchievement(string key, DateTimeOffset unlockedAt)
    {
        Key = string.IsNullOrWhiteSpace(key)
            ? throw new ArgumentException("The achievement key cannot be empty.", nameof(key))
            : key;
        UnlockedAt = unlockedAt;
    }

    public string Key { get; }

    public DateTimeOffset UnlockedAt { get; }
}

public sealed class AchievementContext
{
    public AchievementContext(int level, UserGamificationStats stats)
    {
        if (level < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "The level cannot be below 1.");
        }

        Level = level;
        Stats = stats ?? throw new ArgumentNullException(nameof(stats));
    }

    public int Level { get; }

    public UserGamificationStats Stats { get; }
}
