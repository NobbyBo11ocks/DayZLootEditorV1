using DayZLootEditor.Models;

namespace DayZLootEditor.Services;

public interface IValidationService
{
    IReadOnlyList<ValidationIssue> ValidateTypes(IReadOnlyList<DayzTypeEntry> entries);
}
