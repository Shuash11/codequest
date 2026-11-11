// =============================================================================
// CodeQuest — Remembered name (task 2)
// Tests for the SessionStore: the persisted fact behind "the name must only
// show once". Every case runs against an isolated temp directory — the real
// %LOCALAPPDATA% store is never touched. No network, no forms.
// =============================================================================

using CodeQuest.Services;

namespace CodeQuest.Tests;

/// <summary>One behavior per case; a corrupt store degrades to "not remembered".</summary>
public sealed class SessionStoreTests : IDisposable
{
    private readonly string _directory;
    private readonly SessionStore _store;

    public SessionStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "codequest-session-tests", Guid.NewGuid().ToString("N"));
        _store = new SessionStore(_directory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void SaveThenLoad_RoundTripsTheName()
    {
        _store.SaveUsername("  Joashua  ");

        Assert.Equal("Joashua", _store.LoadUsername());
    }

    [Fact]
    public void Load_WithoutAnySave_ReturnsNull()
    {
        Assert.Null(_store.LoadUsername());
    }

    [Fact]
    public void Clear_RemovesTheRememberedName()
    {
        _store.SaveUsername("Joashua");

        _store.Clear();

        Assert.Null(_store.LoadUsername());
    }

    [Fact]
    public void Clear_WithoutAnySave_IsHarmless()
    {
        _store.Clear();

        Assert.Null(_store.LoadUsername());
    }

    [Fact]
    public void Save_EmptyName_ClearsInsteadOfStoring()
    {
        _store.SaveUsername("Joashua");

        _store.SaveUsername("   ");

        Assert.Null(_store.LoadUsername());
    }

    [Fact]
    public void Load_CorruptFile_ReturnsNullInsteadOfThrowing()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "session.json"), "{ not json at all");

        Assert.Null(_store.LoadUsername());
    }

    [Fact]
    public void Save_CreatesMissingDirectories()
    {
        var nested = new SessionStore(Path.Combine(_directory, "deep", "nested"));

        nested.SaveUsername("Joashua");

        Assert.Equal("Joashua", nested.LoadUsername());
    }

    [Fact]
    public void Save_NewNameOverwritesTheOldOne()
    {
        _store.SaveUsername("Joashua");
        _store.SaveUsername("Ada");

        Assert.Equal("Ada", _store.LoadUsername());
    }
}
