using DayZLootEditor.Models;

namespace DayZLootEditor.Services;

public interface ILootProfileService
{
    IReadOnlyList<LootProfileTemplate> GetTemplates();
    string ApplyTemplate(string templateId, IReadOnlyCollection<DayzTypeEntry> entries);
}
