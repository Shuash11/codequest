// =============================================================================
// CodeQuest — Phase P3: Core OOP Domain Layer
// Tests for: ScoringService (the correct/total math behind the quiz result).
// Look at: the five cases below — all correct, mixed, none correct, an empty
//          attempt and an answer of an unknown question. Every case builds its
//          domain objects in memory, so the test proves the business logic alone,
//          without a database.
// Expected values are hand-computed the same way the seeded sample data is
//          (database/seed.sql): 4 of 5 right = 80 %, 3 of 5 right = 60 %.
// =============================================================================

using CodeQuest.Models;
using CodeQuest.Services;

namespace CodeQuest.Tests;

/// <summary>
/// Phase P3 scoring tests: the number of correct answers, the number of answered
/// questions and the percentage that follows from them.
/// </summary>
public class ScoringServiceTests
{
    private const int QuestionCount = 5;
    private const int ChoicesPerQuestion = 4;

    private readonly IScoringService _scoringService = new ScoringService();

    [Fact]
    public void Score_AllAnswersCorrect_ReturnsFullScore()
    {
        Quiz quiz = BuildQuiz();
        List<SubmittedAnswer> answers = BuildAnswers(quiz, correctQuestions: new[] { 1, 2, 3, 4, 5 });

        QuizScoreResult result = _scoringService.Score(quiz, answers);

        Assert.Equal(5, result.CorrectCount);
        Assert.Equal(5, result.TotalQuestions);
        Assert.Equal(100d, result.Percentage);
        Assert.Equal("5/5 (100%)", result.Summary);
        Assert.False(result.IsEmpty);
    }

    [Fact]
    public void Score_MixedAnswers_CountsOnlyTheCorrectOnes()
    {
        Quiz quiz = BuildQuiz();

        // Questions 1, 3 and 4 are answered right, questions 2 and 5 are answered wrong.
        List<SubmittedAnswer> answers = BuildAnswers(quiz, correctQuestions: new[] { 1, 3, 4 });

        QuizScoreResult result = _scoringService.Score(quiz, answers);

        Assert.Equal(3, result.CorrectCount);
        Assert.Equal(5, result.TotalQuestions);
        Assert.Equal(60d, result.Percentage);
        Assert.Equal("3/5 (60%)", result.Summary);
    }

    [Fact]
    public void Score_NoCorrectAnswers_ReturnsZero()
    {
        Quiz quiz = BuildQuiz();
        List<SubmittedAnswer> answers = BuildAnswers(quiz, correctQuestions: Array.Empty<int>());

        QuizScoreResult result = _scoringService.Score(quiz, answers);

        Assert.Equal(0, result.CorrectCount);
        Assert.Equal(5, result.TotalQuestions);
        Assert.Equal(0d, result.Percentage);
        Assert.Equal("0/5 (0%)", result.Summary);
    }

    [Fact]
    public void Score_EmptyAttempt_ReturnsZeroWithoutDividingByZero()
    {
        Quiz quiz = BuildQuiz();
        List<SubmittedAnswer> answers = new();

        QuizScoreResult result = _scoringService.Score(quiz, answers);

        Assert.Equal(0, result.CorrectCount);
        Assert.Equal(0, result.TotalQuestions);
        Assert.Equal(0d, result.Percentage);
        Assert.True(result.IsEmpty);
        Assert.Equal("0/0 (0%)", result.Summary);
    }

    [Fact]
    public void Score_AnswerOfAnUnknownQuestion_IsNotCounted()
    {
        Quiz quiz = BuildQuiz();
        List<SubmittedAnswer> answers = BuildAnswers(quiz, correctQuestions: new[] { 1, 2 });

        // Question 99 does not belong to this quiz, so it is not part of the attempt.
        answers.Add(new SubmittedAnswer
        {
            AttemptId = 1,
            QuestionId = 99,
            ChosenChoiceId = CorrectChoiceIdOf(99),
        });

        QuizScoreResult result = _scoringService.Score(quiz, answers);

        Assert.Equal(2, result.CorrectCount);
        Assert.Equal(5, result.TotalQuestions);
        Assert.Equal(40d, result.Percentage);
    }

    /// <summary>
    /// Builds a quiz of five multiple choice questions with four options each —
    /// the shape of the seeded demo quiz. The first option of every question
    /// carries the answer key.
    /// </summary>
    private static Quiz BuildQuiz()
    {
        Quiz quiz = new()
        {
            QuizId = 1,
            TopicId = 1,
            Title = "C# Core Concepts",
            Difficulty = Difficulty.Easy,
            Status = QuizStatus.Approved,
        };

        for (int questionId = 1; questionId <= QuestionCount; questionId++)
        {
            MultipleChoiceQuestion question = new()
            {
                QuestionId = questionId,
                QuizId = quiz.QuizId,
                QuestionText = $"Question {questionId}",
                Explanation = $"Explanation {questionId}",
                Position = questionId,
            };

            for (int option = 1; option <= ChoicesPerQuestion; option++)
            {
                question.AddChoice(new Choice
                {
                    ChoiceId = ChoiceIdOf(questionId, option),
                    QuestionId = questionId,
                    ChoiceText = $"Option {option}",
                    IsCorrect = option == 1,
                    Position = option,
                });
            }

            quiz.AddQuestion(question);
        }

        return quiz;
    }

    /// <summary>
    /// Builds the answers of one attempt: every question is answered, the listed
    /// questions are answered right, all others are answered wrong.
    /// </summary>
    private static List<SubmittedAnswer> BuildAnswers(Quiz quiz, IReadOnlyCollection<int> correctQuestions)
    {
        List<SubmittedAnswer> answers = new();

        foreach (Question question in quiz.Questions)
        {
            bool isCorrect = correctQuestions.Contains(question.QuestionId);
            int chosenChoiceId = isCorrect
                ? CorrectChoiceIdOf(question.QuestionId)
                : WrongChoiceIdOf(question.QuestionId);

            answers.Add(new SubmittedAnswer
            {
                AttemptId = 1,
                QuestionId = question.QuestionId,
                ChosenChoiceId = chosenChoiceId,
            });
        }

        return answers;
    }

    private static int ChoiceIdOf(int questionId, int option) => ((questionId - 1) * ChoicesPerQuestion) + option;

    private static int CorrectChoiceIdOf(int questionId) => ChoiceIdOf(questionId, 1);

    private static int WrongChoiceIdOf(int questionId) => ChoiceIdOf(questionId, 2);
}
