using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Rythmbox.Editor.ViewModels;

namespace Rythmbox.Editor.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private EditorViewModel ViewModel => (EditorViewModel)DataContext!;

    private async void OnNewClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        ViewModel.NewPatternCommand.Execute(null);

    private async void OnOpenClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open MIDI pattern",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("MIDI") { Patterns = ["*.mid", "*.midi"] },
            ],
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            ViewModel.OpenFile(path);
        }
    }

    private async void OnSaveClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save MIDI pattern",
            DefaultExtension = "mid",
            SuggestedStartLocation = ViewModel.SuggestSaveFolder() is { } folder
                ? await StorageProvider.TryGetFolderFromPathAsync(folder)
                : null,
            FileTypeChoices =
            [
                new FilePickerFileType("MIDI") { Patterns = ["*.mid"] },
            ],
        });

        if (file?.TryGetLocalPath() is { } path)
        {
            ViewModel.SaveTo(path);
        }
    }

    private async void OnLoadKitClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load Drum Kit",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Kit presets") { Patterns = ["*.json"] },
            ],
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            ViewModel.LoadKit(path);
        }
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source is TextBox)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Space:
                ViewModel.PlayPreviewCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Escape:
                ViewModel.StopPreviewCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}
