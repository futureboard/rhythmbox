using Avalonia.Controls;
using Avalonia.Input;
using Rythmbox.Editor.ViewModels;

namespace Rythmbox.Editor.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not EditorViewModel viewModel || e.Source is TextBox)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Space:
                viewModel.PlayPreviewCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Escape:
                viewModel.StopPreviewCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}
