using Rythmbox.Core.Models;
using SoundFlow.Editing;
using SoundFlow.Metadata.Midi;
using SoundFlow.Midi.Enums;

namespace Rythmbox.Core.Editing;

/// <summary>Imports and exports <see cref="DrumPattern"/> instances as Standard MIDI Files.</summary>
public static class DrumPatternCodec
{
    private const int NoteDurationTicks = 120;

    public static DrumPattern Import(string path)
    {
        using var stream = File.OpenRead(path);
        var midiFile = MidiFileParser.Parse(stream);
        var ppq = midiFile.TicksPerQuarterNote;
        var pattern = new DrumPattern
        {
            Name = Path.GetFileNameWithoutExtension(path),
            Bpm = midiFile.InitialBeatsPerMinute ?? 120,
        };

        var ticksPerStep = TicksPerStep(pattern, ppq);
        var noteToPad = GmPercussionMap.Pads
            .Select((pad, index) => (pad.Note, index))
            .ToDictionary(x => x.Note, x => x.index);

        var maxStep = 0;
        var absoluteTick = 0L;
        foreach (var track in midiFile.Tracks)
        {
            absoluteTick = 0;
            foreach (var midiEvent in track.Events)
            {
                absoluteTick += midiEvent.DeltaTimeTicks;

                if (midiEvent is not ChannelEvent { Message: var message })
                {
                    continue;
                }

                if (message.Command != MidiCommand.NoteOn || message.Velocity == 0)
                {
                    continue;
                }

                if (!noteToPad.TryGetValue(message.NoteNumber, out var padIndex))
                {
                    continue;
                }

                var step = (int)(absoluteTick / ticksPerStep);
                if (step >= 0)
                {
                    pattern.Hits[(padIndex, step)] = (byte)message.Velocity;
                    maxStep = Math.Max(maxStep, step);
                }
            }
        }

        pattern.Bars = Math.Max(1, (maxStep + 1 + pattern.StepsPerBar - 1) / pattern.StepsPerBar);

        return pattern;
    }

    public static void Export(DrumPattern pattern, string path)
    {
        var ppq = DrumPattern.DefaultPpq;
        var sequence = new MidiSequence(ppq, [], [], [], []);
        var ticksPerStep = TicksPerStep(pattern, ppq);

        foreach (var ((pad, step), velocity) in pattern.Hits.OrderBy(h => h.Key.Step).ThenBy(h => h.Key.Pad))
        {
            if ((uint)pad >= (uint)GmPercussionMap.Pads.Count)
            {
                continue;
            }

            var note = GmPercussionMap.Pads[pad].Note;
            var startTick = (long)step * ticksPerStep;
            sequence.AddNote(startTick, NoteDurationTicks, note, velocity);
        }

        var midiFile = sequence.ToMidiFile();
        using var stream = File.Create(path);
        MidiFileWriter.Write(midiFile, stream);
    }

    public static long TicksPerStep(DrumPattern pattern, int ppq) =>
        Math.Max(1, (ppq * 4) / Math.Max(1, pattern.StepsPerBar));
}
