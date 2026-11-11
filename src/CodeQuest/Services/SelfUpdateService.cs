// =============================================================================
// CodeQuest — In-App Update (task 2, hardened after security review)
// The download-and-apply half of the update experience. Owner directive:
// updating must not hand the user to GitHub — the app shows its own
// "Updating" dialog and performs the update:
//   1. Downloading — the release's installer asset streams into a RANDOM
//      per-attempt folder under %TEMP%\CodeQuestUpdate, with progress,
//      cancellation and a 500 MB size cap;
//   2. Verifying  — when GitHub supplies the asset's sha256 digest, the
//      downloaded file must match; a MALFORMED digest stops the update
//      (never trusted as "verified"), an absent one proceeds unverified
//      (documented in docs/security-notes.md);
//   3. Installing — a generated apply-update.ps1 waits for THIS process to
//      actually exit (Wait-Process on the real PID — never a blind sleep),
//      runs the installer silently, relaunches CodeQuest, and deletes the
//      installer and its own folder.
// Look at: ApplyAsync() — every failure returns a typed outcome, never an
//          exception to the UI; a user cancellation and a network timeout
//          are reported differently. IsTrustedUpdateUrl(), BuildRelaunch
//          Script(), IsDigestMatch() and IsUsableDigest() are pure and
//          unit-tested.
// Safety: the installer URL must be https on a GitHub host (checked, not
//          assumed); no GitHub-controlled field ever reaches the script —
//          only the service's own absolute paths, single-quoted; a mismatched
//          digest deletes the file and fails the update. The installer itself
//          is unsigned (no certificate exists) — that accepted risk and its
//          reasoning are recorded in docs/security-notes.md.
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
    /// <summary>The base folder of one update attempt (a random folder inside).</summary>
    public static string UpdateDirectory => Path.Combine(Path.GetTempPath(), "CodeQuestUpdate");

    /// <summary>The installer's silent, no-restart, no-prompt switch set.</summary>
    public const string InstallerSwitches = "/VERYSILENT /NORESTART /SUPPRESSMSGBOXES";

    /// <summary>A release asset larger than this is refused.</summary>
    public const long MaxInstallerBytes = 500L * 1024 * 1024;

    /// <summary>
    /// The one in-app update operation: download → verify → launch. Every
    /// failure returns a typed outcome with a user-ready message; a user
    /// cancellation and a network timeout are reported differently.
    /// </summary>
    public static async Task<SelfUpdateOutcome> ApplyAsync(
        string? installerUrl,
        string? expectedSha256,
        IProgress<SelfUpdateProgress> progress,
        CancellationToken cancellation)
    {
        if (!IsTrustedUpdateUrl(installerUrl))
        {
            return new SelfUpdateOutcome(
                SelfUpdateStatus.Failed,
                "The update address is not a trusted source, so the update was stopped.");
        }

        if (expectedSha256 is not null && !IsUsableDigest(expectedSha256))
        {
            return new SelfUpdateOutcome(
                SelfUpdateStatus.Failed,
                "The update's integrity information is invalid, so the update was stopped.");
        }

        string workDirectory = Path.Combine(UpdateDirectory, Guid.NewGuid().ToString("N"));
        string downloadPath = Path.Combine(workDirectory, "installer.part");
        string installerPath = Path.Combine(workDirectory, "CodeQuest-Setup.exe");
        string scriptPath = Path.Combine(workDirectory, "apply-update.ps1");

        try
        {
            Directory.CreateDirectory(workDirectory);

            long total = await DownloadAsync(installerUrl!, downloadPath, progress, cancellation);

            progress.Report(new SelfUpdateProgress(SelfUpdatePhase.Verifying, total > 0 ? 1d : null));
            if (expectedSha256 is not null && !await VerifyAsync(downloadPath, expectedSha256))
            {
                CleanupDirectory(workDirectory);
                return new SelfUpdateOutcome(
                    SelfUpdateStatus.Failed,
                    "The downloaded update failed its integrity check and was removed. Please try again later.");
            }

            File.Move(downloadPath, installerPath);

            // The download is done — closing the dialog from here on means
            // "abort": never write or launch the install script after the
            // user cancelled (code review m1).
            cancellation.ThrowIfCancellationRequested();

            progress.Report(new SelfUpdateProgress(SelfUpdatePhase.Installing, null));
            string appPath = AppExePath;
            File.WriteAllText(
                scriptPath,
                BuildRelaunchScript(Environment.ProcessId, installerPath, appPath, InstallerSwitches, workDirectory));

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
                    UseShellExecute = false, // windowless: the dialog is the whole experience
                    CreateNoWindow = true,
                },
            };
            bool started = process.Start();
            if (!started)
            {
                // A false Start means nothing was launched: reporting Applied
                // would close the app with no installer and no relaunch
                // (code review m2).
                CleanupDirectory(workDirectory);
                return new SelfUpdateOutcome(
                    SelfUpdateStatus.Failed,
                    "The update could not be started. Please try again later.");
            }

            return SelfUpdateOutcome.Applied();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            CleanupDirectory(workDirectory);
            return SelfUpdateOutcome.Cancelled();
        }
        catch (OperationCanceledException)
        {
            // A HttpClient timeout also surfaces as TaskCanceledException —
            // that is a failure, not a user decision (security review F6).
            CleanupDirectory(workDirectory);
            return new SelfUpdateOutcome(
                SelfUpdateStatus.Failed,
                "The update took too long to download. Check your internet connection and try again.");
        }
        catch (UpdateTooLargeException)
        {
            CleanupDirectory(workDirectory);
            return new SelfUpdateOutcome(
                SelfUpdateStatus.Failed,
                "The update file is unexpectedly large, so the update was stopped.");
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or UnauthorizedAccessException
                                              or System.Net.NetworkInformation.NetworkInformationException)
        {
            CleanupDirectory(workDirectory);
            return new SelfUpdateOutcome(
                SelfUpdateStatus.Failed,
                "The update could not be downloaded. Check your internet connection and try again.");
        }
    }

    /// <summary>
    /// The executable this instance was actually started from (the relaunch
    /// target) — the running binary's own path, not a reconstruction.
    /// </summary>
    public static string AppExePath =>
        Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "CodeQuest.exe");

    /// <summary>
    /// Whether the update address is one this app may download and run: an
    /// https URL on GitHub (release pages and release asset storage only).
    /// </summary>
    public static bool IsTrustedUpdateUrl(string? installerUrl)
    {
        if (string.IsNullOrWhiteSpace(installerUrl)
            || !Uri.TryCreate(installerUrl, UriKind.Absolute, out Uri? uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        string host = uri.Host;
        return host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Streams the installer to <paramref name="downloadPath"/>, reporting the
    /// downloaded fraction (percent changes only — a 36 MB file must not
    /// flood the UI thread with 8 KB updates). Returns the total size, or 0
    /// when the server sent none. A stream beyond the size cap aborts.
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
        if (total > MaxInstallerBytes)
        {
            throw new UpdateTooLargeException();
        }

        await using Stream source = await response.Content.ReadAsStreamAsync(cancellation);
        await using var target = new FileStream(downloadPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);

        byte[] buffer = new byte[1024 * 1024];
        long received = 0;
        int lastReportedPercent = -1;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellation)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), cancellation);
            received += read;

            if (received > MaxInstallerBytes)
            {
                throw new UpdateTooLargeException();
            }

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
    /// The integrity check: true when no digest was supplied (the update
    /// proceeds unverified — GitHub's digest is an enhancement, not a gate;
    /// a MALFORMED digest never reaches here, ApplyAsync refuses it first)
    /// or the file's sha256 equals the expected hex.
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
    /// The apply script (PowerShell — available on every Windows 10/11 box):
    /// wait for THIS process to ACTUALLY exit (never a blind sleep — a
    /// windowless cmd cannot run `timeout` and a locked exe would silently
    /// fail the install), install silently, relaunch CodeQuest, then delete
    /// the installer and this script's own folder. Paths are single-quoted
    /// (literal in PowerShell) with the quote doubled — no expansion of any
    /// kind, no user input involved.
    /// </summary>
    public static string BuildRelaunchScript(
        int processId,
        string installerPath,
        string appExePath,
        string installerSwitches,
        string workDirectory)
    {
        return string.Join(Environment.NewLine,
            "# CodeQuest in-app update: install silently, then relaunch.",
            $"Wait-Process -Id {processId} -Timeout 30 -ErrorAction SilentlyContinue",
            // The app comes back only when the install succeeded — a failed
            // silent install must not spawn a second instance next to a
            // running app (code review m1).
            "$p = Start-Process -FilePath '{0}' -ArgumentList '{1}' -Wait -PassThru",
            "if ($p.ExitCode -eq 0) { Start-Process -FilePath '{2}' }",
            "Remove-Item -LiteralPath '{3}' -Force -ErrorAction SilentlyContinue",
            "Remove-Item -LiteralPath '{4}' -Recurse -Force -ErrorAction SilentlyContinue",
            string.Empty)
            .Replace("{0}", Quote(installerPath))
            .Replace("{1}", installerSwitches)
            .Replace("{2}", Quote(appExePath))
            .Replace("{3}", Quote(installerPath))
            .Replace("{4}", Quote(workDirectory));
    }

    /// <summary>Single-quotes a path for PowerShell ('' is one literal quote).</summary>
    private static string Quote(string path) => path.Replace("'", "''");

    /// <summary>Deletes one attempt folder; anything left behind is harmless.</summary>
    private static void CleanupDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // A locked leftover must not turn one failed attempt into a crash.
        }
    }

    /// <summary>The download exceeded the size cap.</summary>
    private sealed class UpdateTooLargeException : Exception
    {
    }
}
