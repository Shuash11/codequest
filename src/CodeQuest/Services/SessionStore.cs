// Remembers the player's name between launches.

using System.Text.Json;

namespace CodeQuest.Services;

public sealed class SessionStore
{
    private const string FileName = "session.json";

    private readonly string _filePath;

    public SessionStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodeQuest"))
    {
    }

    public SessionStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _filePath = Path.Combine(directory, FileName);
    }

    public string? LoadUsername()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            using FileStream stream = File.OpenRead(_filePath);
            using JsonDocument document = JsonDocument.Parse(stream);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (string.Equals(property.Name, "username", StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.String)
                {
                    string? name = property.Value.GetString()?.Trim();
                    return string.IsNullOrEmpty(name) ? null : name;
                }
            }

            return null;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void SaveUsername(string username)
    {
        string name = username.Trim();
        if (name.Length == 0)
        {
            Clear();
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            string json = JsonSerializer.Serialize(
                new SessionData(name),
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            File.WriteAllText(_filePath, json);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
        }
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed record SessionData(string Username);
}
