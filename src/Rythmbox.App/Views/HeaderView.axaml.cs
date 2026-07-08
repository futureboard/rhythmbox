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

    private async void OnLoadSoundFontClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load SoundFont",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("SoundFont files") { Patterns = ["*.sf2"] },
            ],
        });

        if (files.Count == 0)
        {
            return;
        }

        if (files[0].TryGetLocalPath() is { } path && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SoundFontBrowser.LoadSoundFont(path);
        }
    }
}
