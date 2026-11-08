// =============================================================================
// CodeQuest — Gamification Foundation (plan WP1, acceptance A3)
// OOP rubric: ABSTRACTION — the concrete implementation behind ILevelService.
// Look at: ComputeLevel() below. The inverse of the threshold formula is found
//          by a BINARY SEARCH over the level range instead of a floating-point
//          square root: the search compares exact long arithmetic only, so the
//          result is exact for EVERY long — including huge XP values near
//          long.MaxValue where a double would lose precision. The search is
//          bounded by MaxLevel, the highest level whose threshold still fits
//          into a long (found once, at type init, with the overflow-free
//          comparison "square part <= long.MaxValue / 25").
//          ComputeProgress() — the guard the plan asks for: the fraction is
//          clamped to 0..1 and a zero-size XP window (possible only at the
//          top of the level range) counts as a complete bar instead of
//          dividing by zero.
// Note:    this class is pure math — no I/O of any kind (see ILevelService).
// =============================================================================

using CodeQuest.Models;

namespace CodeQuest.Services;

/// <summary>
/// The XP/level math of the gamification feature (see <see cref="ILevelService"/>
/// for the contract and the rule).
/// </summary>
public sealed class LevelService : ILevelService
{
    /// <summary>The constant of the threshold formula 25(n-1)(n+2).</summary>
    private const long ThresholdFactor = 25;

    /// <summary>
    /// The highest level whose cumulative XP still fits into a long. Every
    /// other level computation stays inside this bound, which makes the
    /// arithmetic overflow-free by construction.
    /// </summary>
    private static readonly int MaxLevel = FindMaxLevel();

    /// <inheritdoc />
    public int XpForCorrectAnswer(Difficulty difficulty) => DifficultyXpWeights.For(difficulty);

    /// <inheritdoc />
    public long CumulativeXpForLevel(int level)
    {
        if (level < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "The level cannot be below 1.");
        }

        if (level > MaxLevel)
        {
            throw new ArgumentOutOfRangeException(
                nameof(level),
                level,
                $"The level is above the highest level whose XP fits into a 64-bit number ({MaxLevel}).");
        }

        return ThresholdFactor * (level - 1) * (level + 2);
    }

    /// <inheritdoc />
    public int ComputeLevel(long totalXp)
    {
        if (totalXp < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalXp), totalXp, "The total XP cannot be negative.");
        }

        // Binary search for the highest level whose threshold is <= totalXp.
        // Invariant: the threshold of `low` is <= totalXp (true for level 1,
        // whose threshold is 0).
        int low = 1;
        int high = MaxLevel;

        while (low < high)
        {
            int middle = low + ((high - low + 1) / 2);

            if (CumulativeXpForLevel(middle) <= totalXp)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        return low;
    }

    /// <inheritdoc />
    public UserProgress ComputeProgress(int userId, long totalXp, int quizzesTaken)
    {
        int level = ComputeLevel(totalXp);
        long levelStartXp = CumulativeXpForLevel(level);
        long xpIntoLevel = totalXp - levelStartXp;

        // At the top of the level range there is no next threshold (its
        // cumulative XP would not fit into a long) — the window is reported as
        // 0 and the progress bar as complete (see UserProgress).
        long xpForNextLevel = 0;
        if (level < MaxLevel)
        {
            xpForNextLevel = CumulativeXpForLevel(level + 1) - levelStartXp;
        }

        return new UserProgress(userId, totalXp, level, xpIntoLevel, xpForNextLevel, quizzesTaken);
    }

    /// <summary>
    /// Finds the highest level whose threshold 25(level-1)(level+2) still fits
    /// into a long, using the overflow-free comparison "square part <=
    /// long.MaxValue / 25" (25 times a value is at most long.MaxValue exactly
    /// when the value is at most long.MaxValue / 25 in whole-number division).
    /// The square part itself is at most about 4.6 * 10^18 for int levels and
    /// therefore never overflows.
    /// </summary>
    private static int FindMaxLevel()
    {
        const long SquarePartLimit = long.MaxValue / ThresholdFactor;

        int low = 1;
        int high = int.MaxValue;

        while (low < high)
        {
            int middle = low + ((high - low + 1) / 2);
            long squarePart = (long)(middle - 1) * (middle + 2);

            if (squarePart <= SquarePartLimit)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        return low;
    }
}
