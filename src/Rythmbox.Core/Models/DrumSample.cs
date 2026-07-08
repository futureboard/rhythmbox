namespace Rythmbox.Core.Models;

/// <summary>A single drum pad sample: mono PCM buffer plus kit metadata (old DrumStage preset format).</summary>
public sealed class DrumSample
{
    public string Label { get; set; } = string.Empty;

    public int MidiNote { get; set; } = -1;

    public int ChokeGroup { get; set; }

    public float Gain { get; set; } = 1f;

    public string? FilePath { get; set; }

    public float[] Samples { get; set; } = [];

    public int SampleRate { get; set; } = 48_000;

    public bool HasAudio => Samples.Length > 0;

    public TimeSpan Duration => SampleRate > 0
        ? TimeSpan.FromSeconds((double)Samples.Length / SampleRate)
        : TimeSpan.Zero;

    public DrumSample Clone()
    {
        var copy = new DrumSample
        {
            Label = Label,
            MidiNote = MidiNote,
            ChokeGroup = ChokeGroup,
            Gain = Gain,
            FilePath = FilePath,
            SampleRate = SampleRate,
            Samples = (float[])Samples.Clone(),
        };
        return copy;
    }
}

/// <summary>A full drum kit preset: one sample slot per pad (old <c>shared/PRESETS/*.json</c> format).</summary>
public sealed class KitPreset
{
    public string Name { get; set; } = "Untitled Kit";

    public List<DrumSample> Pads { get; set; } = [];
}
