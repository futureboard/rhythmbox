using Avalonia.Controls;
using Avalonia.Input;
using Rythmbox.SampleCreator.ViewModels;

namespace Rythmbox.SampleCreator.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not SampleCreatorViewModel viewModel || e.Source is TextBox)
        {
            return;
        }

        if (e.Key == Key.Space && viewModel.SelectedPad is not null)
        {
            viewModel.SelectedPad.PreviewCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            viewModel.StopPreviewCommand.Execute(null);
            e.Handled = true;
        }
    }
}
