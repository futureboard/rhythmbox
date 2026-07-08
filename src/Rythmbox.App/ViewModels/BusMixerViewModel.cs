using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.Core.Engine;
using Rythmbox.Core.Models;

namespace Rythmbox.App.ViewModels;

public sealed partial class BusMixerChannelViewModel : ViewModelBase
{
    private readonly KitSamplePlayer _kitPlayer;

    public BusMixerChannelViewModel(PadBus bus, string label, KitSamplePlayer kitPlayer)
    {
        Bus = bus;
        Label = label;
        _kitPlayer = kitPlayer;
    }

    public PadBus Bus { get; }

    public string Label { get; }

    [ObservableProperty]
    private bool _isMuted;

    partial void OnIsMutedChanged(bool value) => _kitPlayer.SetBusMute(Bus, value);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VolumeLabel))]
    private double _volume = 1.0;

    partial void OnVolumeChanged(double value) => _kitPlayer.SetBusVolume(Bus, (float)value);

    public string VolumeLabel => Volume <= 0.001
        ? "-inf"
        : (20 * Math.Log10(Volume)).ToString("0.0");
}

public sealed class BusMixerViewModel : ViewModelBase
{
    public BusMixerViewModel(KitSamplePlayer kitPlayer)
    {
        Buses =
        [
            new BusMixerChannelViewModel(PadBus.Drum, "DRUM", kitPlayer),
            new BusMixerChannelViewModel(PadBus.Perc, "PERC", kitPlayer),
            new BusMixerChannelViewModel(PadBus.Cym, "CYM", kitPlayer),
        ];
    }

    public IReadOnlyList<BusMixerChannelViewModel> Buses { get; }
}
