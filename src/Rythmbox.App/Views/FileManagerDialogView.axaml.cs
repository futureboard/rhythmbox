using Avalonia.Controls;
using Avalonia.Input;
using Rythmbox.App.ViewModels;

namespace Rythmbox.App.Views;

public partial class FileManagerDialogView : UserControl
{
    public FileManagerDialogView()
    {
        InitializeComponent();
    }

    private void OnBrowserDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not FileManagerDialogViewModel dialog)
        {
            return;
        }

        if (dialog.Mode == FileManagerDialogMode.OpenFile
            && dialog.Browser.SelectedEntry is { IsDirectory: false })
        {
            dialog.ConfirmCommand.Execute(null);
        }
        else if (dialog.Browser.SelectedEntry is { IsDirectory: true })
        {
            dialog.Browser.OpenSelectedCommand.Execute(null);
        }
    }
}
