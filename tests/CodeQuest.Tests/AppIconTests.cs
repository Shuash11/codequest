// The shipped application icon: embedded in the assembly and loadable (task 5
// follow-up). The shell window sets it from UITheme.AppIcon; these checks keep
// the csproj include and the resource name honest.

using CodeQuest.UI;

namespace CodeQuest.Tests;

public class AppIconTests
{
    [Fact]
    public void AppIcon_IsEmbeddedUnderTheExpectedResourceName()
    {
        string[] resourceNames = typeof(UITheme).Assembly.GetManifestResourceNames();

        Assert.Contains("CodeQuest.Assets.app.ico", resourceNames);
    }

    [Fact]
    public void AppIcon_LoadsFromTheEmbeddedResource()
    {
        Assert.NotNull(UITheme.AppIcon);
    }
}
