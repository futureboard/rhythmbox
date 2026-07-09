using Avalonia.Controls;
using Avalonia.Input;

namespace Rythmbox.Editor.Views;

public partial class EditorPageView : UserControl
{
    public EditorPageView()
    {
        InitializeComponent();
    }

    private void OnPageKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ViewModels.EditorViewModel viewModel || e.Source is TextBox)
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
