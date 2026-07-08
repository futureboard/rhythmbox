using CommunityToolkit.Mvvm.ComponentModel;
using Rythmbox.Core.Engine;
using Rythmbox.Core.Models;

namespace Rythmbox.App.ViewModels;

/// <summary>A single row in the MIDI player's track panel, representing one MIDI channel.</summary>
public sealed partial class TrackChannelViewModel : ViewModelBase
{
    private readonly MidiFilePlayer _player;

    public TrackChannelViewModel(MidiFilePlayer player, MidiTrackInfo info)
    {
        _player = player;
        Channel = info.Channel;
        Name = info.Name;
        ProgramNumber = info.ProgramNumber;
        NoteCount = info.NoteCount;
    }

    public int Channel { get; }

    public string Name { get; }

    public int ProgramNumber { get; }

    public int NoteCount { get; }

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private bool _isSoloed;

    [ObservableProperty]
    private double _volume = 1.0;

    partial void OnIsMutedChanged(bool value) => _player.SetTrackMute(Channel, value);

    partial void OnIsSoloedChanged(bool value) => _player.SetTrackSolo(Channel, value);

    partial void OnVolumeChanged(double value) => _player.SetTrackVolume(Channel, (float)value);
}
