// =============================================================================
// CodeQuest — Remembered name (task 2, owner directive): "the name must only
// show once and be saved when the user already entered a name". After one
// successful sign-in the name is kept here; the next launch resolves it
// against the database and goes straight into the app — the name screen
// appears only on the first start, after a log out, or when the remembered
// account no longer exists.
// Look at: the file is %LOCALAPPDATA%\CodeQuest\session.json — machine-local
//          on purpose (the database itself may be copied between machines and
//          carries several accounts; the "who was last signed in HERE" fact
//          belongs to this machine, next to the I5 error.log).
// Failure philosophy (matching the config loader and the error log): a
//          missing, malformed or unwritable file degrades to "no remembered
//          name" — persistence must never block a study session, never throw.
// Privacy: only the account's login name is stored (the app is name-only by
//          directive D1; there are no passwords to remember).
// =============================================================================

using System.Text.Json;

namespace CodeQuest.Services;

/// <summary>
/// Remembers the name of the last successful sign-in so later launches can
/// skip the name screen. Instantiate with the default directory, or inject a
/// directory for tests.
/// </summary>
public sealed class SessionStore
{
    private const string FileName = "session.json";

    private readonly string _filePath;

    /// <summary>Creates the session store at the machine's standard location.</summary>
    public SessionStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodeQuest"))
    {
    }

    /// <summary>Creates the session store inside one concrete directory (tests).</summary>
    public SessionStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _filePath = Path.Combine(directory, FileName);
    }

    /// <summary>
    /// The remembered name, trimmed; null when nothing usable is stored — a
    /// missing, malformed or unreadable file counts as "not remembered".
    /// </summary>
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

            // The key is read case-insensitively: a hand-edited or older
            // file must still resolve (only the name value matters).
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
            return null; // unusable persistence must never block the sign-in flow
        }
    }

    /// <summary>
    /// Remembers one name for the next launch. Best-effort: a full disk or a
    /// locked file is swallowed — the worst case is the name screen showing
    /// again, exactly the old behavior. An empty name clears the memory.
    /// </summary>
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
            // Best-effort by design (see the header).
        }
    }

    /// <summary>Forgets the remembered name (log out). Absence is fine.</summary>
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
            // Best-effort by design (see the header).
        }
    }

    /// <summary>The file's whole content: one remembered login name.</summary>
    private sealed record SessionData(string Username);
}
