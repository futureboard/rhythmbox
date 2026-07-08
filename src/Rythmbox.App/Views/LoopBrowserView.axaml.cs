using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Rythmbox.App.ViewModels;

namespace Rythmbox.App.Views;

public partial class LoopBrowserView : UserControl
{
    public LoopBrowserView()
    {
        InitializeComponent();
    }

    private async void OnBrowseFolderClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose MIDI Loop Folder",
            AllowMultiple = false,
        });

        if (folders.Count == 0)
        {
            return;
        }

        if (folders[0].TryGetLocalPath() is { } path && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetLoopFolder(path);
        }
    }
}
