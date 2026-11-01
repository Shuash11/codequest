// =============================================================================
// CodeQuest — Phase P3: Core OOP Domain Layer
// OOP rubric: ABSTRACTION — the concrete implementation behind IScoringService.
// Look at: Score() below. It walks the submitted answers one by one, finds the
//          question each answer belongs to and checks that question's own answer
//          key (IsCorrectChoice), then counts the right answers — the math is
//          correct/total, the same math the seeded sample data uses (for example
//          4 of 5 answers right = 80%).
// Note:    nothing here is stored; the attempt row keeps the number of correct
//          answers only (plan ruling R-2 #1).
// =============================================================================

using CodeQuest.Models;

namespace CodeQuest.Services;

/// <summary>
/// The scoring implementation used by the quiz-taking screen (P9).
/// </summary>
public sealed class ScoringService : IScoringService
{
    /// <inheritdoc />
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

            // An answer that points at a question of another quiz does not belong to
            // this attempt and is not counted. The data layer rejects such an answer
            // when it is written (plan ruling R-2 #3).
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
