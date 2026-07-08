using Rythmbox.Core.Models;

namespace Rythmbox.Core.Engine;

/// <summary>Scans <c>shared/SUBMIDI/</c> for sub/fill MIDI loops (old DrumStage SUB picker).</summary>
public sealed class SubLoopService
{
    private static readonly string[] MidiExtensions = [".mid", ".midi"];

    public string? CurrentFolder { get; private set; }

    public IReadOnlyList<MidiLoopInfo> Scan(string? folder)
    {
        CurrentFolder = folder;

        if (folder is null || !Directory.Exists(folder))
        {
            return [];
        }

        var loops = new List<MidiLoopInfo>();

        foreach (var file in Directory.EnumerateFiles(folder)
                     .Where(f => MidiExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                     .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            if (TryAnalyze(file, out var loop))
            {
                loops.Add(loop);
            }
        }

        return loops;
    }

    private static bool TryAnalyze(string filePath, out MidiLoopInfo loop)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            var midiFile = SoundFlow.Metadata.Midi.MidiFileParser.Parse(stream);
            var dataProvider = new SoundFlow.Providers.MidiDataProvider(midiFile);

            var hitCount = 0;
            var usedNotes = new HashSet<int>();

            foreach (var track in midiFile.Tracks)
            {
                foreach (var midiEvent in track.Events)
                {
                    if (midiEvent is SoundFlow.Metadata.Midi.ChannelEvent { Message: var message } &&
                        message.Command == SoundFlow.Midi.Enums.MidiCommand.NoteOn &&
                        message.Velocity > 0)
                    {
                        hitCount++;
                        usedNotes.Add(message.NoteNumber);
                    }
                }
            }

            var bpm = midiFile.InitialBeatsPerMinute ?? 120.0;

            loop = new MidiLoopInfo(
                filePath,
                Path.GetFileNameWithoutExtension(filePath),
                hitCount,
                dataProvider.Duration,
                bpm,
                usedNotes);
            return true;
        }
        catch
        {
            loop = null!;
            return false;
        }
    }
}
