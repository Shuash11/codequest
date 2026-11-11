// =============================================================================
// CodeQuest — Gamification Foundation (plan WP1, acceptance A4/A5/A8)
// Tests for: GamificationRepository, run against a FRESH temporary SQLite
// database that every test builds for itself by executing database/schema.sql
// and database/seed.sql — the same pattern as RepositoryTests (phase P4).
// The scratch file lives in the user's temp folder, is unique per test, and
// is deleted in the teardown.
// Look at: the hand-computed expectations. The seeded demo user owns the
//          attempts (quiz 1 Easy 4/5, quiz 1 Easy 2/5, quiz 2 Medium 5/5,
//          quiz 3 Easy 3/5) = 40+20+100+30 = 190 XP over 4 quizzes:
//            leaderboard: XP desc -> quizzes desc -> display name asc, and a
//              ZERO-attempt user is part of the table (XP 0, level 1, A4);
//            stats: the seeded 5/5 run is a perfect run, the crafted Hard
//              3/3 run is the perfect HARD run, the attempts span 4 distinct
//              topics and both categories; the "score 0, zero answers"
//              attempt proves an empty attempt is NOT a perfect run;
//            add-if-new: the first call inserts, the re-run is a no-op (A5),
//              an unknown user is refused by the foreign key as the TYPED
//              data error, and the unlock order is oldest first (ties by key).
// =============================================================================

using CodeQuest.Data;
using CodeQuest.Data.Repositories;
using CodeQuest.Models;
using CodeQuest.Services;
using Microsoft.Data.Sqlite;

namespace CodeQuest.Tests;

/// <summary>
/// Phase WP1 gamification repository tests: every case runs against its own
/// fresh copy of the seeded demo database and asserts hand-computed
/// aggregates over crafted extra rows.
/// </summary>
public class GamificationRepositoryTests : IDisposable
{
    /// <summary>A timestamp with whole seconds — the format the data layer stores (ISO-8601 UTC).</summary>
    private static readonly DateTimeOffset FixedTime = new(2026, 11, 5, 8, 0, 0, TimeSpan.Zero);

    private readonly string _databasePath;
    private readonly Db _db;
    private readonly GamificationRepository _gamification;

    /// <summary>
    /// Builds one fresh seeded database for THIS test (schema.sql + seed.sql
    /// through a connection of the data layer), like RepositoryTests does.
    /// </summary>
    public GamificationRepositoryTests()
    {
        string scratchDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Temp", "opencode");
        Directory.CreateDirectory(scratchDirectory);

        _databasePath = Path.Combine(scratchDirectory, $"codequest-gamification-tests-{Guid.NewGuid():N}.db");
        _db = new Db(_databasePath);

        ExecuteScript(ReadScript("schema.sql"));
        ExecuteScript(ReadScript("seed.sql"));

        _gamification = new GamificationRepository(_db);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // The data layer pools its connections; without clearing the pool the
        // database file stays open and the delete would fail.
        SqliteConnection.ClearAllPools();

        File.Delete(_databasePath);
        File.Delete(_databasePath + "-journal");
        File.Delete(_databasePath + "-wal");
        File.Delete(_databasePath + "-shm");
    }

    // -------------------------------------------------------------------------
    // 1. Leaderboard — ALL users ranked by XP desc, then quizzes desc, then
    //    display name asc; a zero-attempt user is included (A4).
    //    Seeded demo: 40+20+100+30 = 190 XP over 4 quizzes. Crafted ties:
    //    bravo and alpha share 100 XP (bravo took more quizzes -> first),
    //    charlie and delta share 50 XP over 1 quiz each (name decides).
    // -------------------------------------------------------------------------
    [Fact]
    public void GetLeaderboard_OrdersByXpThenQuizzesThenName_AndIncludesZeroAttemptUsers()
    {
        AddUsers(("alpha", "Alpha Player"), ("bravo", "Bravo Player"), ("charlie", "Charlie Chan"), ("delta", "Delta Player"), ("epsilon", "Epsilon Newcomer"));

        // alpha: one Medium run (5 correct = 100 XP). bravo: two Easy runs
        // (5 + 5 = 50 + 50 = 100 XP) over two quizzes. charlie/delta: one Easy
        // run each (5 correct = 50 XP). epsilon: nothing at all.
        Execute(
            """
            INSERT INTO quiz_attempts (attempt_id, quiz_id, user_id, score, attempted_at) VALUES
                (5, 2, 2, 5, '2026-11-05T08:00:00Z'),
                (6, 1, 3, 5, '2026-11-05T08:01:00Z'),
                (7, 3, 3, 5, '2026-11-05T08:02:00Z'),
                (8, 3, 4, 5, '2026-11-05T08:03:00Z'),
                (9, 3, 5, 5, '2026-11-05T08:04:00Z');
            """);

        IReadOnlyList<LeaderboardEntry> leaderboard = _gamification.GetLeaderboard();

        Assert.Equal(6, leaderboard.Count); // every account, including epsilon
        Assert.Equal(
            new[] { "Demo Student", "Bravo Player", "Alpha Player", "Charlie Chan", "Delta Player", "Epsilon Newcomer" },
            leaderboard.Select(entry => entry.DisplayName).ToArray());

        LeaderboardEntry demo = leaderboard[0];
        Assert.Equal(1, demo.UserId);
        Assert.Equal(190, demo.TotalXp);
        Assert.Equal(4, demo.QuizzesTaken);
        Assert.Equal(2, demo.Level); // 100 <= 190 < 250

        LeaderboardEntry bravo = leaderboard[1];
        Assert.Equal(100, bravo.TotalXp);
        Assert.Equal(2, bravo.QuizzesTaken);
        Assert.Equal(2, bravo.Level); // exactly the L2 threshold

        LeaderboardEntry alpha = leaderboard[2];
        Assert.Equal(100, alpha.TotalXp);
        Assert.Equal(1, alpha.QuizzesTaken); // the quizzes tie-break put bravo above

        LeaderboardEntry charlie = leaderboard[3];
        Assert.Equal(50, charlie.TotalXp);
        Assert.Equal(1, charlie.Level);

        LeaderboardEntry delta = leaderboard[4];
        Assert.Equal(50, delta.TotalXp);
        Assert.Equal(1, delta.Level);

        LeaderboardEntry epsilon = leaderboard[5];
        Assert.Equal(0, epsilon.TotalXp);
        Assert.Equal(0, epsilon.QuizzesTaken);
        Assert.Equal(1, epsilon.Level); // a player without attempts is still level 1
    }

    // -------------------------------------------------------------------------
    // 2. Stats — the aggregates of one user, including the perfect-run rule
    //    (an attempt that answered NOTHING is not "perfect") and the Hard
    //    variant. Seeded demo + a crafted Hard 3/3 run; a crafted "rookie"
    //    with a 3/5 run and an empty run; a user without attempts.
    // -------------------------------------------------------------------------
    [Fact]
    public void GetUserStats_DerivesEveryAggregateFromTheAttempts()
    {
        CraftPerfectHardRunForDemo();
        AddUsers(("rookie", "Rookie Player"), ("newcomer", "Newcomer Player"));

        // attempt 6: quiz 1, 3 of 5 correct. attempt 7: quiz 3, score 0, no
        // answers at all — score == answered would be 0 == 0, but an attempt
        // that answered nothing must not count as a perfect run.
        Execute(
            """
            INSERT INTO quiz_attempts (attempt_id, quiz_id, user_id, score, attempted_at) VALUES
                (6, 1, 2, 3, '2026-11-05T08:01:00Z'),
                (7, 3, 2, 0, '2026-11-05T08:02:00Z');
            INSERT INTO submitted_answers (attempt_id, question_id, chosen_choice_id) VALUES
                (6, 1, 2),   -- correct
                (6, 2, 7),   -- correct
                (6, 3, 9),   -- correct
                (6, 4, 13),  -- wrong
                (6, 5, 17);  -- wrong
            """);

        UserGamificationStats demo = _gamification.GetUserStats(1);
        Assert.Equal(1, demo.UserId);
        Assert.Equal(5, demo.QuizzesTaken);           // 4 seeded + the Hard run
        Assert.Equal(17, demo.TotalCorrectAnswers);   // 4 + 2 + 5 + 3 + 3
        Assert.True(demo.HasPerfectRun);              // the seeded 5/5 Medium run
        Assert.True(demo.HasPerfectHardRun);          // the crafted 3/3 Hard run
        Assert.Equal(4, demo.DistinctTopicCount);     // topics 1, 2, 3 and 5
        Assert.True(demo.HasBothCategories);          // C#/Python/SQL + Web Security

        UserGamificationStats rookie = _gamification.GetUserStats(2);
        Assert.Equal(2, rookie.QuizzesTaken);
        Assert.Equal(3, rookie.TotalCorrectAnswers);
        Assert.False(rookie.HasPerfectRun);           // 3 of 5, and the 0/0 run
        Assert.False(rookie.HasPerfectHardRun);
        Assert.Equal(2, rookie.DistinctTopicCount);   // topics 1 and 3, both Programming
        Assert.False(rookie.HasBothCategories);

        UserGamificationStats newcomer = _gamification.GetUserStats(3);
        Assert.Equal(0, newcomer.QuizzesTaken);
        Assert.Equal(0, newcomer.TotalCorrectAnswers);
        Assert.False(newcomer.HasPerfectRun);
        Assert.False(newcomer.HasPerfectHardRun);
        Assert.Equal(0, newcomer.DistinctTopicCount);
        Assert.False(newcomer.HasBothCategories);
    }

    // -------------------------------------------------------------------------
    // 2b. The perfect-run rule against SKIPPED questions (review MAJOR-1): the
    //     player lets a user advance without answering, so a partial attempt
    //     stores score == answered < total. "Perfect" is 100% of the QUIZ.
    // -------------------------------------------------------------------------
    [Fact]
    public void GetUserStats_PartialAttemptWithSkippedQuestions_IsNeverPerfect()
    {
        AddUsers(("partial", "Partial Player"));

        // Attempt 6 on the seeded Hard quiz 5 (three questions): only
        // question 19 answered (correctly), questions 20 and 21 skipped —
        // score 1, answered 1. The pre-review rule compared the score with
        // the ANSWERED count (1 == 1) and permanently unlocked "Flawless"
        // and "Nightmare Slayer" for a one-third run; the fixed rule compares
        // against the quiz's three questions and must never qualify.
        Execute(
            """
            INSERT INTO quiz_attempts (attempt_id, quiz_id, user_id, score, attempted_at) VALUES
                (6, 5, 2, 1, '2026-11-05T08:03:00Z');
            INSERT INTO submitted_answers (attempt_id, question_id, chosen_choice_id) VALUES
                (6, 19, 75);  -- correct, then the run was submitted with two skips
            """);

        UserGamificationStats stats = _gamification.GetUserStats(2);

        Assert.Equal(1, stats.QuizzesTaken);
        Assert.Equal(1, stats.TotalCorrectAnswers);
        Assert.False(stats.HasPerfectRun);
        Assert.False(stats.HasPerfectHardRun);
    }

    // -------------------------------------------------------------------------
    // 3. Total XP — the sum of correct answers x difficulty weight over every
    //    attempt (derived, nothing stored — A3).
    // -------------------------------------------------------------------------
    [Fact]
    public void GetTotalXp_SumsCorrectAnswersTimesTheDifficultyWeight()
    {
        CraftPerfectHardRunForDemo();
        AddUsers(("rookie", "Rookie Player"));
        Execute(
            """
            INSERT INTO quiz_attempts (attempt_id, quiz_id, user_id, score, attempted_at) VALUES
                (6, 1, 2, 3, '2026-11-05T08:01:00Z');
            """);

        // demo: (4+2)*10 Easy + 5*20 Medium + 3*10 Easy + 3*30 Hard = 280.
        Assert.Equal(280, _gamification.GetTotalXp(1));
        // rookie: 3 * 10 Easy = 30.
        Assert.Equal(30, _gamification.GetTotalXp(2));
        // A user without attempts has 0 XP.
        Assert.Equal(0, _gamification.GetTotalXp(3));
    }

    // -------------------------------------------------------------------------
    // 4. Progress — total XP and quiz count flow into the level math of the
    //    level service (A3/A4).
    // -------------------------------------------------------------------------
    [Fact]
    public void GetUserProgress_MixesTotalXpAndQuizCount()
    {
        CraftPerfectHardRunForDemo();
        AddUsers(("rookie", "Rookie Player"));
        Execute(
            """
            INSERT INTO quiz_attempts (attempt_id, quiz_id, user_id, score, attempted_at) VALUES
                (6, 1, 2, 3, '2026-11-05T08:01:00Z');
            """);

        UserProgress demo = _gamification.GetUserProgress(1);
        Assert.Equal(1, demo.UserId);
        Assert.Equal(280, demo.TotalXp);
        Assert.Equal(5, demo.QuizzesTaken);
        Assert.Equal(3, demo.Level);                  // 250 <= 280 < 450
        Assert.Equal(30, demo.XpIntoLevel);           // 280 - 250
        Assert.Equal(200, demo.XpForNextLevel);       // 450 - 250
        Assert.Equal(0.15d, demo.ProgressFraction, 9);

        UserProgress rookie = _gamification.GetUserProgress(2);
        Assert.Equal(30, rookie.TotalXp);
        Assert.Equal(1, rookie.QuizzesTaken);
        Assert.Equal(1, rookie.Level);
        Assert.Equal(30, rookie.XpIntoLevel);
        Assert.Equal(100, rookie.XpForNextLevel);
        Assert.Equal(0.3d, rookie.ProgressFraction, 9);

        // A user without attempts sits at the very start of level 1.
        UserProgress newcomer = _gamification.GetUserProgress(3);
        Assert.Equal(0, newcomer.TotalXp);
        Assert.Equal(1, newcomer.Level);
        Assert.Equal(0d, newcomer.ProgressFraction);
    }

    // -------------------------------------------------------------------------
    // 5. Add-if-new — the first call inserts, the re-run is a no-op (A5), a
    //    mixed call returns only the genuinely new key, a duplicate inside one
    //    call collapses, and the empty input writes nothing.
    // -------------------------------------------------------------------------
    [Fact]
    public void AddAchievementsIfNew_InsertsNewKeys_AndIsIdempotentOnReRun()
    {
        DateTimeOffset before = DateTimeOffset.UtcNow;

        IReadOnlyList<UnlockedAchievement> firstBatch =
            _gamification.AddAchievementsIfNew(1, new[] { "first-quiz", "perfect-run" });

        Assert.Equal(new[] { "first-quiz", "perfect-run" }, firstBatch.Select(unlocked => unlocked.Key).ToArray());
        foreach (UnlockedAchievement unlocked in firstBatch)
        {
            Assert.Equal(TimeSpan.Zero, unlocked.UnlockedAt.Offset); // stored as UTC
            Assert.True(unlocked.UnlockedAt >= before.AddSeconds(-1));
        }

        // The re-run of the same feats changes nothing (acceptance A5).
        Assert.Empty(_gamification.AddAchievementsIfNew(1, new[] { "first-quiz", "perfect-run" }));

        // A mixed call returns ONLY the key that was genuinely new.
        IReadOnlyList<UnlockedAchievement> mixedBatch =
            _gamification.AddAchievementsIfNew(1, new[] { "perfect-run", "level-5" });
        Assert.Equal(new[] { "level-5" }, mixedBatch.Select(unlocked => unlocked.Key).ToArray());

        // A duplicate inside one call collapses to one row.
        IReadOnlyList<UnlockedAchievement> duplicateBatch =
            _gamification.AddAchievementsIfNew(1, new[] { "level-5", "level-5" });
        Assert.Empty(duplicateBatch);

        // Exactly three unlocks are stored for the user. The stored order
        // follows the query rule "oldest first, ties by key" — but the three
        // stamps may land in one whole second (the storage resolution) or
        // straddle a boundary depending on machine timing, so the RULE is
        // asserted here, not one timing-dependent sequence.
        IReadOnlyList<UnlockedAchievement> stored = _gamification.GetAchievements(1);
        Assert.Equal(3, stored.Count);
        Assert.Equal(
            new[] { "first-quiz", "level-5", "perfect-run" }.OrderBy(key => key, StringComparer.Ordinal),
            stored.Select(unlocked => unlocked.Key).OrderBy(key => key, StringComparer.Ordinal));
        for (int i = 1; i < stored.Count; i++)
        {
            UnlockedAchievement previous = stored[i - 1];
            UnlockedAchievement current = stored[i];
            Assert.True(
                previous.UnlockedAt < current.UnlockedAt
                || (previous.UnlockedAt == current.UnlockedAt
                    && string.CompareOrdinal(previous.Key, current.Key) <= 0),
                $"the unlock order rule broke at {previous.Key} -> {current.Key}");
        }
    }

    [Fact]
    public void AddAchievementsIfNew_EmptyInput_WritesNothing()
    {
        Assert.Empty(_gamification.AddAchievementsIfNew(1, Array.Empty<string>()));
        Assert.Empty(_gamification.GetAchievements(1));
    }

    [Fact]
    public void AddAchievementsIfNew_UnknownUser_IsRefusedWithTheTypedException()
    {
        Assert.Throws<ReferentialIntegrityException>(
            () => _gamification.AddAchievementsIfNew(999, new[] { "first-quiz" }));

        // Nothing was written — not for the unknown user, not for anyone.
        Assert.Empty(_gamification.GetAchievements(1));
    }

    // -------------------------------------------------------------------------
    // 6. GetAchievements — oldest unlock first; ties (same stored second) by
    //    key. The rows here are inserted with FIXED timestamps, because the
    //    repository stamps UtcNow at whole-second precision: controlled rows
    //    make the ordering assertion deterministic.
    // -------------------------------------------------------------------------
    [Fact]
    public void GetAchievements_OrdersByUnlockTimeThenKey()
    {
        Execute(
            """
            INSERT INTO user_achievements (user_id, achievement_key, unlocked_at) VALUES
                (1, 'ten-quizzes', '2026-11-05T12:00:00Z'),
                (1, 'first-quiz',  '2026-11-05T09:00:00Z'),
                (1, 'perfect-run', '2026-11-05T12:00:00Z');
            """);

        IReadOnlyList<UnlockedAchievement> stored = _gamification.GetAchievements(1);

        Assert.Equal(
            new[] { "first-quiz", "perfect-run", "ten-quizzes" },
            stored.Select(unlocked => unlocked.Key).ToArray());
        Assert.Equal(
            new DateTimeOffset(2026, 11, 5, 9, 0, 0, TimeSpan.Zero),
            stored[0].UnlockedAt);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Adds the crafted Hard run for the demo user: attempt 5 on the seeded
    /// Hard quiz 5 (topic Web Security) with all three questions answered
    /// correctly — the perfect Hard run the stats test needs.
    /// </summary>
    private void CraftPerfectHardRunForDemo()
    {
        Execute(
            """
            INSERT INTO quiz_attempts (attempt_id, quiz_id, user_id, score, attempted_at) VALUES
                (5, 5, 1, 3, '2026-11-05T08:00:00Z');
            INSERT INTO submitted_answers (attempt_id, question_id, chosen_choice_id) VALUES
                (5, 19, 75),  -- the correct choice of question 19
                (5, 20, 78),  -- the correct choice of question 20
                (5, 21, 84);  -- the correct choice of question 21
            """);
    }

    /// <summary>Inserts extra accounts (the ids follow the seeded user 1: 2, 3, ...).</summary>
    private void AddUsers(params (string Username, string DisplayName)[] users)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("INSERT INTO users (user_id, username, password_hash, display_name, created_at) VALUES");

        for (int index = 0; index < users.Length; index++)
        {
            int userId = index + 2; // the seed used the id 1
            builder.AppendLine(
                $"({userId}, '{users[index].Username}', '', '{users[index].DisplayName}', '2026-11-05T07:00:00Z'){(index + 1 < users.Length ? "," : ";")}");
        }

        Execute(builder.ToString());
    }

    /// <summary>Executes one literal SQL statement through a connection of the data layer.</summary>
    private void Execute(string sql)
    {
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Executes one SQL script (schema or seed) as one batch through a
    /// connection of the data layer — the scripts are read as text and run
    /// unchanged.
    /// </summary>
    private void ExecuteScript(string script)
    {
        Execute(script);
    }

    /// <summary>
    /// Reads one SQL script of the database folder (read-only). The folder is
    /// found by walking up from the test directory, so the test works no
    /// matter which output folder dotnet test has chosen.
    /// </summary>
    private static string ReadScript(string fileName)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "database", fileName);

            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"The SQL script '{fileName}' was not found in a 'database' folder above the test directory.");
    }
}
