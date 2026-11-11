// Checks GitHub for a newer version.

using System.Reflection;
using System.Text.Json;

namespace CodeQuest.Services;

public sealed class UpdateCheckResult
{
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

    public bool IsNewer { get; }

    public string LatestVersion { get; }

    public string CurrentVersion { get; }

    public string ReleaseNotes { get; }

    public string DownloadUrl { get; }

    public string? InstallerUrl { get; }

    public string? InstallerSha256 { get; }
}

public static class UpdateCheckService
{
    public const string ReleasesApiUrl = "https://api.github.com/repos/Shuash11/codequest/releases/latest";

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
            return null;
        }
    }

    public static Version CurrentVersion()
    {
        string? info = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        info = info?.Split('+')[0];

        return Version.TryParse(info, out Version? version)
            ? version
            : new Version(0, 0, 0);
    }

    public static UpdateCheckResult? ParseReleaseJson(string json, Version currentVersion)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            string tag = root.GetProperty("tag_name").GetString() ?? string.Empty;
            if (!IsNewerVersion(tag, currentVersion))
            {
                return null;
            }

            string releasePageUrl = root.GetProperty("html_url").GetString() ?? string.Empty;
            string downloadUrl = releasePageUrl;
            var assets = new List<(string Name, string Url, string? Sha256)>();
            foreach (JsonElement asset in root.GetProperty("assets").EnumerateArray())
            {
                string assetName = asset.GetProperty("name").GetString() ?? string.Empty;
                string assetUrl = asset.TryGetProperty("browser_download_url", out JsonElement urlElement)
                    && urlElement.ValueKind == JsonValueKind.String
                    ? urlElement.GetString() ?? string.Empty
                    : string.Empty; // an asset without a URL must not kill the whole parse (code review m3)
                string? assetDigest = ReadSha256Digest(asset);

                if (downloadUrl == releasePageUrl
                    && assetUrl.Length > 0
                    && assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    downloadUrl = assetUrl;
                }

                assets.Add((assetName, assetUrl, assetDigest));
            }

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
            return null;
        }
    }

    public static string? SelectInstaller(
        IReadOnlyList<(string Name, string Url, string? Sha256)> assets,
        out string? installerSha256)
    {
        // Priority: the documented release asset ("CodeQuest-Setup*"), then
        // any setup-named exe, then the first exe (security review P6).
        foreach (Func<string, bool> matches in new Func<string, bool>[]
                 {
                     name => name.StartsWith("CodeQuest-Setup", StringComparison.OrdinalIgnoreCase),
                     name => name.Contains("setup", StringComparison.OrdinalIgnoreCase),
                 })
        {
            foreach ((string name, string url, string? sha256) in assets)
            {
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(url)
                    && matches(name))
                {
                    installerSha256 = sha256;
                    return url;
                }
            }
        }

        foreach ((string name, string url, string? sha256) in assets)
        {
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(url))
            {
                installerSha256 = sha256;
                return url;
            }
        }

        installerSha256 = null;
        return null;
    }

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

    public static bool IsNewerVersion(string? tagName, Version currentVersion)
    {
        string candidate = (tagName ?? string.Empty).TrimStart('v', 'V').Trim();
        return Version.TryParse(candidate, out Version? latest) && latest > currentVersion;
    }

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
