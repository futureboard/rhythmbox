using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.Core.Engine;
using Rythmbox.Core.Models;

namespace Rythmbox.App.ViewModels;

/// <summary>A single percussion pad button: a fixed GM note that can be triggered live or highlighted when used by the loaded loop.</summary>
public sealed partial class PadViewModel : ViewModelBase
{
    private readonly SoundFontPlayer _soundFontPlayer;

    public PadViewModel(PercussionPad pad, SoundFontPlayer soundFontPlayer)
    {
        Pad = pad;
        _soundFontPlayer = soundFontPlayer;
    }

    public PercussionPad Pad { get; }

    public int Number => Pad.Index + 1;

    public string Label => Pad.Label;

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
        _soundFontPlayer.NoteOn(GmPercussionMap.PercussionChannel, Pad.Note, 110);
    }

    public void Release()
    {
        if (!IsPressed)
        {
            return;
        }

        IsPressed = false;
        _soundFontPlayer.NoteOff(GmPercussionMap.PercussionChannel, Pad.Note);
    }

    /// <summary>Triggers a single drum hit (note-on immediately followed by note-off) for mouse clicks / keyboard shortcuts.</summary>
    [RelayCommand]
    private void Hit()
    {
        Press();
        Release();
    }
}
