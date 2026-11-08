// =============================================================================
// CodeQuest — Gamification Foundation (plan WP1, acceptance A7)
// The idempotent startup migration. Existing databases were built by running
// database/schema.sql at a time when user_achievements did not exist; this
// migrator adds the table (and its index) at every launch with
// CREATE TABLE IF NOT EXISTS / CREATE INDEX IF NOT EXISTS statements, so:
//   * a database that already has the table (fresh install via schema.sql, or
//     a previous launch) is left exactly as it is,
//   * no existing row is ever modified or deleted — the attempts that feed the
//     scoreboard must survive (plan A7),
//   * calling it on every launch is safe and cheap (plan section 5).
// Look at: EnsureSchema() — the two DDL statements run as ONE unit of work in
//          Db.RunInTransaction, which gives the pair a single atomic commit
//          and maps a SQL failure to the typed data errors
//          (DataAccessException.FromSqlite) instead of leaking SqliteException.
// Note:    this is the ONE sanctioned place outside database/schema.sql that
//          creates a table (see the header note of Data/Db.cs).
// =============================================================================

using Microsoft.Data.Sqlite;

namespace CodeQuest.Data;

/// <summary>
/// Adds the gamification tables to an existing database without touching any
/// existing row. Safe to call on every launch.
/// </summary>
public static class DatabaseMigrator
{
    /// <summary>
    /// The unlock records: one row per unlocked achievement of one user.
    /// Same DDL as database/schema.sql (the fresh-install script).
    /// </summary>
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

    /// <summary>The foreign-key index of the table (schema.sql indexes block).</summary>
    private const string EnsureAchievementsIndexSql =
        "CREATE INDEX IF NOT EXISTS idx_user_achievements_user_id ON user_achievements (user_id);";

    /// <summary>
    /// Adds the <c>user_achievements</c> table and its index when the database
    /// does not have them yet. Never modifies or deletes an existing row.
    /// </summary>
    /// <param name="database">The connection factory of the database to migrate.</param>
    public static void EnsureSchema(Db database)
    {
        ArgumentNullException.ThrowIfNull(database);

        // The two statements are one unit of work: either both land or (on a
        // failure) neither does. RunInTransaction also maps every SQL failure
        // to the typed data errors of the data layer.
        _ = database.RunInTransaction<object?>((connection, transaction) =>
        {
            Execute(connection, transaction, EnsureAchievementsTableSql);
            Execute(connection, transaction, EnsureAchievementsIndexSql);
            return null;
        });
    }

    /// <summary>Runs one DDL statement inside the migration transaction.</summary>
    private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        command.ExecuteNonQuery();
    }
}
