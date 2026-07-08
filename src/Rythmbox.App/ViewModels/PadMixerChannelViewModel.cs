using CommunityToolkit.Mvvm.ComponentModel;
using Rythmbox.Core.Engine;
using Rythmbox.Core.Models;

namespace Rythmbox.App.ViewModels;

/// <summary>A single vertical channel strip in the pad mixer: mute/solo/volume for one GM percussion pad.</summary>
public sealed partial class PadMixerChannelViewModel : ViewModelBase
{
    private readonly SoundFontPlayer _soundFontPlayer;

    public PadMixerChannelViewModel(PercussionPad pad, SoundFontPlayer soundFontPlayer)
    {
        Pad = pad;
        _soundFontPlayer = soundFontPlayer;
    }

    public PercussionPad Pad { get; }

    public string Label => Pad.Label;

    public bool IsPerc => Pad.Category == PadCategory.Perc;

    public string CategoryLabel => IsPerc ? "PERC" : "DRUM";

    [ObservableProperty]
    private bool _isMuted;

    partial void OnIsMutedChanged(bool value) => _soundFontPlayer.SetPadMute(Pad.Note, value);

    [ObservableProperty]
    private bool _isSoloed;

    partial void OnIsSoloedChanged(bool value) => _soundFontPlayer.SetPadSolo(Pad.Note, value);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VolumeLabel))]
    private double _volume = 1.0;

    partial void OnVolumeChanged(double value) => _soundFontPlayer.SetPadVolume(Pad.Note, (float)value);

    /// <summary>A DR-880-style dB-ish readout derived from the linear 0..1 volume, e.g. "0.0", "-6.2".</summary>
    public string VolumeLabel => Volume <= 0.001
        ? "-inf"
        : (20 * Math.Log10(Volume)).ToString("0.0");

    public void Trigger() => _soundFontPlayer.NoteOn(GmPercussionMap.PercussionChannel, Pad.Note, 110);
}
