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
        _mapping.MidiNotesChanged += OnMidiNotesChanged;
    }

    public bool IsEnabled { get; set; } = true;

    /// <summary>Raised for every Control Change received: (controllerNumber, value). Used for foot switches.</summary>
    public event Action<int, int>? ControlChangeReceived;

    public void ProcessMidiMessage(MidiMessage message)
    {
        if (!IsEnabled)
        {
            return;
        }

        if (message.Command == MidiCommand.ControlChange)
        {
            ControlChangeReceived?.Invoke(message.ControllerNumber, message.ControllerValue);
            if (message.ControllerNumber is 120 or 123) // All Sound Off / All Notes Off
            {
                _kitPlayer.AllNotesOff();
            }

            return;
        }

        var isNoteOn = message.Command == MidiCommand.NoteOn && message.Velocity > 0;
        var isNoteOff = message.Command == MidiCommand.NoteOff
            || (message.Command == MidiCommand.NoteOn && message.Velocity <= 0);
        if (!isNoteOn && !isNoteOff)
        {
            return;
        }

        var note = message.NoteNumber;

        if (isNoteOn && _mapping.LearnPadIndex is { } learnPad)
        {
            _mapping.SetMidiNote(learnPad, note);
            _mapping.LearnPadIndex = null;
            PadLearnCompleted?.Invoke(learnPad, note);
            return;
        }

        var mappedPads = _mapping.GetPadIndicesForMidiNote(note);
        foreach (var padIndex in mappedPads)
        {
            if (isNoteOn)
            {
                _kitPlayer.TriggerPad(padIndex, message.Velocity / 127f, note);
            }
            else
            {
                _kitPlayer.ReleasePad(padIndex, note);
            }
        }
    }

    public event Action<int, int>? PadLearnCompleted;

    private void OnMidiNotesChanged(int padIndex, IReadOnlyList<int> notes) =>
        _kitPlayer.SetPadMidiNotesByIndex(padIndex, notes);
}
