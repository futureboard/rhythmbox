using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Rythmbox.App.ViewModels;

/// <summary>
/// Combines the loop browser's position-in-list with the player's transport state to drive
/// the "Now Playing" display box (loop index/total, status, name, BPM, progress).
/// </summary>
public sealed partial class NowPlayingViewModel : ViewModelBase
{
    private readonly LoopBrowserViewModel _loopBrowser;
    private readonly PlayerViewModel _player;

    public NowPlayingViewModel(LoopBrowserViewModel loopBrowser, PlayerViewModel player)
    {
        _loopBrowser = loopBrowser;
        _player = player;

        _loopBrowser.PropertyChanged += OnUpstreamPropertyChanged;
        _player.PropertyChanged += OnUpstreamPropertyChanged;
    }

    public string IndexLabel => $"{_loopBrowser.SelectedIndexDisplay:00}/{_loopBrowser.TotalCount:00}";

    public string StatusLabel => _player.IsPlaying ? "PLAYING" : "STOPPED";

    public string LoopName => _loopBrowser.SelectedLoop?.Name ?? "-- No Loop --";

    public string BpmLabel => $"{_player.Bpm:0.0}";

    public double ProgressFraction => _player.DurationSeconds > 0
        ? Math.Clamp(_player.PositionSeconds / _player.DurationSeconds, 0, 1)
        : 0;

    [RelayCommand]
    private void Next() => _loopBrowser.SelectNext();

    [RelayCommand]
    private void Previous() => _loopBrowser.SelectPrevious();

    private void OnUpstreamPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IndexLabel));
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(LoopName));
        OnPropertyChanged(nameof(BpmLabel));
        OnPropertyChanged(nameof(ProgressFraction));
    }
}
