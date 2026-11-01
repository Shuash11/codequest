using CodeQuest.UI;

namespace CodeQuest.Tests;

/// <summary>
/// Phase P0 scaffold smoke test. It proves the test project is wired to the
/// application project; real domain tests arrive in Phase P3 (P3, P4, P5, P11).
/// </summary>
public class ScaffoldTests
{
    [Fact]
    public void TestProject_ReferencesApplicationAssembly()
    {
        var assembly = typeof(MainForm).Assembly;

        Assert.Equal("CodeQuest", assembly.GetName().Name);
    }
}
