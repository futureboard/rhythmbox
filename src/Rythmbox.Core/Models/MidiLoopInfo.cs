namespace Rythmbox.Core.Models;

/// <summary>
/// Metadata about a single MIDI loop file discovered by <see cref="Rythmbox.Core.Engine.LoopLibraryService"/>,
/// used to drive a DR-880-style loop browser (name, hit count, duration, BPM).
/// </summary>
public sealed record MidiLoopInfo(
    string FilePath,
    string Name,
    int HitCount,
    TimeSpan Duration,
    double Bpm,
    IReadOnlySet<int> UsedNoteNumbers);
