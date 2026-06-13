using DayZLootForge.Models;

namespace DayZLootForge.Services;

public interface IValidationService
{
    IReadOnlyList<ValidationIssue> ValidateTypes(IReadOnlyList<DayzTypeEntry> entries);
}
