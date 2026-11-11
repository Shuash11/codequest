// Task 5 rival-numbering tests: the pure campaign rules (Campaign) — level
// titles, level numbers and the next-level scan. The scan must ignore other
// topics, drafts, non-level titles and the quiz the caller just inserted, and
// it must follow the highest + 1 rule across gaps. Pure model tests.

using CodeQuest.Models;

namespace CodeQuest.Tests;

public class CampaignTests
{
    private const int CampaignTopic = 1;

    // -------------------------------------------------------------------------
    // 1. Level titles — exactly "Level " plus digits, nothing else.
    // -------------------------------------------------------------------------
    [Theory]
    [InlineData("Level 1", true)]
    [InlineData("Level 25", true)]
    [InlineData("Level 007", true)]
    [InlineData("Level", false)]
    [InlineData("Level one", false)]
    [InlineData("level 1", false)]
    [InlineData("Level 1 ", false)]
    [InlineData("Level 1 (2)", false)]
    [InlineData("Level 26 is ready", false)]
    [InlineData("Random mix \u2014 2026-11-12 09:00:00", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsLevelTitle_MatchesOnlyTheExactLevelPattern(string? title, bool expected)
    {
        Assert.Equal(expected, Campaign.IsLevelTitle(title));
    }

    [Theory]
    [InlineData("Level 7", 7)]
    [InlineData("Level 25", 25)]
    [InlineData("Level one", 0)]
    [InlineData("Other", 0)]
    [InlineData(null, 0)]
    public void LevelNumberOf_ReturnsTheNumberOrZero(string? title, int expected)
    {
        Assert.Equal(expected, Campaign.LevelNumberOf(title));
    }

    // -------------------------------------------------------------------------
    // 2. The normal case: a full 25-level campaign yields 26.
    // -------------------------------------------------------------------------
    [Fact]
    public void NextLevelNumber_AfterTheFullCampaign_IsTwentySix()
    {
        List<Quiz> quizzes = new();
        for (int level = 1; level <= 25; level++)
        {
            quizzes.Add(MakeQuiz(level, CampaignTopic, $"Level {level}", Difficulty.Insane));
        }

        Assert.Equal(26, Campaign.NextLevelNumber(quizzes, CampaignTopic));
    }

    // -------------------------------------------------------------------------
    // 3. The just-inserted draft is excluded by id: an AI title of "Level 26"
    //    on a draft that is ALREADY counted otherwise must not push the scan.
    // -------------------------------------------------------------------------
    [Fact]
    public void NextLevelNumber_ExcludesTheJustInsertedDraft_EvenWithALevelTitle()
    {
        List<Quiz> quizzes = new();
        for (int level = 1; level <= 25; level++)
        {
            quizzes.Add(MakeQuiz(level, CampaignTopic, $"Level {level}", Difficulty.Insane));
        }

        // The rival's own draft: approved on purpose, so only the id exclusion
        // can keep it out of the scan.
        quizzes.Add(MakeQuiz(99, CampaignTopic, "Level 26", Difficulty.Insane));

        Assert.Equal(26, Campaign.NextLevelNumber(quizzes, CampaignTopic, excludingQuizId: 99));
    }

    // -------------------------------------------------------------------------
    // 4. Drafts never count: a half-finished level does not occupy a number.
    // -------------------------------------------------------------------------
    [Fact]
    public void NextLevelNumber_IgnoresDraftQuizzes()
    {
        List<Quiz> quizzes = new()
        {
            MakeQuiz(1, CampaignTopic, "Level 1", Difficulty.Easy),
            MakeQuiz(2, CampaignTopic, "Level 25", Difficulty.Insane, QuizStatus.Draft),
            MakeQuiz(3, CampaignTopic, "Level 30", Difficulty.Insane, QuizStatus.Draft),
        };

        Assert.Equal(2, Campaign.NextLevelNumber(quizzes, CampaignTopic));
    }

    // -------------------------------------------------------------------------
    // 5. Gaps follow the highest + 1 rule (never fill a hole).
    // -------------------------------------------------------------------------
    [Fact]
    public void NextLevelNumber_AcrossGaps_IsTheHighestPlusOne()
    {
        List<Quiz> quizzes = new()
        {
            MakeQuiz(1, CampaignTopic, "Level 1", Difficulty.Easy),
            MakeQuiz(2, CampaignTopic, "Level 2", Difficulty.Easy),
            MakeQuiz(3, CampaignTopic, "Level 5", Difficulty.Easy),
        };

        Assert.Equal(6, Campaign.NextLevelNumber(quizzes, CampaignTopic));
    }

    // -------------------------------------------------------------------------
    // 6. Other topics and non-level titles are ignored entirely.
    // -------------------------------------------------------------------------
    [Fact]
    public void NextLevelNumber_IgnoresOtherTopicsAndWeirdTitles()
    {
        List<Quiz> quizzes = new()
        {
            MakeQuiz(1, CampaignTopic, "Level 3", Difficulty.Easy),
            MakeQuiz(2, 2, "Level 500", Difficulty.Insane),            // another topic
            MakeQuiz(3, CampaignTopic, "Level 4 (2)", Difficulty.Easy), // dedupe suffix
            MakeQuiz(4, CampaignTopic, "Level one", Difficulty.Easy),
            MakeQuiz(5, CampaignTopic, "Random mix", Difficulty.Medium),
        };

        Assert.Equal(4, Campaign.NextLevelNumber(quizzes, CampaignTopic));
    }

    // -------------------------------------------------------------------------
    // 7. An empty campaign starts at 1.
    // -------------------------------------------------------------------------
    [Fact]
    public void NextLevelNumber_OnAnEmptyCampaign_IsOne()
    {
        Assert.Equal(1, Campaign.NextLevelNumber(Array.Empty<Quiz>(), CampaignTopic));
    }

    private static Quiz MakeQuiz(
        int id,
        int topicId,
        string title,
        Difficulty difficulty,
        QuizStatus status = QuizStatus.Approved) => new()
    {
        QuizId = id,
        TopicId = topicId,
        Title = title,
        Difficulty = difficulty,
        Status = status,
        CreatedAt = new DateTimeOffset(2026, 11, 12, 9, 0, 0, TimeSpan.Zero),
    };
}
