// What the app needs for XP, levels and achievements. (Abstraction)

using CodeQuest.Models;

namespace CodeQuest.Data;

public interface IGamificationRepository
{
    UserGamificationStats GetUserStats(int userId);

    long GetTotalXp(int userId);

    IReadOnlyList<LeaderboardEntry> GetLeaderboard();

    UserProgress GetUserProgress(int userId);

    IReadOnlyList<UnlockedAchievement> GetAchievements(int userId);

    IReadOnlyList<UnlockedAchievement> AddAchievementsIfNew(int userId, IEnumerable<string> achievementKeys);
}
