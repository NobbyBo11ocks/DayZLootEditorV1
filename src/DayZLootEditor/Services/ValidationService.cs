using DayZLootEditor.Models;

namespace DayZLootEditor.Services;

public sealed class ValidationService : IValidationService
{
    public IReadOnlyList<ValidationIssue> ValidateTypes(IReadOnlyList<DayzTypeEntry> entries)
    {
        var issues = new List<ValidationIssue>();
        var perEntryIssues = entries.ToDictionary(entry => entry, _ => new List<ValidationIssue>());

        if (entries.Count == 0)
        {
            issues.Add(new ValidationIssue(ValidationSeverity.Error, string.Empty, "No loot entries were found in this types.xml file."));
        }

        var duplicateNames = entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
            .GroupBy(entry => entry.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            var trimmedName = entry.Name.Trim();
            AddIf(entry, issues, perEntryIssues, ValidationSeverity.Error, string.IsNullOrWhiteSpace(trimmedName), "Item class name is required.");
            AddIf(entry, issues, perEntryIssues, ValidationSeverity.Error, duplicateNames.Contains(trimmedName), "Duplicate item class name. Each DayZ type name must be unique.");

            AddIf(entry, issues, perEntryIssues, ValidationSeverity.Error, entry.Nominal < 0, "Target quantity cannot be negative.");
            AddIf(entry, issues, perEntryIssues, ValidationSeverity.Error, entry.Min < 0, "Minimum quantity cannot be negative.");
            AddIf(entry, issues, perEntryIssues, ValidationSeverity.Error, entry.Lifetime < 0, "Lifetime cannot be negative.");
            AddIf(entry, issues, perEntryIssues, ValidationSeverity.Error, entry.Restock < 0, "Restock cannot be negative.");
            AddIf(entry, issues, perEntryIssues, ValidationSeverity.Error, entry.Cost < 0, "Cost cannot be negative.");
            AddIf(entry, issues, perEntryIssues, ValidationSeverity.Error, entry.Min > entry.Nominal, "Minimum quantity cannot be greater than nominal.");

            AddIf(entry, issues, perEntryIssues, ValidationSeverity.Error, entry.QuantMin < -1 || entry.QuantMin > 100, "Quantity min must be -1 or between 0 and 100.");
            AddIf(entry, issues, perEntryIssues, ValidationSeverity.Error, entry.QuantMax < -1 || entry.QuantMax > 100, "Quantity max must be -1 or between 0 and 100.");

            var hasBothQuantValues = entry.QuantMin >= 0 && entry.QuantMax >= 0;
            AddIf(entry, issues, perEntryIssues, ValidationSeverity.Error, hasBothQuantValues && entry.QuantMin > entry.QuantMax, "Quantity min cannot be greater than quantity max.");
        }

        foreach (var entry in entries)
        {
            var entryIssues = perEntryIssues.TryGetValue(entry, out var foundIssues)
                ? foundIssues
                : new List<ValidationIssue>();

            entry.IssueSummary = string.Join(" | ", entryIssues.Select(issue => issue.Message));
            entry.ValidationState = entryIssues.Count == 0
                ? "OK"
                : entryIssues.Any(issue => issue.Severity == ValidationSeverity.Error) ? "FIX" : "CHECK";
        }

        return issues
            .OrderByDescending(issue => issue.Severity)
            .ThenBy(issue => issue.EntryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(issue => issue.Message, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddIf(
        DayzTypeEntry entry,
        List<ValidationIssue> allIssues,
        Dictionary<DayzTypeEntry, List<ValidationIssue>> perEntryIssues,
        ValidationSeverity severity,
        bool condition,
        string message)
    {
        if (!condition)
        {
            return;
        }

        var issue = new ValidationIssue(severity, entry.Name, message);
        allIssues.Add(issue);
        perEntryIssues[entry].Add(issue);
    }
}
