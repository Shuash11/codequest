// =============================================================================
// CodeQuest — Gamification Foundation (plan WP1, acceptance A3/A4/A5)
// OOP rubric: ABSTRACTION — GamificationRepository is the concrete
//              implementation behind the IGamificationRepository contract.
// Look at: SelectUserAttemptRowsSql — the ONE query behind the per-user
//          aggregates. It joins every attempt of the user with its quiz
//          (difficulty, topic, category) and counts the answered questions of
//          the attempt (the schema stores only the CORRECT count — the
//          answered count is derived by counting submitted_answers, exactly
//          like the schema header documents). The XP is derived in C# from
//          those rows using DifficultyXpWeights (via LevelService), NOT in
//          SQL: the weight rule then lives in exactly one place and the
//          level math of LevelService is reused everywhere.
//          GetLeaderboard() — a LEFT JOIN from users, so accounts without a
//          single attempt are part of the scoreboard (XP 0, level 1); the
//          ordering rule (XP desc, quizzes desc, name asc) is applied in C#
//          over the derived values.
//          AddAchievementsIfNew() — INSERT OR IGNORE inside the one
//          transaction helper of Db (rollback + typed error mapping for free);
//          the "changes" count of each statement tells whether the row was
//          really inserted, so only NEW unlocks travel back to the caller.
// Table:   user_achievements (added by Data/DatabaseMigrator on existing
//          databases; see database/schema.sql for the fresh-install DDL).
// =============================================================================

using CodeQuest.Models;
using CodeQuest.Services;
using Microsoft.Data.Sqlite;

namespace CodeQuest.Data.Repositories;

/// <summary>
/// Reads the gamification aggregates of the attempts and records achievement
/// unlocks (see <see cref="IGamificationRepository"/> for the contract).
/// </summary>
public sealed class GamificationRepository : IGamificationRepository
{
    /// <summary>
    /// One row per attempt of the user: the correct count, the difficulty of
    /// the quiz, its topic and category, how many questions the attempt
    /// answered (counted from submitted_answers — the schema stores only the
    /// correct count), and the quiz's TOTAL question count (review finding
    /// MAJOR-1: "perfect" is 100% of the QUIZ, so a partial attempt that
    /// skipped questions must never qualify — answered alone is not enough).
    /// </summary>
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

    /// <summary>
    /// Every user with the correct count and difficulty of each of their
    /// attempts (or no attempt row at all) — the raw material of the
    /// scoreboard. Ordered by user so one pass groups the rows per user.
    /// </summary>
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

    /// <summary>
    /// Creates the repository with the default level service.
    /// </summary>
    /// <param name="db">The connection factory of the database.</param>
    public GamificationRepository(Db db)
        : this(db, new LevelService())
    {
    }

    /// <summary>
    /// Creates the repository with an explicit level service (tests inject a
    /// replacement here; the production code uses the default constructor).
    /// </summary>
    /// <param name="db">The connection factory of the database.</param>
    /// <param name="levels">The XP/level math the derivation uses.</param>
    public GamificationRepository(Db db, ILevelService levels)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(levels);
        _db = db;
        _levels = levels;
    }

    /// <inheritdoc />
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

            // A perfect run is 100% of the QUIZ (review MAJOR-1): every
            // question answered AND every answer correct. Comparing only with
            // the answered count let a partial attempt qualify — a user can
            // skip questions and submit (the player allows it), so
            // "1 correct of 5, 4 skipped" was 1 == 1 and unlocked "Flawless"
            // permanently (the ledger never revises a first unlock). A quiz
            // with zero questions is vacuous, never perfect.
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

    /// <inheritdoc />
    public long GetTotalXp(int userId)
    {
        // The attempt rows carry everything the XP needs (correct count and
        // difficulty), so the stats query doubles as the XP query.
        IReadOnlyList<UserAttemptRow> rows = ReadAttemptRows(userId);
        long totalXp = 0;

        foreach (UserAttemptRow row in rows)
        {
            totalXp += (long)row.Score * _levels.XpForCorrectAnswer(row.Difficulty);
        }

        return totalXp;
    }

    /// <inheritdoc />
    public IReadOnlyList<LeaderboardEntry> GetLeaderboard()
    {
        // The per-user aggregates: keyed in first-seen order, so the final
        // sort below alone decides the ranking.
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
                    // The LEFT JOIN produces one row per attempt; users without
                    // attempts contribute no such row and stay at 0 XP / 0 quizzes.
                    Difficulty difficulty = SqlValueMap.ParseDifficulty(reader.GetString(3));
                    int score = reader.GetInt32(2);
                    entry.TotalXp += (long)score * _levels.XpForCorrectAnswer(difficulty);
                    entry.QuizzesTaken++;
                }

                users[userId] = entry;
            }
        }

        // Plan FR3: XP descending, then more quizzes, then display name A..Z.
        // The name comparison is ordinal, matching the binary collation the
        // schema's TEXT columns use.
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

    /// <inheritdoc />
    public UserProgress GetUserProgress(int userId)
    {
        // One query serves both aggregates: the row count is the quiz count,
        // the weighted sum is the total XP.
        IReadOnlyList<UserAttemptRow> rows = ReadAttemptRows(userId);
        long totalXp = 0;

        foreach (UserAttemptRow row in rows)
        {
            totalXp += (long)row.Score * _levels.XpForCorrectAnswer(row.Difficulty);
        }

        return _levels.ComputeProgress(userId, totalXp, rows.Count);
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public IReadOnlyList<UnlockedAchievement> AddAchievementsIfNew(int userId, IEnumerable<string> achievementKeys)
    {
        ArgumentNullException.ThrowIfNull(achievementKeys);

        // One key per user: duplicates in the input would race against
        // themselves, so they are collapsed here (INSERT OR IGNORE would
        // ignore them anyway — the distinct list only makes the result
        // deterministic).
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

                // INSERT OR IGNORE reports 0 changed rows when the user already
                // has the key — only real inserts become a result row.
                if (command.ExecuteNonQuery() > 0)
                {
                    inserted.Add(new UnlockedAchievement(key, unlockedAt));
                }
            }

            return (IReadOnlyList<UnlockedAchievement>)inserted;
        });
    }

    /// <summary>
    /// Reads the per-attempt rows of one user (see SelectUserAttemptRowsSql).
    /// </summary>
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

    /// <summary>One attempt of a user with everything the aggregates need.</summary>
    private readonly record struct UserAttemptRow(
        int Score,
        Difficulty Difficulty,
        int TopicId,
        string Category,
        int AnsweredCount,
        int QuestionCount);
}
