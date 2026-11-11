// =============================================================================
// CodeQuest — In-App Update (task 2): the download-and-apply half of the
// update experience. Owner directive: updating must not hand the user to
// GitHub — the app shows its own "Updating" dialog and performs the update:
//   1. Downloading — the release's installer asset streams to
//      %TEMP%\CodeQuestUpdate with progress + cancellation;
//   2. Verifying  — when GitHub supplies the asset's sha256 digest, the
//      downloaded file must match (a silently-running installer is
//      security-sensitive; the digest is the integrity check);
//   3. Installing — a tiny apply-update.cmd waits for this process to exit,
//      runs the installer silently (the Inno script keeps codequest.db and
//      config.local.json on upgrade), and relaunches CodeQuest.
// Look at: ApplyAsync() — every failure returns a typed outcome, never an
//          exception to the UI (the cancelled case reports itself too, so the
//          dialog can close quietly). BuildRelaunchScript(), IsDigestMatch()
//          and IsUsableDigest() are pure and unit-tested.
// Safety: the installer URL must be https (checked, not assumed); no user
//          input ever reaches the command line — only the service's own two
//          absolute paths; a digest that is not well-formed sha256 is not
//          trusted as a verification (the update proceeds unverified rather
//          than failing on a GitHub quirk).
// =============================================================================

using System.Diagnostics;
using System.Security.Cryptography;

namespace CodeQuest.Services;

/// <summary>The phases of an in-app update, in order.</summary>
public enum SelfUpdatePhase
{
    /// <summary>The installer asset is downloading.</summary>
    Downloading,

    /// <summary>The downloaded installer is being checked against its digest.</summary>
    Verifying,

    /// <summary>The installer has been launched; the app is about to exit.</summary>
    Installing,
}

/// <summary>One progress report of the in-app update.</summary>
/// <param name="Phase">The phase the update is in.</param>
/// <param name="Fraction">
/// The downloaded fraction (0..1) when the total size is known; null means
/// "activity without a percent" (the dialog shows its marquee).
/// </param>
public sealed record SelfUpdateProgress(SelfUpdatePhase Phase, double? Fraction);

/// <summary>How an in-app update attempt ended.</summary>
public enum SelfUpdateStatus
{
    /// <summary>The installer was launched; the app will exit and be replaced.</summary>
    Applied,

    /// <summary>The user cancelled (the session simply continues).</summary>
    Cancelled,

    /// <summary>Downloading, verifying or launching failed; <see cref="Outcome"/> carries the message.</summary>
    Failed,
}

/// <summary>The end state of one in-app update attempt.</summary>
public sealed record SelfUpdateOutcome(SelfUpdateStatus Status, string FailureMessage)
{
    /// <summary>The outcome of an applied update (no failure message).</summary>
    public static SelfUpdateOutcome Applied() => new(SelfUpdateStatus.Applied, string.Empty);

    /// <summary>The outcome of a user cancellation (no failure message).</summary>
    public static SelfUpdateOutcome Cancelled() => new(SelfUpdateStatus.Cancelled, string.Empty);
}

/// <summary>
/// Downloads, verifies and launches the update installer — the engine behind
/// the "Updating CodeQuest" dialog. Pure logic is separated for tests;
/// <see cref="ApplyAsync"/> adds the network, file and process operations.
/// </summary>
public static class SelfUpdateService
{
    /// <summary>The working directory of one update attempt.</summary>
    public static string UpdateDirectory => Path.Combine(Path.GetTempPath(), "CodeQuestUpdate");

    /// <summary>Where the downloaded installer lands.</summary>
    public static string InstallerPath => Path.Combine(UpdateDirectory, "CodeQuest-Setup.exe");

    /// <summary>The script that installs and relaunches after this process exits.</summary>
    public static string RelaunchScriptPath => Path.Combine(UpdateDirectory, "apply-update.cmd");

    /// <summary>The executable this instance was started from (the relaunch target).</summary>
    public static string AppExePath => Path.Combine(AppContext.BaseDirectory, "CodeQuest.exe");

    /// <summary>The installer's silent, no-restart, no-prompt switch set.</summary>
    public const string InstallerSwitches = "/VERYSILENT /NORESTART /SUPPRESSMSGBOXES";

    /// <summary>
    /// The one in-app update operation: download → verify → launch. Every
    /// failure returns a typed outcome with a user-ready message; only a
    /// user cancellation comes back as <see cref="SelfUpdateStatus.Cancelled"/>.
    /// </summary>
    public static async Task<SelfUpdateOutcome> ApplyAsync(
        string installerUrl,
        string? expectedSha256,
        IProgress<SelfUpdateProgress> progress,
        CancellationToken cancellation)
    {
        if (!installerUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return new SelfUpdateOutcome(SelfUpdateStatus.Failed, "The update address is not secure, so the update was stopped.");
        }

        try
        {
            Directory.CreateDirectory(UpdateDirectory);
            string downloadPath = InstallerPath + ".part";
            CleanFile(downloadPath);
            CleanFile(InstallerPath);

            long total = await DownloadAsync(installerUrl, downloadPath, progress, cancellation);

            progress.Report(new SelfUpdateProgress(SelfUpdatePhase.Verifying, total > 0 ? 1d : null));
            if (!await VerifyAsync(downloadPath, expectedSha256))
            {
                CleanFile(downloadPath);
                return new SelfUpdateOutcome(
                    SelfUpdateStatus.Failed,
                    "The downloaded update failed its integrity check and was removed. Please try again later.");
            }

            File.Move(downloadPath, InstallerPath);

            progress.Report(new SelfUpdateProgress(SelfUpdatePhase.Installing, null));
            File.WriteAllText(RelaunchScriptPath, BuildRelaunchScript(InstallerPath, AppExePath, InstallerSwitches));
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = RelaunchScriptPath,
                    UseShellExecute = false, // a windowless cmd: the dialog is the whole experience
                    CreateNoWindow = true,
                },
            };
            process.Start();

            return SelfUpdateOutcome.Applied();
        }
        catch (OperationCanceledException)
        {
            CleanFile(InstallerPath + ".part");
            return SelfUpdateOutcome.Cancelled();
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or UnauthorizedAccessException
                                              or System.Net.NetworkInformation.NetworkInformationException)
        {
            CleanFile(InstallerPath + ".part");
            return new SelfUpdateOutcome(
                SelfUpdateStatus.Failed,
                "The update could not be downloaded. Check your internet connection and try again.");
        }
    }

    /// <summary>
    /// Streams the installer to <paramref name="downloadPath"/>, reporting the
    /// downloaded fraction (percent changes only — a 36 MB file must not
    /// flood the UI thread with 8 KB updates). Returns the total size, or 0
    /// when the server sent none.
    /// </summary>
    private static async Task<long> DownloadAsync(
        string installerUrl,
        string downloadPath,
        IProgress<SelfUpdateProgress> progress,
        CancellationToken cancellation)
    {
        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromMinutes(10); // a release asset, not an API ping
        http.DefaultRequestHeaders.UserAgent.ParseAdd("CodeQuest-Update");

        progress.Report(new SelfUpdateProgress(SelfUpdatePhase.Downloading, null));

        using HttpResponseMessage response = await http.GetAsync(
            installerUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellation);
        response.EnsureSuccessStatusCode();
        long total = response.Content.Headers.ContentLength ?? 0;

        await using Stream source = await response.Content.ReadAsStreamAsync(cancellation);
        await using var target = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None);

        byte[] buffer = new byte[1024 * 1024];
        long received = 0;
        int lastReportedPercent = -1;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellation)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), cancellation);
            received += read;

            if (total > 0)
            {
                int percent = (int)(100d * received / total);
                if (percent != lastReportedPercent)
                {
                    lastReportedPercent = percent;
                    progress.Report(new SelfUpdateProgress(SelfUpdatePhase.Downloading, (double)received / total));
                }
            }
            else if (received / (1024 * 1024) != (received - read) / (1024 * 1024))
            {
                progress.Report(new SelfUpdateProgress(SelfUpdatePhase.Downloading, null)); // size unknown: activity only
            }
        }

        return total;
    }

    /// <summary>
    /// The integrity check: true when no usable digest was supplied (the
    /// update proceeds unverified — GitHub's digest is an enhancement, not a
    /// gate) or the file's sha256 equals the expected hex.
    /// </summary>
    private static async Task<bool> VerifyAsync(string downloadPath, string? expectedSha256)
    {
        if (!IsUsableDigest(expectedSha256))
        {
            return true;
        }

        await using var stream = File.OpenRead(downloadPath);
        using SHA256 sha = SHA256.Create();
        string actual = Convert.ToHexString(await sha.ComputeHashAsync(stream));
        return IsDigestMatch(expectedSha256, actual);
    }

    /// <summary>Whether the digest is a well-formed sha256 hex string.</summary>
    public static bool IsUsableDigest(string? digest)
    {
        if (digest is null || digest.Length != 64)
        {
            return false;
        }

        return digest.All(char.IsAsciiHexDigit);
    }

    /// <summary>Whether the expected and actual digests match (case-insensitive).</summary>
    public static bool IsDigestMatch(string? expectedSha256, string actualHex)
    {
        return expectedSha256 is not null
            && IsUsableDigest(expectedSha256)
            && string.Equals(expectedSha256.Trim(), actualHex.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The apply script: give this process time to exit, install silently,
    /// then relaunch — whether or not the install succeeded, an app comes
    /// back (a failed install leaves the old build in place and working).
    /// </summary>
    public static string BuildRelaunchScript(string installerPath, string appExePath, string installerSwitches)
    {
        return string.Join(Environment.NewLine,
            "@echo off",
            "rem CodeQuest in-app update: install silently, then relaunch.",
            "timeout /t 2 /nobreak >nul",
            $"start \"\" /wait \"{installerPath}\" {installerSwitches}",
            $"start \"\" \"{appExePath}\"",
            string.Empty);
    }

    /// <summary>Removes a stale file of a previous attempt; absence is fine.</summary>
    private static void CleanFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // A locked leftover must not abort this attempt; writing over it
            // will surface the real failure if it matters.
        }
    }
}
