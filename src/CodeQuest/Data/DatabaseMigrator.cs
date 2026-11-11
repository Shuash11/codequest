// Adds missing tables to old databases safely.

using Microsoft.Data.Sqlite;

namespace CodeQuest.Data;

public static class DatabaseMigrator
{
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

    public static void EnsureSchema(Db database)
    {
        ArgumentNullException.ThrowIfNull(database);

        _ = database.RunInTransaction<object?>((connection, transaction) =>
        {
            Execute(connection, transaction, EnsureAchievementsTableSql);
            Execute(connection, transaction, EnsureAchievementsIndexSql);
            return null;
        });
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        command.ExecuteNonQuery();
    }
}
