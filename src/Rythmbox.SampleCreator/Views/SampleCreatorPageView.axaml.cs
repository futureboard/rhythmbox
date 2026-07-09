using Avalonia.Controls;
using Avalonia.Input;

namespace Rythmbox.SampleCreator.Views;

public partial class SampleCreatorPageView : UserControl
{
    public SampleCreatorPageView()
    {
        InitializeComponent();
    }

    private void OnPageKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ViewModels.SampleCreatorViewModel viewModel || e.Source is TextBox)
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
