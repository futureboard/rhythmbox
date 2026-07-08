using Rythmbox.Core.Engine;
using Rythmbox.Core.Models;

namespace Rythmbox.App.ViewModels;

public sealed class PercussionPadGridViewModel : ViewModelBase
{
    private readonly PlayerViewModel _player;

    public PercussionPadGridViewModel(KitSamplePlayer kitPlayer, PlayerViewModel player)
    {
        _player = player;
        Pads = GmPercussionMap.Pads.Select(pad => new PadViewModel(pad, kitPlayer)).ToList();

        _player.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlayerViewModel.UsedNoteNumbers))
            {
                RefreshUsage();
            }
        };
    }

    public IReadOnlyList<PadViewModel> Pads { get; }

    public void TriggerPad(int index)
    {
        if ((uint)index < (uint)Pads.Count)
        {
            Pads[index].Press();
        }
    }

    public void ReleasePad(int index)
    {
        if ((uint)index < (uint)Pads.Count)
        {
            Pads[index].Release();
        }
    }

    private void RefreshUsage()
    {
        var used = _player.UsedNoteNumbers;
        foreach (var pad in Pads)
        {
            pad.IsUsedInLoop = used.Contains(pad.Pad.Note);
        }
    }
}
