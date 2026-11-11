// =============================================================================
// CodeQuest — Phase I3: Report Data Services
// Tests for: ReportService (the IReportService implementation), run against a
// FRESH temporary SQLite database that every test builds for itself by
// executing database/schema.sql and the frozen pre-campaign demo fixture
// (tests/CodeQuest.Tests/Fixtures/demo-seed.sql) — the same pattern as
// RepositoryTests (phase P4). The scratch file lives in the user's temp
// folder, is unique per test, and is deleted in the teardown.
// Look at: the hand-computed expectations against the demo fixture:
//   attempts: (quiz 1, 4/5), (quiz 1, 2/5), (quiz 2, 5/5), (quiz 3, 3/5)
//   topic C#      : attempts 2, avg 60%, best 80%, latest 40% -> needs practice
//   topic Python  : attempts 1, avg 100%, best 100%, latest 100%      -> ok
//   topic SQL     : attempts 1, avg 60%, best 60%, latest 60%  -> needs practice
//   topic Networking / Web Security : zero attempts -> the defined empty row
//   difficulty Easy: 3 attempts, (4+2+3)/15 = 60%; Medium: 100%; Hard: zero;
//   Insane: zero (ordered Easy, Medium, Hard, Insane)
//   overall: 4 attempts, 3 quizzes taken, (4+2+5+3)/20 = 70%
//   weak topics : [C# 60%, SQL 60%] — weakest first, ties by name
// A zero-attempt topic and a difficulty with no attempts return the defined
// empty row (zeros, no flag) — never an exception (P11 acceptance).
// =============================================================================

using CodeQuest.Data;
using CodeQuest.Data.Repositories;
using CodeQuest.Models;
using CodeQuest.Services;
using Microsoft.Data.Sqlite;

namespace CodeQuest.Tests;

/// <summary>
/// Phase I3 report tests: every case runs against its own fresh copy of the
/// seeded demo database and asserts hand-computed aggregate numbers.
/// </summary>
public class ReportServiceTests : IDisposable
{
    private readonly string _databasePath;
    private readonly Db _db;
    private readonly ReportService _reports;

    /// <summary>
    /// Builds one fresh seeded database for THIS test (schema.sql + seed.sql
    /// through a connection of the data layer), like RepositoryTests does.
    /// </summary>
    public ReportServiceTests()
    {
        string scratchDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Temp", "opencode");
        Directory.CreateDirectory(scratchDirectory);

        _databasePath = Path.Combine(scratchDirectory, $"codequest-report-tests-{Guid.NewGuid():N}.db");
        _db = new Db(_databasePath);

        ExecuteScript(ReadScript("schema.sql"));
        ExecuteScript(ReadFixture("demo-seed.sql"));

        _reports = new ReportService(
            new AttemptRepository(_db),
            new QuizRepository(_db),
            new TopicRepository(_db));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // The data layer pools its connections; without clearing the pool the
        // database file stays open and the delete would fail.
        SqliteConnection.ClearAllPools();

        File.Delete(_databasePath);
        File.Delete(_databasePath + "-journal");
        File.Delete(_databasePath + "-wal");
        File.Delete(_databasePath + "-shm");
    }

    // -------------------------------------------------------------------------
    // 1. Per-topic rows: every number matches the hand-computed seed math.
    // -------------------------------------------------------------------------
    [Fact]
    public void Build_TopicRows_MatchSeedData()
    {
        PracticeReport report = _reports.Build(userId: 1);

        Assert.Equal(5, report.Topics.Count); // one row per seeded topic, including the empty ones

        TopicReport csharp = report.Topics.Single(row => row.TopicName == "C#");
        Assert.Equal(2, csharp.Attempts);
        Assert.Equal(60d, csharp.AveragePercent); // (4 + 2) / 2 of 5 = 60%
        Assert.Equal(80d, csharp.BestPercent);
        Assert.Equal(40d, csharp.LatestPercent); // the 11:00 attempt (2/5) is the newest
        Assert.True(csharp.NeedsPractice); // 60% < the 70% threshold

        TopicReport python = report.Topics.Single(row => row.TopicName == "Python");
        Assert.Equal(1, python.Attempts);
        Assert.Equal(100d, python.AveragePercent);
        Assert.Equal(100d, python.BestPercent);
        Assert.Equal(100d, python.LatestPercent);
        Assert.False(python.NeedsPractice);

        TopicReport sql = report.Topics.Single(row => row.TopicName == "SQL");
        Assert.Equal(1, sql.Attempts);
        Assert.Equal(60d, sql.AveragePercent);
        Assert.Equal(60d, sql.BestPercent);
        Assert.Equal(60d, sql.LatestPercent);
        Assert.True(sql.NeedsPractice);
    }

    // -------------------------------------------------------------------------
    // 2. Zero attempts = the defined empty row: zeros, no flag, no exception.
    // -------------------------------------------------------------------------
    [Fact]
    public void Build_TopicWithZeroAttempts_ReturnsDefinedEmptyRow()
    {
        PracticeReport report = _reports.Build(userId: 1);

        TopicReport networking = report.Topics.Single(row => row.TopicName == "Networking");
        Assert.Equal(0, networking.Attempts);
        Assert.Equal(0d, networking.AveragePercent);
        Assert.Equal(0d, networking.BestPercent);
        Assert.Equal(0d, networking.LatestPercent);
        Assert.False(networking.NeedsPractice); // zero data is no judgement
    }

    // -------------------------------------------------------------------------
    // 3. Per-difficulty rows: Easy 60%, Medium 100%, Hard empty, Insane empty
    //    (the four rows always come in the declaration order of the enum).
    // -------------------------------------------------------------------------
    [Fact]
    public void Build_DifficultyRows_MatchSeedData()
    {
        PracticeReport report = _reports.Build(userId: 1);

        Assert.Equal(4, report.Difficulties.Count);
        Assert.Equal(
            new[] { Difficulty.Easy, Difficulty.Medium, Difficulty.Hard, Difficulty.Insane },
            report.Difficulties.Select(row => row.Difficulty).ToArray());

        DifficultyReport easy = report.Difficulties.Single(row => row.Difficulty == Difficulty.Easy);
        Assert.Equal(3, easy.Attempts); // quizzes 1 and 3 are Easy
        Assert.Equal(60d, easy.AveragePercent); // (4 + 2 + 3) / 15 = 60%

        DifficultyReport medium = report.Difficulties.Single(row => row.Difficulty == Difficulty.Medium);
        Assert.Equal(1, medium.Attempts);
        Assert.Equal(100d, medium.AveragePercent);

        DifficultyReport hard = report.Difficulties.Single(row => row.Difficulty == Difficulty.Hard);
        Assert.Equal(0, hard.Attempts); // only the Draft quiz is Hard — it has no attempts
        Assert.Equal(0d, hard.AveragePercent);

        DifficultyReport insane = report.Difficulties.Single(row => row.Difficulty == Difficulty.Insane);
        Assert.Equal(0, insane.Attempts); // the demo fixture holds no Insane quiz
        Assert.Equal(0d, insane.AveragePercent);
    }

    // -------------------------------------------------------------------------
    // 4. Overall summary and the weak-topic list (weakest first, ties by name).
    // -------------------------------------------------------------------------
    [Fact]
    public void Build_OverallAndWeakTopics_MatchSeedData()
    {
        PracticeReport report = _reports.Build(userId: 1);

        Assert.Equal(4, report.Overall.TotalAttempts);
        Assert.Equal(3, report.Overall.QuizzesTaken); // quizzes 1, 2 and 3 were taken
        Assert.Equal(70d, report.Overall.AveragePercent); // (4 + 2 + 5 + 3) / 20 = 70%

        Assert.Equal(2, report.WeakTopics.Count);
        Assert.Equal(new[] { "C#", "SQL" }, report.WeakTopics.Select(row => row.TopicName).ToArray());
        Assert.All(report.WeakTopics, row => Assert.True(row.AveragePercent < ReportService.PracticeThresholdPercent));
    }

    // -------------------------------------------------------------------------
    // 5. The difficulty filter narrows the data: only Medium's attempt counts.
    // -------------------------------------------------------------------------
    [Fact]
    public void Build_WithDifficultyFilter_NarrowsTheData()
    {
        PracticeReport report = _reports.Build(userId: 1, difficulty: Difficulty.Medium);

        Assert.Equal(1, report.Overall.TotalAttempts);
        Assert.Equal(1, report.Overall.QuizzesTaken);
        Assert.Equal(100d, report.Overall.AveragePercent);

        TopicReport python = report.Topics.Single(row => row.TopicName == "Python");
        Assert.Equal(1, python.Attempts);
        TopicReport csharp = report.Topics.Single(row => row.TopicName == "C#");
        Assert.Equal(0, csharp.Attempts); // filtered out — the defined empty row again
    }

    // -------------------------------------------------------------------------
    // 6. A user with no attempts gets the all-zeros report, not an exception.
    // -------------------------------------------------------------------------
    [Fact]
    public void Build_UserWithNoAttempts_ReturnsAllZeroReport()
    {
        PracticeReport report = _reports.Build(userId: 999); // no such user, no attempts

        Assert.Equal(0, report.Overall.TotalAttempts);
        Assert.Equal(0, report.Overall.QuizzesTaken);
        Assert.Equal(0d, report.Overall.AveragePercent);
        Assert.Empty(report.WeakTopics);
        Assert.Equal(5, report.Topics.Count); // the topic rows still exist (empty rows)
        Assert.All(report.Topics, row => Assert.Equal(0, row.Attempts));
    }

    // -------------------------------------------------------------------------
    // Scratch-database plumbing (the RepositoryTests pattern, self-contained).
    // -------------------------------------------------------------------------

    private void ExecuteScript(string script)
    {
        using var connection = _db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = script;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Reads one SQL script of the database folder (read-only). The folder is
    /// found by walking up from the test directory, so the test works no
    /// matter which output folder dotnet test has chosen.
    /// </summary>
    private static string ReadScript(string fileName)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "database", fileName);

            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"The SQL script '{fileName}' was not found in a 'database' folder above the test directory.");
    }

    /// <summary>
    /// Reads the frozen pre-campaign demo fixture (tests/CodeQuest.Tests/Fixtures)
    /// — the rich sample data this file's hand-computed expectations are built
    /// on. The folder is found by walking up from the test directory.
    /// </summary>
    private static string ReadFixture(string fileName)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "tests", "CodeQuest.Tests", "Fixtures", fileName);

            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"The SQL fixture '{fileName}' was not found in the tests/CodeQuest.Tests/Fixtures folder above the test directory.");
    }
}
