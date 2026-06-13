using DayZLootEditor.Services;

namespace DayZLootEditor.Tests;

public sealed class BackupServiceTests
{
    [Fact]
    public async Task CreateBackupAsync_CreatesUniqueFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "DayZLootEditorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var sourcePath = Path.Combine(tempDir, "types.xml");
        await File.WriteAllTextAsync(sourcePath, "<types />");

        var service = new BackupService();
        var first = await service.CreateBackupAsync(sourcePath);
        var second = await service.CreateBackupAsync(sourcePath);

        Assert.NotEqual(first, second);
        Assert.True(File.Exists(first));
        Assert.True(File.Exists(second));
    }


    [Fact]
    public async Task CreateBackupAsync_CanReadSourceWhileAnotherReaderHasItOpen()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "DayZLootEditorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var sourcePath = Path.Combine(tempDir, "types.xml");
        await File.WriteAllTextAsync(sourcePath, "<types />");

        using var readLock = File.Open(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        var service = new BackupService();
        var backupPath = await service.CreateBackupAsync(sourcePath);

        Assert.True(File.Exists(backupPath));
        Assert.Equal("<types />", await File.ReadAllTextAsync(backupPath));
    }

}
