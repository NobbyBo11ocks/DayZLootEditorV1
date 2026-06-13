namespace DayZLootEditor.Services;

public interface IBackupService
{
    Task<string> CreateBackupAsync(string sourcePath, CancellationToken cancellationToken = default);
}
