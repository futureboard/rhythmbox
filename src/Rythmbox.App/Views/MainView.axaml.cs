using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Rythmbox.App.ViewModels;

namespace Rythmbox.App.Views;

public partial class MainView : UserControl
{
    private MainWindowViewModel? _viewModel;
    private bool _isBooting;
    private bool _isClosing;

    public MainView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        LostFocus += OnLostFocus;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        Focus();
        await InitializeRuntimeAsync();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        _isClosing = true;
        DisposeRuntime();
    }

    private void OnLostFocus(object? sender, RoutedEventArgs e) => _viewModel?.PadGrid.ClearPointerStates();

    public async Task InitializeRuntimeAsync()
    {
        if (_isBooting || _isClosing || _viewModel is not null)
        {
            return;
        }

        _isBooting = true;
        try
        {
            ReportBootStatus("Loading Rythmbox runtime…");
            var viewModel = await Task.Run(() => new MainWindowViewModel(ReportBootStatus));

            if (_isClosing)
            {
                viewModel.Dispose();
                return;
            }

            _viewModel = viewModel;
            DataContext = viewModel;
            AppShell.IsVisible = true;
            SplashOverlay.IsVisible = false;
        }
        catch (Exception ex)
        {
            ReportBootStatus($"Startup failed: {ex.Message}");
        }
        finally
        {
            _isBooting = false;
        }
    }

    public void DisposeRuntime()
    {
        _viewModel?.Dispose();
        _viewModel = null;
    }

    private void ReportBootStatus(string status)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            SplashStatus.Text = status;
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (!_isClosing)
            {
                SplashStatus.Text = status;
            }
        });
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source is TextBox || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (viewModel.IsEditorPage)
        {
            if (HandleEditorKeys(viewModel, e))
            {
                return;
            }
        }
        else if (viewModel.IsMacroPage)
        {
            if (HandleSampleCreatorKeys(viewModel, e))
            {
                return;
            }
        }

        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);

        // Ctrl +/-/0 adjust the in-app scale (application zoom), independent of the OS DPI.
        if (ctrl)
        {
            switch (e.Key)
            {
                case Key.OemPlus or Key.Add:
                    viewModel.IncreaseAppScaleCommand.Execute(null);
                    e.Handled = true;
                    return;

                case Key.OemMinus or Key.Subtract:
                    viewModel.DecreaseAppScaleCommand.Execute(null);
                    e.Handled = true;
                    return;

                case Key.D0 or Key.NumPad0:
                    viewModel.ResetAppScaleCommand.Execute(null);
                    e.Handled = true;
                    return;
            }
        }

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
                ToggleFullScreen();
                e.Handled = true;
                break;

            case Key.T:
                viewModel.Tempo.TapTempoCommand.Execute(null);
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

    /// <summary>
    /// Toggles full screen when hosted in a desktop window. On DRM/KMS (embedded
    /// Linux) there is no window and the surface is always full screen, so this is a no-op.
    /// </summary>
    private void ToggleFullScreen()
    {
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            window.WindowState = window.WindowState == WindowState.FullScreen
                ? WindowState.Normal
                : WindowState.FullScreen;
        }
    }

    private static bool HandleEditorKeys(MainWindowViewModel viewModel, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Space:
                viewModel.Editor.PlayPreviewCommand.Execute(null);
                e.Handled = true;
                return true;

            case Key.Escape:
                viewModel.Editor.StopPreviewCommand.Execute(null);
                e.Handled = true;
                return true;
        }

        return false;
    }

    private static bool HandleSampleCreatorKeys(MainWindowViewModel viewModel, KeyEventArgs e)
    {
        var creator = viewModel.SampleCreator;
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);

        switch (e.Key)
        {
            case Key.Space when creator.SelectedPad is not null:
                creator.SelectedPad.PreviewCommand.Execute(null);
                e.Handled = true;
                return true;

            case Key.Escape:
                creator.StopPreviewCommand.Execute(null);
                e.Handled = true;
                return true;

            case Key.O when ctrl:
                creator.BrowseOpenPresetCommand.Execute(null);
                e.Handled = true;
                return true;

            case Key.S when ctrl:
                creator.BrowseSaveKitCommand.Execute(null);
                e.Handled = true;
                return true;
        }

        return false;
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
