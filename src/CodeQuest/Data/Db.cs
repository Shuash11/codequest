// Opens the SQLite database.

using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace CodeQuest.Data;

public sealed class Db
{
    public const string DatabasePathConfigKey = "databasePath";

    public const string DefaultDatabaseFileName = "codequest.db";

    public const string ConfigFileRelativePath = "config/config.local.json";

    private readonly string _databasePath;

    public Db(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("The database path cannot be empty.", nameof(databasePath));
        }

        _databasePath = Path.GetFullPath(databasePath);
    }

    public Db()
        : this(ResolveDatabasePath())
    {
    }

    public string DatabasePath => _databasePath;

    public SqliteConnection OpenConnection()
    {
        SqliteConnectionStringBuilder builder = new()
        {
            DataSource = _databasePath,
        };

        SqliteConnection connection = new(builder.ToString());

        try
        {
            connection.Open();

            using SqliteCommand pragma = connection.CreateCommand();
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();

            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    public T RunInTransaction<T>(Func<SqliteConnection, SqliteTransaction, T> work)
    {
        ArgumentNullException.ThrowIfNull(work);

        using SqliteConnection connection = OpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();

        try
        {
            T result = work(connection, transaction);
            transaction.Commit();
            return result;
        }
        catch (SqliteException exception)
        {
            RollbackQuietly(transaction);
            throw DataAccessException.FromSqlite(exception);
        }
        catch
        {
            RollbackQuietly(transaction);
            throw;
        }
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

    private static string ResolveDatabasePath()
    {
        string applicationRoot = FindApplicationRoot();
        string? configuredPath = ReadDatabasePathFromConfig(applicationRoot);

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(applicationRoot, configuredPath);
        }

        return Path.Combine(applicationRoot, DefaultDatabaseFileName);
    }

    private static string FindApplicationRoot()
    {
        foreach (string start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            DirectoryInfo? directory = new(start);

            while (directory is not null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "config")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        return AppContext.BaseDirectory;
    }

    private static string? ReadDatabasePathFromConfig(string applicationRoot)
    {
        string configPath = Path.Combine(applicationRoot, "config", "config.local.json");

        if (!File.Exists(configPath))
        {
            return null;
        }

        try
        {
            using FileStream stream = File.OpenRead(configPath);
            using JsonDocument document = JsonDocument.Parse(stream);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!document.RootElement.TryGetProperty(DatabasePathConfigKey, out JsonElement entry))
            {
                return null;
            }

            if (entry.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            string? path = entry.GetString();
            return string.IsNullOrWhiteSpace(path) ? null : path.Trim();
        }
        catch (JsonException)
        {
            return null;
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
}
