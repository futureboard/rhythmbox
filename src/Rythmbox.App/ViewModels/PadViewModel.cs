using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.Core.Engine;
using Rythmbox.Core.Models;

namespace Rythmbox.App.ViewModels;

/// <summary>A single percussion pad button: a fixed GM note that can be triggered live or highlighted when used by the loaded loop.</summary>
public sealed partial class PadViewModel : ViewModelBase
{
    private readonly KitSamplePlayer? _kitPlayer;

    private PadViewModel(PercussionPad pad, KitSamplePlayer? kitPlayer, bool isPlaceholder)
    {
        Pad = pad;
        _kitPlayer = kitPlayer;
        IsPlaceholder = isPlaceholder;
    }

    public PadViewModel(PercussionPad pad, KitSamplePlayer kitPlayer)
        : this(pad, kitPlayer, isPlaceholder: false)
    {
    }

    public static PadViewModel CreatePlaceholder(int slotIndex) =>
        new(new PercussionPad(-1, string.Empty, -1, PadCategory.Drum, PadBus.Drum), null, isPlaceholder: true)
        {
            PlaceholderSlot = slotIndex,
        };

    public PercussionPad Pad { get; }

    public bool IsPlaceholder { get; }

    public int PlaceholderSlot { get; private init; }

    public int Number => IsPlaceholder ? 0 : Pad.Index + 1;

    public string Label => IsPlaceholder ? string.Empty : Pad.Label;

    public string NoteName => IsPlaceholder ? string.Empty : MidiNoteNames.Format(Pad.Note);

    public string NoteDetail => IsPlaceholder ? string.Empty : $"{NoteName} / {Pad.Note}";

    [ObservableProperty]
    private bool _isUsedInLoop;

    [ObservableProperty]
    private bool _isPressed;

    public void Press()
    {
        if (IsPlaceholder || _kitPlayer is null)
        {
            return;
        }

        if (IsPressed)
        {
            return;
        }

        IsPressed = true;
        _kitPlayer.TriggerPad(Pad.Index, 110f / 127f);
    }

    public void Release()
    {
        if (!IsPressed)
        {
            return;
        }

        IsPressed = false;
        // One-shot samples; release is visual only.
    }

    [RelayCommand]
    private void Hit()
    {
        if (IsPlaceholder)
        {
            return;
        }

        Press();
        Release();
    }
}
