namespace Rythmbox.Core.Models;

/// <summary>Broad category used to color-code a pad's mixer channel strip (DRUM vs. hand PERC).</summary>
public enum PadCategory
{
    Drum,
    Perc,
}

/// <summary>Musical mixer channels that combine related GM percussion notes.</summary>
public enum DrumMixGroup
{
    Kick,
    Snare,
    HiHat,
    Toms,
    Cymbals,
    Percussion,
}

/// <summary>A single General MIDI percussion pad: a fixed note number on the GM percussion channel.</summary>
public sealed record PercussionPad(int Index, string Label, int Note, PadCategory Category, PadBus Bus);

/// <summary>
/// General MIDI Level 1 percussion map (notes 35–81). Used by the kit player, mixer, and pad grids.
/// </summary>
public static class GmPercussionMap
{
    /// <summary>MIDI channel reserved for percussion by the General MIDI convention (0-based: channel 10).</summary>
    public const int PercussionChannel = 9;

    public const int FirstNote = 35;
    public const int LastNote = 81;

    /// <summary>Display order used by the mixer; each group owns several GM notes.</summary>
    public static readonly IReadOnlyList<DrumMixGroup> MixGroups =
    [
        DrumMixGroup.Kick,
        DrumMixGroup.Snare,
        DrumMixGroup.HiHat,
        DrumMixGroup.Toms,
        DrumMixGroup.Cymbals,
        DrumMixGroup.Percussion,
    ];

    public static DrumMixGroup GetMixGroup(int note) => note switch
    {
        35 or 36 => DrumMixGroup.Kick,
        37 or 38 or 39 or 40 => DrumMixGroup.Snare,
        42 or 44 or 46 => DrumMixGroup.HiHat,
        41 or 43 or 45 or 47 or 48 or 50 => DrumMixGroup.Toms,
        49 or 51 or 52 or 53 or 55 or 57 or 59 => DrumMixGroup.Cymbals,
        _ => DrumMixGroup.Percussion,
    };

    public static string GetMixGroupLabel(DrumMixGroup group) => group switch
    {
        DrumMixGroup.Kick => "KICK",
        DrumMixGroup.Snare => "SNARE",
        DrumMixGroup.HiHat => "HATS / OH",
        DrumMixGroup.Toms => "TOMS",
        DrumMixGroup.Cymbals => "CYMBALS",
        _ => "PERC",
    };

    public static readonly IReadOnlyList<PercussionPad> Pads =
    [
        new(0, "A.Kick", 35, PadCategory.Drum, PadBus.Drum),
        new(1, "Kick", 36, PadCategory.Drum, PadBus.Drum),
        new(2, "Rim", 37, PadCategory.Drum, PadBus.Drum),
        new(3, "Snare", 38, PadCategory.Drum, PadBus.Drum),
        new(4, "Clap", 39, PadCategory.Drum, PadBus.Drum),
        new(5, "E.Snr", 40, PadCategory.Drum, PadBus.Drum),
        new(6, "F.Tom", 41, PadCategory.Drum, PadBus.Perc),
        new(7, "C.Hat", 42, PadCategory.Drum, PadBus.Perc),
        new(8, "H.F.Tm", 43, PadCategory.Drum, PadBus.Perc),
        new(9, "P.Hat", 44, PadCategory.Drum, PadBus.Perc),
        new(10, "L.Tom", 45, PadCategory.Drum, PadBus.Perc),
        new(11, "O.Hat", 46, PadCategory.Drum, PadBus.Perc),
        new(12, "M.Tom", 47, PadCategory.Drum, PadBus.Perc),
        new(13, "H.M.Tm", 48, PadCategory.Drum, PadBus.Perc),
        new(14, "Cym1", 49, PadCategory.Drum, PadBus.Cym),
        new(15, "H.Tom", 50, PadCategory.Drum, PadBus.Perc),
        new(16, "Ride", 51, PadCategory.Drum, PadBus.Cym),
        new(17, "China", 52, PadCategory.Drum, PadBus.Cym),
        new(18, "Rd.Bell", 53, PadCategory.Drum, PadBus.Cym),
        new(19, "Tamb", 54, PadCategory.Perc, PadBus.Perc),
        new(20, "Splash", 55, PadCategory.Drum, PadBus.Cym),
        new(21, "Cowbell", 56, PadCategory.Perc, PadBus.Perc),
        new(22, "Cym2", 57, PadCategory.Drum, PadBus.Cym),
        new(23, "Vibra", 58, PadCategory.Perc, PadBus.Perc),
        new(24, "Ride2", 59, PadCategory.Drum, PadBus.Cym),
        new(25, "H.Bongo", 60, PadCategory.Perc, PadBus.Perc),
        new(26, "L.Bongo", 61, PadCategory.Perc, PadBus.Perc),
        new(27, "M.Cnga", 62, PadCategory.Perc, PadBus.Perc),
        new(28, "O.Cnga", 63, PadCategory.Perc, PadBus.Perc),
        new(29, "L.Cnga", 64, PadCategory.Perc, PadBus.Perc),
        new(30, "H.Timb", 65, PadCategory.Perc, PadBus.Perc),
        new(31, "L.Timb", 66, PadCategory.Perc, PadBus.Perc),
        new(32, "H.Agogo", 67, PadCategory.Perc, PadBus.Perc),
        new(33, "L.Agogo", 68, PadCategory.Perc, PadBus.Perc),
        new(34, "Cabasa", 69, PadCategory.Perc, PadBus.Perc),
        new(35, "Maracas", 70, PadCategory.Perc, PadBus.Perc),
        new(36, "Sh.Wh", 71, PadCategory.Perc, PadBus.Perc),
        new(37, "Lg.Wh", 72, PadCategory.Perc, PadBus.Perc),
        new(38, "Sh.Gui", 73, PadCategory.Perc, PadBus.Perc),
        new(39, "Lg.Gui", 74, PadCategory.Perc, PadBus.Perc),
        new(40, "Claves", 75, PadCategory.Perc, PadBus.Perc),
        new(41, "H.Wood", 76, PadCategory.Perc, PadBus.Perc),
        new(42, "L.Wood", 77, PadCategory.Perc, PadBus.Perc),
        new(43, "M.Cuica", 78, PadCategory.Perc, PadBus.Perc),
        new(44, "O.Cuica", 79, PadCategory.Perc, PadBus.Perc),
        new(45, "M.Tri", 80, PadCategory.Perc, PadBus.Perc),
        new(46, "O.Tri", 81, PadCategory.Perc, PadBus.Perc),
    ];
}
