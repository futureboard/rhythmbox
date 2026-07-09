using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Rythmbox.App.ViewModels;

namespace Rythmbox.App.Views;

public partial class HeaderView : UserControl
{
    public HeaderView()
    {
        InitializeComponent();
    }

    private void OnToggleFullscreenClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            window.WindowState = window.WindowState == WindowState.FullScreen
                ? WindowState.Normal
                : WindowState.FullScreen;
        }
    }

    private async void OnLoadKitClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load Drum Kit",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Kit presets") { Patterns = ["*.json", "*.apak"] },
            ],
        });

        if (files.Count == 0)
        {
            return;
        }

        if (files[0].TryGetLocalPath() is { } path && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.KitBrowser.LoadKit(path);
        }
    }
}
