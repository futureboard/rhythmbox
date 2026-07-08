using Avalonia.Controls;
using Avalonia.Input;
using Rythmbox.App.ViewModels;

namespace Rythmbox.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source is TextBox || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Space:
                viewModel.Player.PlayPauseCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Escape:
                viewModel.QuitCommand.Execute(null);
                e.Handled = true;
                break;

            default:
                if (TryGetPadIndex(e.Key) is { } index)
                {
                    viewModel.PadGrid.TriggerPad(index);
                    e.Handled = true;
                }

                break;
        }
    }

    private void OnWindowKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Source is TextBox || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (TryGetPadIndex(e.Key) is { } index)
        {
            viewModel.PadGrid.ReleasePad(index);
            e.Handled = true;
        }
    }

    private static int? TryGetPadIndex(Key key) => key switch
    {
        >= Key.D1 and <= Key.D8 => key - Key.D1,
        >= Key.NumPad1 and <= Key.NumPad8 => key - Key.NumPad1,
        _ => null,
    };
}
