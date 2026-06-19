using DZServerToolkit.Models;

namespace DZServerToolkit.Services;

public interface ILootProfileService
{
    IReadOnlyList<LootProfileTemplate> GetTemplates();
    string ApplyTemplate(string templateId, IReadOnlyCollection<DayzTypeEntry> entries);
}
