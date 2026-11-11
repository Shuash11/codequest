// =============================================================================
// CodeQuest — In-App Update (task 2)
// Tests for the pure logic of SelfUpdateService: the apply-update script the
// app leaves behind for the silent install + relaunch, and the sha256 digest
// handling behind the integrity check. No network, no processes, no files.
// =============================================================================

using CodeQuest.Services;

namespace CodeQuest.Tests;

/// <summary>One behavior per case; the script contract is what PowerShell runs.</summary>
public class SelfUpdateServiceTests
{
    private const string Installer = @"C:\Users\someone\AppData\Local\Temp\CodeQuestUpdate\abc\CodeQuest-Setup.exe";
    private const string AppExe = @"C:\Users\someone\AppData\Local\Programs\CodeQuest\CodeQuest.exe";
    private const string WorkDir = @"C:\Users\someone\AppData\Local\Temp\CodeQuestUpdate\abc";

    [Fact]
    public void BuildRelaunchScript_WaitsForTheRealExitThenInstallsThenRelaunches()
    {
        string script = SelfUpdateService.BuildRelaunchScript(
            4242, Installer, AppExe, SelfUpdateService.InstallerSwitches, WorkDir);

        Assert.Contains("Wait-Process -Id 4242", script);              // the running app exits first — no blind sleep
        Assert.Contains("-FilePath '{0}'".Replace("{0}", Quote(Installer)), script); // quoted literal path
        Assert.Contains("/VERYSILENT", script);                        // no wizard windows
        Assert.Contains("/NORESTART", script);
        Assert.Contains($"-FilePath '{Quote(AppExe)}'", script);       // the app comes back by itself
        Assert.Contains($"Remove-Item -LiteralPath '{Quote(WorkDir)}'", script); // cleanup (security review P4)
    }

    [Fact]
    public void BuildRelaunchScript_InstallsBeforeRelaunch()
    {
        string script = SelfUpdateService.BuildRelaunchScript(
            4242, Installer, AppExe, SelfUpdateService.InstallerSwitches, WorkDir);

        Assert.True(
            script.IndexOf("/VERYSILENT", StringComparison.Ordinal) < script.IndexOf($"-FilePath '{Quote(AppExe)}'", StringComparison.Ordinal),
            "the installer must run before the relaunch");
    }

    [Theory]
    [InlineData("C:\\path with spaces\\CodeQuest-Setup.exe")]
    [InlineData("C:\\users\\o'brien\\CodeQuest-Setup.exe")]
    public void BuildRelaunchScript_PathsArePowerShellLiterals(string path)
    {
        string script = SelfUpdateService.BuildRelaunchScript(1, path, AppExe, SelfUpdateService.InstallerSwitches, WorkDir);

        Assert.Contains(Quote(path), script); // single-quoted, quote doubled — nothing expands
    }

    [Fact]
    public void IsTrustedUpdateUrl_AcceptsOnlyHttpsGithubHosts()
    {
        Assert.True(SelfUpdateService.IsTrustedUpdateUrl("https://github.com/Shuash11/codequest/releases/download/v1.2.0/CodeQuest-Setup-1.2.0.exe"));
        Assert.True(SelfUpdateService.IsTrustedUpdateUrl("https://objects.githubusercontent.com/x"));
        Assert.False(SelfUpdateService.IsTrustedUpdateUrl("http://github.com/x"));               // not https
        Assert.False(SelfUpdateService.IsTrustedUpdateUrl("https://evil.example.com/x"));       // not a GitHub host
        Assert.False(SelfUpdateService.IsTrustedUpdateUrl("file://C:/Windows/System32/cmd.exe"));
        Assert.False(SelfUpdateService.IsTrustedUpdateUrl(null));
        Assert.False(SelfUpdateService.IsTrustedUpdateUrl("not a url"));
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

    private static string Quote(string path) => path.Replace("'", "''");
}
