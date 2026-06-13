namespace DayZLootForge.Services;

public interface IFileDialogService
{
    Task<string?> PickTypesFileAsync();
    Task<string?> PickMissionFolderAsync();
    Task<string?> PickSaveTypesPathAsync(string suggestedFileName);
    Task<bool> ConfirmDiscardChangesAsync(string title, string message);
}
