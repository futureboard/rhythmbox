using Rythmbox.Core.Engine;
using Rythmbox.Core.Models;

namespace Rythmbox.App.ViewModels;

/// <summary>The full percussion kit mixer: one vertical channel strip per GM pad.</summary>
public sealed class PadMixerViewModel : ViewModelBase
{
    public PadMixerViewModel(KitSamplePlayer kitPlayer)
    {
        Channels = GmPercussionMap.Pads
            .Select(pad => new PadMixerChannelViewModel(pad, kitPlayer))
            .ToList();
    }

    public IReadOnlyList<PadMixerChannelViewModel> Channels { get; }
}
