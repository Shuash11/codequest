// The campaign level rules: titles, numbers and the next level.

using System.Text.RegularExpressions;

namespace CodeQuest.Models;

public static class Campaign
{
    public const string TopicName = "Campaign";

    private static readonly Regex LevelTitlePattern = new("^Level ([0-9]+)$", RegexOptions.Compiled);

    // A level title is exactly "Level " followed by digits (nothing else).
    public static bool IsLevelTitle(string? title)
        => !string.IsNullOrWhiteSpace(title) && LevelTitlePattern.IsMatch(title);

    // The level number of a level title, or 0 when the title is not one.
    public static int LevelNumberOf(string? title)
    {
        if (!IsLevelTitle(title))
        {
            return 0;
        }

        return int.TryParse(LevelTitlePattern.Match(title!).Groups[1].Value, out int level) ? level : 0;
    }

    // The next level number for one topic: the highest existing level + 1.
    // Drafts never count (a half-finished level does not occupy a number) and
    // the caller can exclude the quiz it just inserted itself.
    public static int NextLevelNumber(IEnumerable<Quiz> quizzes, int topicId, int? excludingQuizId = null)
    {
        ArgumentNullException.ThrowIfNull(quizzes);

        int highest = 0;
        foreach (Quiz quiz in quizzes)
        {
            if (quiz.TopicId != topicId
                || quiz.Status == QuizStatus.Draft
                || (excludingQuizId is { } excluded && quiz.QuizId == excluded))
            {
                continue;
            }

            int level = LevelNumberOf(quiz.Title);
            if (level > highest)
            {
                highest = level;
            }
        }

        return highest + 1;
    }
}
