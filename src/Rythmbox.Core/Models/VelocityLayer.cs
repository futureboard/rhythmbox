namespace Rythmbox.Core.Models;

/// <summary>One velocity zone with optional round-robin alternates (SFZ-style).</summary>
public sealed class VelocityLayer
{
    public int VelocityLow { get; set; } = 1;

    public int VelocityHigh { get; set; } = 127;

    public List<string> RoundRobinPaths { get; set; } = [];

    public List<float[]> RoundRobinSamples { get; set; } = [];

    public bool HasSamples => RoundRobinSamples.Count > 0;
}
