// Checks which achievements are unlocked.

using CodeQuest.Models;

namespace CodeQuest.Services;

public sealed class AchievementEvaluator : IAchievementEvaluator
{
    private readonly record struct AchievementRule(
        AchievementDefinition Definition,
        Func<AchievementContext, bool> IsMet);

    private static readonly IReadOnlyList<AchievementRule> Rules = new[]
    {
        new AchievementRule(
            new AchievementDefinition(
                "first-quiz",
                "First Quest",
                "Complete your first quiz.",
                "\uE768"),
            context => context.Stats.QuizzesTaken >= 1),
        new AchievementRule(
            new AchievementDefinition(
                "perfect-run",
                "Flawless",
                "Answer every question of a quiz correctly.",
                "\uE73E"),
            context => context.Stats.HasPerfectRun),
        new AchievementRule(
            new AchievementDefinition(
                "perfect-hard",
                "Nightmare Slayer",
                "Get a perfect score on a Hard quiz.",
                "\uE930"),
            context => context.Stats.HasPerfectHardRun),
        new AchievementRule(
            new AchievementDefinition(
                "ten-quizzes",
                "Scholar",
                "Complete 10 quizzes.",
                "\uE7BC"),
            context => context.Stats.QuizzesTaken >= 10),
        new AchievementRule(
            new AchievementDefinition(
                "thirty-quizzes",
                "Grandmaster",
                "Complete 30 quizzes.",
                "\uE8F1"),
            context => context.Stats.QuizzesTaken >= 30),
        new AchievementRule(
            new AchievementDefinition(
                "fifty-correct",
                "Sharpshooter",
                "Answer 50 questions correctly in total.",
                "\uE8FB"),
            context => context.Stats.TotalCorrectAnswers >= 50),
        new AchievementRule(
            new AchievementDefinition(
                "level-5",
                "Ascendant",
                "Reach level 5.",
                "\uE870"),
            context => context.Level >= 5),
        new AchievementRule(
            new AchievementDefinition(
                "level-10",
                "Legend",
                "Reach level 10.",
                "\uE945"),
            context => context.Level >= 10),
        new AchievementRule(
            new AchievementDefinition(
                "three-topics",
                "Explorer",
                "Take quizzes from 3 different topics.",
                "\uE81E"),
            context => context.Stats.DistinctTopicCount >= 3),
        new AchievementRule(
            new AchievementDefinition(
                "both-categories",
                "Dual Discipline",
                "Take quizzes from both Programming and Cybersecurity.",
                "\uEA86"),
            context => context.Stats.HasBothCategories),
    };

    private static readonly IReadOnlyList<AchievementDefinition> Definitions =
        Rules.Select(rule => rule.Definition).ToArray();

    public IReadOnlyList<AchievementDefinition> Catalog => Definitions;

    public IReadOnlyList<AchievementDefinition> Evaluate(AchievementContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        List<AchievementDefinition> met = new();

        foreach (AchievementRule rule in Rules)
        {
            if (rule.IsMet(context))
            {
                met.Add(rule.Definition);
            }
        }

        return met;
    }
}
