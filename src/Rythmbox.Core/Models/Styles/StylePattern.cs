namespace Rythmbox.Core.Models.Styles;

public sealed class StylePattern
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public PatternType Type { get; init; }

    public string Variation { get; init; } = string.Empty;

    public int Bars { get; init; } = 4;

    public bool OneShot { get; init; }

    public PatternPlaybackMode PlaybackMode { get; init; } = PatternPlaybackMode.Loop;

    public string? MidiRelativePath { get; init; }

    public string? ResolvedMidiPath { get; set; }

    public bool HasMidiFile => !string.IsNullOrEmpty(ResolvedMidiPath) && File.Exists(ResolvedMidiPath);
}
