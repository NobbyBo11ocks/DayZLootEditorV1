namespace DayZLootForge.Services;

public interface IRecentFilesService
{
    IReadOnlyList<string> GetRecentTypesFiles();
    IReadOnlyList<string> GetRecentMissionFolders();
    string GetLastWorkspace();
    bool GetHasCompletedFirstMissionLoad();
    Task AddRecentTypesFileAsync(string path, CancellationToken cancellationToken = default);
    Task AddRecentMissionFolderAsync(string path, CancellationToken cancellationToken = default);
    Task SetLastWorkspaceAsync(string workspace, CancellationToken cancellationToken = default);
    Task SetHasCompletedFirstMissionLoadAsync(bool value, CancellationToken cancellationToken = default);
}
