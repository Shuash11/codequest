// =============================================================================
// CodeQuest — Phase P4: Data Access Layer
// OOP rubric: ENCAPSULATION — the database file, the connection settings and the
//              transaction boundaries live inside this class; nothing else in the
//              project opens a connection or starts a transaction.
// Look at: OpenConnection() below — the ONLY place where a SqliteConnection is
//          created. Every connection runs "PRAGMA foreign_keys = ON;" before it
//          is handed out, because SQLite keeps foreign keys OFF per connection
//          by default (https://sqlite.org/foreignkeys.html section 2). Without
//          that line, deleting a topic that still owns quizzes would silently
//          orphan the quizzes instead of being refused by the database.
//          RunInTransaction() below — the one place where a unit of work gets
//          its transaction boundary, its rollback and its error mapping.
// Config:   the database path is READ (never written) from
//          config/config.local.json, key "databasePath"
//          (DatabasePathConfigKey). That file does not carry the key today, so
//          the fallback is the application-local file "codequest.db" in the
//          application root — the folder that holds the "config" folder (the
//          repository root during development) or, in a published build, the
//          folder of the executable. A relative "databasePath" is resolved
//          against the application root.
//          Phase P5 formalises the config loading (Services/AppConfig.cs); the
//          convention documented here is the contract that phase builds on.
// Note:    this class never creates tables and never seeds data — the database
//          file is built by running database/schema.sql and database/seed.sql
//          against it (see the header of database/schema.sql). The one
//          sanctioned exception is Data/DatabaseMigrator.EnsureSchema(), whose
//          idempotent CREATE TABLE IF NOT EXISTS migration adds the
//          user_achievements table to existing databases at startup without
//          touching any existing row.
// =============================================================================

using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace CodeQuest.Data;

/// <summary>
/// The connection factory of the application. It owns the path of the SQLite
/// file, opens every connection with foreign keys switched on, and runs the
/// multi-row writes inside a transaction.
/// </summary>
public sealed class Db
{
    /// <summary>The key of the database path inside config/config.local.json.</summary>
    public const string DatabasePathConfigKey = "databasePath";

    /// <summary>The file name of the database when the config does not name one.</summary>
    public const string DefaultDatabaseFileName = "codequest.db";

    /// <summary>The config file that may carry the database path, relative to the application root.</summary>
    public const string ConfigFileRelativePath = "config/config.local.json";

    private readonly string _databasePath;

    /// <summary>
    /// Creates the factory for one concrete database file. The tests use this
    /// constructor to point at their own scratch database.
    /// </summary>
    /// <param name="databasePath">The path of the SQLite file.</param>
    public Db(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("The database path cannot be empty.", nameof(databasePath));
        }

        _databasePath = Path.GetFullPath(databasePath);
    }

    /// <summary>
    /// Creates the factory for the database of the running application: the path
    /// from the config file, or the application-local default when the config
    /// does not carry one.
    /// </summary>
    public Db()
        : this(ResolveDatabasePath())
    {
    }

    /// <summary>The full path of the SQLite file this factory works with.</summary>
    public string DatabasePath => _databasePath;

    /// <summary>
    /// Opens a connection to the database with foreign keys switched ON. This is
    /// the only way to get a connection — no repository creates one itself.
    /// </summary>
    /// <returns>An open connection; the caller disposes it.</returns>
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

            // Foreign keys are OFF by default per connection — switch them ON for
            // THIS connection, every single time one is opened.
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

    /// <summary>
    /// Runs one unit of work inside a transaction: the work is committed when it
    /// returns and rolled back when it fails, so a half-written quiz or a
    /// half-written attempt never stays in the database.
    /// A SQLite failure is turned into the typed error of the data layer
    /// (see <see cref="DataAccessException.FromSqlite"/>), so the caller can
    /// catch a duplicate or a broken reference instead of a raw SqliteException.
    /// </summary>
    /// <typeparam name="T">What the unit of work returns.</typeparam>
    /// <param name="work">The work to run; it receives the connection and the transaction.</param>
    /// <returns>What the unit of work returned.</returns>
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

    /// <summary>
    /// Rolls the transaction back, ignoring a rollback that the database itself
    /// refuses. The original error — the one that matters — is rethrown by the
    /// caller of <see cref="RunInTransaction{T}"/>.
    /// </summary>
    private static void RollbackQuietly(SqliteTransaction transaction)
    {
        try
        {
            transaction.Rollback();
        }
        catch (InvalidOperationException)
        {
            // The transaction was already finished (committed or rolled back).
        }
        catch (SqliteException)
        {
            // The database refuses the rollback; the original error is reported.
        }
    }

    /// <summary>
    /// Resolves the path of the database file: the "databasePath" entry of
    /// config/config.local.json when it carries one, otherwise the
    /// application-local default file.
    /// </summary>
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

    /// <summary>
    /// Finds the application root: the closest folder above the running program
    /// that holds the "config" folder. During development that is the
    /// repository root; in a published build it is the folder of the executable
    /// when the config was shipped next to it, otherwise the folder of the
    /// executable itself.
    /// </summary>
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

    /// <summary>
    /// Reads the "databasePath" entry of config/config.local.json. The file is
    /// only read — this class never creates or changes it. A missing file, a
    /// missing entry or a malformed file simply means "use the default path".
    /// </summary>
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
            // A malformed config file must not stop the application: the
            // application-local default path is used instead.
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
