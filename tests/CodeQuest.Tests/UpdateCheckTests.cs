// =============================================================================
// CodeQuest — Version Update Check
// Tests for: the pure update-check logic (UpdateCheckService) — GitHub release
// JSON parsing, the version comparison behind "is a newer release out?", and
// the markdown-to-plain-text conversion the update dialog renders.
// No network, no forms: every case feeds hand-written payloads and versions.
// =============================================================================

using CodeQuest.Services;

namespace CodeQuest.Tests;

/// <summary>
/// Version-update-check tests: one behavior per case, payloads hand-written to
/// mirror the real GitHub releases/latest response shape.
/// </summary>
public class UpdateCheckTests
{
    private static readonly Version Current = new(1, 0, 0);

    private const string ReleaseJson = """
        {
          "tag_name": "v1.1.0",
          "html_url": "https://github.com/Shuash11/codequest/releases/tag/v1.1.0",
          "body": "## What's new\n- **Quiz taking** now remembers your answers.\n- Fixed `report` averages.",
          "assets": [
            { "name": "CodeQuest-win-x64.zip", "browser_download_url": "https://github.com/Shuash11/codequest/releases/download/v1.1.0/CodeQuest-win-x64.zip" }
          ]
        }
        """;

    [Fact]
    public void ParseReleaseJson_NewerTag_ReturnsResultWithAssetUrl()
    {
        UpdateCheckResult? result = UpdateCheckService.ParseReleaseJson(ReleaseJson, Current);

        Assert.NotNull(result);
        Assert.True(result.IsNewer);
        Assert.Equal("1.1.0", result.LatestVersion);
        Assert.Equal("1.0.0", result.CurrentVersion);
        Assert.Equal(
            "https://github.com/Shuash11/codequest/releases/download/v1.1.0/CodeQuest-win-x64.zip",
            result.DownloadUrl);
    }

    [Fact]
    public void ParseReleaseJson_ReleaseNotes_AreCleanedOfMarkdown()
    {
        UpdateCheckResult? result = UpdateCheckService.ParseReleaseJson(ReleaseJson, Current);

        Assert.NotNull(result);
        Assert.DoesNotContain("##", result.ReleaseNotes);
        Assert.DoesNotContain("**", result.ReleaseNotes);
        Assert.DoesNotContain("`", result.ReleaseNotes);
        Assert.Contains("\u2022 Quiz taking now remembers your answers.", result.ReleaseNotes);
        Assert.Contains("What's new", result.ReleaseNotes);
    }

    [Fact]
    public void ParseReleaseJson_OlderOrEqualTag_IsNotNewer()
    {
        const string older = """{ "tag_name": "v0.9.0", "assets": [], "body": "" }""";
        const string equal = """{ "tag_name": "v1.0.0", "assets": [], "body": "" }""";

        Assert.Null(UpdateCheckService.ParseReleaseJson(older, Current));
        Assert.Null(UpdateCheckService.ParseReleaseJson(equal, Current));
    }

    [Fact]
    public void ParseReleaseJson_MalformedPayload_ReturnsNull()
    {
        Assert.Null(UpdateCheckService.ParseReleaseJson("not json at all", Current));
        Assert.Null(UpdateCheckService.ParseReleaseJson("""{ "unrelated": true }""", Current));
        Assert.Null(UpdateCheckService.ParseReleaseJson("""{ "tag_name": "not-a-version", "assets": [], "body": "" }""", Current));
    }

    [Fact]
    public void ParseReleaseJson_NoZipAsset_FallsBackToReleasePage()
    {
        const string json = """
            {
              "tag_name": "v1.1.0",
              "html_url": "https://github.com/Shuash11/codequest/releases/tag/v1.1.0",
              "body": "notes",
              "assets": []
            }
            """;

        UpdateCheckResult? result = UpdateCheckService.ParseReleaseJson(json, Current);

        Assert.NotNull(result);
        Assert.Equal("https://github.com/Shuash11/codequest/releases/tag/v1.1.0", result.DownloadUrl);
    }

    [Fact]
    public void IsNewerVersion_ComparesNumericallyAndStripsTheTag()
    {
        Assert.True(UpdateCheckService.IsNewerVersion("v1.1.0", Current));
        Assert.True(UpdateCheckService.IsNewerVersion("v1.10.0", Current)); // 1.10 beats 1.9 numerically
        Assert.True(UpdateCheckService.IsNewerVersion("2.0.0", Current));
        Assert.False(UpdateCheckService.IsNewerVersion("v1.0.0", Current));
        Assert.False(UpdateCheckService.IsNewerVersion("v0.9.9", Current));
        Assert.False(UpdateCheckService.IsNewerVersion("garbage", Current));
        Assert.False(UpdateCheckService.IsNewerVersion(null, Current));
    }

    [Fact]
    public void ReleaseNotesToPlainText_EmptyNotes_FallsBackToFriendlyLine()
    {
        Assert.Equal("Bug fixes and improvements.", UpdateCheckService.ReleaseNotesToPlainText(null));
        Assert.Equal("Bug fixes and improvements.", UpdateCheckService.ReleaseNotesToPlainText("   "));
    }
}
