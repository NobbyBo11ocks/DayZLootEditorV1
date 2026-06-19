using DZServerToolkit.Models;

namespace DZServerToolkit.Services;

public interface IValidationService
{
    IReadOnlyList<ValidationIssue> ValidateTypes(IReadOnlyList<DayzTypeEntry> entries);
}
