using Rythmbox.Core.Models;
using SoundFlow.Midi.Enums;
using SoundFlow.Midi.Interfaces;
using SoundFlow.Midi.Structs;

namespace Rythmbox.Core.Engine;

/// <summary>
/// Routes live MIDI input to GM percussion pads using <see cref="PadMappingService"/>,
/// with optional MIDI-learn support (old DrumStage Settings page).
/// </summary>
public sealed class PadMidiRouter : IMidiControllable
{
    private readonly KitSamplePlayer _kitPlayer;
    private readonly PadMappingService _mapping;

    public PadMidiRouter(KitSamplePlayer kitPlayer, PadMappingService mapping)
    {
        _kitPlayer = kitPlayer;
        _mapping = mapping;
    }

    public bool IsEnabled { get; set; } = true;

    public event Action<int, int>? PadTriggered;

    public void ProcessMidiMessage(MidiMessage message)
    {
        if (!IsEnabled)
        {
            return;
        }

        var isNoteOn = message.Command == MidiCommand.NoteOn && message.Velocity > 0;
        if (!isNoteOn)
        {
            return;
        }

        var note = message.NoteNumber;

        if (_mapping.LearnPadIndex is { } learnPad)
        {
            _mapping.SetMidiNote(learnPad, note);
            _mapping.LearnPadIndex = null;
            PadLearnCompleted?.Invoke(learnPad, note);
            return;
        }

        if (_mapping.FindPadByMidiNote(note) is { } padIndex)
        {
            PadTriggered?.Invoke(padIndex, message.Velocity);
            _kitPlayer.TriggerPad(padIndex, message.Velocity / 127f);
        }
    }

    public event Action<int, int>? PadLearnCompleted;
}
