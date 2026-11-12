// The campaign level rules: titles, numbers and the next level.

using System.Text.RegularExpressions;

namespace CodeQuest.Models;

public static class Campaign
{
    public const string TopicName = "Campaign";

    // The campaign gate (owner directive): a level is PASSED at 75% or more.
    // Only a passed level makes the AI rival generate the next one; the mark
    // on the level card shows the exact percentage achieved - passing is a
    // proof of success, not a requirement of a perfect run.
    public const int PassingScorePercent = 75;

    public static bool IsPassing(int percent) => percent >= PassingScorePercent;

    // The best percentage across the user's attempts on one quiz (correct
    // counts against the same question total); null when there are none or
    // the total is unusable.
    public static int? BestPercent(IReadOnlyList<(int Correct, int Total)> attempts)
    {
        int? best = null;
        foreach ((int correct, int total) in attempts)
        {
            if (total <= 0)
            {
                continue;
            }

            int percent = (int)Math.Round(correct * 100d / total, MidpointRounding.AwayFromZero);
            if (best is null || percent > best)
            {
                best = percent;
            }
        }

        return best;
    }

    // "Level N" (built-in) or "Level N — {username}" (AI-generated for one
    // player; the owner suffix keeps the (topic, title) uniqueness).
    private static readonly Regex LevelTitlePattern = new("^Level ([0-9]+)( \u2014 .+)?$", RegexOptions.Compiled);

    // The title the rival gives a level it generated for one player.
    public static string GeneratedTitle(int level, string username)
        => $"Level {level} \u2014 {username}";

    // The next level for ONE player: the highest of the built-in levels plus
    // that player's own generated levels, + 1. Other players' levels and
    // drafts never count.
    public static int NextLevelNumberForUser(
        IEnumerable<Quiz> quizzes, int topicId, int userId, int? excludingQuizId = null)
    {
        ArgumentNullException.ThrowIfNull(quizzes);

        int highest = 0;
        foreach (Quiz quiz in quizzes)
        {
            if (quiz.TopicId != topicId
                || quiz.Status == QuizStatus.Draft
                || (quiz.GeneratedForUserId is { } owner && owner != userId)
                || (excludingQuizId is { } excluded && quiz.QuizId == excluded))
            {
                continue;
            }

            int number = LevelNumberOf(quiz.Title);
            if (number > highest)
            {
                highest = number;
            }
        }

        return highest + 1;
    }

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

    // The ladder: Easy through Level 5, Medium through 12, Hard through 19,
    // Insane from Level 20 on.
    public static Difficulty DifficultyForLevel(int level) => level switch
    {
        <= 5 => Difficulty.Easy,
        <= 12 => Difficulty.Medium,
        <= 19 => Difficulty.Hard,
        _ => Difficulty.Insane,
    };

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
