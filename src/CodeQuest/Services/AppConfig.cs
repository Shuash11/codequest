// Reads the settings file.

using System.Text.Json;

namespace CodeQuest.Services;

public sealed class AiProviderConfig
{
    public AiProviderConfig(string name, string baseUrl, string apiKey, string model, bool enabled)
    {
        Name = name;
        BaseUrl = baseUrl;
        ApiKey = apiKey;
        Model = model;
        Enabled = enabled;
    }

    public string Name { get; }

    public string BaseUrl { get; }

    public string ApiKey { get; }

    public string Model { get; }

    public bool Enabled { get; }
}

public sealed class AppConfig
{
    private AppConfig(IReadOnlyList<AiProviderConfig> aiProviders, string? databasePath)
    {
        AiProviders = aiProviders;
        DatabasePath = databasePath;
    }

    public IReadOnlyList<AiProviderConfig> AiProviders { get; }

    public string? DatabasePath { get; }

    public static AppConfig Load()
    {
        string? configPath = FindConfigFile();

        return configPath is null ? Empty() : LoadFromFile(configPath);
    }

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

    public static AppConfig Empty() => new(Array.Empty<AiProviderConfig>(), null);

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

            providers.Add(new AiProviderConfig(
                name,
                ReadTrimmedString(entry, "baseUrl") ?? string.Empty,
                ReadTrimmedString(entry, "apiKey") ?? string.Empty,
                ReadTrimmedString(entry, "model") ?? string.Empty,
                ReadEnabledFlag(entry)));
        }

        return providers;
    }

    private static string? ReadDatabasePath(JsonElement root)
    {
        string? path = ReadTrimmedString(root, Data.Db.DatabasePathConfigKey);

        return string.IsNullOrEmpty(path) ? null : path;
    }

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

    private static bool ReadEnabledFlag(JsonElement entry)
    {
        if (!entry.TryGetProperty("enabled", out JsonElement flag))
        {
            return true;
        }

        return flag.ValueKind != JsonValueKind.False;
    }

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
