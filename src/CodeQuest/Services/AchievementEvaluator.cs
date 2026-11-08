// =============================================================================
// CodeQuest — Gamification Foundation (plan WP1, acceptance A5)
// OOP rubric: ABSTRACTION — the concrete implementation behind
//              IAchievementEvaluator; POLYMORPHISM — every rule is one
//              predicate over the same AchievementContext, walked uniformly.
// Look at: the Rules array below. It is the SINGLE definition of the catalog:
//          each entry pairs the player-facing definition (key, title,
//          description, glyph) with its rule as one predicate. Evaluate()
//          walks the array and returns every entry whose predicate holds —
//          in catalog order, so the result is deterministic. The keys are the
//          stable identifiers stored in user_achievements; they must never be
//          renamed (they are already unlock history).
// Note:    the rules read ONLY the aggregates of the context — no database,
//          no clock — so every rule boundary is unit-testable in isolation.
// =============================================================================

using CodeQuest.Models;

namespace CodeQuest.Services;

/// <summary>
/// The code-defined achievement catalog and its evaluation
/// (see <see cref="IAchievementEvaluator"/> for the contract).
/// </summary>
public sealed class AchievementEvaluator : IAchievementEvaluator
{
    /// <summary>One catalog entry paired with the rule that unlocks it.</summary>
    private readonly record struct AchievementRule(
        AchievementDefinition Definition,
        Func<AchievementContext, bool> IsMet);

    /// <summary>
    /// The catalog in display order. Titles and descriptions speak the
    /// player's language — no internal jargon (no table names, no "XP").
    /// </summary>
    private static readonly IReadOnlyList<AchievementRule> Rules = new[]
    {
        new AchievementRule(
            new AchievementDefinition(
                "first-quiz",
                "First Quest",
                "Complete your first quiz.",
                "\uE768"), // Segoe MDL2 "Play" (§5.14's verified assignments)
            context => context.Stats.QuizzesTaken >= 1),
        new AchievementRule(
            new AchievementDefinition(
                "perfect-run",
                "Flawless",
                "Answer every question of a quiz correctly.",
                "\uE73E"), // CheckMark
            context => context.Stats.HasPerfectRun),
        new AchievementRule(
            new AchievementDefinition(
                "perfect-hard",
                "Nightmare Slayer",
                "Get a perfect score on a Hard quiz.",
                "\uE930"), // Completed
            context => context.Stats.HasPerfectHardRun),
        new AchievementRule(
            new AchievementDefinition(
                "ten-quizzes",
                "Scholar",
                "Complete 10 quizzes.",
                "\uE7BC"), // ReadingList
            context => context.Stats.QuizzesTaken >= 10),
        new AchievementRule(
            new AchievementDefinition(
                "thirty-quizzes",
                "Grandmaster",
                "Complete 30 quizzes.",
                "\uE8F1"), // Library
            context => context.Stats.QuizzesTaken >= 30),
        new AchievementRule(
            new AchievementDefinition(
                "fifty-correct",
                "Sharpshooter",
                "Answer 50 questions correctly in total.",
                "\uE8FB"), // Accept
            context => context.Stats.TotalCorrectAnswers >= 50),
        new AchievementRule(
            new AchievementDefinition(
                "level-5",
                "Ascendant",
                "Reach level 5.",
                "\uE870"), // SignalBars5
            context => context.Level >= 5),
        new AchievementRule(
            new AchievementDefinition(
                "level-10",
                "Legend",
                "Reach level 10.",
                "\uE945"), // LightningBolt
            context => context.Level >= 10),
        new AchievementRule(
            new AchievementDefinition(
                "three-topics",
                "Explorer",
                "Take quizzes from 3 different topics.",
                "\uE81E"), // MapLayers
            context => context.Stats.DistinctTopicCount >= 3),
        new AchievementRule(
            new AchievementDefinition(
                "both-categories",
                "Dual Discipline",
                "Take quizzes from both Programming and Cybersecurity.",
                "\uEA86"), // Puzzle
            context => context.Stats.HasBothCategories),
    };

    /// <summary>The definitions of the catalog, materialised once.</summary>
    private static readonly IReadOnlyList<AchievementDefinition> Definitions =
        Rules.Select(rule => rule.Definition).ToArray();

    /// <inheritdoc />
    public IReadOnlyList<AchievementDefinition> Catalog => Definitions;

    /// <inheritdoc />
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
