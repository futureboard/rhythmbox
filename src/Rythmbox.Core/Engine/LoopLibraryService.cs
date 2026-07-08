using Rythmbox.Core.Models;
using SoundFlow.Metadata.Midi;
using SoundFlow.Midi.Enums;
using SoundFlow.Providers;

namespace Rythmbox.Core.Engine;

/// <summary>
/// Scans a folder for Standard MIDI Files and summarizes each as a "loop" (name, hit count,
/// duration, BPM, and which note numbers it uses) for a DR-880-style loop browser.
/// </summary>
public sealed class LoopLibraryService
{
    private static readonly string[] MidiExtensions = [".mid", ".midi", ".seq"];

    public string? CurrentFolder { get; private set; }

    /// <summary>Lists sub-banks under a RYTHM root (Main + each immediate subfolder).</summary>
    public IReadOnlyList<LoopBank> ScanBanks(string rootFolder)
    {
        if (!Directory.Exists(rootFolder))
        {
            return [];
        }

        var banks = new List<LoopBank> { new("Main", rootFolder) };

        foreach (var dir in Directory.EnumerateDirectories(rootFolder).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            banks.Add(new LoopBank(Path.GetFileName(dir), dir));
        }

        return banks;
    }

    public IReadOnlyList<MidiLoopInfo> Scan(string folder)
    {
        CurrentFolder = folder;

        if (!Directory.Exists(folder))
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
            var midiFile = MidiFileParser.Parse(stream);
            var dataProvider = new MidiDataProvider(midiFile);

            var hitCount = 0;
            var usedNotes = new HashSet<int>();

            foreach (var track in midiFile.Tracks)
            {
                foreach (var midiEvent in track.Events)
                {
                    if (midiEvent is ChannelEvent { Message: var message } &&
                        message.Command == MidiCommand.NoteOn &&
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
            // Skip files that fail to parse (corrupt/unsupported) rather than crash the scan.
            loop = null!;
            return false;
        }
    }
}
