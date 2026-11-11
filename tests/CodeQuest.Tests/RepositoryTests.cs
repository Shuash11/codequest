// =============================================================================
// CodeQuest — Phase P4: Data Access Layer
// Tests for: the repository layer of phase P4 (all seven repositories), run
// against a FRESH temporary SQLite database that every test builds for itself
// by executing database/schema.sql and the frozen pre-campaign demo fixture
// (tests/CodeQuest.Tests/Fixtures/demo-seed.sql, read as text — the scripts
// are never modified). The scratch file lives in the user's temp folder with
// a unique name per test and is deleted again in the teardown; nothing is
// left in the repository tree.
// Look at: the nine cases below — one behavior each, independent and hand-
//          verifiable against the demo fixture:
//            1. seeded round-trip (counts and spot values),
//            2. topic CRUD,
//            3. atomic quiz write (quiz + questions + choices in one call),
//            4. status update Draft -> Approved,
//            5. FK-enforced delete THROWS (proves PRAGMA foreign_keys=ON),
//            6. atomic rollback (forced failure leaves no partial rows),
//            7. R-2 #3 rejection (answer of a foreign question — nothing kept),
//            8. SQL-injection sanity (hostile text stored as literal data),
//            9. duplicate rejection (UNIQUE topics.name -> DuplicateEntity).
// The in-memory model construction follows the ScoringServiceTests pattern of
// phase P3; the persistence goes through the repositories only.
// =============================================================================

using CodeQuest.Data;
using CodeQuest.Data.Repositories;
using CodeQuest.Models;
using Microsoft.Data.Sqlite;

namespace CodeQuest.Tests;

/// <summary>
/// Phase P4 repository tests: every case runs against its own fresh copy of
/// the demo database (schema.sql + the frozen demo fixture) and proves one
/// behavior of the data access layer.
/// </summary>
public class RepositoryTests : IDisposable
{
    /// <summary>A timestamp with whole seconds — the format the data layer stores (ISO-8601 UTC).</summary>
    private static readonly DateTimeOffset FixedTime = new(2026, 11, 4, 8, 0, 0, TimeSpan.Zero);

    private readonly string _databasePath;
    private readonly Db _db;
    private readonly TopicRepository _topics;
    private readonly QuizRepository _quizzes;
    private readonly QuestionRepository _questions;
    private readonly ChoiceRepository _choices;
    private readonly AttemptRepository _attempts;
    private readonly SubmittedAnswerRepository _answers;
    private readonly UserRepository _users;

    /// <summary>
    /// Builds one fresh seeded database for THIS test: a uniquely named SQLite
    /// file in the temp folder, created by executing the schema and seed
    /// scripts through a connection of the data layer.
    /// </summary>
    public RepositoryTests()
    {
        string scratchDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Temp", "opencode");
        Directory.CreateDirectory(scratchDirectory);

        _databasePath = Path.Combine(scratchDirectory, $"codequest-repository-tests-{Guid.NewGuid():N}.db");
        _db = new Db(_databasePath);

        ExecuteScript(ReadScript("schema.sql"));
        ExecuteScript(ReadFixture("demo-seed.sql"));

        _topics = new TopicRepository(_db);
        _quizzes = new QuizRepository(_db);
        _questions = new QuestionRepository(_db);
        _choices = new ChoiceRepository(_db);
        _attempts = new AttemptRepository(_db);
        _answers = new SubmittedAnswerRepository(_db);
        _users = new UserRepository(_db);
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
    // 1. Seeded round-trip — every count and spot value matches database/seed.sql
    //    (5 topics, 5 quizzes, 21 questions, 84 choices, 4 attempts, 20 answers,
    //    user 'demo'; the approved quizzes are the takeable ones).
    // -------------------------------------------------------------------------
    [Fact]
    public void SeededDatabase_RoundTrip_MatchesSeedCountsAndSpotValues()
    {
        // Topics: 3 programming and 2 cybersecurity subjects (seed.sql lines 52-57).
        Assert.Equal(5, _topics.GetAll().Count);
        Assert.Equal(3, _topics.GetByCategory("Programming").Count);
        Assert.Equal(2, _topics.GetByCategory("Cybersecurity").Count);

        Topic? csharp = _topics.GetById(1);
        Assert.NotNull(csharp);
        Assert.Equal("C#", csharp.Name);
        Assert.Equal("Programming", csharp.Category);

        // Quizzes: 1-3 Approved (takeable), 4-5 Draft (listed separately).
        IReadOnlyList<Quiz> quizzes = _quizzes.GetAll();
        Assert.Equal(5, quizzes.Count);
        Assert.Equal(new[] { 1, 2, 3 }, quizzes.Where(q => q.Status == QuizStatus.Approved).Select(q => q.QuizId).ToArray());
        Assert.Equal(new[] { 4, 5 }, quizzes.Where(q => q.Status == QuizStatus.Draft).Select(q => q.QuizId).ToArray());

        // Every seeded question exists with its options, and the answer key of
        // every question holds exactly one correct option.
        int questionCount = 0;
        int choiceCount = 0;

        foreach (Quiz quiz in quizzes)
        {
            Quiz? loaded = _quizzes.GetById(quiz.QuizId);
            Assert.NotNull(loaded);

            questionCount += loaded.Questions.Count;

            foreach (Question question in loaded.Questions)
            {
                choiceCount += question.Choices.Count;
                Assert.Equal(1, question.Choices.Count(choice => choice.IsCorrect));
            }
        }

        Assert.Equal(21, questionCount);
        Assert.Equal(84, choiceCount);

        // Spot values of quiz 1 (seed.sql lines 64-130).
        Quiz? quiz1 = _quizzes.GetById(1);
        Assert.NotNull(quiz1);
        Assert.Equal("C# Core Concepts", quiz1.Title);
        Assert.Equal(Difficulty.Easy, quiz1.Difficulty);
        Assert.Equal(QuizStatus.Approved, quiz1.Status);
        Assert.Equal(5, quiz1.Questions.Count);

        Question first = quiz1.Questions[0];
        Assert.Equal("Which C# keyword declares a constant whose value is fixed at compile time?", first.QuestionText);

        Choice? correct = first.GetCorrectChoice();
        Assert.NotNull(correct);
        Assert.Equal("const", correct.ChoiceText);

        // Attempts: four runs by the demo user, newest first — 13:00 (3),
        // 12:00 (5), 11:00 (2), 10:00 (4) — two of them on quiz 1.
        IReadOnlyList<QuizAttempt> attempts = _attempts.GetByUser(1);
        Assert.Equal(4, attempts.Count);
        Assert.Equal(new[] { 3, 5, 2, 4 }, attempts.Select(attempt => attempt.Score).ToArray());
        Assert.Equal(2, _attempts.GetByQuiz(1).Count);

        // Answers: 5 per attempt; attempt 1 picked option 15 for question 4.
        int answerCount = 0;
        for (int attemptId = 1; attemptId <= 4; attemptId++)
        {
            answerCount += _answers.GetByAttempt(attemptId).Count;
        }

        Assert.Equal(20, answerCount);

        SubmittedAnswer? answer = _answers.GetByAttemptAndQuestion(1, 4);
        Assert.NotNull(answer);
        Assert.Equal(15, answer.ChosenChoiceId);

        // User: the single seeded login account.
        UserAccount? demo = _users.GetByUsername("demo");
        Assert.NotNull(demo);
        Assert.Equal(1, demo.UserId);
        Assert.Equal("Demo Student", demo.DisplayName);
        Assert.Single(_users.GetAll());
    }

    // -------------------------------------------------------------------------
    // 2. Topic CRUD — create, update and delete a standalone topic round-trips.
    // -------------------------------------------------------------------------
    [Fact]
    public void TopicRepository_AddUpdateDelete_RoundTrips()
    {
        Topic created = new()
        {
            Name = "Cloud Security",
            Category = "Cybersecurity",
            Description = "Cloud security fundamentals: identity, encryption, and shared responsibility.",
            CreatedAt = FixedTime,
        };

        int topicId = _topics.Add(created);
        Assert.True(topicId > 5); // the seed used the ids 1..5

        Topic? loaded = _topics.GetById(topicId);
        Assert.NotNull(loaded);
        Assert.Equal("Cloud Security", loaded.Name);
        Assert.Equal("Cybersecurity", loaded.Category);
        Assert.Equal("Cloud security fundamentals: identity, encryption, and shared responsibility.", loaded.Description);
        Assert.Equal(FixedTime, loaded.CreatedAt);

        loaded.Name = "Cloud Security Basics";
        loaded.Description = "Updated description of the cloud security topic.";
        _topics.Update(loaded);

        Topic? updated = _topics.GetById(topicId);
        Assert.NotNull(updated);
        Assert.Equal("Cloud Security Basics", updated.Name);
        Assert.Equal("Updated description of the cloud security topic.", updated.Description);

        Assert.True(_topics.Delete(topicId));
        Assert.Null(_topics.GetById(topicId));
        Assert.Equal(5, _topics.GetAll().Count); // back to the seeded topics only
    }

    // -------------------------------------------------------------------------
    // 3. Atomic quiz write — a quiz WITH its questions and options persists in
    //    one call, with correct positions and exactly one correct option per
    //    question.
    // -------------------------------------------------------------------------
    [Fact]
    public void QuizRepository_AddAtomicQuiz_PersistsQuestionsAndChoices()
    {
        Quiz quiz = BuildQuiz("Atomic Write Quiz");
        quiz.TopicId = 1; // the seeded C# topic

        int quizId = _quizzes.Add(quiz);
        Assert.True(quizId > 5); // the seed used the quiz ids 1..5

        Quiz? loaded = _quizzes.GetById(quizId);
        Assert.NotNull(loaded);
        Assert.Equal(1, loaded.TopicId);
        Assert.Equal("Atomic Write Quiz", loaded.Title);
        Assert.Equal(Difficulty.Medium, loaded.Difficulty);
        Assert.Equal(QuizStatus.Draft, loaded.Status);
        Assert.Equal(FixedTime, loaded.CreatedAt);
        Assert.Equal(3, loaded.Questions.Count);

        for (int position = 1; position <= loaded.Questions.Count; position++)
        {
            Question question = loaded.Questions[position - 1];

            Assert.Equal(position, question.Position); // display order is 1, 2, 3
            Assert.Equal(4, question.Choices.Count);   // four options each

            for (int choicePosition = 1; choicePosition <= question.Choices.Count; choicePosition++)
            {
                Assert.Equal(choicePosition, question.Choices[choicePosition - 1].Position);
            }

            // The answer key: exactly one correct option, the one the test built.
            Choice? correct = question.GetCorrectChoice();
            Assert.NotNull(correct);
            Assert.Equal($"Correct answer of question {position}", correct.ChoiceText);
        }

        Assert.Equal(6, _quizzes.GetAll().Count); // 5 seeded + 1 new
    }

    // -------------------------------------------------------------------------
    // 4. Status update — Draft -> Approved through the repository persists
    //    (the review workflow of plan ruling R-2 #4).
    // -------------------------------------------------------------------------
    [Fact]
    public void QuizRepository_UpdateStatus_PersistsApproval()
    {
        Quiz? draft = _quizzes.GetById(4); // seeded "Networking Basics", Draft
        Assert.NotNull(draft);
        Assert.Equal(QuizStatus.Draft, draft.Status);

        draft.Status = QuizStatus.Approved;
        _quizzes.Update(draft);

        Quiz? approved = _quizzes.GetById(4);
        Assert.NotNull(approved);
        Assert.Equal(QuizStatus.Approved, approved.Status);

        Assert.Equal(new[] { 4 }, _quizzes.GetByStatus(QuizStatus.Approved).Where(q => q.QuizId > 3).Select(q => q.QuizId).ToArray());
        Assert.Equal(new[] { 5 }, _quizzes.GetByStatus(QuizStatus.Draft).Select(q => q.QuizId).ToArray());
    }

    // -------------------------------------------------------------------------
    // 5. FK-enforced delete THROWS — deleting a topic that still owns quizzes
    //    is refused by the database, which proves the connection factory runs
    //    "PRAGMA foreign_keys = ON" on every connection it opens.
    // -------------------------------------------------------------------------
    [Fact]
    public void TopicRepository_DeleteTopicWithQuizzes_ThrowsReferentialIntegrity()
    {
        Assert.NotNull(_topics.GetById(1)); // the seeded C# topic owns quizzes 1

        Assert.Throws<ReferentialIntegrityException>(() => _topics.Delete(1));

        // The refused delete changed nothing: the topic is still there.
        Assert.NotNull(_topics.GetById(1));
    }

    // -------------------------------------------------------------------------
    // 6. Atomic rollback — a forced failure in the middle of a multi-row quiz
    //    write (the third question reuses position 2, breaking the UNIQUE
    //    (quiz_id, position) rule) leaves NO partial rows behind.
    // -------------------------------------------------------------------------
    [Fact]
    public void QuizRepository_AddForcedFailure_LeavesNoPartialRows()
    {
        Quiz quiz = BuildQuiz("Broken Quiz");

        // The third question takes the position of the second one: the write
        // fails after the quiz header and two complete questions were inserted.
        quiz.Questions[2].Position = 2;

        Assert.Throws<DuplicateEntityException>(() => _quizzes.Add(quiz));

        // Nothing of the failed write remains: still exactly the five seeded
        // quizzes, their 21 questions and their 84 options.
        IReadOnlyList<Quiz> quizzes = _quizzes.GetAll();
        Assert.Equal(5, quizzes.Count);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, quizzes.Select(q => q.QuizId).ToArray());

        int questionCount = 0;
        int choiceCount = 0;

        foreach (Quiz seededQuiz in quizzes)
        {
            Quiz? loaded = _quizzes.GetById(seededQuiz.QuizId);
            Assert.NotNull(loaded);

            questionCount += loaded.Questions.Count;

            foreach (Question question in loaded.Questions)
            {
                choiceCount += question.Choices.Count;
            }
        }

        Assert.Equal(21, questionCount);
        Assert.Equal(84, choiceCount);
    }

    // -------------------------------------------------------------------------
    // 7. R-2 #3 rejection — an answer whose question does not belong to the
    //    attempt's quiz refuses the WHOLE attempt: AnswerValidationException
    //    and nothing is written.
    // -------------------------------------------------------------------------
    [Fact]
    public void AttemptRepository_AddWithForeignQuestionAnswer_ThrowsAndPersistsNothing()
    {
        QuizAttempt attempt = new()
        {
            QuizId = 1, // the seeded C# quiz owns the questions 1..5
            UserId = 1,
            Score = 4,
            AttemptedAt = FixedTime,
        };

        List<SubmittedAnswer> answers = new()
        {
            // A valid answer: question 1 belongs to quiz 1, choice 2 to question 1.
            new SubmittedAnswer { QuestionId = 1, ChosenChoiceId = 2 },
            // A foreign answer: question 6 belongs to quiz 2 (choice 24 is genuinely its option).
            new SubmittedAnswer { QuestionId = 6, ChosenChoiceId = 24 },
        };

        AnswerValidationException exception = Assert.Throws<AnswerValidationException>(
            () => _attempts.AddWithAnswers(attempt, answers));
        Assert.Equal(6, exception.QuestionId);
        Assert.Equal(1, exception.QuizId);

        // Nothing was written: still the four seeded attempts with their
        // twenty answers, and question 6 was answered only by the seeded run.
        Assert.Equal(4, _attempts.GetByUser(1).Count);

        int answerCount = 0;
        for (int attemptId = 1; attemptId <= 4; attemptId++)
        {
            answerCount += _answers.GetByAttempt(attemptId).Count;
        }

        Assert.Equal(20, answerCount);
        Assert.Single(_answers.GetByQuestion(6));
    }

    // -------------------------------------------------------------------------
    // 8. SQL-injection sanity — hostile text is stored as literal data; the
    //    topics table is intact afterwards.
    // -------------------------------------------------------------------------
    [Fact]
    public void TopicRepository_AddSqlInjectionName_StoresLiteralTextAndKeepsTable()
    {
        const string hostileName = "'; DROP TABLE topics;--";

        Topic created = new()
        {
            Name = hostileName,
            Category = "Programming",
            Description = "A topic whose name tries to look like SQL.",
            CreatedAt = FixedTime,
        };

        int topicId = _topics.Add(created);

        Topic? loaded = _topics.GetById(topicId);
        Assert.NotNull(loaded);
        Assert.Equal(hostileName, loaded.Name); // round-trips as literal data

        // The table is intact: all five seeded topics plus the new one, and
        // the seeded rows still read back.
        IReadOnlyList<Topic> topics = _topics.GetAll();
        Assert.Equal(6, topics.Count);
        Assert.Contains(topics, topic => topic.TopicId == 1 && topic.Name == "C#");
    }

    // -------------------------------------------------------------------------
    // 9. Duplicate rejection — the schema's real UNIQUE rule is topics.name
    //    (database/schema.sql line 45, globally unique). Adding a topic with
    //    a name that already exists raises DuplicateEntityException.
    // -------------------------------------------------------------------------
    [Fact]
    public void TopicRepository_AddDuplicateName_ThrowsDuplicateEntity()
    {
        Topic duplicate = new()
        {
            Name = "C#", // the seeded topic 1 carries this name already
            Category = "Cybersecurity", // a different category does not help — name alone is unique
            Description = "A duplicate of the seeded C# topic.",
            CreatedAt = FixedTime,
        };

        Assert.Throws<DuplicateEntityException>(() => _topics.Add(duplicate));

        Assert.Equal(5, _topics.GetAll().Count); // nothing was written
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a quiz of three multiple choice questions with four options each
    /// — the shape of the seeded demo quizzes. The option with the text
    /// "Correct answer of question n" carries the answer key of question n.
    /// </summary>
    private static Quiz BuildQuiz(string title)
    {
        Quiz quiz = new()
        {
            TopicId = 1,
            Title = title,
            Difficulty = Difficulty.Medium,
            Status = QuizStatus.Draft,
            CreatedAt = FixedTime,
        };

        for (int position = 1; position <= 3; position++)
        {
            MultipleChoiceQuestion question = new()
            {
                QuestionText = $"Question {position} of {title}",
                Explanation = $"Explanation {position}",
                Position = position,
                CreatedAt = FixedTime,
            };

            for (int option = 1; option <= 4; option++)
            {
                question.AddChoice(new Choice
                {
                    ChoiceText = option == 1 ? $"Correct answer of question {position}" : $"Option {position}.{option}",
                    IsCorrect = option == 1,
                    Position = option,
                });
            }

            quiz.AddQuestion(question);
        }

        return quiz;
    }

    /// <summary>
    /// Executes one SQL script (schema or seed) as one batch through a
    /// connection of the data layer — the scripts are read as text and run
    /// unchanged.
    /// </summary>
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
