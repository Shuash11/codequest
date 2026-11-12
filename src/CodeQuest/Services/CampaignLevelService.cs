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
    // FOR ONE PLAYER, stamps the ladder difficulty, records the ownership and
    // approves it; returns false when the quiz no longer exists.
    public bool NameAndApproveNextLevel(int quizId, int topicId, int userId, string username)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        // The just-inserted draft is excluded from the scan, so an AI title
        // like "Level 26" can never push the next number past the real one;
        // other players' generated levels never count either.
        int nextLevel = Campaign.NextLevelNumberForUser(
            _quizzes.GetAll(), topicId, userId, excludingQuizId: quizId);
        string title = Campaign.GeneratedTitle(nextLevel, username);

        // Approved levels are counted by the scan, so an occupied title can
        // only be a leftover Draft of this same player (e.g. one the Generate
        // screen saved): it is replaced or the rename would collide on every
        // retry forever.
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
        quiz.Difficulty = Campaign.DifficultyForLevel(nextLevel); // Easy..Insane by the level ladder
        quiz.Status = QuizStatus.Approved;
        quiz.GeneratedForUserId = userId; // ownership: the level belongs to its player
        _quizzes.Update(quiz);
        return true;
    }
}
