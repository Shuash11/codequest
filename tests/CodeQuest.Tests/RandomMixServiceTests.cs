// Random mix (task 4): the service builds a fresh quiz from random questions
// across every APPROVED quiz, copies them under the dedicated "Random Mix"
// topic with shuffled answers, and refuses when too few questions exist.
// Every case runs against its own fresh schema-only SQLite database — full
// control over what is approved, so the draw rules are deterministic.

using CodeQuest.Data;
using CodeQuest.Data.Repositories;
using CodeQuest.Models;
using CodeQuest.Services;
using Microsoft.Data.Sqlite;

namespace CodeQuest.Tests;

public class RandomMixServiceTests : IDisposable
{
    private readonly string _databasePath;
    private readonly Db _db;
    private readonly QuizRepository _quizzes;
    private readonly QuestionRepository _questions;
    private readonly TopicRepository _topics;
    private readonly RandomMixService _service;

    public RandomMixServiceTests()
    {
        string scratchDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Temp", "opencode");
        Directory.CreateDirectory(scratchDirectory);

        _databasePath = Path.Combine(scratchDirectory, $"codequest-mix-tests-{Guid.NewGuid():N}.db");
        _db = new Db(_databasePath);
        ExecuteScript(ReadScript("schema.sql"));

        _quizzes = new QuizRepository(_db);
        _questions = new QuestionRepository(_db);
        _topics = new TopicRepository(_db);
        _service = new RandomMixService(_quizzes, _questions, _topics);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
        File.Delete(_databasePath + "-journal");
        File.Delete(_databasePath + "-wal");
        File.Delete(_databasePath + "-shm");
    }

    [Fact]
    public void CountDrawableQuestions_CountsOnlyApprovedQuizzes()
    {
        int topicId = AddTopic("C#");
        int approved = AddQuizWithQuestions(topicId, "Approved quiz", QuizStatus.Approved, multipleChoiceCount: 2);
        AddQuizWithQuestions(topicId, "Draft quiz", QuizStatus.Draft, multipleChoiceCount: 3);

        Assert.Equal(2, _service.CountDrawableQuestions());
    }

    [Fact]
    public void CreateMix_PersistsAnApprovedMediumQuizUnderTheRandomMixTopic()
    {
        SeedTwoApprovedQuizzes();

        int mixId = _service.CreateMix();

        Quiz mix = _quizzes.GetById(mixId);
        Assert.NotNull(mix);
        Assert.Equal(Difficulty.Medium, mix.Difficulty);
        Assert.Equal(QuizStatus.Approved, mix.Status);
        Assert.StartsWith("Random mix", mix.Title);

        Topic topic = _topics.GetAll().Single(t => t.TopicId == mix.TopicId);
        Assert.Equal("Random Mix", topic.Name);
    }

    [Fact]
    public void CreateMix_DrawsEveryApprovedQuestion_AndNothingFromDrafts()
    {
        SeedTwoApprovedQuizzes(); // 3 + 2 = 5 approved questions
        int topicId = AddTopic("General Knowledge");
        AddQuizWithQuestions(topicId, "Draft quiz", QuizStatus.Draft, multipleChoiceCount: 4);

        int mixId = _service.CreateMix();

        IReadOnlyList<Question> mixQuestions = _questions.GetByQuiz(mixId);
        Assert.Equal(5, mixQuestions.Count); // min(10, 5 drawable)
        Assert.All(mixQuestions, question => Assert.False(
            question.QuestionText.Contains("DRAFT-", StringComparison.Ordinal),
            "a draft question must never be drawn"));
    }

    [Fact]
    public void CreateMix_CopiesChoicesWithExactlyOneCorrectAnswer()
    {
        SeedTwoApprovedQuizzes();

        int mixId = _service.CreateMix();

        foreach (Question question in _questions.GetByQuiz(mixId))
        {
            int expected = question.QuestionKind == "True/False" ? 2 : 4;
            Assert.Equal(expected, question.Choices.Count);
            Assert.Equal(1, question.Choices.Count(choice => choice.IsCorrect));
            Assert.Equal(
                question.Choices.Count,
                question.Choices.Select(choice => choice.Position).Distinct().Count());
        }
    }

    [Fact]
    public void CreateMix_PreservesTheQuestionKind()
    {
        SeedTwoApprovedQuizzes(); // one multiple-choice quiz, one true/false quiz

        int mixId = _service.CreateMix();

        IReadOnlyList<Question> mixQuestions = _questions.GetByQuiz(mixId);
        Assert.Contains(mixQuestions, question => question.QuestionKind == "Multiple Choice");
        Assert.Contains(mixQuestions, question => question.QuestionKind == "True/False");
    }

    [Fact]
    public void CreateMix_ReusesOneRandomMixTopic()
    {
        SeedTwoApprovedQuizzes();

        _service.CreateMix();
        _service.CreateMix();

        Assert.Single(_topics.GetAll().Where(t => t.Name == "Random Mix"));
        Assert.Equal(2, _quizzes.GetAll().Count(quiz => quiz.Title.StartsWith("Random mix", StringComparison.Ordinal)));
    }

    [Fact]
    public void CreateMix_WithTooFewQuestions_ThrowsAndCreatesNothing()
    {
        int topicId = AddTopic("C#");
        AddQuizWithQuestions(topicId, "Tiny quiz", QuizStatus.Approved, multipleChoiceCount: 2);

        Assert.Throws<RandomMixException>(() => _service.CreateMix());
        Assert.Empty(_quizzes.GetAll().Where(quiz => quiz.Title.StartsWith("Random mix", StringComparison.Ordinal)));
    }

    [Fact]
    public void CreateMix_WithFewerThanTenQuestions_TakesThemAll()
    {
        SeedTwoApprovedQuizzes(); // 5 questions

        int mixId = _service.CreateMix();

        Assert.Equal(5, _questions.GetByQuiz(mixId).Count);
    }

    private void SeedTwoApprovedQuizzes()
    {
        int csharp = AddTopic("C#");
        AddQuizWithQuestions(csharp, "Approved choice quiz", QuizStatus.Approved, multipleChoiceCount: 3, draftCount: 0);
        int security = AddTopic("Web Security");
        AddQuizWithQuestions(security, "Approved truefalse quiz", QuizStatus.Approved, multipleChoiceCount: 0, trueFalseCount: 2);
    }

    private int AddTopic(string name)
    {
        var topic = new Topic
        {
            Name = name,
            Category = "Programming",
            Description = $"{name} topic",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _topics.Add(topic);
        return topic.TopicId;
    }

    private int AddQuizWithQuestions(
        int topicId,
        string title,
        QuizStatus status,
        int multipleChoiceCount = 0,
        int trueFalseCount = 0,
        int draftCount = 0)
    {
        var quiz = new Quiz
        {
            TopicId = topicId,
            Title = title,
            Difficulty = Difficulty.Easy,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        int quizId = _quizzes.Add(quiz);

        int position = 1;
        for (int i = 0; i < multipleChoiceCount; i++)
        {
            _questions.Add(BuildMultipleChoice(quizId, position, $"Q{position}"));
            position++;
        }

        for (int i = 0; i < trueFalseCount; i++)
        {
            _questions.Add(BuildTrueFalse(quizId, position, $"Q{position}"));
            position++;
        }

        for (int i = 0; i < draftCount; i++)
        {
            _questions.Add(BuildMultipleChoice(quizId, position, $"DRAFT-Q{position}"));
            position++;
        }

        return quizId;
    }

    private static Question BuildMultipleChoice(int quizId, int position, string text)
    {
        var question = new MultipleChoiceQuestion
        {
            QuizId = quizId,
            QuestionText = text,
            Explanation = $"{text} explanation",
            Position = position,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        for (int choice = 1; choice <= 4; choice++)
        {
            question.AddChoice(new Choice
            {
                ChoiceText = $"{text} option {choice}",
                IsCorrect = choice == 1,
                Position = choice,
            });
        }

        return question;
    }

    private static Question BuildTrueFalse(int quizId, int position, string text)
    {
        var question = new TrueFalseQuestion
        {
            QuizId = quizId,
            QuestionText = text,
            Explanation = $"{text} explanation",
            Position = position,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        question.AddChoice(new Choice { ChoiceText = "True", IsCorrect = true, Position = 1 });
        question.AddChoice(new Choice { ChoiceText = "False", IsCorrect = false, Position = 2 });
        return question;
    }

    private void ExecuteScript(string script)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = script;
        command.ExecuteNonQuery();
    }

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
