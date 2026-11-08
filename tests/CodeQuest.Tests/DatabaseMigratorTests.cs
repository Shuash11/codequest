// =============================================================================
// CodeQuest — Gamification Foundation (plan WP1, acceptance A7/A8)
// Tests for: DatabaseMigrator (the idempotent startup migration), run against
// a FRESH temporary SQLite database built the same way RepositoryTests builds
// its own (schema.sql + seed.sql, read as text and executed unchanged).
// Look at: the "legacy database" case — schema.sql of THIS release already
//          contains user_achievements, so the test drops the table again to
//          reconstruct the exact pre-gamification state an existing
//          installation is in; the migrator must add the table without
//          touching the seeded rows (users, attempts, answers), must survive
//          a second run, and must leave a table whose foreign key to users is
//          ENFORCED — an unlock for an unknown user fails with the TYPED data
//          error (the mapping of Db.RunInTransaction), never a raw
//          SqliteException.
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
    private readonly string _databasePath;
    private readonly Db _db;

    /// <summary>
    /// Builds one fresh seeded database for THIS test (schema.sql + seed.sql
    /// through a connection of the data layer), like RepositoryTests does.
    /// </summary>
    public DatabaseMigratorTests()
    {
        string scratchDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Temp", "opencode");
        Directory.CreateDirectory(scratchDirectory);

        _databasePath = Path.Combine(scratchDirectory, $"codequest-migrator-tests-{Guid.NewGuid():N}.db");
        _db = new Db(_databasePath);

        ExecuteScript(ReadScript("schema.sql"));
        ExecuteScript(ReadScript("seed.sql"));
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
    //    migrator must be a no-op on it (safe to call unconditionally).
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
    // Helpers
    // -------------------------------------------------------------------------

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
}
