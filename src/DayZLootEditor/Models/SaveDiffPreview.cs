namespace DayZLootEditor.Models;

public sealed class SaveDiffPreview
{
    public string Summary { get; init; } = string.Empty;
    public int AddedLines { get; init; }
    public int RemovedLines { get; init; }
    public int ChangedSections { get; init; }
    public IReadOnlyList<SaveDiffSection> Sections { get; init; } = Array.Empty<SaveDiffSection>();
    public bool HasChanges => AddedLines > 0 || RemovedLines > 0;
}

public sealed class SaveDiffSection
{
    public string Title { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyList<SaveDiffLine> Lines { get; init; } = Array.Empty<SaveDiffLine>();
}

public sealed class SaveDiffLine
{
    public string OriginalText { get; init; } = string.Empty;
    public string UpdatedText { get; init; } = string.Empty;
    public string ChangeKind { get; init; } = "Context";
    public bool IsChanged => !string.Equals(ChangeKind, "Context", StringComparison.Ordinal);
}
