// =============================================================================
// CodeQuest — Gamification Foundation (plan WP1, acceptance A5)
// OOP rubric: ABSTRACTION — the contract of the achievement evaluation.
// Look at: IAchievementEvaluator below. It exposes the CODE-DEFINED catalog
//          (key, title, description, glyph — what the Achievements screen
//          renders) and the one evaluation operation: given a user's
//          aggregates (AchievementContext), which achievements of the catalog
//          are met right now. The recording of unlocks (the INSERT that must
//          never duplicate a row) is the repository's job, not the evaluator's.
// Note:    the catalog lives in code, not in a table (plan FR4) — adding an
//          achievement is a code change, the user_achievements table stores
//          only who unlocked what and when.
// =============================================================================

using CodeQuest.Models;

namespace CodeQuest.Services;

/// <summary>
/// Evaluates the achievement catalog against one user's aggregates. The
/// implementation lives in <see cref="AchievementEvaluator"/>; the submit flow
/// of WP5 depends on this interface only.
/// </summary>
public interface IAchievementEvaluator
{
    /// <summary>The full catalog in display order — what the Achievements screen lists.</summary>
    IReadOnlyList<AchievementDefinition> Catalog { get; }

    /// <summary>
    /// The catalog entries whose rule is met by the given context, in catalog
    /// order. Whether an entry is NEW for the user (not unlocked before) is not
    /// part of this answer — the caller filters that against the stored rows.
    /// </summary>
    /// <param name="context">The user's level and gamification aggregates.</param>
    /// <returns>The met entries; empty when none is met.</returns>
    IReadOnlyList<AchievementDefinition> Evaluate(AchievementContext context);
}
