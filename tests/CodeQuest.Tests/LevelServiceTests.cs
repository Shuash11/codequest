// =============================================================================
// CodeQuest — Gamification Foundation (plan WP1, acceptance A3/A8)
// Tests for: LevelService (the XP/level math). PURE unit tests — no database,
// no UI: every expected value below is hand-computed from the threshold
// formula 25(n-1)(n+2) the plan fixes (L1=0, L2=100, L3=250, L4=450, L5=700,
// L6=1000) and from the difficulty weights Easy 10 / Medium 20 / Hard 30 /
// Insane 40.
// Look at: the boundary cases 99/100 and 249/250 (A3 asks for exactly these),
//          the round-trip ComputeLevel(CumulativeXpForLevel(n)) == n, the huge
//          XP value near long.MaxValue (the binary search must stay exact
//          where a floating-point inverse would drift) and the progress
//          fraction, which must never leave 0..1.
// =============================================================================

using CodeQuest.Models;
using CodeQuest.Services;

namespace CodeQuest.Tests;

/// <summary>
/// Phase WP1 level-math tests: XP weights, cumulative thresholds, the
/// XP-to-level inverse and the progress within a level.
/// </summary>
public class LevelServiceTests
{
    private readonly ILevelService _levels = new LevelService();

    // ---------------------------------------------------------------------
    // XP per correct answer — the difficulty weights of plan FR2.
    // ---------------------------------------------------------------------
    [Theory]
    [InlineData(Difficulty.Easy, 10)]
    [InlineData(Difficulty.Medium, 20)]
    [InlineData(Difficulty.Hard, 30)]
    [InlineData(Difficulty.Insane, 40)]
    public void XpForCorrectAnswer_UsesTheDifficultyWeight(Difficulty difficulty, int expectedXp)
    {
        Assert.Equal(expectedXp, _levels.XpForCorrectAnswer(difficulty));
    }

    [Fact]
    public void XpForCorrectAnswer_UnknownDifficulty_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _levels.XpForCorrectAnswer((Difficulty)999));
    }

    // ---------------------------------------------------------------------
    // Cumulative thresholds — 25(n-1)(n+2), hand-computed.
    // ---------------------------------------------------------------------
    [Theory]
    [InlineData(1, 0)]     // level 1 starts at 0 XP (A3)
    [InlineData(2, 100)]   // 25 * 1 * 4
    [InlineData(3, 250)]   // 25 * 2 * 5
    [InlineData(4, 450)]   // 25 * 3 * 6
    [InlineData(5, 700)]   // 25 * 4 * 7
    [InlineData(6, 1000)]  // 25 * 5 * 8
    public void CumulativeXpForLevel_MatchesThePlanThresholds(int level, long expectedXp)
    {
        Assert.Equal(expectedXp, _levels.CumulativeXpForLevel(level));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void CumulativeXpForLevel_LevelBelowOne_Throws(int level)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _levels.CumulativeXpForLevel(level));
    }

    // ---------------------------------------------------------------------
    // ComputeLevel — the inverse, exact at every boundary (A3).
    // ---------------------------------------------------------------------
    [Theory]
    [InlineData(0, 1)]        // 0 XP is level 1
    [InlineData(1, 1)]
    [InlineData(99, 1)]       // one XP below the L2 threshold
    [InlineData(100, 2)]      // exactly the L2 threshold
    [InlineData(101, 2)]
    [InlineData(249, 2)]      // one XP below the L3 threshold
    [InlineData(250, 3)]      // exactly the L3 threshold
    [InlineData(449, 3)]
    [InlineData(450, 4)]
    [InlineData(699, 4)]
    [InlineData(700, 5)]
    [InlineData(999, 5)]
    [InlineData(1000, 6)]
    [InlineData(100000, 62)]  // 25*61*64 = 97600 <= 100000 < 25*62*65 = 100750
    public void ComputeLevel_IsExactAtTheThresholds(long totalXp, int expectedLevel)
    {
        Assert.Equal(expectedLevel, _levels.ComputeLevel(totalXp));
    }

    [Fact]
    public void ComputeLevel_JustBelowEachThreshold_StaysOnThePreviousLevel()
    {
        for (int level = 2; level <= 12; level++)
        {
            long oneXpBelow = _levels.CumulativeXpForLevel(level) - 1;
            Assert.Equal(level - 1, _levels.ComputeLevel(oneXpBelow));
        }
    }

    [Fact]
    public void ComputeLevel_OfEveryThreshold_RoundTrips()
    {
        foreach (int level in new[] { 1, 2, 3, 4, 5, 10, 25, 40, 100, 1000 })
        {
            Assert.Equal(level, _levels.ComputeLevel(_levels.CumulativeXpForLevel(level)));
        }
    }

    [Fact]
    public void ComputeLevel_NegativeXp_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _levels.ComputeLevel(-1));
    }

    [Fact]
    public void ComputeLevel_LargestPossibleXp_StaysExactAndInsideTheRange()
    {
        int level = _levels.ComputeLevel(long.MaxValue);

        // The threshold of the found level fits into a long and round-trips.
        Assert.True(_levels.CumulativeXpForLevel(level) <= long.MaxValue);
        Assert.Equal(level, _levels.ComputeLevel(_levels.CumulativeXpForLevel(level)));

        // It is the TOP level: the threshold of the next level would no longer
        // fit into a long (and the search found it without any overflow).
        Assert.Throws<ArgumentOutOfRangeException>(() => _levels.CumulativeXpForLevel(level + 1));

        // And it is far above everyday levels — the search did not collapse.
        Assert.True(level > 1000);
    }

    // ---------------------------------------------------------------------
    // ComputeProgress — level, XP window and the clamped fraction (A3/A4).
    // ---------------------------------------------------------------------
    [Fact]
    public void ComputeProgress_AtZeroXp_StartsLevel1WithTheFullWindowAhead()
    {
        UserProgress progress = _levels.ComputeProgress(7, totalXp: 0, quizzesTaken: 0);

        Assert.Equal(7, progress.UserId);
        Assert.Equal(1, progress.Level);
        Assert.Equal(0, progress.XpIntoLevel);
        Assert.Equal(100, progress.XpForNextLevel); // the 1 -> 2 gap is 100 XP
        Assert.Equal(0, progress.QuizzesTaken);
        Assert.Equal(0d, progress.ProgressFraction);
    }

    [Fact]
    public void ComputeProgress_MidLevel_ShowsTheFractionOfTheWindow()
    {
        UserProgress progress = _levels.ComputeProgress(1, totalXp: 50, quizzesTaken: 2);

        Assert.Equal(1, progress.Level);
        Assert.Equal(50, progress.XpIntoLevel);
        Assert.Equal(100, progress.XpForNextLevel);
        Assert.Equal(0.5d, progress.ProgressFraction, 9);
    }

    [Fact]
    public void ComputeProgress_OneXpBelowTheBoundary_ShowsAlmostOne()
    {
        UserProgress progress = _levels.ComputeProgress(1, totalXp: 99, quizzesTaken: 1);

        Assert.Equal(1, progress.Level);
        Assert.Equal(0.99d, progress.ProgressFraction, 9);
    }

    [Fact]
    public void ComputeProgress_AtALevelBoundary_RestartsTheFractionInTheNextLevel()
    {
        UserProgress progress = _levels.ComputeProgress(1, totalXp: 100, quizzesTaken: 1);

        Assert.Equal(2, progress.Level);
        Assert.Equal(0, progress.XpIntoLevel);
        Assert.Equal(150, progress.XpForNextLevel); // the 2 -> 3 gap is 150 XP
        Assert.Equal(0d, progress.ProgressFraction);
    }

    [Fact]
    public void ComputeProgress_Level3AndLevel5_WindowsMatchTheFormula()
    {
        UserProgress level3 = _levels.ComputeProgress(1, totalXp: 250, quizzesTaken: 3);
        Assert.Equal(3, level3.Level);
        Assert.Equal(200, level3.XpForNextLevel); // 450 - 250

        UserProgress level5 = _levels.ComputeProgress(1, totalXp: 700, quizzesTaken: 5);
        Assert.Equal(5, level5.Level);
        Assert.Equal(300, level5.XpForNextLevel); // 1000 - 700
    }

    [Fact]
    public void ComputeProgress_FractionStaysBetweenZeroAndOne()
    {
        for (long totalXp = 0; totalXp <= 5000; totalXp += 13)
        {
            UserProgress progress = _levels.ComputeProgress(1, totalXp, quizzesTaken: 1);

            Assert.True(progress.ProgressFraction >= 0d, $"Fraction below 0 at {totalXp} XP.");
            Assert.True(progress.ProgressFraction <= 1d, $"Fraction above 1 at {totalXp} XP.");
        }
    }

    [Fact]
    public void UserProgress_RejectsAnInconsistentWindow()
    {
        // A negative XP window is impossible by construction — the shape refuses it.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new UserProgress(1, totalXp: 50, level: 1, xpIntoLevel: 50, xpForNextLevel: -1, quizzesTaken: 1));
    }
}
