using Avalonia.Controls;
using Avalonia.Input;
using Rythmbox.App.ViewModels;

namespace Rythmbox.App.Views;

public partial class FileManagerBrowserView : UserControl
{
    public FileManagerBrowserView()
    {
        InitializeComponent();
    }

    private void OnEntryDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is FileManagerViewModel viewModel)
        {
            viewModel.OpenSelectedCommand.Execute(null);
        }
    }
}
