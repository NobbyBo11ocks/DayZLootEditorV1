using DayZLootEditor.Services;

namespace DayZLootEditor.Tests;

public sealed class TextDiffServiceTests
{
    [Fact]
    public void BuildLineDiff_ShowsAddedAndRemovedLines()
    {
        var service = new TextDiffService();

        var diff = service.BuildLineDiff("a\nb\n", "a\nc\n", "Preview");

        Assert.Contains("- b", diff, StringComparison.Ordinal);
        Assert.Contains("+ c", diff, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildFriendlyDiff_GroupsChangesIntoReadableSections()
    {
        var service = new TextDiffService();

        var preview = service.BuildFriendlyDiff(
            "<types>\n    <type name=\"AKM\">\n        <nominal>1</nominal>\n    </type>\n</types>\n",
            "<types>\n    <type name=\"AKM\">\n        <nominal>3</nominal>\n    </type>\n</types>\n");

        Assert.True(preview.HasChanges);
        Assert.Single(preview.Sections);
        Assert.Equal("Item: AKM", preview.Sections[0].Title);
        Assert.Contains(preview.Sections[0].Lines, line => line.ChangeKind == "Changed");
    }
}
