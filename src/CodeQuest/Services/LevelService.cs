// Turns XP into levels.

using CodeQuest.Models;

namespace CodeQuest.Services;

public sealed class LevelService : ILevelService
{
    private const long ThresholdFactor = 25;

    private static readonly int MaxLevel = FindMaxLevel();

    public int XpForCorrectAnswer(Difficulty difficulty) => DifficultyXpWeights.For(difficulty);

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

    public int ComputeLevel(long totalXp)
    {
        if (totalXp < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalXp), totalXp, "The total XP cannot be negative.");
        }

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

    public UserProgress ComputeProgress(int userId, long totalXp, int quizzesTaken)
    {
        int level = ComputeLevel(totalXp);
        long levelStartXp = CumulativeXpForLevel(level);
        long xpIntoLevel = totalXp - levelStartXp;

        long xpForNextLevel = 0;
        if (level < MaxLevel)
        {
            xpForNextLevel = CumulativeXpForLevel(level + 1) - levelStartXp;
        }

        return new UserProgress(userId, totalXp, level, xpIntoLevel, xpForNextLevel, quizzesTaken);
    }

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
