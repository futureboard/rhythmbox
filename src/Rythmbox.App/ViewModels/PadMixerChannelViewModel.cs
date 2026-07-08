using CommunityToolkit.Mvvm.ComponentModel;
using Rythmbox.Core.Engine;
using Rythmbox.Core.Models;

namespace Rythmbox.App.ViewModels;

/// <summary>A single vertical channel strip in the pad mixer: mute/solo/volume for one GM percussion pad.</summary>
public sealed partial class PadMixerChannelViewModel : ViewModelBase
{
    private readonly KitSamplePlayer _kitPlayer;

    public PadMixerChannelViewModel(PercussionPad pad, KitSamplePlayer kitPlayer)
    {
        Pad = pad;
        _kitPlayer = kitPlayer;
    }

    public PercussionPad Pad { get; }

    public string Label => Pad.Label;

    public string CategoryLabel => Pad.Bus switch
    {
        PadBus.Perc => "PERC",
        PadBus.Cym => "CYM",
        _ => "DRUM",
    };

    public bool IsPerc => Pad.Bus == PadBus.Perc;

    public bool IsCym => Pad.Bus == PadBus.Cym;

    [ObservableProperty]
    private bool _isMuted;

    partial void OnIsMutedChanged(bool value) => _kitPlayer.SetPadMute(Pad.Note, value);

    [ObservableProperty]
    private bool _isSoloed;

    partial void OnIsSoloedChanged(bool value) => _kitPlayer.SetPadSolo(Pad.Note, value);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VolumeLabel))]
    private double _volume = 1.0;

    partial void OnVolumeChanged(double value) => _kitPlayer.SetPadVolume(Pad.Note, (float)value);

    /// <summary>A DR-880-style dB-ish readout derived from the linear 0..1 volume, e.g. "0.0", "-6.2".</summary>
    public string VolumeLabel => Volume <= 0.001
        ? "-inf"
        : (20 * Math.Log10(Volume)).ToString("0.0");

    public void Trigger() => _kitPlayer.TriggerPad(Pad.Index, 110f / 127f);
}
