using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Rythmbox.App.ViewModels;

/// <summary>Timed status messages for the footer bar (old DrumStage status strip).</summary>
public sealed partial class StatusViewModel : ViewModelBase
{
    private readonly DispatcherTimer _clearTimer;

    public StatusViewModel()
    {
        _clearTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _clearTimer.Tick += (_, _) =>
        {
            _clearTimer.Stop();
            Message = DefaultMessage;
        };
    }

    public const string DefaultMessage = "SPACE play/pause  |  1-8 audition pads  |  ↑↓ tempo  |  ←→ loops  |  ESC quit";

    [ObservableProperty]
    private string _message = DefaultMessage;

    public void Show(string message, double seconds = 3)
    {
        Message = message;
        _clearTimer.Stop();
        _clearTimer.Interval = TimeSpan.FromSeconds(seconds);
        _clearTimer.Start();
    }
}
