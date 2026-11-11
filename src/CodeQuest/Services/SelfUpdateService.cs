// Downloads and installs the update.

using System.Diagnostics;
using System.Security.Cryptography;

namespace CodeQuest.Services;

public enum SelfUpdatePhase
{
    Downloading,

    Verifying,

    Installing,
}

public sealed record SelfUpdateProgress(SelfUpdatePhase Phase, double? Fraction);

public enum SelfUpdateStatus
{
    Applied,

    Cancelled,

    Failed,
}

public sealed record SelfUpdateOutcome(SelfUpdateStatus Status, string FailureMessage)
{
    public static SelfUpdateOutcome Applied() => new(SelfUpdateStatus.Applied, string.Empty);

    public static SelfUpdateOutcome Cancelled() => new(SelfUpdateStatus.Cancelled, string.Empty);
}

public static class SelfUpdateService
{
    public static string UpdateDirectory => Path.Combine(Path.GetTempPath(), "CodeQuestUpdate");

    public static string InstallerPath => Path.Combine(UpdateDirectory, "CodeQuest-Setup.exe");

    public static string RelaunchScriptPath => Path.Combine(UpdateDirectory, "apply-update.cmd");

    public static string AppExePath => Path.Combine(AppContext.BaseDirectory, "CodeQuest.exe");

    public const string InstallerSwitches = "/VERYSILENT /NORESTART /SUPPRESSMSGBOXES";

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
                    UseShellExecute = false,
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

    private static async Task<long> DownloadAsync(
        string installerUrl,
        string downloadPath,
        IProgress<SelfUpdateProgress> progress,
        CancellationToken cancellation)
    {
        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromMinutes(10);
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
                progress.Report(new SelfUpdateProgress(SelfUpdatePhase.Downloading, null));
            }
        }

        return total;
    }

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

    public static bool IsUsableDigest(string? digest)
    {
        if (digest is null || digest.Length != 64)
        {
            return false;
        }

        return digest.All(char.IsAsciiHexDigit);
    }

    public static bool IsDigestMatch(string? expectedSha256, string actualHex)
    {
        return expectedSha256 is not null
            && IsUsableDigest(expectedSha256)
            && string.Equals(expectedSha256.Trim(), actualHex.Trim(), StringComparison.OrdinalIgnoreCase);
    }

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
        }
    }
}
