// =============================================================================
// CodeQuest — Phase P8: Topic & Quiz Management
// The pure filter of the quiz management grid (spec 5.4): search by title
// plus the topic and status filters, combined. No UI types and no database —
// the quizzes come from the repository once and the filter narrows them in
// memory (the data layer owns no title search; the local data set is small,
// so the screen filters live — P8 acceptance: "search filters the grid live").
// Unit-testable on its own (plan: pure-logic tests).
// =============================================================================

using CodeQuest.Models;

namespace CodeQuest.UI;

/// <summary>
/// The live filter of the quiz management screen: title search + topic filter
/// + status filter, applied together.
/// </summary>
public static class QuizListFilter
{
    /// <summary>
    /// Returns the quizzes that match every active filter, in the given order.
    /// </summary>
    /// <param name="quizzes">The quizzes to narrow.</param>
    /// <param name="searchText">The title search text; null/whitespace matches all.</param>
    /// <param name="topicId">The topic filter; null matches every topic.</param>
    /// <param name="status">The status filter; null matches every status.</param>
    /// <returns>The matching quizzes, in the order they were given.</returns>
    public static IReadOnlyList<Quiz> Apply(
        IReadOnlyList<Quiz> quizzes,
        string? searchText,
        int? topicId,
        QuizStatus? status)
    {
        ArgumentNullException.ThrowIfNull(quizzes);

        string? needle = string.IsNullOrWhiteSpace(searchText) ? null : searchText.Trim();

        List<Quiz> result = new();

        foreach (Quiz quiz in quizzes)
        {
            if (needle is not null && !quiz.Title.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (topicId.HasValue && quiz.TopicId != topicId.Value)
            {
                continue;
            }

            if (status.HasValue && quiz.Status != status.Value)
            {
                continue;
            }

            result.Add(quiz);
        }

        return result;
    }
}
