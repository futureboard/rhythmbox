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

        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        switch (e.Key)
        {
            case Key.Space:
                viewModel.Player.PlayPauseCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Escape:
                if (viewModel.Tempo.IsPickerOpen)
                {
                    viewModel.Tempo.TogglePickerCommand.Execute(null);
                }
                else if (viewModel.SubLoops.IsOpen)
                {
                    viewModel.SubLoops.CloseCommand.Execute(null);
                }
                else
                {
                    viewModel.QuitCommand.Execute(null);
                }

                e.Handled = true;
                break;

            case Key.F11:
                WindowState = WindowState == WindowState.FullScreen
                    ? WindowState.Normal
                    : WindowState.FullScreen;
                e.Handled = true;
                break;

            case Key.Up:
                viewModel.NudgeTempo(1, shift);
                e.Handled = true;
                break;

            case Key.Down:
                viewModel.NudgeTempo(-1, shift);
                e.Handled = true;
                break;

            case Key.Left:
                viewModel.LoopBrowser.SelectPrevious();
                e.Handled = true;
                break;

            case Key.Right:
                viewModel.LoopBrowser.SelectNext();
                e.Handled = true;
                break;

            case Key.Q:
                if (viewModel.PadGrid.Pads.Count > 0)
                {
                    var selected = viewModel.PadGrid.Pads[0];
                    selected.HitCommand.Execute(null);
                }

                e.Handled = true;
                break;

            default:
                if (TryGetKeyName(e.Key) is { } keyName &&
                    viewModel.FindPadByKey(keyName) is { } padIndex)
                {
                    viewModel.PadGrid.TriggerPad(padIndex);
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

        if (TryGetKeyName(e.Key) is { } keyName &&
            viewModel.FindPadByKey(keyName) is { } padIndex)
        {
            viewModel.PadGrid.ReleasePad(padIndex);
            e.Handled = true;
        }
    }

    private static string? TryGetKeyName(Key key) => key switch
    {
        >= Key.D0 and <= Key.D9 => ((int)(key - Key.D0)).ToString(),
        >= Key.A and <= Key.Z => key.ToString(),
        Key.OemMinus => "-",
        Key.OemPlus => "=",
        Key.OemOpenBrackets => "[",
        Key.OemCloseBrackets => "]",
        Key.OemBackslash => "\\",
        Key.OemSemicolon => ";",
        Key.OemQuotes => "'",
        Key.OemComma => ",",
        Key.OemPeriod => ".",
        _ => null,
    };
}
