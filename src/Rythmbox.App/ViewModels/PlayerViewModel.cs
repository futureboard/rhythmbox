using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.Core.Engine;
using SoundFlow.Enums;

namespace Rythmbox.App.ViewModels;

public sealed partial class PlayerViewModel : ViewModelBase, IDisposable
{
    private readonly MidiFilePlayer _player;
    private readonly DispatcherTimer _timer;
    private bool _isSyncingPosition;

    public PlayerViewModel(MidiFilePlayer player)
    {
        _player = player;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _timer.Tick += (_, _) => SyncFromEngine();
        _timer.Start();
    }

    public ObservableCollection<TrackChannelViewModel> Tracks { get; } = new();

    [ObservableProperty]
    private string? _loadedFileName;

    [ObservableProperty]
    private double _positionSeconds;

    [ObservableProperty]
    private double _durationSeconds;

    [ObservableProperty]
    private string _positionText = "00:00";

    [ObservableProperty]
    private string _durationText = "00:00";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayPauseLabel))]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isLooping;

    [ObservableProperty]
    private double _bpm = 120.0;

    public string PlayPauseLabel => IsPlaying ? "Pause" : "Play";

    /// <summary>The distinct note numbers used anywhere in the currently loaded loop.</summary>
    public IReadOnlySet<int> UsedNoteNumbers => _player.UsedNoteNumbers;

    partial void OnIsLoopingChanged(bool value) => _player.IsLooping = value;

    partial void OnPositionSecondsChanged(double value)
    {
        if (_isSyncingPosition || !_player.IsLoaded)
        {
            return;
        }

        _player.Seek(TimeSpan.FromSeconds(value));
    }

    public void OpenFile(string path)
    {
        _player.Load(path);

        LoadedFileName = Path.GetFileName(path);
        DurationSeconds = _player.Duration.TotalSeconds;
        DurationText = FormatTime(_player.Duration);
        IsLooping = _player.IsLooping;
        Bpm = _player.Bpm;
        OnPropertyChanged(nameof(UsedNoteNumbers));

        Tracks.Clear();
        foreach (var track in _player.Tracks)
        {
            Tracks.Add(new TrackChannelViewModel(_player, track));
        }

        _player.Play();
    }

    [RelayCommand]
    private void PlayPause()
    {
        if (!_player.IsLoaded)
        {
            return;
        }

        if (_player.State == PlaybackState.Playing)
        {
            _player.Pause();
        }
        else
        {
            _player.Play();
        }
    }

    [RelayCommand]
    private void Stop() => _player.Stop();

    private void SyncFromEngine()
    {
        if (!_player.IsLoaded)
        {
            return;
        }

        IsPlaying = _player.State == PlaybackState.Playing;

        _isSyncingPosition = true;
        PositionSeconds = _player.Position.TotalSeconds;
        _isSyncingPosition = false;

        PositionText = FormatTime(_player.Position);
    }

    private static string FormatTime(TimeSpan time) => time.ToString(@"mm\:ss");

    public void Dispose() => _timer.Stop();
}
