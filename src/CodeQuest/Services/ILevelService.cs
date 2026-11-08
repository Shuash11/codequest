// =============================================================================
// CodeQuest — Gamification Foundation (plan WP1, acceptance A3)
// OOP rubric: ABSTRACTION — the contract of the XP/level math.
// Look at: ILevelService below. It states the four operations the gamification
//          feature needs: the XP of one correct answer, the cumulative XP of a
//          level, the inverse (XP -> level) and the progress within a level.
//          The implementation (LevelService) is PURE MATH — no database, no
//          UI, no clock — so every rule is unit-testable in isolation.
// Rule:    cumulative XP to reach level n = 25(n-1)(n+2)
//          (plan FR2 / owner decision) -> L1 = 0, L2 = 100, L3 = 250,
//          L4 = 450, L5 = 700, L6 = 1000. XP is derived from the attempts and
//          never stored (plan ruling: no XP column).
// =============================================================================

using CodeQuest.Models;

namespace CodeQuest.Services;

/// <summary>
/// The XP/level rules of the gamification feature. The implementation lives in
/// <see cref="LevelService"/>; the gamification repository and the screens of
/// WP5 depend on this interface only.
/// </summary>
public interface ILevelService
{
    /// <summary>
    /// The XP one CORRECT answer earns in a quiz of the given difficulty
    /// (Easy 10 / Medium 20 / Hard 30 — plan FR2).
    /// </summary>
    /// <param name="difficulty">The difficulty of the quiz that was taken.</param>
    /// <returns>The XP weight of the difficulty.</returns>
    int XpForCorrectAnswer(Difficulty difficulty);

    /// <summary>
    /// The TOTAL XP needed to reach the given level
    /// (<c>25 * (level - 1) * (level + 2)</c>): level 1 starts at 0 XP.
    /// </summary>
    /// <param name="level">The level, 1 or above.</param>
    /// <returns>The cumulative XP threshold of the level.</returns>
    long CumulativeXpForLevel(int level);

    /// <summary>
    /// The level a total XP reaches — the inverse of
    /// <see cref="CumulativeXpForLevel"/>: the highest level whose threshold is
    /// not above the XP. Monotonic and never below 1 (0 XP is level 1).
    /// </summary>
    /// <param name="totalXp">The total XP; cannot be negative.</param>
    /// <returns>The level, 1 or above.</returns>
    int ComputeLevel(long totalXp);

    /// <summary>
    /// The level progress of one user: level, the XP inside the current level,
    /// the size of the level's XP window and the clamped progress fraction.
    /// </summary>
    /// <param name="userId">The user the progress belongs to (carried into the result).</param>
    /// <param name="totalXp">The user's total XP.</param>
    /// <param name="quizzesTaken">How many quizzes the user has completed (carried into the result).</param>
    /// <returns>The progress the scoreboard card shows.</returns>
    UserProgress ComputeProgress(int userId, long totalXp, int quizzesTaken);
}
