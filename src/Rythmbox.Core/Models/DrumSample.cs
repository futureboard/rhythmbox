namespace Rythmbox.Core.Models;

using Rythmbox.Core.Samples;

/// <summary>A single drum pad sample: mono PCM buffer plus kit metadata (old DrumStage preset format).</summary>
public sealed class DrumSample
{
    public string Label { get; set; } = string.Empty;

    public int MidiNote { get; set; } = -1;

    /// <summary>
    /// Additional MIDI notes assigned to this physical pad. <see cref="MidiNote"/>
    /// remains the primary legacy field so older presets keep loading unchanged.
    /// </summary>
    public List<int> MidiNotes { get; set; } = [];

    /// <summary>
    /// Returns the persisted/runtime assignment list while honoring the legacy
    /// primary-note property. This keeps callers that still set only
    /// <see cref="MidiNote"/> from silently retaining a stale default list.
    /// </summary>
    public int[] ResolveMidiNotes()
    {
        var notes = MidiNotes
            .Where(static note => note is >= 0 and <= 127)
            .Distinct()
            .Order()
            .ToList();

        if (MidiNote is < 0 or > 127)
        {
            return notes.ToArray();
        }

        if (!notes.Contains(MidiNote))
        {
            // A legacy writer changed only MidiNote. Treat that as a complete
            // single-note replacement rather than an accidental layer.
            return [MidiNote];
        }

        if (notes[0] != MidiNote)
        {
            notes.Remove(MidiNote);
            notes.Insert(0, MidiNote);
        }

        return notes.ToArray();
    }

    /// <summary>
    /// Stable physical pad slot. MIDI notes are user-editable, so they cannot
    /// be used as the identity of the sample slot when a kit is reloaded.
    /// </summary>
    public int PadIndex { get; set; } = -1;

    /// <summary>Musical mixer output selected for this pad.</summary>
    public DrumMixGroup OutputGroup { get; set; } = DrumMixGroup.Percussion;

    public int ChokeGroup { get; set; }

    public float Gain { get; set; } = 1f;

    /// <summary>Pitch offset in semitones applied at playback (0 = original). Not read from WAV metadata.</summary>
    public float PitchSemitones { get; set; }

    /// <summary>Amplitude envelope authored in Kit Builder and applied by the live player.</summary>
    public PadEnvelopeSettings Envelope { get; set; } = new();

    public List<VelocityLayer> VelocityLayers { get; set; } = [];

    public bool HasVelocityLayers => VelocityLayers.Exists(static layer => layer.HasSamples);

    public string? FilePath { get; set; }

    public float[] Samples { get; set; } = [];

    /// <summary>
    /// File-backed source used by JSON presets. It intentionally remains
    /// unmaterialized so loading a large kit does not allocate every PCM buffer.
    /// </summary>
    public MemoryMappedWavSample? MappedSample { get; set; }

    public int SampleRate { get; set; } = 48_000;

    public bool HasAudio => Samples.Length > 0 || MappedSample is { FrameCount: > 0 } || HasVelocityLayers;

    public TimeSpan Duration => EffectiveSampleRate > 0
        ? TimeSpan.FromSeconds((double)FrameCount / EffectiveSampleRate)
        : TimeSpan.Zero;

    public int FrameCount => Samples.Length > 0 ? Samples.Length : MappedSample?.FrameCount ?? 0;

    public int EffectiveSampleRate => Samples.Length > 0
        ? SampleRate
        : MappedSample?.SampleRate ?? SampleRate;

    /// <summary>Converts a mapped source to editable PCM only on explicit editor use.</summary>
    public float[] EnsureEditableSamples()
    {
        if (Samples.Length > 0 || MappedSample is null)
        {
            return Samples;
        }

        Samples = MappedSample.DecodeMono();
        SampleRate = WavCodec.TargetSampleRate;
        return Samples;
    }

    public DrumSample Clone()
    {
        var copy = new DrumSample
        {
            Label = Label,
            MidiNote = MidiNote,
            MidiNotes = [.. MidiNotes],
            PadIndex = PadIndex,
            OutputGroup = OutputGroup,
            ChokeGroup = ChokeGroup,
            Gain = Gain,
            PitchSemitones = PitchSemitones,
            Envelope = Envelope.Clone(),
            VelocityLayers = VelocityLayers
                .Select(layer => new VelocityLayer
                {
                    VelocityLow = layer.VelocityLow,
                    VelocityHigh = layer.VelocityHigh,
                    RoundRobinPaths = [.. layer.RoundRobinPaths],
                    RoundRobinSamples = layer.RoundRobinSamples
                        .Select(samples => (float[])samples.Clone())
                        .ToList(),
                    RoundRobinMappedSamples = [.. layer.RoundRobinMappedSamples],
                })
                .ToList(),
            FilePath = FilePath,
            SampleRate = SampleRate,
            Samples = (float[])Samples.Clone(),
            MappedSample = MappedSample,
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
