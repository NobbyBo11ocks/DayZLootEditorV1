namespace DayZLootForge.Services;

public sealed class BackupService : IBackupService
{
    public async Task<string> CreateBackupAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new ArgumentException("Backup source path is required.", nameof(sourcePath));
        }

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Cannot backup because the source file does not exist.", sourcePath);
        }

        var sourceDirectory = Path.GetDirectoryName(sourcePath)
            ?? throw new InvalidOperationException("Cannot determine the source directory.");
        var backupDirectory = Path.Combine(sourceDirectory, "DayZLootForgeBackups");
        Directory.CreateDirectory(backupDirectory);

        var stamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss-fff");
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..6];
        var fileName = Path.GetFileNameWithoutExtension(sourcePath);
        var extension = Path.GetExtension(sourcePath);
        var backupPath = Path.Combine(backupDirectory, $"{fileName}.{stamp}.{uniqueSuffix}{extension}.bak");

        await using var source = File.Open(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var destination = File.Open(backupPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);

        return backupPath;
    }
}
