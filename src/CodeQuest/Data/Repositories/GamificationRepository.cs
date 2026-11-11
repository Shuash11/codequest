// Reads XP, levels and achievements. (Abstraction)

using CodeQuest.Models;
using CodeQuest.Services;
using Microsoft.Data.Sqlite;

namespace CodeQuest.Data.Repositories;

public sealed class GamificationRepository : IGamificationRepository
{
    private const string SelectUserAttemptRowsSql =
        """
        SELECT a.score,
               q.difficulty,
               q.topic_id,
               t.category,
               (SELECT COUNT(*)
                FROM submitted_answers sa
                WHERE sa.attempt_id = a.attempt_id) AS answered_count,
               (SELECT COUNT(*)
                FROM questions qq
                WHERE qq.quiz_id = a.quiz_id) AS question_count
        FROM quiz_attempts a
        JOIN quizzes q ON q.quiz_id = a.quiz_id
        JOIN topics t ON t.topic_id = q.topic_id
        WHERE a.user_id = $userId;
        """;

    private const string SelectLeaderboardRowsSql =
        """
        SELECT u.user_id,
               u.display_name,
               a.score,
               q.difficulty
        FROM users u
        LEFT JOIN quiz_attempts a ON a.user_id = u.user_id
        LEFT JOIN quizzes q ON q.quiz_id = a.quiz_id
        ORDER BY u.user_id;
        """;

    private const string SelectAchievementsSql =
        """
        SELECT achievement_key, unlocked_at
        FROM user_achievements
        WHERE user_id = $userId
        ORDER BY unlocked_at ASC, achievement_key ASC;
        """;

    private const string InsertOrIgnoreAchievementSql =
        """
        INSERT OR IGNORE INTO user_achievements (user_id, achievement_key, unlocked_at)
        VALUES ($userId, $achievementKey, $unlockedAt);
        """;

    private readonly Db _db;
    private readonly ILevelService _levels;

    public GamificationRepository(Db db)
        : this(db, new LevelService())
    {
    }

    public GamificationRepository(Db db, ILevelService levels)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(levels);
        _db = db;
        _levels = levels;
    }

    // Reads one player's XP and level.
    public UserGamificationStats GetUserStats(int userId)
    {
        IReadOnlyList<UserAttemptRow> rows = ReadAttemptRows(userId);

        int quizzesTaken = 0;
        long totalCorrect = 0;
        bool hasPerfectRun = false;
        bool hasPerfectHardRun = false;
        HashSet<int> topicIds = new();
        HashSet<string> categories = new();

        foreach (UserAttemptRow row in rows)
        {
            quizzesTaken++;
            totalCorrect += row.Score;

            if (row.QuestionCount > 0
                && row.AnsweredCount == row.QuestionCount
                && row.Score == row.QuestionCount)
            {
                hasPerfectRun = true;

                if (row.Difficulty == Difficulty.Hard)
                {
                    hasPerfectHardRun = true;
                }
            }

            topicIds.Add(row.TopicId);
            categories.Add(row.Category);
        }

        return new UserGamificationStats(
            userId,
            quizzesTaken,
            totalCorrect,
            hasPerfectRun,
            hasPerfectHardRun,
            topicIds.Count,
            categories.Contains("Programming") && categories.Contains("Cybersecurity"));
    }

    // Reads one player's total XP.
    public long GetTotalXp(int userId)
    {
        IReadOnlyList<UserAttemptRow> rows = ReadAttemptRows(userId);
        long totalXp = 0;

        foreach (UserAttemptRow row in rows)
        {
            totalXp += (long)row.Score * _levels.XpForCorrectAnswer(row.Difficulty);
        }

        return totalXp;
    }

    // Reads all players ranked by XP.
    public IReadOnlyList<LeaderboardEntry> GetLeaderboard()
    {
        Dictionary<int, (string DisplayName, long TotalXp, int QuizzesTaken)> users = new();

        using (SqliteConnection connection = _db.OpenConnection())
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = SelectLeaderboardRowsSql;

            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                int userId = reader.GetInt32(0);

                if (!users.TryGetValue(userId, out (string DisplayName, long TotalXp, int QuizzesTaken) entry))
                {
                    entry = (reader.GetString(1), 0, 0);
                }

                if (!reader.IsDBNull(2))
                {
                    Difficulty difficulty = SqlValueMap.ParseDifficulty(reader.GetString(3));
                    int score = reader.GetInt32(2);
                    entry.TotalXp += (long)score * _levels.XpForCorrectAnswer(difficulty);
                    entry.QuizzesTaken++;
                }

                users[userId] = entry;
            }
        }

        return users
            .Select(user => new LeaderboardEntry(
                user.Key,
                user.Value.DisplayName,
                _levels.ComputeLevel(user.Value.TotalXp),
                user.Value.TotalXp,
                user.Value.QuizzesTaken))
            .OrderByDescending(entry => entry.TotalXp)
            .ThenByDescending(entry => entry.QuizzesTaken)
            .ThenBy(entry => entry.DisplayName, StringComparer.Ordinal)
            .ToArray();
    }

    // Reads one player's level progress.
    public UserProgress GetUserProgress(int userId)
    {
        IReadOnlyList<UserAttemptRow> rows = ReadAttemptRows(userId);
        long totalXp = 0;

        foreach (UserAttemptRow row in rows)
        {
            totalXp += (long)row.Score * _levels.XpForCorrectAnswer(row.Difficulty);
        }

        return _levels.ComputeProgress(userId, totalXp, rows.Count);
    }

    // Reads a player's unlocked achievements.
    public IReadOnlyList<UnlockedAchievement> GetAchievements(int userId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectAchievementsSql;
        command.Parameters.AddWithValue("$userId", userId);

        List<UnlockedAchievement> achievements = new();

        using (SqliteDataReader reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                achievements.Add(new UnlockedAchievement(
                    reader.GetString(0),
                    SqlValueMap.FromStorage(reader.GetString(1))));
            }
        }

        return achievements;
    }

    // Records new achievements once.
    public IReadOnlyList<UnlockedAchievement> AddAchievementsIfNew(int userId, IEnumerable<string> achievementKeys)
    {
        ArgumentNullException.ThrowIfNull(achievementKeys);

        List<string> keys = achievementKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct()
            .ToList();

        if (keys.Count == 0)
        {
            return Array.Empty<UnlockedAchievement>();
        }

        return _db.RunInTransaction((connection, transaction) =>
        {
            List<UnlockedAchievement> inserted = new();

            foreach (string key in keys)
            {
                DateTimeOffset unlockedAt = DateTimeOffset.UtcNow;

                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = InsertOrIgnoreAchievementSql;
                command.Transaction = transaction;
                command.Parameters.AddWithValue("$userId", userId);
                command.Parameters.AddWithValue("$achievementKey", key);
                command.Parameters.AddWithValue("$unlockedAt", SqlValueMap.ToStorage(unlockedAt));

                if (command.ExecuteNonQuery() > 0)
                {
                    inserted.Add(new UnlockedAchievement(key, unlockedAt));
                }
            }

            return (IReadOnlyList<UnlockedAchievement>)inserted;
        });
    }

    private IReadOnlyList<UserAttemptRow> ReadAttemptRows(int userId)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectUserAttemptRowsSql;
        command.Parameters.AddWithValue("$userId", userId);

        List<UserAttemptRow> rows = new();

        using (SqliteDataReader reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                rows.Add(new UserAttemptRow(
                    reader.GetInt32(0),
                    SqlValueMap.ParseDifficulty(reader.GetString(1)),
                    reader.GetInt32(2),
                    reader.GetString(3),
                    Convert.ToInt32(reader.GetInt64(4)),
                    Convert.ToInt32(reader.GetInt64(5))));
            }
        }

        return rows;
    }

    private readonly record struct UserAttemptRow(
        int Score,
        Difficulty Difficulty,
        int TopicId,
        string Category,
        int AnsweredCount,
        int QuestionCount);
}
