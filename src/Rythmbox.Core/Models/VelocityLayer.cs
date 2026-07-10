namespace Rythmbox.Core.Models;

using Rythmbox.Core.Samples;

/// <summary>One velocity zone with optional round-robin alternates (SFZ-style).</summary>
public sealed class VelocityLayer
{
    public int VelocityLow { get; set; } = 1;

    public int VelocityHigh { get; set; } = 127;

    public List<string> RoundRobinPaths { get; set; } = [];

    public List<float[]> RoundRobinSamples { get; set; } = [];

    /// <summary>
    /// On-disk round robins aligned with <see cref="RoundRobinSamples"/>. A
    /// mapped source keeps the kit light until it is played or edited.
    /// </summary>
    public List<MemoryMappedWavSample?> RoundRobinMappedSamples { get; set; } = [];

    public bool HasSamples => RoundRobinSamples.Any(static samples => samples.Length > 0)
        || RoundRobinMappedSamples.Any(static sample => sample is { FrameCount: > 0 });

    /// <summary>Materializes one mapped round robin for explicit waveform editing only.</summary>
    public float[] EnsureEditableRoundRobinSamples(int index)
    {
        if ((uint)index >= (uint)RoundRobinSamples.Count)
        {
            return [];
        }

        if (RoundRobinSamples[index].Length > 0)
        {
            return RoundRobinSamples[index];
        }

        if ((uint)index >= (uint)RoundRobinMappedSamples.Count || RoundRobinMappedSamples[index] is not { } mapped)
        {
            return RoundRobinSamples[index];
        }

        RoundRobinSamples[index] = mapped.DecodeMono();
        return RoundRobinSamples[index];
    }
}
