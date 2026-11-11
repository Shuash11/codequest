// Task 5 run-ordering tests (acceptance C3): Quiz.OrderQuestionsForRun keeps
// the tiers progressive (Easy -> Medium -> Hard -> Insane) while shuffling the
// order inside each tier, and Question.ShuffleChoices shuffles the options.
// Both reassign the display positions. Pure model tests — no database, no UI.

using CodeQuest.Models;

namespace CodeQuest.Tests;

public class RunOrderTests
{
    // -------------------------------------------------------------------------
    // 1. The tier ladder — a scrambled quiz comes back Easy, Easy, Medium,
    //    Hard, Insane with positions 1..5, whatever the incoming order was.
    // -------------------------------------------------------------------------
    [Fact]
    public void OrderQuestionsForRun_GroupsTheTiersAscending_AndReassignsPositions()
    {
        Quiz quiz = new();
        quiz.AddQuestion(BuildQuestion("q1", Difficulty.Insane));
        quiz.AddQuestion(BuildQuestion("q2", Difficulty.Medium));
        quiz.AddQuestion(BuildQuestion("q3", Difficulty.Easy));
        quiz.AddQuestion(BuildQuestion("q4", Difficulty.Hard));
        quiz.AddQuestion(BuildQuestion("q5", Difficulty.Easy));

        quiz.OrderQuestionsForRun(new Random(2026));

        Assert.Equal(
            new[] { Difficulty.Easy, Difficulty.Easy, Difficulty.Medium, Difficulty.Hard, Difficulty.Insane },
            quiz.Questions.Select(question => question.Difficulty).ToArray());
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, quiz.Questions.Select(question => question.Position).ToArray());
    }

    // -------------------------------------------------------------------------
    // 2. The shuffle — inside one tier the order is a fresh permutation (never
    //    the constant stored order) and every question keeps its content.
    // -------------------------------------------------------------------------
    [Fact]
    public void OrderQuestionsForRun_ShufflesInsideATier_WithoutLosingQuestions()
    {
        Quiz quiz = new();
        for (int index = 1; index <= 10; index++)
        {
            quiz.AddQuestion(BuildQuestion($"q{index}", Difficulty.Medium));
        }

        string[] stored = quiz.Questions.Select(question => question.QuestionText).ToArray();

        quiz.OrderQuestionsForRun(new Random(20261112));

        string[] shuffled = quiz.Questions.Select(question => question.QuestionText).ToArray();
        Assert.NotEqual(stored, shuffled);
        Assert.Equal(stored.OrderBy(text => text, StringComparer.Ordinal), shuffled.OrderBy(text => text, StringComparer.Ordinal));
        Assert.Equal(Enumerable.Range(1, 10), quiz.Questions.Select(question => question.Position));
    }

    // -------------------------------------------------------------------------
    // 3. The answer shuffle — the four options come back as a new permutation
    //    with positions 1..4, exactly one correct answer, and the key intact.
    // -------------------------------------------------------------------------
    [Fact]
    public void ShuffleChoices_ShufflesTheOptions_AndKeepsTheAnswerKey()
    {
        MultipleChoiceQuestion question = BuildQuestion("q", Difficulty.Hard, correctIndex: 2);
        string correctText = question.GetCorrectChoice()!.ChoiceText;
        string[] stored = question.Choices.Select(choice => choice.ChoiceText).ToArray();

        question.ShuffleChoices(new Random(7));

        string[] shuffled = question.Choices.Select(choice => choice.ChoiceText).ToArray();
        Assert.NotEqual(stored, shuffled);
        Assert.Equal(stored.OrderBy(text => text, StringComparer.Ordinal), shuffled.OrderBy(text => text, StringComparer.Ordinal));
        Assert.Equal(new[] { 1, 2, 3, 4 }, question.Choices.Select(choice => choice.Position).ToArray());
        Assert.Single(question.Choices, choice => choice.IsCorrect);
        Assert.Equal(correctText, question.GetCorrectChoice()!.ChoiceText);
    }

    private static MultipleChoiceQuestion BuildQuestion(string text, Difficulty difficulty, int correctIndex = 0)
    {
        MultipleChoiceQuestion question = new()
        {
            QuestionText = text,
            Explanation = $"{text} explanation",
            Difficulty = difficulty,
        };

        for (int index = 0; index < 4; index++)
        {
            question.AddChoice(new Choice
            {
                ChoiceText = $"{text} option {index}",
                IsCorrect = index == correctIndex,
                Position = index + 1,
            });
        }

        return question;
    }
}
