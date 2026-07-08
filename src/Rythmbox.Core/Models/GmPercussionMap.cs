namespace Rythmbox.Core.Models;

/// <summary>A single General MIDI percussion pad: a fixed note number on the GM percussion channel.</summary>
public sealed record PercussionPad(int Index, string Label, int Note);

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
        new(0, "Kick", 36),
        new(1, "Snare", 38),
        new(2, "C.Hat", 42),
        new(3, "O.Hat", 46),
        new(4, "Cym1", 49),
        new(5, "Cym2", 57),
        new(6, "H.Tom", 50),
        new(7, "M.Tom", 47),
        new(8, "L.Tom", 45),
        new(9, "F.Tom", 41),
        new(10, "China", 52),
        new(11, "Ride", 51),
        new(12, "Rim", 37),
        new(13, "Cowbell", 56),
        new(14, "L.Conga", 64),
        new(15, "Mt.Conga", 63),
        new(16, "H.Conga", 62),
        new(17, "Hi Bongo", 60),
        new(18, "Tamb", 54),
    ];
}
