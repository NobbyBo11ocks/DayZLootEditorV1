using DayZLootForge.Services;

namespace DayZLootEditor.Tests;

public sealed class RecentFilesServiceTests
{
    [Fact]
    public async Task AddRecentTypesFileAsync_PutsNewestFileFirstWithoutDuplicates()
    {
        var tempDir = CreateTempDirectory();
        var service = CreateService(tempDir);
        var first = Path.Combine(tempDir, "a.xml");
        var second = Path.Combine(tempDir, "b.xml");

        await service.AddRecentTypesFileAsync(first);
        await service.AddRecentTypesFileAsync(second);
        await service.AddRecentTypesFileAsync(first);

        var items = service.GetRecentTypesFiles();
        Assert.Equal(Path.GetFullPath(first), items[0]);
        Assert.Contains(Path.GetFullPath(second), items);
        Assert.Equal(2, items.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public async Task AddRecentMissionFolderAsync_PersistsNormalizedFolderPaths()
    {
        var tempDir = CreateTempDirectory();
        var service = CreateService(tempDir);
        var missionFolder = Path.Combine(tempDir, "mission");
        Directory.CreateDirectory(missionFolder);

        await service.AddRecentMissionFolderAsync(Path.Combine(missionFolder, "..", "mission"));

        var items = service.GetRecentMissionFolders();
        Assert.Single(items);
        Assert.Equal(Path.GetFullPath(missionFolder), items[0]);
    }

    private static RecentFilesService CreateService(string tempDir) =>
        new(Path.Combine(tempDir, "recent-files.json"));

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "DayZLootEditorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public async Task SetLastWorkspaceAsync_PersistsWorkspaceChoice()
    {
        var tempDir = CreateTempDirectory();
        var service = CreateService(tempDir);

        await service.SetLastWorkspaceAsync("Custom CE Files");

        Assert.Equal("Custom CE Files", service.GetLastWorkspace());
    }

    [Fact]
    public async Task SetHasCompletedFirstMissionLoadAsync_PersistsFlag()
    {
        var tempDir = CreateTempDirectory();
        var service = CreateService(tempDir);

        await service.SetHasCompletedFirstMissionLoadAsync(true);

        Assert.True(service.GetHasCompletedFirstMissionLoad());
    }

}
