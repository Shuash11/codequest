// =============================================================================
// CodeQuest — In-App Update (task 2)
// Tests for the pure logic of SelfUpdateService: the apply-update script the
// app leaves behind for the silent install + relaunch, and the sha256 digest
// handling behind the integrity check. No network, no processes, no files.
// =============================================================================

using CodeQuest.Services;

namespace CodeQuest.Tests;

/// <summary>One behavior per case; the script contract is what cmd.exe runs.</summary>
public class SelfUpdateServiceTests
{
    private const string Installer = @"C:\Users\someone\AppData\Local\Temp\CodeQuestUpdate\CodeQuest-Setup.exe";
    private const string AppExe = @"C:\Users\someone\AppData\Local\Programs\CodeQuest\CodeQuest.exe";

    [Fact]
    public void BuildRelaunchScript_WaitsThenInstallsSilentlyThenRelaunches()
    {
        string script = SelfUpdateService.BuildRelaunchScript(Installer, AppExe, SelfUpdateService.InstallerSwitches);

        Assert.Contains("timeout /t 2", script);                       // the running app exits first
        Assert.Contains($"/wait \"{Installer}\"", script);             // quoted: paths may contain spaces
        Assert.Contains("/VERYSILENT", script);                        // no wizard windows
        Assert.Contains("/NORESTART", script);
        Assert.Contains($"start \"\" \"{AppExe}\"", script);           // the app comes back by itself
    }

    [Fact]
    public void BuildRelaunchScript_InstallsBeforeRelaunch()
    {
        string script = SelfUpdateService.BuildRelaunchScript(Installer, AppExe, SelfUpdateService.InstallerSwitches);

        Assert.True(
            script.IndexOf("/wait", StringComparison.Ordinal) < script.IndexOf($"start \"\" \"{AppExe}\"", StringComparison.Ordinal),
            "the installer must run before the relaunch");
    }

    [Fact]
    public void IsUsableDigest_AcceptsOnlyWellFormedSha256Hex()
    {
        Assert.True(SelfUpdateService.IsUsableDigest(new string('a', 64)));
        Assert.True(SelfUpdateService.IsUsableDigest(new string('A', 64)));
        Assert.True(SelfUpdateService.IsUsableDigest("5C6C214CC7AB1D7F0559C605E9F925027846A36AE878E3B404D80954E4FEC2B2"));

        Assert.False(SelfUpdateService.IsUsableDigest(null));
        Assert.False(SelfUpdateService.IsUsableDigest("abc"));                                  // wrong length
        Assert.False(SelfUpdateService.IsUsableDigest(new string('g', 64)));                    // not hex
        Assert.False(SelfUpdateService.IsUsableDigest("sha256:" + new string('a', 64)));        // prefix not stripped
    }

    [Fact]
    public void IsDigestMatch_ComparesCaseInsensitively()
    {
        const string digest = "5C6C214CC7AB1D7F0559C605E9F925027846A36AE878E3B404D80954E4FEC2B2";

        Assert.True(SelfUpdateService.IsDigestMatch(digest, digest.ToLowerInvariant()));
        Assert.False(SelfUpdateService.IsDigestMatch(digest, new string('0', 64)));
    }

    [Fact]
    public void IsDigestMatch_MalformedExpected_NeverMatches()
    {
        Assert.False(SelfUpdateService.IsDigestMatch(null, new string('a', 64)));
        Assert.False(SelfUpdateService.IsDigestMatch("garbage", new string('a', 64)));
    }
}
