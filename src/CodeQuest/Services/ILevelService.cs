// The level contract. (Abstraction)

using CodeQuest.Models;

namespace CodeQuest.Services;

public interface ILevelService
{
    int XpForCorrectAnswer(Difficulty difficulty);

    long CumulativeXpForLevel(int level);

    int ComputeLevel(long totalXp);

    UserProgress ComputeProgress(int userId, long totalXp, int quizzesTaken);
}
