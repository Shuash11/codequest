// Adds missing tables to old databases safely.

using Microsoft.Data.Sqlite;

namespace CodeQuest.Data;

public static class DatabaseMigrator
{
    private const string CampaignResourceName = "CodeQuest.Database.campaign.sql";

    private const string EnsureAchievementsTableSql =
        """
        CREATE TABLE IF NOT EXISTS user_achievements (
            user_id         INTEGER NOT NULL,
            achievement_key TEXT    NOT NULL,
            unlocked_at     TEXT    NOT NULL,

            PRIMARY KEY (user_id, achievement_key),

            CONSTRAINT fk_user_achievements_user
                FOREIGN KEY (user_id) REFERENCES users (user_id) ON DELETE CASCADE
        );
        """;

    private const string EnsureAchievementsIndexSql =
        "CREATE INDEX IF NOT EXISTS idx_user_achievements_user_id ON user_achievements (user_id);";

    private const string AddQuestionDifficultySql =
        "ALTER TABLE questions ADD COLUMN difficulty TEXT NOT NULL DEFAULT 'Medium';";

    private const string BackfillQuestionDifficultySql =
        """
        UPDATE questions
        SET difficulty = (SELECT difficulty FROM quizzes WHERE quizzes.quiz_id = questions.quiz_id)
        WHERE difficulty = 'Medium';
        """;

    private const string SelectQuizzesCreateSql =
        "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = 'quizzes';";

    // The quizzes CHECK rebuild (task 5): the new table mirrors the current
    // schema exactly, with 'Insane' added to the difficulty CHECK.
    private static readonly string[] RebuildQuizzesStatements =
    {
        """
        CREATE TABLE quizzes_new (
            quiz_id    INTEGER PRIMARY KEY AUTOINCREMENT,
            topic_id   INTEGER NOT NULL,
            title      TEXT    NOT NULL,
            difficulty TEXT    NOT NULL CHECK (difficulty IN ('Easy', 'Medium', 'Hard', 'Insane')),
            status     TEXT    NOT NULL DEFAULT 'Draft' CHECK (status IN ('Draft', 'Approved')),
            created_at TEXT    NOT NULL,

            CONSTRAINT fk_quizzes_topic
                FOREIGN KEY (topic_id) REFERENCES topics (topic_id) ON DELETE RESTRICT,

            CONSTRAINT uq_quizzes_topic_title UNIQUE (topic_id, title)
        );
        """,
        "INSERT INTO quizzes_new SELECT quiz_id, topic_id, title, difficulty, status, created_at FROM quizzes;",
        "DROP TABLE quizzes;",
        "ALTER TABLE quizzes_new RENAME TO quizzes;",
        "CREATE INDEX idx_quizzes_topic_id ON quizzes (topic_id);",
    };

    public static void EnsureSchema(Db database)
    {
        ArgumentNullException.ThrowIfNull(database);

        EnsureAchievementsTable(database);
        EnsureQuestionDifficultyColumn(database);
        EnsureQuizOwnershipColumn(database);
        EnsureInsaneDifficultyAccepted(database);
        EnsureCampaign(database);
    }

    // Ownership (task 5): the AI rival stamps the player a generated level
    // belongs to; the column is added once and defaults to NULL (shared
    // content: built-in levels, manual quizzes and the Random Mix).
    private static void EnsureQuizOwnershipColumn(Db database)
    {
        using SqliteConnection connection = database.OpenConnection();

        if (ColumnExists(connection, "quizzes", "generated_for_user_id"))
        {
            return;
        }

        using SqliteTransaction transaction = connection.BeginTransaction();

        try
        {
            Execute(
                connection,
                transaction,
                "ALTER TABLE quizzes ADD COLUMN generated_for_user_id INTEGER NULL;");
            transaction.Commit();
        }
        catch (SqliteException exception)
        {
            RollbackQuietly(transaction);
            throw DataAccessException.FromSqlite(exception);
        }
    }

    private static void EnsureAchievementsTable(Db database)
    {
        _ = database.RunInTransaction<object?>((connection, transaction) =>
        {
            Execute(connection, transaction, EnsureAchievementsTableSql);
            Execute(connection, transaction, EnsureAchievementsIndexSql);
            return null;
        });
    }

    private static void EnsureQuestionDifficultyColumn(Db database)
    {
        using SqliteConnection connection = database.OpenConnection();

        if (ColumnExists(connection, "questions", "difficulty"))
        {
            return;
        }

        using SqliteTransaction transaction = connection.BeginTransaction();

        try
        {
            Execute(connection, transaction, AddQuestionDifficultySql);
            Execute(connection, transaction, BackfillQuestionDifficultySql);
            transaction.Commit();
        }
        catch (SqliteException exception)
        {
            RollbackQuietly(transaction);
            throw DataAccessException.FromSqlite(exception);
        }
    }

    private static void EnsureInsaneDifficultyAccepted(Db database)
    {
        using SqliteConnection connection = database.OpenConnection();

        string? createSql = ReadQuizzesCreateSql(connection);
        if (createSql is null || createSql.Contains("Insane", StringComparison.Ordinal))
        {
            return;
        }

        // PRAGMAs are transaction-agnostic: they must be set on the connection
        // BEFORE the rebuild, and foreign_keys must be OFF while the quizzes
        // table is dropped and renamed. legacy_alter_table keeps the RENAME
        // from rewriting the other tables' foreign-key clauses.
        ExecutePragma(connection, "PRAGMA foreign_keys = OFF;");
        ExecutePragma(connection, "PRAGMA legacy_alter_table = ON;");

        try
        {
            using SqliteTransaction transaction = connection.BeginTransaction();

            try
            {
                foreach (string statement in RebuildQuizzesStatements)
                {
                    Execute(connection, transaction, statement);
                }

                transaction.Commit();
            }
            catch (SqliteException exception)
            {
                RollbackQuietly(transaction);
                throw DataAccessException.FromSqlite(exception);
            }

            // Diagnostic only: the rebuild must never orphan a row, so the
            // check is run and its (empty) result is ignored.
            ExecutePragma(connection, "PRAGMA foreign_key_check;");
        }
        finally
        {
            ExecutePragma(connection, "PRAGMA foreign_keys = ON;");
            ExecutePragma(connection, "PRAGMA legacy_alter_table = OFF;");
        }
    }

    // Ensures the built-in campaign: the Campaign topic and its Level 1 only —
    // every later level is generated by the AI rival. Unplayed leftovers of
    // the old built-in set (Levels 2..25 from before the owner directive) are
    // DELETED here — the rival regenerates the frontier level on demand, so
    // nothing is lost. Levels the player already attempted are history and
    // stay (the attempt FK would also refuse their deletion).
    private static void EnsureCampaign(Db database)
    {
        try
        {
            string? campaignScript = ReadCampaignScript();
            if (string.IsNullOrWhiteSpace(campaignScript))
            {
                return; // a missing campaign script never blocks startup
            }

            _ = database.RunInTransaction<object?>((connection, transaction) =>
            {
                Execute(connection, transaction, campaignScript);
                DeleteUnplayedCampaignLevels(connection, transaction);
                return null;
            });
        }
        catch (Exception)
        {
            // Content only, never structure: a malformed legacy row can abort
            // the script (the shared transaction rolls the campaign back, so
            // the next launch retries it). The app must never fail to start
            // for a content problem.
        }
    }

    // Deletes Campaign levels numbered 2 or higher that the player never
    // attempted AND that carry the built-in seed's fixed timestamps — the
    // leftovers of the old built-in set. Levels the AI generated later use
    // UtcNow, so the frontier of the campaign is never deleted (an unplayed
    // generated level simply waits for the player). Attempted levels are
    // kept too: the quiz_attempts FK also refuses their deletion.
    private static void DeleteUnplayedCampaignLevels(
        SqliteConnection connection, SqliteTransaction transaction)
    {
        const string deleteSql = """
            DELETE FROM quizzes
            WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
              AND title LIKE 'Level %'
              AND CAST(SUBSTR(title, 7) AS INTEGER) >= 2
              AND created_at IN ('2026-11-12T08:59:00Z', '2026-11-12T09:00:00Z')
              AND NOT EXISTS (SELECT 1 FROM quiz_attempts a WHERE a.quiz_id = quizzes.quiz_id);
            """;
        Execute(connection, transaction, deleteSql);
    }

    private static string? ReadCampaignScript()
    {
        // Shipped inside the exe: the installed app has no database/ folder.
        try
        {
            using Stream? stream = typeof(DatabaseMigrator).Assembly.GetManifestResourceStream(CampaignResourceName);
            if (stream is not null)
            {
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
        }
        catch (IOException)
        {
            // fall through to the file on disk
        }

        return ReadCampaignScriptFile();
    }

    private static string? ReadCampaignScriptFile()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "database", "campaign.sql");

            if (File.Exists(candidate))
            {
                try
                {
                    return File.ReadAllText(candidate);
                }
                catch (IOException)
                {
                    return null;
                }
                catch (UnauthorizedAccessException)
                {
                    return null;
                }
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static bool ColumnExists(SqliteConnection connection, string table, string column)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({table});";

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ReadQuizzesCreateSql(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectQuizzesCreateSql;

        object? value = command.ExecuteScalar();
        return value as string;
    }

    private static void ExecutePragma(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        command.ExecuteNonQuery();
    }

    private static void RollbackQuietly(SqliteTransaction transaction)
    {
        try
        {
            transaction.Rollback();
        }
        catch (InvalidOperationException)
        {
        }
        catch (SqliteException)
        {
        }
    }
}
