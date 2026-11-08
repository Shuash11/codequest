// =============================================================================
// CodeQuest — Gamification Foundation (plan WP1, acceptance A2/A3/A4/A5)
// The value shapes of the gamification feature plus the one lookup table of
// the XP rules. Nothing here touches the database or the UI: these objects
// are computed projections (aggregates over quiz_attempts) that the data
// layer produces and the screens of WP5 consume.
// Decisions:
//   * Every shape below is IMMUTABLE — values are set once through the
//     constructor and exposed as get-only properties (the QuizScoreResult
//     pattern of phase P3). Unlike UserAccount, these objects are never
//     written back to a table row, so a settable property would only invite
//     accidental mutation; immutability makes them safe to hand to the UI.
//   * DifficultyXpWeights is the SINGLE definition of the XP rule
//     "10 / 20 / 30 XP per correct answer" (plan FR2 / owner decision). Both
//     the level math (LevelService) and the XP derivation in the
//     GamificationRepository read it from here, so the rule lives in one
//     place only.
//   * AchievementContext composes a UserGamificationStats (plus the level)
//     instead of repeating its seven fields — the achievement rules read the
//     same numbers the scoreboard does, so one aggregate is carried as-is.
// =============================================================================

namespace CodeQuest.Models;

/// <summary>
/// The XP awarded per CORRECT answer, by difficulty of the quiz
/// (plan FR2, owner decision "XP per correct answer, weighted by difficulty").
/// This static lookup is the single definition of the rule; the level service
/// and the gamification repository both read it from here.
/// </summary>
public static class DifficultyXpWeights
{
    /// <summary>XP per correct answer in an <see cref="Difficulty.Easy"/> quiz.</summary>
    public const int Easy = 10;

    /// <summary>XP per correct answer in a <see cref="Difficulty.Medium"/> quiz.</summary>
    public const int Medium = 20;

    /// <summary>XP per correct answer in a <see cref="Difficulty.Hard"/> quiz.</summary>
    public const int Hard = 30;

    /// <summary>
    /// Returns the XP of one correct answer in a quiz of the given difficulty.
    /// </summary>
    /// <param name="difficulty">The difficulty of the quiz that was taken.</param>
    /// <returns>The XP weight: 10 (Easy), 20 (Medium) or 30 (Hard).</returns>
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

/// <summary>
/// One row of the scoreboard (plan FR3): a ranked account with the values the
/// table shows. Users without a single attempt appear as well — with 0 XP,
/// level 1 and 0 quizzes (acceptance A4 ranks ALL accounts).
/// </summary>
public sealed class LeaderboardEntry
{
    /// <summary>Creates one leaderboard row.</summary>
    /// <param name="userId">The ranked account (schema column <c>users.user_id</c>).</param>
    /// <param name="displayName">The name shown in the table (schema column <c>users.display_name</c>).</param>
    /// <param name="level">The level the account's total XP reaches.</param>
    /// <param name="totalXp">The XP derived from every attempt of the account.</param>
    /// <param name="quizzesTaken">How many quizzes the account has completed.</param>
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

    /// <summary>The ranked account (schema column <c>users.user_id</c>).</summary>
    public int UserId { get; }

    /// <summary>The name shown in the table (schema column <c>users.display_name</c>).</summary>
    public string DisplayName { get; }

    /// <summary>The level the account's total XP reaches.</summary>
    public int Level { get; }

    /// <summary>The XP derived from every attempt of the account.</summary>
    public long TotalXp { get; }

    /// <summary>How many quizzes the account has completed.</summary>
    public int QuizzesTaken { get; }
}

/// <summary>
/// The achievement-relevant aggregates of ONE user, derived from their attempts
/// (plan FR4). The achievement evaluator consumes this object; nothing of it is
/// stored — every value is derived the same way the scoreboard derives its own.
/// </summary>
public sealed class UserGamificationStats
{
    /// <summary>Creates the aggregate of one user.</summary>
    /// <param name="userId">The user the aggregates belong to.</param>
    /// <param name="quizzesTaken">How many quizzes the user has completed.</param>
    /// <param name="totalCorrectAnswers">How many questions the user has answered correctly in total.</param>
    /// <param name="hasPerfectRun">True when at least one attempt answered every question correctly (and answered at least one).</param>
    /// <param name="hasPerfectHardRun">True when at least one HARD attempt was perfect in that sense.</param>
    /// <param name="distinctTopicCount">How many different topics the user's attempts span.</param>
    /// <param name="hasBothCategories">True when the attempts span both Programming and Cybersecurity topics.</param>
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

    /// <summary>The user the aggregates belong to.</summary>
    public int UserId { get; }

    /// <summary>How many quizzes the user has completed.</summary>
    public int QuizzesTaken { get; }

    /// <summary>How many questions the user has answered correctly in total.</summary>
    public long TotalCorrectAnswers { get; }

    /// <summary>
    /// True when at least one attempt of the user answered every question of
    /// the quiz correctly. An attempt that answered no question at all does not
    /// count — there was nothing to be perfect about.
    /// </summary>
    public bool HasPerfectRun { get; }

    /// <summary>The same perfect-run rule, restricted to attempts on HARD quizzes.</summary>
    public bool HasPerfectHardRun { get; }

    /// <summary>How many different topics the user's attempts span.</summary>
    public int DistinctTopicCount { get; }

    /// <summary>True when the attempts span both the Programming and the Cybersecurity category.</summary>
    public bool HasBothCategories { get; }
}

/// <summary>
/// The level progress of one user (plan FR3): the personal summary the
/// scoreboard card and the sidebar chip show. The progress fraction is always
/// between 0 and 1 — the constructor clamps it, so the UI can trust the range.
/// </summary>
public sealed class UserProgress
{
    /// <summary>
    /// Creates the progress of one user. The progress fraction is derived here:
    /// the XP gathered in the current level divided by the size of the level's
    /// XP window, clamped to 0..1. A window of 0 or less XP means the top
    /// reachable level is complete — the fraction is then 1.
    /// </summary>
    /// <param name="userId">The user the progress belongs to.</param>
    /// <param name="totalXp">The user's total XP.</param>
    /// <param name="level">The level the total XP reaches.</param>
    /// <param name="xpIntoLevel">The XP gathered inside the current level (total XP minus the level's threshold).</param>
    /// <param name="xpForNextLevel">The XP window of the current level (next threshold minus this threshold).</param>
    /// <param name="quizzesTaken">How many quizzes the user has completed.</param>
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

    /// <summary>The user the progress belongs to.</summary>
    public int UserId { get; }

    /// <summary>The user's total XP.</summary>
    public long TotalXp { get; }

    /// <summary>The level the total XP reaches.</summary>
    public int Level { get; }

    /// <summary>The XP gathered inside the current level.</summary>
    public long XpIntoLevel { get; }

    /// <summary>The XP window of the current level (the XP that separates this level from the next).</summary>
    public long XpForNextLevel { get; }

    /// <summary>The progress through the current level, always between 0 and 1.</summary>
    public double ProgressFraction { get; }

    /// <summary>How many quizzes the user has completed.</summary>
    public int QuizzesTaken { get; }
}

/// <summary>
/// One entry of the achievement catalog (plan FR4): what the Achievements
/// screen shows. The catalog is defined in code
/// (<c>Services/AchievementEvaluator.cs</c>) — only the UNLOCK moments are
/// stored, in the <c>user_achievements</c> table.
/// </summary>
public sealed class AchievementDefinition
{
    /// <summary>Creates one catalog entry.</summary>
    /// <param name="key">The stable identifier stored in the database (never shown to the player).</param>
    /// <param name="title">The player-friendly name, for example "Flawless".</param>
    /// <param name="description">The player-friendly description of what unlocks the achievement.</param>
    /// <param name="glyph">The symbol shown on the achievement card.</param>
    public AchievementDefinition(string key, string title, string description, string glyph)
    {
        Key = RequireText(key, nameof(key));
        Title = RequireText(title, nameof(title));
        Description = RequireText(description, nameof(description));
        Glyph = RequireText(glyph, nameof(glyph));
    }

    /// <summary>The stable identifier stored in the database (for example <c>"perfect-run"</c>).</summary>
    public string Key { get; }

    /// <summary>The player-friendly name shown on the card.</summary>
    public string Title { get; }

    /// <summary>The player-friendly description of what unlocks the achievement.</summary>
    public string Description { get; }

    /// <summary>The symbol shown on the achievement card.</summary>
    public string Glyph { get; }

    /// <summary>Reads one required catalog text and refuses an empty one.</summary>
    private static string RequireText(string value, string parameterName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("The catalog text cannot be empty.", parameterName)
            : value;
}

/// <summary>
/// One achievement a user has unlocked (schema table <c>user_achievements</c>):
/// the catalog key plus the moment of the unlock.
/// </summary>
public sealed class UnlockedAchievement
{
    /// <summary>Creates one unlocked achievement.</summary>
    /// <param name="key">The catalog key of the achievement (schema column <c>achievement_key</c>).</param>
    /// <param name="unlockedAt">When the achievement was unlocked (schema column <c>unlocked_at</c>).</param>
    public UnlockedAchievement(string key, DateTimeOffset unlockedAt)
    {
        Key = string.IsNullOrWhiteSpace(key)
            ? throw new ArgumentException("The achievement key cannot be empty.", nameof(key))
            : key;
        UnlockedAt = unlockedAt;
    }

    /// <summary>The catalog key of the achievement (schema column <c>achievement_key</c>).</summary>
    public string Key { get; }

    /// <summary>When the achievement was unlocked (schema column <c>unlocked_at</c>).</summary>
    public DateTimeOffset UnlockedAt { get; }
}

/// <summary>
/// The input of the achievement evaluation (plan FR4): the user's aggregates
/// plus the level their total XP reaches. It composes
/// <see cref="UserGamificationStats"/> instead of repeating its fields — the
/// achievement rules read exactly the numbers the repository derives anyway.
/// </summary>
public sealed class AchievementContext
{
    /// <summary>Creates the evaluation input of one user.</summary>
    /// <param name="level">The level the user's total XP reaches (see LevelService.ComputeLevel).</param>
    /// <param name="stats">The user's gamification aggregates.</param>
    public AchievementContext(int level, UserGamificationStats stats)
    {
        if (level < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "The level cannot be below 1.");
        }

        Level = level;
        Stats = stats ?? throw new ArgumentNullException(nameof(stats));
    }

    /// <summary>The level the user's total XP reaches.</summary>
    public int Level { get; }

    /// <summary>The user's gamification aggregates.</summary>
    public UserGamificationStats Stats { get; }
}
