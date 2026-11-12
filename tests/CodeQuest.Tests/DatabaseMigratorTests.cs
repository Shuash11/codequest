// =============================================================================
// CodeQuest — Gamification Foundation (plan WP1, acceptance A7/A8) + task 5
// Tests for: DatabaseMigrator (the idempotent startup migration), run against
// a FRESH temporary SQLite database built the same way RepositoryTests builds
// its own (schema.sql + the frozen demo fixture, read as text and executed
// unchanged).
// Look at: the "legacy database" cases —
//          * achievements: schema.sql of this release already contains
//            user_achievements, so the test drops the table again to
//            reconstruct the pre-gamification state; the migrator adds it
//            without touching the rows and the FK to users stays ENFORCED;
//          * task 5: a PRE-CAMPAIGN database (the 3-value quizzes CHECK, no
//            questions.difficulty) is rebuilt so 'Insane' passes, the
//            difficulty column is backfilled from the parent quiz, and the
//            built-in campaign (the Campaign topic + Level 1 only — the AI
//            generates every later level) is inserted — idempotently;
//          * the campaign script must also be EMBEDDED in the application
//            (the installed app has no database/ folder).
// =============================================================================

using CodeQuest.Data;
using Microsoft.Data.Sqlite;

namespace CodeQuest.Tests;

/// <summary>
/// Phase WP1 migrator tests: fresh legacy database, idempotent re-run, data
/// preservation and foreign-key enforcement.
/// </summary>
public class DatabaseMigratorTests : IDisposable
{
    private readonly string _scratchDirectory;
    private readonly string _databasePath;
    private readonly Db _db;
    private readonly List<string> _extraDatabasePaths = new();

    /// <summary>
    /// Builds one fresh seeded database for THIS test (schema.sql + the frozen
    /// demo fixture through a connection of the data layer), like
    /// RepositoryTests does.
    /// </summary>
    public DatabaseMigratorTests()
    {
        _scratchDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Temp", "opencode");
        Directory.CreateDirectory(_scratchDirectory);

        _databasePath = Path.Combine(_scratchDirectory, $"codequest-migrator-tests-{Guid.NewGuid():N}.db");
        _db = new Db(_databasePath);

        ExecuteScript(ReadScript("schema.sql"));
        ExecuteScript(ReadFixture("demo-seed.sql"));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // The data layer pools its connections; without clearing the pool the
        // database file stays open and the delete would fail.
        SqliteConnection.ClearAllPools();

        DeleteDatabase(_databasePath);
        foreach (string path in _extraDatabasePaths)
        {
            DeleteDatabase(path);
        }
    }

    private static void DeleteDatabase(string databasePath)
    {
        File.Delete(databasePath);
        File.Delete(databasePath + "-journal");
        File.Delete(databasePath + "-wal");
        File.Delete(databasePath + "-shm");
    }

    /// <summary>Creates one extra empty scratch database for a test that needs its own file.</summary>
    private Db CreateScratchDatabase(string label)
    {
        string path = Path.Combine(_scratchDirectory, $"codequest-migrator-tests-{label}-{Guid.NewGuid():N}.db");
        _extraDatabasePaths.Add(path);
        return new Db(path);
    }

    // -------------------------------------------------------------------------
    // 1. The legacy case — a database from before the gamification release has
    //    NO user_achievements table. The migrator adds it (and its index) and
    //    preserves every seeded row; a second run is a safe no-op.
    // -------------------------------------------------------------------------
    [Fact]
    public void EnsureSchema_AddsTheTableToALegacyDatabase_AndPreservesEveryRow()
    {
        // Reconstruct the pre-gamification state: drop the table (and its
        // index, which the schema created with it) from the fresh database.
        Execute("DROP TABLE user_achievements;");
        Assert.Equal(0, Scalar("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'user_achievements';"));

        DatabaseMigrator.EnsureSchema(_db);

        Assert.Equal(1, Scalar("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'user_achievements';"));
        Assert.Equal(1, Scalar("SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'idx_user_achievements_user_id';"));

        // The existing rows survived the migration untouched (plan A7: the
        // attempts feed the scoreboard, so they must survive).
        Assert.Equal(1, Scalar("SELECT COUNT(*) FROM users;"));
        Assert.Equal(4, Scalar("SELECT COUNT(*) FROM quiz_attempts;"));
        Assert.Equal(20, Scalar("SELECT COUNT(*) FROM submitted_answers;"));

        // Running it again (every launch) succeeds and changes nothing.
        DatabaseMigrator.EnsureSchema(_db);

        Assert.Equal(1, Scalar("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'user_achievements';"));
        Assert.Equal(1, Scalar("SELECT COUNT(*) FROM users;"));
        Assert.Equal(4, Scalar("SELECT COUNT(*) FROM quiz_attempts;"));
        Assert.Equal(20, Scalar("SELECT COUNT(*) FROM submitted_answers;"));
    }

    // -------------------------------------------------------------------------
    // 2. The fresh-install case — schema.sql already created the table; the
    //    achievements step must be a no-op on it (safe to call unconditionally;
    //    the task-5 steps ensure the Insane CHECK and the campaign as well).
    // -------------------------------------------------------------------------
    [Fact]
    public void EnsureSchema_OnAFreshSchemaDatabase_IsASafeNoOp()
    {
        Assert.Equal(1, Scalar("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'user_achievements';"));

        DatabaseMigrator.EnsureSchema(_db);

        Assert.Equal(1, Scalar("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'user_achievements';"));
        Assert.Equal(1, Scalar("SELECT COUNT(*) FROM users;"));
        Assert.Equal(4, Scalar("SELECT COUNT(*) FROM quiz_attempts;"));
    }

    // -------------------------------------------------------------------------
    // 3. The migrated table enforces its foreign key: an unlock for an unknown
    //    user is refused and arrives as the TYPED data error (the mapping of
    //    RunInTransaction), while an unlock for the seeded user is accepted.
    // -------------------------------------------------------------------------
    [Fact]
    public void EnsureSchema_AchievementsTable_EnforcesTheUserForeignKey()
    {
        DatabaseMigrator.EnsureSchema(_db);

        Assert.Throws<ReferentialIntegrityException>(() => _db.RunInTransaction<object?>((connection, _) =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "INSERT INTO user_achievements (user_id, achievement_key, unlocked_at) " +
                "VALUES (999, 'first-quiz', '2026-11-05T09:00:00Z');";
            command.ExecuteNonQuery();
            return null;
        }));

        // The valid row is accepted (user 1 is the seeded account).
        _db.RunInTransaction<object?>((connection, _) =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "INSERT INTO user_achievements (user_id, achievement_key, unlocked_at) " +
                "VALUES (1, 'first-quiz', '2026-11-05T09:00:00Z');";
            command.ExecuteNonQuery();
            return null;
        });

        Assert.Equal(1, Scalar("SELECT COUNT(*) FROM user_achievements WHERE user_id = 1 AND achievement_key = 'first-quiz';"));
    }

    // -------------------------------------------------------------------------
    // 4. Task 5, the pre-campaign legacy case — the old 3-value quizzes CHECK
    //    and no questions.difficulty column. The migrator must widen the CHECK
    //    (so 'Insane' inserts), backfill every question from its parent quiz,
    //    keep all pre-existing rows and their values, keep the foreign keys
    //    working, and be a no-op on the second run.
    // -------------------------------------------------------------------------
    [Fact]
    public void EnsureSchema_OnALegacyDatabase_RebuildsQuizzesAndBackfillsQuestionDifficulty()
    {
        Db legacy = CreateScratchDatabase("legacy");
        ExecuteScript(legacy, LegacyPreCampaignSchemaSql);
        ExecuteScript(
            legacy,
            """
            INSERT INTO users (user_id, username, password_hash, display_name, created_at) VALUES
                (1, 'legacy', '', 'Legacy Student', '2026-11-01T09:00:00Z');
            INSERT INTO topics (topic_id, name, category, description, created_at) VALUES
                (1, 'Legacy Topic', 'Programming', 'The pre-campaign demo topic.', '2026-11-01T09:00:00Z');
            INSERT INTO quizzes (quiz_id, topic_id, title, difficulty, status, created_at) VALUES
                (1, 1, 'Legacy Quiz', 'Hard', 'Approved', '2026-11-01T09:10:00Z');
            INSERT INTO questions (question_id, quiz_id, question_text, explanation, position, created_at) VALUES
                (1, 1, 'Which keyword declares a constant?', 'const declares a compile-time constant.', 1, '2026-11-01T09:10:00Z');
            INSERT INTO choices (choice_id, question_id, choice_text, is_correct, position) VALUES
                (1, 1, 'static', 0, 1),
                (2, 1, 'const', 1, 2),
                (3, 1, 'readonly', 0, 3),
                (4, 1, 'sealed', 0, 4);
            INSERT INTO quiz_attempts (attempt_id, quiz_id, user_id, score, attempted_at) VALUES
                (1, 1, 1, 1, '2026-11-01T10:00:00Z');
            INSERT INTO submitted_answers (attempt_id, question_id, chosen_choice_id) VALUES
                (1, 1, 2);
            INSERT INTO user_achievements (user_id, achievement_key, unlocked_at) VALUES
                (1, 'first-quiz', '2026-11-01T10:00:00Z');
            """);

        DatabaseMigrator.EnsureSchema(legacy);

        // The rebuilt quizzes table accepts 'Insane' — and the RENAME did not
        // rewrite the questions table's foreign key away from quizzes.
        Assert.Contains("'Insane'", ReadCreateSql(legacy, "quizzes"), StringComparison.Ordinal);
        Assert.Contains("REFERENCES quizzes", ReadCreateSql(legacy, "questions"), StringComparison.Ordinal);
        Assert.Equal(0, Scalar(legacy, "SELECT COUNT(*) FROM pragma_foreign_key_check;"));

        Execute(
            legacy,
            "INSERT INTO quizzes (topic_id, title, difficulty, status, created_at) " +
            "VALUES (1, 'Legacy Insane Level', 'Insane', 'Approved', '2026-11-12T10:00:00Z');");

        // Every pre-existing row survived with its values; the new column was
        // backfilled from the parent quiz ('Hard'), and the campaign landed.
        Assert.Equal(1, Scalar(legacy, "SELECT COUNT(*) FROM users WHERE display_name = 'Legacy Student';"));
        Assert.Equal("Legacy Quiz", Text(legacy, "SELECT title FROM quizzes WHERE quiz_id = 1;"));
        Assert.Equal("Hard", Text(legacy, "SELECT difficulty FROM quizzes WHERE quiz_id = 1;"));
        Assert.Equal("Approved", Text(legacy, "SELECT status FROM quizzes WHERE quiz_id = 1;"));
        Assert.Equal(1, Scalar(legacy, "SELECT COUNT(*) FROM quiz_attempts WHERE attempt_id = 1 AND score = 1;"));
        Assert.Equal(1, Scalar(legacy, "SELECT COUNT(*) FROM submitted_answers WHERE attempt_id = 1 AND chosen_choice_id = 2;"));
        Assert.Equal(1, Scalar(legacy, "SELECT COUNT(*) FROM user_achievements;"));
        Assert.Equal("Hard", Text(legacy, "SELECT difficulty FROM questions WHERE question_id = 1;"));
        Assert.Equal(2, Scalar(legacy, "SELECT COUNT(*) FROM topics;"));                      // Legacy Topic + Campaign
        Assert.Equal(3, Scalar(legacy, "SELECT COUNT(*) FROM quizzes;"));                     // 1 legacy + campaign Level 1 + the Insane insert
        Assert.Equal(6, Scalar(legacy, "SELECT COUNT(*) FROM questions;"));                   // 1 legacy + 5 campaign
        Assert.Equal(24, Scalar(legacy, "SELECT COUNT(*) FROM choices;"));                    // 4 legacy + 20 campaign

        // The second run (every launch) changes nothing.
        DatabaseMigrator.EnsureSchema(legacy);

        Assert.Equal(1, Scalar(legacy, "SELECT COUNT(*) FROM users;"));
        Assert.Equal(2, Scalar(legacy, "SELECT COUNT(*) FROM topics;"));
        Assert.Equal(3, Scalar(legacy, "SELECT COUNT(*) FROM quizzes;"));
        Assert.Equal(6, Scalar(legacy, "SELECT COUNT(*) FROM questions;"));
        Assert.Equal(24, Scalar(legacy, "SELECT COUNT(*) FROM choices;"));
        Assert.Equal("Legacy Quiz", Text(legacy, "SELECT title FROM quizzes WHERE quiz_id = 1;"));
        Assert.Equal(0, Scalar(legacy, "SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    // -------------------------------------------------------------------------
    // 5. Task 5, the fresh-install campaign — EnsureSchema on a schema-only
    //    database builds exactly one Campaign topic with its single built-in
    //    level: "Level 1" (Easy, 5 four-choice questions with exactly one
    //    correct each, Approved). Every later level is generated by the AI
    //    rival. A re-run changes nothing.
    // -------------------------------------------------------------------------
    [Fact]
    public void EnsureSchema_OnAFreshSchemaOnlyDatabase_CreatesTheCampaign_AndIsIdempotent()
    {
        Db fresh = CreateScratchDatabase("campaign");
        ExecuteScript(fresh, ReadScript("schema.sql"));

        DatabaseMigrator.EnsureSchema(fresh);

        Assert.Equal(1, Scalar(fresh,
            "SELECT COUNT(*) FROM topics WHERE name = 'Campaign' AND category = 'Programming' " +
            "AND description = 'The progressive campaign \u2014 clear every level.';"));
        Assert.Equal(1, Scalar(fresh,
            "SELECT COUNT(*) FROM quizzes WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign');"));
        Assert.Equal("Level 1", Text(fresh, "SELECT title FROM quizzes;"));
        Assert.Equal("Easy", Text(fresh, "SELECT difficulty FROM quizzes;"));
        Assert.Equal("Approved", Text(fresh, "SELECT status FROM quizzes;"));
        Assert.Equal(0, Scalar(fresh, CampaignLadderMismatchSql));
        Assert.Equal(5, Scalar(fresh, "SELECT COUNT(*) FROM questions;"));
        Assert.Equal(0, Scalar(fresh,
            "SELECT COUNT(*) FROM (SELECT quiz_id FROM questions GROUP BY quiz_id HAVING COUNT(*) <> 5);"));
        Assert.Equal(20, Scalar(fresh, "SELECT COUNT(*) FROM choices;"));
        Assert.Equal(0, Scalar(fresh,
            "SELECT COUNT(*) FROM (SELECT question_id FROM choices GROUP BY question_id " +
            "HAVING COUNT(*) <> 4 OR SUM(is_correct) <> 1);"));
        Assert.Equal(0, Scalar(fresh,
            "SELECT COUNT(*) FROM questions q JOIN quizzes z ON z.quiz_id = q.quiz_id WHERE q.difficulty <> z.difficulty;"));

        DatabaseMigrator.EnsureSchema(fresh);

        Assert.Equal(1, Scalar(fresh, "SELECT COUNT(*) FROM topics;"));
        Assert.Equal(1, Scalar(fresh, "SELECT COUNT(*) FROM quizzes;"));
        Assert.Equal(5, Scalar(fresh, "SELECT COUNT(*) FROM questions;"));
        Assert.Equal(20, Scalar(fresh, "SELECT COUNT(*) FROM choices;"));
    }

    // -------------------------------------------------------------------------
    // 6. The campaign script must ship INSIDE the application: the installed
    //    app has no database/ folder, so the migrator reads the embedded
    //    resource (the file on disk is only the build-time fallback).
    // -------------------------------------------------------------------------
    [Fact]
    public void CampaignScript_IsEmbeddedInTheApplicationUnderTheExpectedResourceName()
    {
        string[] resourceNames = typeof(DatabaseMigrator).Assembly.GetManifestResourceNames();

        Assert.Contains("CodeQuest.Database.campaign.sql", resourceNames);
    }

    // -------------------------------------------------------------------------
    // 7. The build-time path (CI): schema.sql + seed.sql + campaign.sql must
    //    produce the shipped database — the demo login user and exactly the
    //    single built-in campaign level, with no pre-campaign quizzes left.
    // -------------------------------------------------------------------------
    [Fact]
    public void SeedAndCampaignScripts_BuildTheShippedDatabaseContents()
    {
        Db fresh = CreateScratchDatabase("scripts");
        ExecuteScript(fresh, ReadScript("schema.sql"));
        ExecuteScript(fresh, ReadScript("seed.sql"));
        ExecuteScript(fresh, ReadScript("campaign.sql"));

        Assert.Equal(1, Scalar(fresh, "SELECT COUNT(*) FROM users WHERE username = 'demo';"));
        Assert.Equal(1, Scalar(fresh, "SELECT COUNT(*) FROM quizzes WHERE status = 'Approved';"));
        Assert.Equal("Level 1", Text(fresh, "SELECT title FROM quizzes;"));
        Assert.Equal(5, Scalar(fresh, "SELECT COUNT(*) FROM questions;"));
        Assert.Equal(0, Scalar(fresh, "SELECT COUNT(*) FROM quizzes WHERE title NOT LIKE 'Level %';"));
    }

    // -------------------------------------------------------------------------
    // 8. Task 5, the title-collision case: another topic already owns a quiz
    //    titled "Level 1", and the Campaign topic already owns a Draft
    //    "Level 1" with the wrong difficulty. EnsureSchema must still deliver
    //    the built-in campaign level — the Campaign row repaired to
    //    Approved/Easy, nothing beyond it — and leave the foreign quiz
    //    completely untouched.
    // -------------------------------------------------------------------------
    [Fact]
    public void EnsureSchema_WithAForeignLevelTitleAndADraftCampaignLevel_RepairsAndCompletesTheCampaign()
    {
        Db legacy = CreateScratchDatabase("collision");
        ExecuteScript(legacy, LegacyPreCampaignSchemaSql);
        ExecuteScript(
            legacy,
            """
            INSERT INTO users (user_id, username, password_hash, display_name, created_at) VALUES
                (1, 'legacy', '', 'Legacy Student', '2026-11-01T09:00:00Z');
            INSERT INTO topics (topic_id, name, category, description, created_at) VALUES
                (1, 'Legacy Topic', 'Programming', 'A topic that already owns a Level 1 title.', '2026-11-01T09:00:00Z'),
                (2, 'Campaign', 'Programming', 'The progressive campaign.', '2026-11-01T09:00:00Z');
            INSERT INTO quizzes (quiz_id, topic_id, title, difficulty, status, created_at) VALUES
                (1, 1, 'Level 1', 'Medium', 'Approved', '2026-11-01T09:10:00Z'),
                (2, 2, 'Level 1', 'Hard', 'Draft', '2026-11-01T09:20:00Z');
            INSERT INTO questions (question_id, quiz_id, question_text, explanation, position, created_at) VALUES
                (1, 2, 'Pre-existing draft question?', 'Kept as the level content seed.', 1, '2026-11-01T09:20:00Z');
            """);

        DatabaseMigrator.EnsureSchema(legacy);

        // The foreign quiz is untouched: same topic, values and no duplicate.
        Assert.Equal(1, Scalar(legacy, "SELECT COUNT(*) FROM quizzes WHERE topic_id = 1 AND title = 'Level 1';"));
        Assert.Equal("Medium", Text(legacy, "SELECT difficulty FROM quizzes WHERE quiz_id = 1;"));
        Assert.Equal("Approved", Text(legacy, "SELECT status FROM quizzes WHERE quiz_id = 1;"));
        Assert.Equal("2026-11-01T09:10:00Z", Text(legacy, "SELECT created_at FROM quizzes WHERE quiz_id = 1;"));

        // The Campaign topic holds exactly one Level 1 — the repaired draft.
        Assert.Equal(1, Scalar(legacy, "SELECT COUNT(*) FROM quizzes WHERE topic_id = 2 AND title = 'Level 1';"));
        Assert.Equal(2, Scalar(legacy, "SELECT quiz_id FROM quizzes WHERE topic_id = 2 AND title = 'Level 1';"));
        Assert.Equal("Easy", Text(legacy, "SELECT difficulty FROM quizzes WHERE quiz_id = 2;"));
        Assert.Equal("Approved", Text(legacy, "SELECT status FROM quizzes WHERE quiz_id = 2;"));

        // The campaign holds exactly its single built-in level, approved.
        Assert.Equal(1, Scalar(legacy, "SELECT COUNT(*) FROM quizzes WHERE topic_id = 2 AND status = 'Approved';"));
        Assert.Equal(0, Scalar(legacy, CampaignLadderMismatchSql));

        // The level has its 5 questions and 20 choices; the pre-existing draft
        // question was kept and aligned to the ladder.
        Assert.Equal(5, Scalar(legacy, "SELECT COUNT(*) FROM questions;"));
        Assert.Equal(5, Scalar(legacy, "SELECT COUNT(*) FROM questions WHERE quiz_id = 2;"));
        Assert.Equal(20, Scalar(legacy,
            "SELECT COUNT(*) FROM choices WHERE question_id IN (SELECT question_id FROM questions WHERE quiz_id = 2);"));
        Assert.Equal(0, Scalar(legacy,
            "SELECT COUNT(*) FROM questions q JOIN quizzes z ON z.quiz_id = q.quiz_id WHERE q.difficulty <> z.difficulty;"));

        // The second run changes nothing.
        DatabaseMigrator.EnsureSchema(legacy);

        Assert.Equal(2, Scalar(legacy, "SELECT COUNT(*) FROM quizzes;"));
        Assert.Equal(5, Scalar(legacy, "SELECT COUNT(*) FROM questions;"));
        Assert.Equal(1, Scalar(legacy, "SELECT COUNT(*) FROM quizzes WHERE topic_id = 1 AND title = 'Level 1';"));
    }

    // -------------------------------------------------------------------------
    // 9. The corrupt-legacy-row case: a Campaign-topic Draft "Level 1" whose
    //    choice text collides with a campaign choice text aborts the campaign
    //    script mid-way. EnsureSchema must NOT throw — the campaign is content,
    //    not structure: the app starts, the structural migrations stay applied,
    //    and the next launch retries the campaign.
    // -------------------------------------------------------------------------
    [Fact]
    public void EnsureSchema_WithACorruptCampaignRow_DoesNotThrowAndKeepsTheAppSchema()
    {
        Db legacy = CreateScratchDatabase("corrupt");
        ExecuteScript(legacy, LegacyPreCampaignSchemaSql);
        ExecuteScript(
            legacy,
            """
            INSERT INTO topics (topic_id, name, category, description, created_at) VALUES
                (1, 'Campaign', 'Programming', 'The progressive campaign.', '2026-11-01T09:00:00Z');
            INSERT INTO quizzes (quiz_id, topic_id, title, difficulty, status, created_at) VALUES
                (1, 1, 'Level 1', 'Medium', 'Draft', '2026-11-01T09:10:00Z');
            INSERT INTO questions (question_id, quiz_id, question_text, explanation, position, created_at) VALUES
                (1, 1, 'Corrupt first question?', 'Its choice text collides with the campaign key.', 1, '2026-11-01T09:10:00Z');
            INSERT INTO choices (choice_id, question_id, choice_text, is_correct, position) VALUES
                (1, 1, 'const', 0, 1);
            """);

        // The run repairs the draft, inserts the campaign questions, then the
        // Level 1 choice insert collides with the existing 'const' text; the
        // shared transaction rolls the campaign steps back. This must not
        // throw (the app must still start).
        DatabaseMigrator.EnsureSchema(legacy);

        // The structural migrations ran OUTSIDE the guard and stayed applied.
        Assert.Contains("'Insane'", ReadCreateSql(legacy, "quizzes"), StringComparison.Ordinal);
        Assert.Equal(1, Scalar(legacy, "SELECT COUNT(*) FROM pragma_table_info('questions') WHERE name = 'difficulty';"));
        Assert.Equal("Medium", Text(legacy, "SELECT difficulty FROM questions WHERE question_id = 1;"));
        Assert.Equal(0, Scalar(legacy, "SELECT COUNT(*) FROM pragma_foreign_key_check;"));

        // The campaign content is one transaction, so the failure rolled it
        // ALL back: the corrupt row is exactly as it was and no campaign step
        // landed. Partial application is possible in principle; this rollback
        // is the accepted behavior — every launch retries the whole script.
        Assert.Equal(1, Scalar(legacy, "SELECT COUNT(*) FROM topics WHERE name = 'Campaign';"));
        Assert.Equal(1, Scalar(legacy, "SELECT COUNT(*) FROM quizzes WHERE topic_id = 1;"));
        Assert.Equal("Draft", Text(legacy, "SELECT status FROM quizzes WHERE quiz_id = 1;"));
        Assert.Equal("Medium", Text(legacy, "SELECT difficulty FROM quizzes WHERE quiz_id = 1;"));
        Assert.Equal(0, Scalar(legacy, "SELECT COUNT(*) FROM quizzes WHERE title = 'Level 2';"));

        // The retry (every launch) is safe and still never throws.
        DatabaseMigrator.EnsureSchema(legacy);

        Assert.Equal(1, Scalar(legacy, "SELECT COUNT(*) FROM quizzes WHERE topic_id = 1;"));
        Assert.Equal("Draft", Text(legacy, "SELECT status FROM quizzes WHERE quiz_id = 1;"));
        Assert.Equal(1, Scalar(legacy, "SELECT COUNT(*) FROM pragma_table_info('questions') WHERE name = 'difficulty';"));
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private const string CampaignLadderMismatchSql =
        """
        SELECT COUNT(*) FROM quizzes z
        WHERE z.topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
          AND ((CAST(substr(z.title, 7) AS INTEGER) BETWEEN 1 AND 5 AND z.difficulty <> 'Easy')
            OR (CAST(substr(z.title, 7) AS INTEGER) BETWEEN 6 AND 12 AND z.difficulty <> 'Medium')
            OR (CAST(substr(z.title, 7) AS INTEGER) BETWEEN 13 AND 19 AND z.difficulty <> 'Hard')
            OR (CAST(substr(z.title, 7) AS INTEGER) BETWEEN 20 AND 25 AND z.difficulty <> 'Insane'));
        """;

    // The exact pre-task-5 (1.4.0) schema: the old 3-value quizzes CHECK and
    // no questions.difficulty column — everything else unchanged.
    private const string LegacyPreCampaignSchemaSql =
        """
        CREATE TABLE users (
            user_id       INTEGER PRIMARY KEY AUTOINCREMENT,
            username      TEXT    NOT NULL UNIQUE,
            password_hash TEXT    NOT NULL,
            display_name  TEXT    NOT NULL,
            created_at    TEXT    NOT NULL
        );

        CREATE TABLE topics (
            topic_id    INTEGER PRIMARY KEY AUTOINCREMENT,
            name        TEXT    NOT NULL UNIQUE,
            category    TEXT    NOT NULL CHECK (category IN ('Programming', 'Cybersecurity')),
            description TEXT    NOT NULL,
            created_at  TEXT    NOT NULL
        );

        CREATE TABLE quizzes (
            quiz_id    INTEGER PRIMARY KEY AUTOINCREMENT,
            topic_id   INTEGER NOT NULL,
            title      TEXT    NOT NULL,
            difficulty TEXT    NOT NULL CHECK (difficulty IN ('Easy', 'Medium', 'Hard')),
            status     TEXT    NOT NULL DEFAULT 'Draft' CHECK (status IN ('Draft', 'Approved')),
            created_at TEXT    NOT NULL,
            CONSTRAINT fk_quizzes_topic FOREIGN KEY (topic_id) REFERENCES topics (topic_id) ON DELETE RESTRICT,
            CONSTRAINT uq_quizzes_topic_title UNIQUE (topic_id, title)
        );

        CREATE TABLE questions (
            question_id   INTEGER PRIMARY KEY AUTOINCREMENT,
            quiz_id       INTEGER NOT NULL,
            question_text TEXT    NOT NULL,
            explanation   TEXT    NOT NULL,
            position      INTEGER NOT NULL CHECK (position >= 1),
            created_at    TEXT    NOT NULL,
            CONSTRAINT fk_questions_quiz FOREIGN KEY (quiz_id) REFERENCES quizzes (quiz_id) ON DELETE CASCADE,
            CONSTRAINT uq_questions_quiz_position UNIQUE (quiz_id, position)
        );

        CREATE TABLE choices (
            choice_id   INTEGER PRIMARY KEY AUTOINCREMENT,
            question_id INTEGER NOT NULL,
            choice_text TEXT    NOT NULL,
            is_correct  INTEGER NOT NULL DEFAULT 0 CHECK (is_correct IN (0, 1)),
            position    INTEGER NOT NULL CHECK (position >= 1),
            CONSTRAINT fk_choices_question FOREIGN KEY (question_id) REFERENCES questions (question_id) ON DELETE CASCADE,
            CONSTRAINT uq_choices_question_text UNIQUE (question_id, choice_text),
            CONSTRAINT uq_choices_question_choice UNIQUE (question_id, choice_id)
        );

        CREATE TABLE quiz_attempts (
            attempt_id   INTEGER PRIMARY KEY AUTOINCREMENT,
            quiz_id      INTEGER NOT NULL,
            user_id      INTEGER NOT NULL,
            score        INTEGER NOT NULL CHECK (score >= 0),
            attempted_at TEXT    NOT NULL,
            CONSTRAINT fk_quiz_attempts_quiz FOREIGN KEY (quiz_id) REFERENCES quizzes (quiz_id) ON DELETE RESTRICT,
            CONSTRAINT fk_quiz_attempts_user FOREIGN KEY (user_id) REFERENCES users (user_id) ON DELETE RESTRICT
        );

        CREATE TABLE submitted_answers (
            attempt_id       INTEGER NOT NULL,
            question_id      INTEGER NOT NULL,
            chosen_choice_id INTEGER NOT NULL,
            PRIMARY KEY (attempt_id, question_id),
            CONSTRAINT fk_submitted_answers_attempt FOREIGN KEY (attempt_id) REFERENCES quiz_attempts (attempt_id) ON DELETE CASCADE,
            CONSTRAINT fk_submitted_answers_question FOREIGN KEY (question_id) REFERENCES questions (question_id) ON DELETE RESTRICT,
            CONSTRAINT fk_submitted_answers_choice FOREIGN KEY (chosen_choice_id) REFERENCES choices (choice_id) ON DELETE RESTRICT,
            CONSTRAINT fk_submitted_answers_choice_question FOREIGN KEY (question_id, chosen_choice_id)
                REFERENCES choices (question_id, choice_id) ON DELETE RESTRICT
        );

        CREATE TABLE user_achievements (
            user_id         INTEGER NOT NULL,
            achievement_key TEXT    NOT NULL,
            unlocked_at     TEXT    NOT NULL,
            PRIMARY KEY (user_id, achievement_key),
            CONSTRAINT fk_user_achievements_user FOREIGN KEY (user_id) REFERENCES users (user_id) ON DELETE CASCADE
        );

        CREATE INDEX idx_quizzes_topic_id              ON quizzes (topic_id);
        CREATE INDEX idx_questions_quiz_id             ON questions (quiz_id);
        CREATE INDEX idx_choices_question_id           ON choices (question_id);
        CREATE INDEX idx_quiz_attempts_quiz_id         ON quiz_attempts (quiz_id);
        CREATE INDEX idx_quiz_attempts_user_id         ON quiz_attempts (user_id);
        CREATE INDEX idx_submitted_answers_attempt_id  ON submitted_answers (attempt_id);
        CREATE INDEX idx_submitted_answers_question_id ON submitted_answers (question_id);
        CREATE INDEX idx_submitted_answers_choice_id   ON submitted_answers (chosen_choice_id);
        CREATE INDEX idx_user_achievements_user_id     ON user_achievements (user_id);
        """;

    /// <summary>Runs one scalar query on the given database and returns it as a long.</summary>
    private static long Scalar(Db database, string sql)
    {
        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar());
    }

    /// <summary>Runs one scalar query on the given database and returns it as text.</summary>
    private static string? Text(Db database, string sql)
    {
        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        object? value = command.ExecuteScalar();
        return value is null or DBNull ? null : Convert.ToString(value);
    }

    /// <summary>Executes one literal SQL statement through a connection of the given database.</summary>
    private static void Execute(Db database, string sql)
    {
        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    /// <summary>Executes one SQL script as one batch through a connection of the given database.</summary>
    private static void ExecuteScript(Db database, string script)
    {
        Execute(database, script);
    }

    /// <summary>Reads the stored CREATE TABLE text of one table.</summary>
    private static string ReadCreateSql(Db database, string table)
        => Text(database, $"SELECT sql FROM sqlite_master WHERE type = 'table' AND name = '{table}';") ?? string.Empty;

    /// <summary>Runs one scalar query and returns it as a long.</summary>
    private long Scalar(string sql)
    {
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar());
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

    /// <summary>
    /// Reads the frozen pre-campaign demo fixture (tests/CodeQuest.Tests/Fixtures)
    /// — the rich sample data the row-preservation expectations are built on.
    /// </summary>
    private static string ReadFixture(string fileName)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "tests", "CodeQuest.Tests", "Fixtures", fileName);

            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"The SQL fixture '{fileName}' was not found in the tests/CodeQuest.Tests/Fixtures folder above the test directory.");
    }
}
