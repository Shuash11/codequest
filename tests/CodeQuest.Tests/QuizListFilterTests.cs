// =============================================================================
// CodeQuest — Phase P8: Topic & Quiz Management
// Tests for: the pure filter of the quiz management grid
// (CodeQuest.UI.QuizListFilter) — the search-by-title plus topic/status
// filter combination of spec 5.4, applied together, case-insensitive. Pure
// logic over in-memory models: no database, no file system (the fresh-runner
// convention has nothing to provision here).
// =============================================================================

using CodeQuest.Models;
using CodeQuest.UI;

namespace CodeQuest.Tests;

/// <summary>
/// Phase P8 quiz filter tests: the filters narrow exactly like the spec's
/// quiz management screen describes (search + topic + status, combined).
/// </summary>
public class QuizListFilterTests
{
    private static Quiz Quiz(int id, int topicId, string title, QuizStatus status) => new()
    {
        QuizId = id,
        TopicId = topicId,
        Title = title,
        Difficulty = Difficulty.Easy,
        Status = status,
        CreatedAt = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero),
    };

    private static readonly Quiz[] Sample =
    {
        Quiz(1, 1, "C# Core Concepts", QuizStatus.Approved),
        Quiz(2, 2, "Python Essentials", QuizStatus.Approved),
        Quiz(3, 1, "C# Spot Checks", QuizStatus.Draft),
        Quiz(4, 3, "SQL Query Fundamentals", QuizStatus.Draft),
    };

    [Fact]
    public void NoFilters_ReturnsEverythingInTheGivenOrder()
    {
        IReadOnlyList<Quiz> result = QuizListFilter.Apply(Sample, searchText: null, topicId: null, status: null);

        Assert.Equal(new[] { 1, 2, 3, 4 }, result.Select(quiz => quiz.QuizId));
    }

    [Fact]
    public void TitleSearch_IsCaseInsensitive()
    {
        IReadOnlyList<Quiz> result = QuizListFilter.Apply(Sample, searchText: "core cONcepts", topicId: null, status: null);

        Assert.Equal(new[] { 1 }, result.Select(quiz => quiz.QuizId));
    }

    [Fact]
    public void TitleSearch_MatchesPartialTitles()
    {
        IReadOnlyList<Quiz> result = QuizListFilter.Apply(Sample, searchText: "c#", topicId: null, status: null);

        Assert.Equal(new[] { 1, 3 }, result.Select(quiz => quiz.QuizId));
    }

    [Fact]
    public void TitleSearch_WhitespaceOnly_MatchesAll()
    {
        IReadOnlyList<Quiz> result = QuizListFilter.Apply(Sample, searchText: "   ", topicId: null, status: null);

        Assert.Equal(4, result.Count);
    }

    [Fact]
    public void TopicFilter_KeepsOnlyThatTopicsQuizzes()
    {
        IReadOnlyList<Quiz> result = QuizListFilter.Apply(Sample, searchText: null, topicId: 1, status: null);

        Assert.Equal(new[] { 1, 3 }, result.Select(quiz => quiz.QuizId));
    }

    [Fact]
    public void StatusFilter_KeepsOnlyThatStatus()
    {
        IReadOnlyList<Quiz> drafts = QuizListFilter.Apply(Sample, searchText: null, topicId: null, QuizStatus.Draft);
        IReadOnlyList<Quiz> approved = QuizListFilter.Apply(Sample, searchText: null, topicId: null, QuizStatus.Approved);

        Assert.Equal(new[] { 3, 4 }, drafts.Select(quiz => quiz.QuizId));
        Assert.Equal(new[] { 1, 2 }, approved.Select(quiz => quiz.QuizId));
    }

    [Fact]
    public void Filters_Combine()
    {
        IReadOnlyList<Quiz> result = QuizListFilter.Apply(Sample, searchText: "c#", topicId: 1, QuizStatus.Draft);

        Assert.Equal(new[] { 3 }, result.Select(quiz => quiz.QuizId));
    }

    [Fact]
    public void NoMatch_ReturnsAnEmptyList()
    {
        IReadOnlyList<Quiz> result = QuizListFilter.Apply(Sample, searchText: "rust", topicId: null, status: null);

        Assert.Empty(result);
    }

    [Fact]
    public void UnknownTopic_MatchesNothing()
    {
        IReadOnlyList<Quiz> result = QuizListFilter.Apply(Sample, searchText: null, topicId: 99, status: null);

        Assert.Empty(result);
    }
}
