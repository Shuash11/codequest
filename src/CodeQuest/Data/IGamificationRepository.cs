// =============================================================================
// CodeQuest — Gamification Foundation (plan WP1, acceptance A3/A4/A5)
// OOP rubric: ABSTRACTION — the contract of the gamification data access.
// Look at: IGamificationRepository below. It describes what the gamification
//          feature needs from the database: the per-user aggregates (stats and
//          total XP), the scoreboard rows, the level progress, and the
//          achievement unlocks (read + add-if-new). The concrete ADO.NET
//          implementation is written against exactly this interface, following
//          the repository conventions of phase P4 (every connection comes from
//          Db, every SQL failure is mapped through DataAccessException).
// Note:    XP is DERIVED from the attempts — no XP column exists (plan A3);
//          the only table this feature writes is user_achievements.
// =============================================================================

using CodeQuest.Models;

namespace CodeQuest.Data;

/// <summary>
/// Reads the gamification aggregates of the attempts and records achievement
/// unlocks (schema tables <c>quiz_attempts</c>, <c>quizzes</c>, <c>topics</c>,
/// <c>users</c> and <c>user_achievements</c>).
/// </summary>
public interface IGamificationRepository
{
    /// <summary>
    /// The achievement-relevant aggregates of one user, derived from their
    /// attempts (quiz counts, correct answers, perfect runs, topics).
    /// A user without attempts gets the zero baseline (all counts 0, no flags).
    /// </summary>
    /// <param name="userId">The user to aggregate.</param>
    UserGamificationStats GetUserStats(int userId);

    /// <summary>
    /// The total XP of one user: the sum over their attempts of
    /// (correct answers × difficulty weight). Derived from the attempts —
    /// nothing is stored. A user without attempts has 0 XP.
    /// </summary>
    /// <param name="userId">The user to aggregate.</param>
    long GetTotalXp(int userId);

    /// <summary>
    /// The scoreboard (plan FR3): EVERY user — including users without a
    /// single attempt (XP 0, level 1) — ordered by total XP descending, then
    /// by quizzes taken descending, then by display name ascending.
    /// </summary>
    IReadOnlyList<LeaderboardEntry> GetLeaderboard();

    /// <summary>The level progress of one user (level, XP inside the level, fraction).</summary>
    /// <param name="userId">The user to aggregate.</param>
    UserProgress GetUserProgress(int userId);

    /// <summary>The achievements one user has unlocked, oldest unlock first.</summary>
    /// <param name="userId">The user to read.</param>
    IReadOnlyList<UnlockedAchievement> GetAchievements(int userId);

    /// <summary>
    /// Records the given achievements for one user, skipping every key the user
    /// already has (INSERT OR IGNORE semantics, all rows in ONE transaction —
    /// re-completing a feat never duplicates a row, acceptance A5). Returns only
    /// the rows that were actually inserted, each stamped with the current UTC
    /// time. An empty input inserts nothing and returns an empty list. An
    /// unknown user id is refused by the foreign key and surfaces as the typed
    /// data error.
    /// </summary>
    /// <param name="userId">The user that unlocked the achievements.</param>
    /// <param name="achievementKeys">The catalog keys to record.</param>
    /// <returns>The newly recorded unlocks, in the order of the given keys.</returns>
    IReadOnlyList<UnlockedAchievement> AddAchievementsIfNew(int userId, IEnumerable<string> achievementKeys);
}
