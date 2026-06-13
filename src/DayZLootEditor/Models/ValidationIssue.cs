namespace DayZLootForge.Models;

public enum ValidationSeverity
{
    Info,
    Warning,
    Error
}

public sealed record ValidationIssue(
    ValidationSeverity Severity,
    string EntryName,
    string Message)
{
    public string SeverityText => Severity.ToString().ToUpperInvariant();
    public string DisplayText => string.IsNullOrWhiteSpace(EntryName)
        ? Message
        : $"{EntryName}: {Message}";
}
