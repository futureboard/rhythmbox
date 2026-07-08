namespace Rythmbox.Core.Models;

/// <summary>Broad category used to color-code a pad's mixer channel strip (DRUM vs. hand PERC).</summary>
public enum PadCategory
{
    Drum,
    Perc,
}

/// <summary>A single General MIDI percussion pad: a fixed note number on the GM percussion channel.</summary>
public sealed record PercussionPad(int Index, string Label, int Note, PadCategory Category);

/// <summary>
/// The standard General MIDI percussion map, laid out as 19 pads (4 columns) similar to a
/// DR-880-style pad grid. Works with any GM-compatible SoundFont since the note numbers are fixed.
/// </summary>
public static class GmPercussionMap
{
    /// <summary>MIDI channel reserved for percussion by the General MIDI convention (0-based: channel 10).</summary>
    public const int PercussionChannel = 9;

    public static readonly IReadOnlyList<PercussionPad> Pads =
    [
        new(0, "Kick", 36, PadCategory.Drum),
        new(1, "Snare", 38, PadCategory.Drum),
        new(2, "C.Hat", 42, PadCategory.Drum),
        new(3, "O.Hat", 46, PadCategory.Drum),
        new(4, "Cym1", 49, PadCategory.Drum),
        new(5, "Cym2", 57, PadCategory.Drum),
        new(6, "H.Tom", 50, PadCategory.Drum),
        new(7, "M.Tom", 47, PadCategory.Drum),
        new(8, "L.Tom", 45, PadCategory.Drum),
        new(9, "F.Tom", 41, PadCategory.Drum),
        new(10, "China", 52, PadCategory.Drum),
        new(11, "Ride", 51, PadCategory.Drum),
        new(12, "Rim", 37, PadCategory.Drum),
        new(13, "Cowbell", 56, PadCategory.Perc),
        new(14, "L.Conga", 64, PadCategory.Perc),
        new(15, "Mt.Conga", 63, PadCategory.Perc),
        new(16, "H.Conga", 62, PadCategory.Perc),
        new(17, "Hi Bongo", 60, PadCategory.Perc),
        new(18, "Tamb", 54, PadCategory.Perc),
    ];
}
