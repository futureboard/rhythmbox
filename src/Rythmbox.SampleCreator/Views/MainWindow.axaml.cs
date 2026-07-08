using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Rythmbox.SampleCreator.ViewModels;

namespace Rythmbox.SampleCreator.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private SampleCreatorViewModel ViewModel => (SampleCreatorViewModel)DataContext!;

    private void OnNewClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        ViewModel.NewKitCommand.Execute(null);

    private async void OnOpenClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open kit preset",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Kit preset") { Patterns = ["*.json"] },
            ],
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            ViewModel.OpenPreset(path);
        }
    }

    private async void OnSaveClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save kit preset",
            DefaultExtension = "json",
            SuggestedStartLocation = ViewModel.SuggestPresetFolder() is { } folder
                ? await StorageProvider.TryGetFolderFromPathAsync(folder)
                : null,
            FileTypeChoices =
            [
                new FilePickerFileType("Kit preset") { Patterns = ["*.json"] },
            ],
        });

        if (file?.TryGetLocalPath() is { } path)
        {
            ViewModel.SavePreset(path);
        }
    }

    private async void OnImportWavClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import WAV sample",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Audio") { Patterns = ["*.wav", "*.wave"] },
            ],
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            ViewModel.ImportWavToSelected(path);
        }
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source is TextBox)
        {
            return;
        }

        if (e.Key == Key.Space && ViewModel.SelectedPad is not null)
        {
            ViewModel.SelectedPad.PreviewCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ViewModel.StopPreviewCommand.Execute(null);
            e.Handled = true;
        }
    }
}
