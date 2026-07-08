using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.Core.Engine;
using Rythmbox.Core.Models;

namespace Rythmbox.App.ViewModels;

/// <summary>A single percussion pad button: a fixed GM note that can be triggered live or highlighted when used by the loaded loop.</summary>
public sealed partial class PadViewModel : ViewModelBase
{
    private readonly KitSamplePlayer _kitPlayer;

    public PadViewModel(PercussionPad pad, KitSamplePlayer kitPlayer)
    {
        Pad = pad;
        _kitPlayer = kitPlayer;
    }

    public PercussionPad Pad { get; }

    public int Number => Pad.Index + 1;

    public string Label => Pad.Label;

    public string NoteName => MidiNoteNames.Format(Pad.Note);

    public string NoteDetail => $"{NoteName} / {Pad.Note}";

    [ObservableProperty]
    private bool _isUsedInLoop;

    [ObservableProperty]
    private bool _isPressed;

    public void Press()
    {
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

    /// <summary>Triggers a single drum hit (note-on immediately followed by note-off) for mouse clicks / keyboard shortcuts.</summary>
    [RelayCommand]
    private void Hit()
    {
        Press();
        Release();
    }
}
