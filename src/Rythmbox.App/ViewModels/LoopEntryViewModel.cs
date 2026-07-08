using CommunityToolkit.Mvvm.ComponentModel;
using Rythmbox.Core.Models;

namespace Rythmbox.App.ViewModels;

/// <summary>A single row in the MIDI loop browser.</summary>
public sealed partial class LoopEntryViewModel : ViewModelBase
{
    public LoopEntryViewModel(MidiLoopInfo info)
    {
        Info = info;
    }

    public MidiLoopInfo Info { get; }

    public string Name => Info.Name;

    public string HitsLabel => $"{Info.HitCount} hits";

    public string DurationLabel => $"{Info.Duration.TotalSeconds:0.00} s";

    public string BpmLabel => $"{Info.Bpm:0.0} BPM";

    [ObservableProperty]
    private bool _isSelected;
}
