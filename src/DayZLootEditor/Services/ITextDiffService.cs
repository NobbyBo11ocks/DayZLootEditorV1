using DayZLootForge.Models;

namespace DayZLootForge.Services;

public interface ITextDiffService
{
    string BuildLineDiff(string originalText, string updatedText, string title);
    SaveDiffPreview BuildFriendlyDiff(string originalText, string updatedText);
}
