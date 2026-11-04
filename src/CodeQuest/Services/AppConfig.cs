// =============================================================================
// CodeQuest — Phase P5: AI Provider Chain (configuration)
// Contract: contracts/ai-quiz-api.md section 8 — the shape of the local
//           configuration file. The file is read ONCE here and its values are
//           handed out as plain objects; nothing else in the application opens
//           the config file (the connection factory of the data layer keeps its
//           own documented read of the same file for the database path, and
//           this class reuses its key constant so the two never drift apart).
// Look at: LoadFromFile() — a missing file, a file of the pre-P2 layout (some
//           other settings key and no provider list), a malformed file or a
//           partially filled file all degrade to "no providers configured":
//           the chain then returns the typed empty-provider-list failure
//           (contract 7.6) and the offline quizzes keep working. The loader
//           NEVER throws at startup and never writes the file back.
// Secrets: the API key strings are held in memory only. They are never written
//           to a log, an exception message or any file (contract 7.8,
//           invariant 3; grep-audited in P15).
// Defaults: an entry without an "enabled" flag counts as ENABLED (ruling
//           R-3 b) — the skip rules of contract 3.2 apply on top of this.
// =============================================================================

using System.Text.Json;

namespace CodeQuest.Services;

/// <summary>
/// One entry of the configured provider chain (contract section 8): a wire
/// tier with its base URL, its secret key, its model slug and its switch.
/// </summary>
public sealed class AiProviderConfig
{
    /// <summary>Creates one provider entry from the values of the config file.</summary>
    public AiProviderConfig(string name, string baseUrl, string apiKey, string model, bool enabled)
    {
        Name = name;
        BaseUrl = baseUrl;
        ApiKey = apiKey;
        Model = model;
        Enabled = enabled;
    }

    /// <summary>The stable tier identifier used in results and reason strings (contract 2).</summary>
    public string Name { get; }

    /// <summary>The provider base URL including "/v1"; the client appends "/chat/completions".</summary>
    public string BaseUrl { get; }

    /// <summary>The secret key of the tier. Empty means the entry is skipped (contract 3.2).</summary>
    public string ApiKey { get; }

    /// <summary>The model slug sent verbatim as the wire "model" field (contract 5.3).</summary>
    public string Model { get; }

    /// <summary>False skips the entry regardless of its key (contract 3.2).</summary>
    public bool Enabled { get; }
}

/// <summary>
/// The application configuration read once at startup (contract section 8):
/// the ordered provider chain and, for later phases, the database path.
/// </summary>
public sealed class AppConfig
{
    private AppConfig(IReadOnlyList<AiProviderConfig> aiProviders, string? databasePath)
    {
        AiProviders = aiProviders;
        DatabasePath = databasePath;
    }

    /// <summary>The provider chain in config order — array order is chain priority (contract 3.1).</summary>
    public IReadOnlyList<AiProviderConfig> AiProviders { get; }

    /// <summary>
    /// The optional "databasePath" entry, using the same key and relative-path
    /// convention the connection factory of the data layer documents. Null when
    /// the file carries none (the factory's documented default then applies).
    /// </summary>
    public string? DatabasePath { get; }

    /// <summary>
    /// Reads the local configuration of the running application — the file the
    /// data layer documents as "config/config.local.json" next to the
    /// application root. Never throws: a missing or unreadable file counts as
    /// "no providers configured".
    /// </summary>
    public static AppConfig Load()
    {
        string? configPath = FindConfigFile();

        return configPath is null ? Empty() : LoadFromFile(configPath);
    }

    /// <summary>
    /// Reads one concrete configuration file. A missing file, a malformed file
    /// or a file without a provider list yields a configuration with NO
    /// providers (contract 7.6) and the database path when the file carries one.
    /// </summary>
    public static AppConfig LoadFromFile(string path)
    {
        if (!File.Exists(path))
        {
            return Empty();
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Empty();
            }

            return new AppConfig(
                ReadAiProviders(document.RootElement),
                ReadDatabasePath(document.RootElement));
        }
        catch (JsonException)
        {
            // A malformed file must not stop the application: no providers, no
            // database path — the chain fails typed, the offline path stands.
            return Empty();
        }
        catch (IOException)
        {
            return Empty();
        }
        catch (UnauthorizedAccessException)
        {
            return Empty();
        }
    }

    /// <summary>The configuration of an application without a readable config file.</summary>
    public static AppConfig Empty() => new(Array.Empty<AiProviderConfig>(), null);

    /// <summary>
    /// Reads the "aiProviders" array (contract 8). Entries that are not usable
    /// at all — not an object, or without a non-empty name, because the name is
    /// the tier identifier every reason string needs (contract 2) — are
    /// ignored. Everything else is kept, even when incomplete: the skip rules
    /// of contract 3.2 decide later whether the entry is attempted, and a
    /// skipped entry stays visible in the failure report.
    /// </summary>
    private static IReadOnlyList<AiProviderConfig> ReadAiProviders(JsonElement root)
    {
        if (!root.TryGetProperty("aiProviders", out JsonElement list)
            || list.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<AiProviderConfig>();
        }

        List<AiProviderConfig> providers = new();

        foreach (JsonElement entry in list.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            string? name = ReadTrimmedString(entry, "name");

            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            // Ruling R-3 b: an absent flag defaults to ENABLED; so does a flag
            // of the wrong type — the skip rules decide on top of the default.
            providers.Add(new AiProviderConfig(
                name,
                ReadTrimmedString(entry, "baseUrl") ?? string.Empty,
                ReadTrimmedString(entry, "apiKey") ?? string.Empty,
                ReadTrimmedString(entry, "model") ?? string.Empty,
                ReadEnabledFlag(entry)));
        }

        return providers;
    }

    /// <summary>
    /// Reads the optional "databasePath" entry (the key constant of the data
    /// layer, reused here so the two readers cannot drift apart).
    /// </summary>
    private static string? ReadDatabasePath(JsonElement root)
    {
        string? path = ReadTrimmedString(root, Data.Db.DatabasePathConfigKey);

        return string.IsNullOrEmpty(path) ? null : path;
    }

    /// <summary>Reads one optional string entry, trimmed; null when absent or not a string.</summary>
    private static string? ReadTrimmedString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value)
            || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        string? text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    /// <summary>
    /// Reads the optional "enabled" switch of one provider entry. An absent or
    /// mistyped flag keeps the documented default: the entry counts as enabled
    /// (contract 8, ruling R-3 b) — the skip rules of contract 3.2 still apply.
    /// </summary>
    private static bool ReadEnabledFlag(JsonElement entry)
    {
        if (!entry.TryGetProperty("enabled", out JsonElement flag))
        {
            return true;
        }

        return flag.ValueKind != JsonValueKind.False;
    }

    /// <summary>
    /// Finds the config file the same way the connection factory of the data
    /// layer finds its database: the closest folder above the running program
    /// that holds the "config" folder (the repository root during development,
    /// the executable folder in a published build).
    /// </summary>
    private static string? FindConfigFile()
    {
        foreach (string start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            DirectoryInfo? directory = new(start);

            while (directory is not null)
            {
                string candidate = Path.Combine(directory.FullName, "config", "config.local.json");

                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }
        }

        return null;
    }
}
