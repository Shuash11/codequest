// The rival level naming: the next number, its ladder difficulty and the
// replacement of a leftover Draft that occupies the number.

using CodeQuest.Data;
using CodeQuest.Data.Repositories;
using CodeQuest.Models;
using CodeQuest.Services;
using Microsoft.Data.Sqlite;

namespace CodeQuest.Tests;

public class CampaignLevelServiceTests : IDisposable
{
    private static readonly DateTimeOffset FixedTime = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);

    private readonly string _databasePath;
    private readonly Db _db;
    private readonly TopicRepository _topics;
    private readonly QuizRepository _quizzes;
    private readonly CampaignLevelService _levels;

    public CampaignLevelServiceTests()
    {
        string scratchDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Temp", "opencode");
        Directory.CreateDirectory(scratchDirectory);

        _databasePath = Path.Combine(scratchDirectory, $"codequest-rival-level-tests-{Guid.NewGuid():N}.db");
        _db = new Db(_databasePath);

        ExecuteScript(ReadScript("schema.sql"));

        _topics = new TopicRepository(_db);
        _quizzes = new QuizRepository(_db);
        _levels = new CampaignLevelService(_quizzes);
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
    // 1. The exact dead-end: approved "Level 25" + a leftover DRAFT "Level 26"
    //    -> the scan yields 26 -> the occupier draft is deleted -> the rename
    //    lands: approved "Level 26" exists once, the old draft id is gone.
    // -------------------------------------------------------------------------
    [Fact]
    public void NameAndApproveNextLevel_WhenALeftoverDraftOccupiesTheNumber_ReplacesIt()
    {
        int topicId = InsertCampaignTopic();
        InsertQuiz(topicId, "Level 25", Difficulty.Insane, QuizStatus.Approved);
        int staleDraftId = InsertQuiz(topicId, Campaign.GeneratedTitle(26, "joashua"), Difficulty.Medium, QuizStatus.Draft, questionCount: 1);

        // The AI draft the UI just inserted; "Level 26" was taken, so the
        // suffix loop gave it "Level 26 (2)".
        int aiDraftId = InsertQuiz(topicId, Campaign.GeneratedTitle(26, "joashua") + " (2)", Difficulty.Medium, QuizStatus.Draft, questionCount: 2);

        // Precondition â€” this is the scenario that used to collide forever.
        Assert.Equal(26, Campaign.NextLevelNumber(_quizzes.GetAll(), topicId));
        Assert.True(_quizzes.ExistsByTitle(topicId, Campaign.GeneratedTitle(26, "joashua"), ignoreQuizId: aiDraftId));

        bool named = _levels.NameAndApproveNextLevel(aiDraftId, topicId, 1, "joashua");

        Assert.True(named);

        // Exactly one "Level 26" â€” the AI draft â€” approved at the ladder tier.
        IReadOnlyList<Quiz> topicQuizzes = _quizzes.GetByTopic(topicId);
        Assert.Equal(2, topicQuizzes.Count);
        Quiz level26 = Assert.Single(topicQuizzes, quiz => quiz.Title == Campaign.GeneratedTitle(26, "joashua"));
        Assert.Equal(1, level26.GeneratedForUserId);
        Assert.Equal(aiDraftId, level26.QuizId);
        Assert.Equal(QuizStatus.Approved, level26.Status);
        Assert.Equal(Difficulty.Insane, level26.Difficulty);

        // The stale draft id is gone with its content (cascade; it had no
        // attempts â€” StartQuiz only runs approved quizzes).
        Assert.Null(_quizzes.GetById(staleDraftId));
        Assert.Equal(0, Scalar($"SELECT COUNT(*) FROM questions WHERE quiz_id = {staleDraftId};"));
        Assert.Equal(4, Scalar("SELECT COUNT(*) FROM choices;")); // the AI draft's 2x2 survive

        // The AI content survived the rename, and the campaign moved on.
        Assert.Equal(2, _quizzes.GetById(aiDraftId)!.Questions.Count);
        Assert.Equal(27, Campaign.NextLevelNumber(_quizzes.GetAll(), topicId));
    }

    // -------------------------------------------------------------------------
    // 2. The AI draft may itself carry the next title ("Level 2" when only
    //    "Level 1" exists): the self-exclusion guard must not delete it and
    //    the update lands on its own row.
    // -------------------------------------------------------------------------
    [Fact]
    public void NameAndApproveNextLevel_WhenTheDraftItselfHoldsTheTitle_ApprovesItInPlace()
    {
        int topicId = InsertCampaignTopic();
        InsertQuiz(topicId, "Level 1", Difficulty.Easy, QuizStatus.Approved);
        int aiDraftId = InsertQuiz(topicId, Campaign.GeneratedTitle(2, "joashua"), Difficulty.Medium, QuizStatus.Draft, questionCount: 1);

        bool named = _levels.NameAndApproveNextLevel(aiDraftId, topicId, 1, "joashua");

        Assert.True(named);
        IReadOnlyList<Quiz> topicQuizzes = _quizzes.GetByTopic(topicId);
        Quiz level2 = Assert.Single(topicQuizzes, quiz => quiz.Title == Campaign.GeneratedTitle(2, "joashua"));
        Assert.Equal(aiDraftId, level2.QuizId);
        Assert.Equal(QuizStatus.Approved, level2.Status);
        Assert.Equal(Difficulty.Easy, level2.Difficulty); // the ladder tier of Level 2
        Assert.Equal(2, topicQuizzes.Count);
    }

    // -------------------------------------------------------------------------
    // 3. The difficulty follows the ladder, not a fixed tier: with Levels 1-5
    //    in place, the rival's next level is 6 â€” Medium, even though the
    //    generation request asked for Insane content.
    // -------------------------------------------------------------------------
    [Fact]
    public void NameAndApproveNextLevel_StampsTheLadderDifficultyOfTheNextNumber()
    {
        int topicId = InsertCampaignTopic();
        for (int level = 1; level <= 5; level++)
        {
            InsertQuiz(topicId, $"Level {level}", Difficulty.Easy, QuizStatus.Approved);
        }

        int aiDraftId = InsertQuiz(topicId, "AI authored title", Difficulty.Insane, QuizStatus.Draft, questionCount: 1);

        bool named = _levels.NameAndApproveNextLevel(aiDraftId, topicId, 1, "joashua");

        Assert.True(named);
        Quiz level6 = Assert.Single(_quizzes.GetByTopic(topicId), quiz => quiz.Title == Campaign.GeneratedTitle(6, "joashua"));
        Assert.Equal(Difficulty.Medium, level6.Difficulty);
        Assert.Equal(QuizStatus.Approved, level6.Status);
    }

    // -------------------------------------------------------------------------
    // 4. The guarded helper itself: the belt-and-braces delete never touches an
    //    approved quiz and never reaches another topic; only the exact-title
    //    Draft of the given topic is removed.
    // -------------------------------------------------------------------------
    [Fact]
    public void DeleteDraftWithTitle_DeletesOnlyTheExactTitleDraftOfThatTopic()
    {
        int topicId = InsertCampaignTopic();
        int approvedId = InsertQuiz(topicId, "Level 25", Difficulty.Insane, QuizStatus.Approved);
        int draftId = InsertQuiz(topicId, "Level 26", Difficulty.Medium, QuizStatus.Draft);

        int otherTopicId = _topics.Add(new Topic
        {
            Name = "Other Topic",
            Category = "Programming",
            Description = "A topic with a same-named draft.",
            CreatedAt = FixedTime,
        });
        int otherDraftId = InsertQuiz(otherTopicId, "Level 26", Difficulty.Medium, QuizStatus.Draft);

        // An approved title never matches, even with the exact title.
        Assert.False(_quizzes.DeleteDraftWithTitle(topicId, "Level 25"));
        Assert.NotNull(_quizzes.GetById(approvedId));

        // The draft of this topic is deleted; the other topic's draft stays.
        Assert.True(_quizzes.DeleteDraftWithTitle(topicId, "Level 26"));
        Assert.Null(_quizzes.GetById(draftId));
        Assert.NotNull(_quizzes.GetById(otherDraftId));

        // A second call finds nothing to delete.
        Assert.False(_quizzes.DeleteDraftWithTitle(topicId, "Level 26"));
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private int InsertCampaignTopic()
        => _topics.Add(new Topic
        {
            Name = Campaign.TopicName,
            Category = "Programming",
            Description = "The progressive campaign.",
            CreatedAt = FixedTime,
        });

    private int InsertQuiz(
        int topicId,
        string title,
        Difficulty difficulty,
        QuizStatus status,
        int questionCount = 0,
        int? generatedForUserId = null)
    {
        var quiz = new Quiz
        {
            TopicId = topicId,
            Title = title,
            Difficulty = difficulty,
            Status = status,
            CreatedAt = FixedTime,
            GeneratedForUserId = generatedForUserId,
        };

        for (int index = 0; index < questionCount; index++)
        {
            var question = new MultipleChoiceQuestion
            {
                QuestionText = $"{title} question {index + 1}?",
                Explanation = "Because the correct option says so.",
                Position = index + 1,
                CreatedAt = FixedTime,
                Difficulty = difficulty,
            };
            question.AddChoice(new Choice { ChoiceText = "correct", IsCorrect = true, Position = 1 });
            question.AddChoice(new Choice { ChoiceText = "wrong", IsCorrect = false, Position = 2 });
            quiz.AddQuestion(question);
        }

        return _quizzes.Add(quiz);
    }

    private long Scalar(string sql)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private void ExecuteScript(string script)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = script;
        command.ExecuteNonQuery();
    }

    // Walks up from the test directory, so the script is found no matter which
    // output folder dotnet test has chosen.
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
}
