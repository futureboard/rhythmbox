namespace Rythmbox.Core.Models.Styles;

/// <summary>Fixed pattern pad slots shown on the Machine page.</summary>
public sealed record PatternPadSlot(
    string Id,
    string Label,
    string? PatternKey,
    PatternPadKind Kind);

public enum PatternPadKind
{
    Pattern,
    Stop,
    TapTempo,
}

public static class PatternPadLayout
{
    public static readonly IReadOnlyList<PatternPadSlot> Slots =
    [
        new("intro_a", "Intro A", "intro_a", PatternPadKind.Pattern),
        new("intro_b", "Intro B", "intro_b", PatternPadKind.Pattern),
        new("verse_a", "Verse A", "verse_a", PatternPadKind.Pattern),
        new("verse_b", "Verse B", "verse_b", PatternPadKind.Pattern),
        new("chorus_a", "Chorus A", "chorus_a", PatternPadKind.Pattern),
        new("chorus_b", "Chorus B", "chorus_b", PatternPadKind.Pattern),
        new("fill_1", "Fill 1", "fill_1", PatternPadKind.Pattern),
        new("fill_2", "Fill 2", "fill_2", PatternPadKind.Pattern),
        new("break", "Break", "break", PatternPadKind.Pattern),
        new("ending", "Ending", "ending", PatternPadKind.Pattern),
        new("stop", "Stop", null, PatternPadKind.Stop),
        new("tap_tempo", "Tap Tempo", null, PatternPadKind.TapTempo),
    ];
}
