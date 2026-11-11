// Names the rival's next campaign level and approves it.

using CodeQuest.Data;
using CodeQuest.Models;

namespace CodeQuest.Services;

public sealed class CampaignLevelService
{
    private readonly IQuizRepository _quizzes;

    public CampaignLevelService(IQuizRepository quizzes)
    {
        ArgumentNullException.ThrowIfNull(quizzes);
        _quizzes = quizzes;
    }

    // Titles the just-inserted quiz as the next level of one campaign topic
    // and approves it; returns false when the quiz no longer exists.
    public bool NameAndApproveNextLevel(int quizId, int topicId, Difficulty difficulty)
    {
        // The just-inserted draft is excluded from the scan, so an AI title
        // like "Level 26" can never push the next number past the real one.
        int nextLevel = Campaign.NextLevelNumber(_quizzes.GetAll(), topicId, excludingQuizId: quizId);
        string title = $"Level {nextLevel}";

        // Approved levels are counted by the scan, so an occupied title can
        // only be a leftover Draft (e.g. one the Generate screen saved): it is
        // replaced or the rename would collide on every retry forever.
        if (_quizzes.ExistsByTitle(topicId, title, ignoreQuizId: quizId))
        {
            _quizzes.DeleteDraftWithTitle(topicId, title);
        }

        Quiz? quiz = _quizzes.GetById(quizId);
        if (quiz is null)
        {
            return false;
        }

        quiz.Title = title;
        quiz.Difficulty = difficulty;
        quiz.Status = QuizStatus.Approved;
        _quizzes.Update(quiz);
        return true;
    }
}
