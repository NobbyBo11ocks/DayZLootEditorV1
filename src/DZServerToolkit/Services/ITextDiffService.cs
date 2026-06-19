using DZServerToolkit.Models;

namespace DZServerToolkit.Services;

public interface ITextDiffService
{
    string BuildLineDiff(string originalText, string updatedText, string title);
    SaveDiffPreview BuildFriendlyDiff(string originalText, string updatedText);
}
