using DayZLootEditor.Models;

namespace DayZLootEditor.Services;

public sealed record CustomCeLoadResult(
    string EconomyCorePath,
    IReadOnlyList<CustomCeFileEntry> Entries,
    IReadOnlyList<ValidationIssue> Issues);

public sealed record CustomCePreview(
    string Folder,
    string FileName,
    string Type,
    string RelativePath,
    string FullPath,
    string ExpectedRootName,
    bool FileExists,
    bool IsRegistered,
    string Summary);

public interface ICustomCeService
{
    IReadOnlyList<string> SupportedTypes { get; }
    string GetEconomyCorePath(string missionFolder);
    Task<CustomCeLoadResult> LoadAsync(string missionFolder, CancellationToken cancellationToken = default);
    CustomCePreview BuildPreview(string missionFolder, string folder, string fileName, string type);
    Task<CustomCeFileEntry> AddOrRegisterFileAsync(
        string missionFolder,
        string folder,
        string fileName,
        string type,
        CancellationToken cancellationToken = default);
    Task<bool> UnregisterFileAsync(
        string missionFolder,
        string folder,
        string fileName,
        string type,
        bool deleteFile,
        CancellationToken cancellationToken = default);
    Task<CustomCeFileEntry> RepairFileRootAsync(
        string missionFolder,
        string folder,
        string fileName,
        string type,
        CancellationToken cancellationToken = default);
}
