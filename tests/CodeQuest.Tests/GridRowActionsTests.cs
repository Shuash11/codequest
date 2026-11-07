// =============================================================================
// CodeQuest — Phase P8.1: Accessibility & Feedback Fix Round
// Tests for: the row-actions discoverability copy of recipe 4.6 (F1)
// (CodeQuest.UI.GridRowActions). The accessible row description lists a row's
// actions in column order — §4.6's canonical example, the quiz grid's
// "Actions: Edit, Edit questions, Delete." — and the hint line beneath every
// grid card with an action column is the spec's exact copy. Pure logic: no
// database, no forms, no UI thread.
// =============================================================================

using CodeQuest.UI;

namespace CodeQuest.Tests;

/// <summary>
/// Phase P8.1 recipe-4.6 discoverability tests: the description wording and
/// the on-screen hint line, each checkable against the spec's exact copy.
/// </summary>
public class GridRowActionsTests
{
    [Fact]
    public void Describe_ListsActionsInColumnOrder()
    {
        string description = GridRowActions.Describe(new[] { "Edit", "Edit questions", "Delete" });

        Assert.Equal("Actions: Edit, Edit questions, Delete.", description);
    }

    [Fact]
    public void Describe_SingleAction_DropsNoPunctuation()
    {
        string description = GridRowActions.Describe(new[] { "Edit" });

        Assert.Equal("Actions: Edit.", description);
    }

    [Fact]
    public void HintText_IsTheDesignOfRecordCopy()
    {
        Assert.Equal("Enter activates a focused row action.", GridRowActions.HintText);
    }
}
