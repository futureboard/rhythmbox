using CommunityToolkit.Mvvm.ComponentModel;
using Rythmbox.Core.Models;

namespace Rythmbox.App.ViewModels;

/// <summary>Four beat LEDs synced to loop playback position (old DrumStage topbar).</summary>
public sealed partial class BeatLedViewModel : ViewModelBase
{
    private readonly PlayerViewModel _player;

    public BeatLedViewModel(PlayerViewModel player)
    {
        _player = player;
        _player.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(PlayerViewModel.IsPlaying) or nameof(PlayerViewModel.PositionSeconds) or nameof(PlayerViewModel.NativeBpm))
            {
                Refresh();
            }
        };
    }

    public IReadOnlyList<double> LedBrightness { get; } = new double[4];

    private readonly double[] _brightness = new double[4];

    public double Led0 => _brightness[0];
    public double Led1 => _brightness[1];
    public double Led2 => _brightness[2];
    public double Led3 => _brightness[3];

    public void Refresh()
    {
        if (!_player.IsPlaying || _player.DurationSeconds <= 0 || _player.NativeBpm <= 0)
        {
            SetAll(0);
            return;
        }

        var beatPos = _player.PositionSeconds * _player.NativeBpm / 60.0 % 4.0;
        var activeIdx = (int)beatPos;

        for (var i = 0; i < 4; i++)
        {
            _brightness[i] = i == activeIdx
                ? Math.Max(0, 1.0 - (beatPos - activeIdx) * 1.6)
                : 0;
        }

        OnPropertyChanged(nameof(Led0));
        OnPropertyChanged(nameof(Led1));
        OnPropertyChanged(nameof(Led2));
        OnPropertyChanged(nameof(Led3));
    }

    private void SetAll(double value)
    {
        for (var i = 0; i < 4; i++)
        {
            _brightness[i] = value;
        }

        OnPropertyChanged(nameof(Led0));
        OnPropertyChanged(nameof(Led1));
        OnPropertyChanged(nameof(Led2));
        OnPropertyChanged(nameof(Led3));
    }
}
