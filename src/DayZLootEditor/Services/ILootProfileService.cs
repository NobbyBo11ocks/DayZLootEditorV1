using DayZLootForge.Models;

namespace DayZLootForge.Services;

public interface ILootProfileService
{
    IReadOnlyList<LootProfileTemplate> GetTemplates();
    string ApplyTemplate(string templateId, IReadOnlyCollection<DayzTypeEntry> entries);
}
