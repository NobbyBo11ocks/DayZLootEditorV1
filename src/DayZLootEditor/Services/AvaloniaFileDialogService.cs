using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace DayZLootForge.Services;

public sealed class AvaloniaFileDialogService : IFileDialogService
{
    private static readonly FilePickerFileType XmlFileType = new("DayZ XML files")
    {
        Patterns = new[] { "*.xml" },
        MimeTypes = new[] { "text/xml", "application/xml" }
    };

    private Window? _host;

    public void SetHost(Window host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    public async Task<string?> PickTypesFileAsync()
    {
        EnsureHost();
        var files = await _host!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open DayZ types.xml",
            AllowMultiple = false,
            FileTypeFilter = new[] { XmlFileType, FilePickerFileTypes.All }
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickMissionFolderAsync()
    {
        EnsureHost();
        var folders = await _host!.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open DayZ mission folder",
            AllowMultiple = false
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickSaveTypesPathAsync(string suggestedFileName)
    {
        EnsureHost();
        var file = await _host!.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save DayZ types.xml",
            SuggestedFileName = string.IsNullOrWhiteSpace(suggestedFileName) ? "types.xml" : suggestedFileName,
            DefaultExtension = "xml",
            FileTypeChoices = new[] { XmlFileType }
        });

        return file?.TryGetLocalPath();
    }


    public async Task<bool> ConfirmDiscardChangesAsync(string title, string message)
    {
        EnsureHost();
        var dialog = new ConfirmDiscardDialog(title, message)
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        return await dialog.ShowDialog<bool>(_host!).ConfigureAwait(true);
    }

    private void EnsureHost()
    {
        if (_host is null)
        {
            throw new InvalidOperationException("File dialog host has not been initialized yet.");
        }
    }
}


public sealed class ConfirmDiscardDialog : Window
{
    public ConfirmDiscardDialog(string title, string message)
    {
        Title = string.IsNullOrWhiteSpace(title) ? "Discard Changes" : title;
        Width = 460;
        Height = 190;
        MinWidth = 460;
        MinHeight = 190;
        CanResize = false;

        var discardButton = new Button { Content = "Discard Changes", MinWidth = 120 };
        var cancelButton = new Button { Content = "Cancel", MinWidth = 120 };

        discardButton.Click += (_, _) => Close(true);
        cancelButton.Click += (_, _) => Close(false);

        Content = new Border
        {
            Padding = new Avalonia.Thickness(16),
            Child = new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Spacing = 10,
                        Children =
                        {
                            cancelButton,
                            discardButton
                        }
                    }
                }
            }
        };
    }
}
