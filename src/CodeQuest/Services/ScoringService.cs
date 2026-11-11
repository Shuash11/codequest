// Scores an attempt. (Polymorphism)

using CodeQuest.Models;

namespace CodeQuest.Services;

public sealed class ScoringService : IScoringService
{
    public QuizScoreResult Score(Quiz quiz, IEnumerable<SubmittedAnswer> submittedAnswers)
    {
        ArgumentNullException.ThrowIfNull(quiz);
        ArgumentNullException.ThrowIfNull(submittedAnswers);

        int correctCount = 0;
        int totalQuestions = 0;

        foreach (SubmittedAnswer submittedAnswer in submittedAnswers)
        {
            if (submittedAnswer is null)
            {
                continue;
            }

            Question? question = quiz.FindQuestion(submittedAnswer.QuestionId);
            if (question is null)
            {
                continue;
            }

            totalQuestions++;

            Choice? chosenChoice = question.Choices.FirstOrDefault(
                choice => choice.ChoiceId == submittedAnswer.ChosenChoiceId);

            if (question.IsCorrectChoice(chosenChoice))
            {
                correctCount++;
            }
        }

        return new QuizScoreResult(correctCount, totalQuestions);
    }
}
