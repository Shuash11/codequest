// The achievement contract. (Abstraction)

using CodeQuest.Models;

namespace CodeQuest.Services;

public interface IAchievementEvaluator
{
    IReadOnlyList<AchievementDefinition> Catalog { get; }

    IReadOnlyList<AchievementDefinition> Evaluate(AchievementContext context);
}
