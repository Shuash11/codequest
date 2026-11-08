// =============================================================================
// CodeQuest — Version Update Check (I6+)
// A friendly update experience for the distributed build: once per launch the
// app asks GitHub's public releases API whether a newer release exists. If it
// does, the UI shows a dialog with the release notes ("what changed") and a
// one-click download.
// Look at: CheckAsync() — the whole network call is wrapped so that EVERY
//          failure (offline, rate limit, timeout, bad JSON) returns null and
//          the app simply continues: a failed update check must never block a
//          study session. ParseReleaseJson() and IsNewerVersion() are pure and
//          unit-tested; the comparison strips the leading "v" of release tags
//          ("v1.1.0") and compares numerically, so "1.10.0" correctly beats
//          "1.9.0".
// Privacy: the request carries no data beyond a User-Agent header, goes to
//          GitHub's public API only, and is never authenticated.
// =============================================================================

using System.Reflection;
using System.Text.Json;

namespace CodeQuest.Services;

/// <summary>The outcome of one update check.</summary>
public sealed class UpdateCheckResult
{
    /// <summary>Creates the result of one update check.</summary>
    public UpdateCheckResult(
        bool isNewer,
        string latestVersion,
        string currentVersion,
        string releaseNotes,
        string downloadUrl)
    {
        IsNewer = isNewer;
        LatestVersion = latestVersion;
        CurrentVersion = currentVersion;
        ReleaseNotes = releaseNotes;
        DownloadUrl = downloadUrl;
    }

    /// <summary>True when the latest release is newer than the running build.</summary>
    public bool IsNewer { get; }

    /// <summary>The latest release's version, as written in its tag.</summary>
    public string LatestVersion { get; }

    /// <summary>The version of the running build.</summary>
    public string CurrentVersion { get; }

    /// <summary>The release notes, cleaned of markdown markup for the dialog.</summary>
    public string ReleaseNotes { get; }

    /// <summary>Where the update package can be downloaded (the release asset).</summary>
    public string DownloadUrl { get; }
}

/// <summary>
/// Checks GitHub for a newer release. Pure logic is separated
/// (<see cref="ParseReleaseJson"/>, <see cref="IsNewerVersion"/>,
/// <see cref="ReleaseNotesToPlainText"/>) so it is unit-testable without a
/// network; <see cref="CheckAsync"/> adds only the HTTP call.
/// </summary>
public static class UpdateCheckService
{
    /// <summary>The public releases API of the distribution repository.</summary>
    public const string ReleasesApiUrl = "https://api.github.com/repos/Shuash11/codequest/releases/latest";

    /// <summary>
    /// The one network operation: GET the latest release, parse it, compare.
    /// Every failure returns null — a failed check is silent by design.
    /// </summary>
    public static async Task<UpdateCheckResult?> CheckAsync()
    {
        try
        {
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(8);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("CodeQuest-Update-Check");

            string json = await http.GetStringAsync(ReleasesApiUrl);
            return ParseReleaseJson(json, CurrentVersion());
        }
        catch
        {
            // Offline, rate-limited, timeout, bad JSON: the app keeps working.
            return null;
        }
    }

    /// <summary>The running build's version, from the entry assembly.</summary>
    public static Version CurrentVersion()
    {
        string? info = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        info = info?.Split('+')[0]; // drop a possible source-revision suffix

        return Version.TryParse(info, out Version? version)
            ? version
            : new Version(0, 0, 0); // an unknown build never claims an update exists
    }

    /// <summary>
    /// Parses one GitHub releases/latest response against the running version.
    /// Returns null when the JSON is unusable or the tag carries no parseable
    /// version; returns a non-newer result when the release is current.
    /// </summary>
    public static UpdateCheckResult? ParseReleaseJson(string json, Version currentVersion)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            string tag = root.GetProperty("tag_name").GetString() ?? string.Empty;
            if (!IsNewerVersion(tag, currentVersion))
            {
                return null; // the running build is current (or the tag is malformed) — nothing to show
            }

            // The download URL: the release's .zip asset (the packaged build),
            // falling back to the release page itself.
            string downloadUrl = root.GetProperty("html_url").GetString() ?? string.Empty;
            foreach (JsonElement asset in root.GetProperty("assets").EnumerateArray())
            {
                string assetName = asset.GetProperty("name").GetString() ?? string.Empty;
                if (assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? downloadUrl;
                    break;
                }
            }

            string notes = ReleaseNotesToPlainText(root.GetProperty("body").GetString());

            return new UpdateCheckResult(
                isNewer: true,
                latestVersion: tag.TrimStart('v', 'V'),
                currentVersion: currentVersion.ToString(),
                releaseNotes: notes,
                downloadUrl: downloadUrl);
        }
        catch
        {
            return null; // unexpected payload shape — treated like any other failed check
        }
    }

    /// <summary>
    /// Whether a release tag (for example "v1.10.0") is newer than the running
    /// version. The leading "v"/"V" is stripped and the rest compared
    /// numerically, so "1.10.0" correctly beats "1.9.0".
    /// </summary>
    public static bool IsNewerVersion(string? tagName, Version currentVersion)
    {
        string candidate = (tagName ?? string.Empty).TrimStart('v', 'V').Trim();
        return Version.TryParse(candidate, out Version? latest) && latest > currentVersion;
    }

    /// <summary>
    /// Turns release-note markdown into plain dialog text: headings lose
    /// their "#", bold and code markers disappear, list dashes become bullets.
    /// </summary>
    public static string ReleaseNotesToPlainText(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return "Bug fixes and improvements.";
        }

        IEnumerable<string> cleaned = markdown
            .Replace("\r\n", "\n")
            .Split('\n')
            .Select(line =>
            {
                string text = line.TrimEnd();
                text = text.TrimStart('#').Trim();
                text = text.Replace("**", string.Empty).Replace("`", string.Empty);
                if (text.StartsWith("- ", StringComparison.Ordinal))
                {
                    text = "\u2022 " + text[2..];
                }

                return text;
            });

        return string.Join(Environment.NewLine, cleaned).Trim();
    }
}
