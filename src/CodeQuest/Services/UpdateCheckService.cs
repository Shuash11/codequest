// =============================================================================
// CodeQuest — Version Update Check (I6+)
// A friendly update experience for the distributed build: once per launch the
// app asks GitHub's public releases API whether a newer release exists. If it
// does, the UI shows a dialog with the release notes ("what changed") and a
// one-click update — the app downloads the release's installer asset itself
// (SelectInstaller), verifies it against GitHub's sha256 digest when one is
// supplied, and runs it silently; a release without an installer falls back
// to the old browser hand-off.
// Look at: CheckAsync() — the whole network call is wrapped so that EVERY
//          failure (offline, rate limit, timeout, bad JSON) returns null and
//          the app simply continues: a failed update check must never block a
//          study session. ParseReleaseJson(), SelectInstaller() and
//          IsNewerVersion() are pure and unit-tested; the comparison strips
//          the leading "v" of release tags ("v1.1.0") and compares
//          numerically, so "1.10.0" correctly beats "1.9.0".
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
        string downloadUrl,
        string? installerUrl = null,
        string? installerSha256 = null)
    {
        IsNewer = isNewer;
        LatestVersion = latestVersion;
        CurrentVersion = currentVersion;
        ReleaseNotes = releaseNotes;
        DownloadUrl = downloadUrl;
        InstallerUrl = installerUrl;
        InstallerSha256 = installerSha256;
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

    /// <summary>
    /// The release's installer asset for the in-app update (downloaded and run
    /// silently by the app itself); null when the release carries no installer
    /// and the dialog falls back to opening the browser.
    /// </summary>
    public string? InstallerUrl { get; }

    /// <summary>
    /// The installer asset's GitHub-supplied sha256 digest (hex, the "sha256:"
    /// prefix stripped) for the post-download integrity check; null when the
    /// API response carries no digest for the asset.
    /// </summary>
    public string? InstallerSha256 { get; }
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

            // Collect the release assets once: the browser fallback wants the
            // first .zip (the packaged build), the in-app update wants the
            // installer (.exe) and its integrity digest.
            string releasePageUrl = root.GetProperty("html_url").GetString() ?? string.Empty;
            string downloadUrl = releasePageUrl;
            var assets = new List<(string Name, string Url, string? Sha256)>();
            foreach (JsonElement asset in root.GetProperty("assets").EnumerateArray())
            {
                string assetName = asset.GetProperty("name").GetString() ?? string.Empty;
                string assetUrl = asset.GetProperty("browser_download_url").GetString() ?? string.Empty;
                string? assetDigest = ReadSha256Digest(asset);

                if (downloadUrl == releasePageUrl
                    && assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    downloadUrl = assetUrl;
                }

                assets.Add((assetName, assetUrl, assetDigest));
            }

            // The in-app update runs the installer asset itself; without one,
            // the dialog keeps the old browser hand-off.
            string? installerUrl = SelectInstaller(assets, out string? installerSha256);

            string notes = ReleaseNotesToPlainText(root.GetProperty("body").GetString());

            return new UpdateCheckResult(
                isNewer: true,
                latestVersion: tag.TrimStart('v', 'V'),
                currentVersion: currentVersion.ToString(),
                releaseNotes: notes,
                downloadUrl: downloadUrl,
                installerUrl: installerUrl,
                installerSha256: installerSha256);
        }
        catch
        {
            return null; // unexpected payload shape — treated like any other failed check
        }
    }

    /// <summary>
    /// Picks the installer asset the in-app update runs: the first ".exe"
    /// asset, preferring a name that carries "setup". Never returns a digest
    /// without its asset — <paramref name="installerSha256"/> is set only when
    /// an installer was found and GitHub supplied one.
    /// </summary>
    public static string? SelectInstaller(
        IReadOnlyList<(string Name, string Url, string? Sha256)> assets,
        out string? installerSha256)
    {
        installerSha256 = null;
        string? firstInstallerUrl = null;
        string? firstInstallerSha256 = null;

        foreach ((string name, string url, string? sha256) in assets)
        {
            if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(url))
            {
                continue;
            }

            if (firstInstallerUrl is null)
            {
                firstInstallerUrl = url;
                firstInstallerSha256 = sha256;
            }

            if (name.Contains("setup", StringComparison.OrdinalIgnoreCase))
            {
                installerSha256 = sha256;
                return url; // the named installer wins over the first .exe
            }
        }

        installerSha256 = firstInstallerSha256;
        return firstInstallerUrl;
    }

    /// <summary>
    /// Reads one asset's "digest" entry ("sha256:<hex>", as GitHub now
    /// supplies it) and returns the bare hex; null when absent or not the
    /// sha256 form — an unknown digest form must never silently pass as
    /// "verified".
    /// </summary>
    private static string? ReadSha256Digest(JsonElement asset)
    {
        if (!asset.TryGetProperty("digest", out JsonElement digest)
            || digest.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        string value = digest.GetString() ?? string.Empty;
        const string prefix = "sha256:";
        return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? value[prefix.Length..]
            : null;
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
