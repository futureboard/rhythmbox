using Avalonia.Controls;
using Avalonia.Interactivity;
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
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var path = await viewModel.FileDialog.PickFolderAsync(
            viewModel.LoopBrowser.RootFolder,
            viewModel.Localization.FilesPickFolder);

        if (path is not null)
        {
            viewModel.SetLoopFolder(path);
        }
    }
}
