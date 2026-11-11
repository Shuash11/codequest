// The search and filter rules of the quiz list.

using CodeQuest.Models;

namespace CodeQuest.UI;

public static class QuizListFilter
{
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
