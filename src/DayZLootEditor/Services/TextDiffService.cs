using System.Text.RegularExpressions;
using DayZLootForge.Models;

namespace DayZLootForge.Services;

public sealed class TextDiffService : ITextDiffService
{
    private const int ContextLineCount = 2;

    public string BuildLineDiff(string originalText, string updatedText, string title)
    {
        var originalLines = NormalizeLines(originalText);
        var updatedLines = NormalizeLines(updatedText);
        var diffLines = BuildDiffLines(originalLines, updatedLines);

        return $"{title}{Environment.NewLine}{new string('=', title.Length)}{Environment.NewLine}{Environment.NewLine}" +
               string.Join(Environment.NewLine, diffLines);
    }

    public SaveDiffPreview BuildFriendlyDiff(string originalText, string updatedText)
    {
        var originalLines = NormalizeLines(originalText);
        var updatedLines = NormalizeLines(updatedText);
        var operations = BuildOperations(originalLines, updatedLines);

        var sections = BuildSections(operations);
        var addedLines = sections.Sum(section => section.Lines.Count(line => string.Equals(line.ChangeKind, "Added", StringComparison.Ordinal)));
        var removedLines = sections.Sum(section => section.Lines.Count(line => string.Equals(line.ChangeKind, "Removed", StringComparison.Ordinal)));
        var changedLinePairs = sections.Sum(section => section.Lines.Count(line => string.Equals(line.ChangeKind, "Changed", StringComparison.Ordinal)));

        if (sections.Count == 0)
        {
            return new SaveDiffPreview
            {
                Summary = "No save changes detected. The current editor state matches the file on disk."
            };
        }

        var summaryParts = new List<string>();
        if (changedLinePairs > 0)
        {
            summaryParts.Add($"{changedLinePairs:N0} changed line pair{Pluralize(changedLinePairs)}");
        }

        if (addedLines > 0)
        {
            summaryParts.Add($"{addedLines:N0} added line{Pluralize(addedLines)}");
        }

        if (removedLines > 0)
        {
            summaryParts.Add($"{removedLines:N0} removed line{Pluralize(removedLines)}");
        }

        return new SaveDiffPreview
        {
            Summary = $"This save will update {sections.Count:N0} section{Pluralize(sections.Count)}. " +
                      string.Join(", ", summaryParts) + ".",
            AddedLines = addedLines + changedLinePairs,
            RemovedLines = removedLines + changedLinePairs,
            ChangedSections = sections.Count,
            Sections = sections
        };
    }

    private static IReadOnlyList<string> NormalizeLines(string text)
    {
        return (text ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Split('\n')
            .ToList();
    }

    private static IReadOnlyList<string> BuildDiffLines(IReadOnlyList<string> original, IReadOnlyList<string> updated)
    {
        var operations = BuildOperations(original, updated);
        return operations.Select(operation => operation.Kind switch
        {
            DiffOperationKind.Equal => $"  {operation.OriginalText}",
            DiffOperationKind.Removed => $"- {operation.OriginalText}",
            DiffOperationKind.Added => $"+ {operation.UpdatedText}",
            _ => string.Empty
        }).ToList();
    }

    private static IReadOnlyList<DiffOperation> BuildOperations(IReadOnlyList<string> original, IReadOnlyList<string> updated)
    {
        var lcs = BuildLcsTable(original, updated);
        var results = new List<DiffOperation>();
        var i = 0;
        var j = 0;

        while (i < original.Count && j < updated.Count)
        {
            if (string.Equals(original[i], updated[j], StringComparison.Ordinal))
            {
                results.Add(new DiffOperation(DiffOperationKind.Equal, original[i], updated[j]));
                i++;
                j++;
                continue;
            }

            if (lcs[i + 1, j] >= lcs[i, j + 1])
            {
                results.Add(new DiffOperation(DiffOperationKind.Removed, original[i], string.Empty));
                i++;
            }
            else
            {
                results.Add(new DiffOperation(DiffOperationKind.Added, string.Empty, updated[j]));
                j++;
            }
        }

        while (i < original.Count)
        {
            results.Add(new DiffOperation(DiffOperationKind.Removed, original[i++], string.Empty));
        }

        while (j < updated.Count)
        {
            results.Add(new DiffOperation(DiffOperationKind.Added, string.Empty, updated[j++]));
        }

        return results;
    }

    private static List<SaveDiffSection> BuildSections(IReadOnlyList<DiffOperation> operations)
    {
        var sections = new List<SaveDiffSection>();
        var index = 0;
        var sectionNumber = 1;

        while (index < operations.Count)
        {
            if (operations[index].Kind == DiffOperationKind.Equal)
            {
                index++;
                continue;
            }

            var changeStart = index;
            var changeEnd = index;
            while (changeEnd < operations.Count && operations[changeEnd].Kind != DiffOperationKind.Equal)
            {
                changeEnd++;
            }

            var contextStart = Math.Max(0, changeStart - ContextLineCount);
            var contextEndExclusive = Math.Min(operations.Count, changeEnd + ContextLineCount);

            var lines = BuildSectionLines(operations.Skip(contextStart).Take(contextEndExclusive - contextStart).ToList());
            var title = BuildSectionTitle(lines, sectionNumber);
            var summary = BuildSectionSummary(lines);

            sections.Add(new SaveDiffSection
            {
                Title = title,
                Summary = summary,
                Lines = lines
            });

            sectionNumber++;
            index = contextEndExclusive;
        }

        return sections;
    }

    private static List<SaveDiffLine> BuildSectionLines(IReadOnlyList<DiffOperation> operations)
    {
        var results = new List<SaveDiffLine>();
        var pendingRemoved = new List<string>();
        var pendingAdded = new List<string>();

        void FlushPending()
        {
            var pairCount = Math.Max(pendingRemoved.Count, pendingAdded.Count);
            for (var pairIndex = 0; pairIndex < pairCount; pairIndex++)
            {
                var originalText = pairIndex < pendingRemoved.Count ? pendingRemoved[pairIndex] : string.Empty;
                var updatedText = pairIndex < pendingAdded.Count ? pendingAdded[pairIndex] : string.Empty;
                var kind = !string.IsNullOrEmpty(originalText) && !string.IsNullOrEmpty(updatedText)
                    ? "Changed"
                    : string.IsNullOrEmpty(originalText)
                        ? "Added"
                        : "Removed";

                results.Add(new SaveDiffLine
                {
                    OriginalText = originalText,
                    UpdatedText = updatedText,
                    ChangeKind = kind
                });
            }

            pendingRemoved.Clear();
            pendingAdded.Clear();
        }

        foreach (var operation in operations)
        {
            switch (operation.Kind)
            {
                case DiffOperationKind.Equal:
                    FlushPending();
                    results.Add(new SaveDiffLine
                    {
                        OriginalText = operation.OriginalText,
                        UpdatedText = operation.UpdatedText,
                        ChangeKind = "Context"
                    });
                    break;

                case DiffOperationKind.Removed:
                    pendingRemoved.Add(operation.OriginalText);
                    break;

                case DiffOperationKind.Added:
                    pendingAdded.Add(operation.UpdatedText);
                    break;
            }
        }

        FlushPending();
        return results;
    }

    private static string BuildSectionTitle(IReadOnlyList<SaveDiffLine> lines, int sectionNumber)
    {
        var joined = string.Join("\n", lines.Select(line => string.IsNullOrWhiteSpace(line.UpdatedText) ? line.OriginalText : line.UpdatedText));
        var match = Regex.Match(joined, "<type\\s+name=\"([^\"]+)\"", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return $"Item: {match.Groups[1].Value}";
        }

        return $"Change block {sectionNumber}";
    }

    private static string BuildSectionSummary(IReadOnlyList<SaveDiffLine> lines)
    {
        var changed = lines.Count(line => string.Equals(line.ChangeKind, "Changed", StringComparison.Ordinal));
        var added = lines.Count(line => string.Equals(line.ChangeKind, "Added", StringComparison.Ordinal));
        var removed = lines.Count(line => string.Equals(line.ChangeKind, "Removed", StringComparison.Ordinal));

        var summaryParts = new List<string>();
        if (changed > 0)
        {
            summaryParts.Add($"{changed:N0} changed");
        }

        if (added > 0)
        {
            summaryParts.Add($"{added:N0} added");
        }

        if (removed > 0)
        {
            summaryParts.Add($"{removed:N0} removed");
        }

        return summaryParts.Count == 0
            ? "Context only"
            : string.Join(" • ", summaryParts) + " line" + (changed + added + removed == 1 ? string.Empty : "s");
    }

    private static string Pluralize(int count) => count == 1 ? string.Empty : "s";

    private static int[,] BuildLcsTable(IReadOnlyList<string> original, IReadOnlyList<string> updated)
    {
        var table = new int[original.Count + 1, updated.Count + 1];
        for (var i = original.Count - 1; i >= 0; i--)
        {
            for (var j = updated.Count - 1; j >= 0; j--)
            {
                table[i, j] = string.Equals(original[i], updated[j], StringComparison.Ordinal)
                    ? table[i + 1, j + 1] + 1
                    : Math.Max(table[i + 1, j], table[i, j + 1]);
            }
        }

        return table;
    }

    private sealed record DiffOperation(DiffOperationKind Kind, string OriginalText, string UpdatedText);

    private enum DiffOperationKind
    {
        Equal = 0,
        Removed = 1,
        Added = 2
    }
}
