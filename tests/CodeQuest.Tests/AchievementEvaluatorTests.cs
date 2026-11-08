// =============================================================================
// CodeQuest — Gamification Foundation (plan WP1, acceptance A5/A8)
// Tests for: AchievementEvaluator (the code-defined catalog and its rules).
// PURE unit tests — a context is built in memory, no database: the evaluator
// reads only the aggregates it is given.
// Look at: one case per rule — every rule fires EXACTLY at its condition and
//          does NOT fire one step below it (A8); the count/level rules also
//          still fire above the threshold (they are ">= " rules). Plus the
//          two aggregate cases (nothing met -> empty, everything met -> all
//          ten) and the catalog invariants (exactly 10 unique keys, the exact
//          plan titles, player-friendly text everywhere).
// =============================================================================

using CodeQuest.Models;
using CodeQuest.Services;

namespace CodeQuest.Tests;

/// <summary>
/// Phase WP1 achievement tests: catalog shape and the exact firing boundary
/// of every rule.
/// </summary>
public class AchievementEvaluatorTests
{
    /// <summary>The exact plan keys, in catalog order (plan FR4).</summary>
    private static readonly string[] PlanKeys =
    {
        "first-quiz",
        "perfect-run",
        "perfect-hard",
        "ten-quizzes",
        "thirty-quizzes",
        "fifty-correct",
        "level-5",
        "level-10",
        "three-topics",
        "both-categories",
    };

    /// <summary>The exact plan titles, in catalog order.</summary>
    private static readonly string[] PlanTitles =
    {
        "First Quest",
        "Flawless",
        "Nightmare Slayer",
        "Scholar",
        "Grandmaster",
        "Sharpshooter",
        "Ascendant",
        "Legend",
        "Explorer",
        "Dual Discipline",
    };

    private readonly IAchievementEvaluator _evaluator = new AchievementEvaluator();

    // ---------------------------------------------------------------------
    // Catalog invariants.
    // ---------------------------------------------------------------------
    [Fact]
    public void Catalog_HasExactlyTheTenPlanKeys_InOrder()
    {
        Assert.Equal(10, _evaluator.Catalog.Count);
        Assert.Equal(PlanKeys, _evaluator.Catalog.Select(definition => definition.Key).ToArray());
    }

    [Fact]
    public void Catalog_UsesTheExactPlanTitles()
    {
        Assert.Equal(PlanTitles, _evaluator.Catalog.Select(definition => definition.Title).ToArray());
    }

    [Fact]
    public void Catalog_KeysAreUnique()
    {
        Assert.Equal(PlanKeys.Length, _evaluator.Catalog.Select(definition => definition.Key).Distinct().Count());
    }

    [Fact]
    public void Catalog_EveryEntryCarriesPlayerFriendlyText()
    {
        foreach (AchievementDefinition definition in _evaluator.Catalog)
        {
            Assert.False(string.IsNullOrWhiteSpace(definition.Title), $"{definition.Key} has no title.");
            Assert.False(string.IsNullOrWhiteSpace(definition.Description), $"{definition.Key} has no description.");
            Assert.False(string.IsNullOrWhiteSpace(definition.Glyph), $"{definition.Key} has no glyph.");
        }
    }

    // ---------------------------------------------------------------------
    // Rule boundaries — fires exactly AT the condition, not below it.
    // ---------------------------------------------------------------------
    [Fact]
    public void FirstQuiz_FiresExactlyAtTheFirstQuiz()
    {
        Assert.DoesNotContain(_evaluator.Evaluate(Context(quizzesTaken: 0)), definition => definition.Key == "first-quiz");
        Assert.Contains(_evaluator.Evaluate(Context(quizzesTaken: 1)), definition => definition.Key == "first-quiz");
    }

    [Fact]
    public void PerfectRun_FiresExactlyOnAPerfectAttempt()
    {
        Assert.DoesNotContain(_evaluator.Evaluate(Context(hasPerfectRun: false)), definition => definition.Key == "perfect-run");
        Assert.Contains(_evaluator.Evaluate(Context(hasPerfectRun: true)), definition => definition.Key == "perfect-run");
    }

    [Fact]
    public void PerfectHardRun_FiresExactlyOnAPerfectHardAttempt()
    {
        Assert.DoesNotContain(_evaluator.Evaluate(Context(hasPerfectHardRun: false)), definition => definition.Key == "perfect-hard");
        Assert.Contains(_evaluator.Evaluate(Context(hasPerfectHardRun: true)), definition => definition.Key == "perfect-hard");
    }

    [Fact]
    public void TenQuizzes_FiresExactlyAtTen()
    {
        Assert.DoesNotContain(_evaluator.Evaluate(Context(quizzesTaken: 9)), definition => definition.Key == "ten-quizzes");
        Assert.Contains(_evaluator.Evaluate(Context(quizzesTaken: 10)), definition => definition.Key == "ten-quizzes");
        Assert.Contains(_evaluator.Evaluate(Context(quizzesTaken: 11)), definition => definition.Key == "ten-quizzes");
    }

    [Fact]
    public void ThirtyQuizzes_FiresExactlyAtThirty()
    {
        Assert.DoesNotContain(_evaluator.Evaluate(Context(quizzesTaken: 29)), definition => definition.Key == "thirty-quizzes");
        Assert.Contains(_evaluator.Evaluate(Context(quizzesTaken: 30)), definition => definition.Key == "thirty-quizzes");
        Assert.Contains(_evaluator.Evaluate(Context(quizzesTaken: 31)), definition => definition.Key == "thirty-quizzes");
    }

    [Fact]
    public void FiftyCorrect_FiresExactlyAtFifty()
    {
        Assert.DoesNotContain(_evaluator.Evaluate(Context(totalCorrectAnswers: 49)), definition => definition.Key == "fifty-correct");
        Assert.Contains(_evaluator.Evaluate(Context(totalCorrectAnswers: 50)), definition => definition.Key == "fifty-correct");
        Assert.Contains(_evaluator.Evaluate(Context(totalCorrectAnswers: 51)), definition => definition.Key == "fifty-correct");
    }

    [Fact]
    public void Level5_FiresExactlyAtLevel5()
    {
        Assert.DoesNotContain(_evaluator.Evaluate(Context(level: 4)), definition => definition.Key == "level-5");
        Assert.Contains(_evaluator.Evaluate(Context(level: 5)), definition => definition.Key == "level-5");
        Assert.Contains(_evaluator.Evaluate(Context(level: 6)), definition => definition.Key == "level-5");
    }

    [Fact]
    public void Level10_FiresExactlyAtLevel10()
    {
        Assert.DoesNotContain(_evaluator.Evaluate(Context(level: 9)), definition => definition.Key == "level-10");
        Assert.Contains(_evaluator.Evaluate(Context(level: 10)), definition => definition.Key == "level-10");
        Assert.Contains(_evaluator.Evaluate(Context(level: 11)), definition => definition.Key == "level-10");
    }

    [Fact]
    public void ThreeTopics_FiresExactlyAtThree()
    {
        Assert.DoesNotContain(_evaluator.Evaluate(Context(distinctTopicCount: 2)), definition => definition.Key == "three-topics");
        Assert.Contains(_evaluator.Evaluate(Context(distinctTopicCount: 3)), definition => definition.Key == "three-topics");
        Assert.Contains(_evaluator.Evaluate(Context(distinctTopicCount: 4)), definition => definition.Key == "three-topics");
    }

    [Fact]
    public void BothCategories_FiresExactlyWhenBothAreCovered()
    {
        Assert.DoesNotContain(_evaluator.Evaluate(Context(hasBothCategories: false)), definition => definition.Key == "both-categories");
        Assert.Contains(_evaluator.Evaluate(Context(hasBothCategories: true)), definition => definition.Key == "both-categories");
    }

    // ---------------------------------------------------------------------
    // Aggregate behavior.
    // ---------------------------------------------------------------------
    [Fact]
    public void Evaluate_ContextMeetingNothing_ReturnsEmpty()
    {
        Assert.Empty(_evaluator.Evaluate(Context()));
    }

    [Fact]
    public void Evaluate_ContextMeetingEverything_ReturnsAllTenInCatalogOrder()
    {
        AchievementContext context = Context(
            level: 10,
            quizzesTaken: 30,
            totalCorrectAnswers: 50,
            hasPerfectRun: true,
            hasPerfectHardRun: true,
            distinctTopicCount: 3,
            hasBothCategories: true);

        Assert.Equal(PlanKeys, _evaluator.Evaluate(context).Select(definition => definition.Key).ToArray());
    }

    [Fact]
    public void Evaluate_ReturnsOnlyTheMetEntries_InCatalogOrder()
    {
        // 12 quizzes (first-quiz, ten-quizzes), 60 correct (fifty-correct),
        // everything else below its condition.
        AchievementContext context = Context(
            level: 1,
            quizzesTaken: 12,
            totalCorrectAnswers: 60);

        Assert.Equal(
            new[] { "first-quiz", "ten-quizzes", "fifty-correct" },
            _evaluator.Evaluate(context).Select(definition => definition.Key).ToArray());
    }

    /// <summary>Builds an evaluation context; every unmentioned aggregate stays at its zero baseline.</summary>
    private static AchievementContext Context(
        int level = 1,
        int quizzesTaken = 0,
        long totalCorrectAnswers = 0,
        bool hasPerfectRun = false,
        bool hasPerfectHardRun = false,
        int distinctTopicCount = 0,
        bool hasBothCategories = false)
        => new(
            level,
            new UserGamificationStats(
                1,
                quizzesTaken,
                totalCorrectAnswers,
                hasPerfectRun,
                hasPerfectHardRun,
                distinctTopicCount,
                hasBothCategories));
}
