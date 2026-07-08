namespace Rythmbox.Core.Models.Styles;

public sealed class StyleDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Category { get; init; }

    public string Description { get; init; } = string.Empty;

    public double DefaultTempo { get; init; } = 120;

    public double TempoMin { get; init; } = 90;

    public double TempoMax { get; init; } = 140;

    public string TimeSignature { get; init; } = "4/4";

    public string Feel { get; init; } = "straight";

    public string DefaultKit { get; init; } = "gm_standard";

    public IReadOnlyList<string> Tags { get; init; } = [];

    public IReadOnlyDictionary<string, StylePattern> Patterns { get; init; }
        = new Dictionary<string, StylePattern>();

    public RhythmMacros Macros { get; init; } = RhythmMacros.Default;

    public string StyleDirectory { get; init; } = string.Empty;

    public IReadOnlyList<string> ValidationWarnings { get; init; } = [];

    public IReadOnlyList<string> ValidationErrors { get; init; } = [];

    public bool IsValid => ValidationErrors.Count == 0;
}
