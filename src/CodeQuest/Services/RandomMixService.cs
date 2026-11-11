// Builds a fresh "Random mix" quiz: random questions from every approved
// quiz, answers shuffled, saved as a real quiz under a dedicated topic so
// history, review, XP and achievements all work unchanged. Instant, offline.

using CodeQuest.Data;
using CodeQuest.Data.Repositories;
using CodeQuest.Models;

namespace CodeQuest.Services;

/// <summary>The mix could not be built (too few drawable questions).</summary>
public sealed class RandomMixException : Exception
{
    public RandomMixException(string message) : base(message)
    {
    }
}

public sealed class RandomMixService : IRandomMixService
{
    public const string TopicName = "Random Mix";

    private const int QuestionsPerMix = 10;

    public int MinimumQuestions => 4;

    private readonly QuizRepository _quizzes;
    private readonly QuestionRepository _questions;
    private readonly TopicRepository _topics;

    public RandomMixService(QuizRepository quizzes, QuestionRepository questions, TopicRepository topics)
    {
        ArgumentNullException.ThrowIfNull(quizzes);
        ArgumentNullException.ThrowIfNull(questions);
        ArgumentNullException.ThrowIfNull(topics);
        _quizzes = quizzes;
        _questions = questions;
        _topics = topics;
    }

    public int CountDrawableQuestions()
    {
        int total = 0;
        foreach (Quiz quiz in _quizzes.GetByStatus(QuizStatus.Approved))
        {
            total += _questions.GetByQuiz(quiz.QuizId).Count;
        }

        return total;
    }

    public int CreateMix()
    {
        var pool = new List<Question>();
        foreach (Quiz quiz in _quizzes.GetByStatus(QuizStatus.Approved))
        {
            pool.AddRange(_questions.GetByQuiz(quiz.QuizId));
        }

        if (pool.Count < MinimumQuestions)
        {
            throw new RandomMixException(
                $"A random mix needs at least {MinimumQuestions} approved questions, and only {pool.Count} exist.");
        }

        List<Question> drawn = pool
            .OrderBy(_ => Random.Shared.Next())
            .Take(Math.Min(QuestionsPerMix, pool.Count))
            .ToList();

        Topic topic = EnsureRandomMixTopic();

        // Two mixes can be created within the same second (the tests do it
        // on purpose): the topic's UNIQUE (topic, title) rule needs a
        // collision-free title.
        string title = $"Random mix \u2014 {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        if (_quizzes.ExistsByTitle(topic.TopicId, title))
        {
            int suffix = 2;
            while (_quizzes.ExistsByTitle(topic.TopicId, $"{title} \u00b7 {suffix}"))
            {
                suffix++;
            }

            title = $"{title} \u00b7 {suffix}";
        }

        var mixQuiz = new Quiz
        {
            TopicId = topic.TopicId,
            Title = title,
            Difficulty = Difficulty.Medium,
            Status = QuizStatus.Approved,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        int quizId = _quizzes.Add(mixQuiz);

        int position = 1;
        foreach (Question source in drawn)
        {
            _questions.Add(CopyQuestion(source, quizId, position));
            position++;
        }

        return quizId;
    }

    private Topic EnsureRandomMixTopic()
    {
        foreach (Topic topic in _topics.GetAll())
        {
            if (topic.Name.Equals(TopicName, StringComparison.OrdinalIgnoreCase))
            {
                return topic;
            }
        }

        var topic2 = new Topic
        {
            Name = TopicName,
            Category = "Programming",
            Description = "A fresh quiz drawn from every topic \u2014 every take is different.",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _topics.Add(topic2);
        return topic2;
    }

    private static Question CopyQuestion(Question source, int quizId, int position)
    {
        Question copy = source.QuestionKind == "True/False"
            ? new TrueFalseQuestion()
            : new MultipleChoiceQuestion();
        copy.QuizId = quizId;
        copy.QuestionText = source.QuestionText;
        copy.Explanation = source.Explanation;
        copy.Position = position;
        copy.CreatedAt = DateTimeOffset.UtcNow;

        int choicePosition = 1;
        foreach (Choice sourceChoice in source.Choices.OrderBy(_ => Random.Shared.Next()))
        {
            copy.AddChoice(new Choice
            {
                ChoiceText = sourceChoice.ChoiceText,
                IsCorrect = sourceChoice.IsCorrect,
                Position = choicePosition,
            });
            choicePosition++;
        }

        return copy;
    }
}
