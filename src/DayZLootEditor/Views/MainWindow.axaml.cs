using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DayZLootEditor.ViewModels;

namespace DayZLootEditor.Views;

public partial class MainWindow : Window
{
    private bool _closeConfirmed;

    public MainWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
        KeyDown += OnKeyDown;
        WindowState = WindowState.Maximized;
        CanResize = false;
    }

    public MainWindow(TypesEditorViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }


    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not TypesEditorViewModel viewModel)
        {
            return;
        }

        var keyModifiers = e.KeyModifiers;
        var isCtrl = keyModifiers.HasFlag(KeyModifiers.Control);

        if (!isCtrl)
        {
            return;
        }

        if (e.Key == Key.O && keyModifiers.HasFlag(KeyModifiers.Shift))
        {
            if (viewModel.OpenTypesFileCommand.CanExecute(null))
            {
                viewModel.OpenTypesFileCommand.Execute(null);
                e.Handled = true;
            }

            return;
        }

        if (e.Key == Key.O)
        {
            if (viewModel.OpenMissionFolderCommand.CanExecute(null))
            {
                viewModel.OpenMissionFolderCommand.Execute(null);
                e.Handled = true;
            }

            return;
        }

        if (e.Key == Key.S && keyModifiers.HasFlag(KeyModifiers.Shift))
        {
            if (viewModel.SaveAsCommand.CanExecute(null))
            {
                viewModel.SaveAsCommand.Execute(null);
                e.Handled = true;
            }

            return;
        }

        if (e.Key == Key.S)
        {
            if (viewModel.SaveCommand.CanExecute(null))
            {
                viewModel.SaveCommand.Execute(null);
                e.Handled = true;
            }

            return;
        }

        if (e.Key == Key.U)
        {
            if (viewModel.UnloadLoadedFileCommand.CanExecute(null))
            {
                viewModel.UnloadLoadedFileCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeConfirmed || DataContext is not TypesEditorViewModel viewModel || !viewModel.HasUnsavedChanges)
        {
            return;
        }

        e.Cancel = true;
        var dialog = new UnsavedChangesDialog
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var result = await dialog.ShowDialog<UnsavedChangesChoice>(this);
        switch (result)
        {
            case UnsavedChangesChoice.SaveAndClose:
                var saved = await viewModel.SaveBeforeCloseAsync().ConfigureAwait(true);
                if (!saved)
                {
                    return;
                }

                _closeConfirmed = true;
                Close();
                break;

            case UnsavedChangesChoice.Discard:
                _closeConfirmed = true;
                Close();
                break;

            default:
                break;
        }
    }
}

public enum UnsavedChangesChoice
{
    Cancel = 0,
    SaveAndClose = 1,
    Discard = 2
}

public sealed class UnsavedChangesDialog : Window
{
    public UnsavedChangesDialog()
    {
        Title = "Unsaved Changes";
        Width = 440;
        Height = 180;
        MinWidth = 440;
        MinHeight = 180;
        CanResize = false;

        var saveButton = new Button { Content = "Save and Close", MinWidth = 110 };
        var discardButton = new Button { Content = "Discard", MinWidth = 110 };
        var cancelButton = new Button { Content = "Cancel", MinWidth = 110 };

        saveButton.Click += (_, _) => Close(UnsavedChangesChoice.SaveAndClose);
        discardButton.Click += (_, _) => Close(UnsavedChangesChoice.Discard);
        cancelButton.Click += (_, _) => Close(UnsavedChangesChoice.Cancel);

        Content = new Border
        {
            Padding = new Thickness(16),
            Child = new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    new TextBlock
                    {
                        Text = "You have unsaved loot changes. Save before closing?",
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        Spacing = 10,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Children = { cancelButton, discardButton, saveButton }
                    }
                }
            }
        };
    }
}
