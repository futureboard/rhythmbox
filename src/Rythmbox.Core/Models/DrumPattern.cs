namespace Rythmbox.Core.Models;

/// <summary>
/// A step-sequencer drum pattern: sparse hits on a GM percussion grid (pad index × step index).
/// </summary>
public sealed class DrumPattern
{
    public const int DefaultStepsPerBar = 16;
    public const int DefaultPpq = 480;
    public const byte DefaultVelocity = 100;

    public int StepsPerBar { get; set; } = DefaultStepsPerBar;

    public int Bars { get; set; } = 1;

    public double Bpm { get; set; } = 120;

    public string Name { get; set; } = "Untitled";

    public int TotalSteps => StepsPerBar * Bars;

    /// <summary>Key: (pad index 0..18, step index 0..TotalSteps-1). Value: note-on velocity.</summary>
    public Dictionary<(int Pad, int Step), byte> Hits { get; } = new();

    public bool HasHit(int pad, int step) => Hits.ContainsKey((pad, step));

    public byte GetVelocity(int pad, int step) =>
        Hits.TryGetValue((pad, step), out var velocity) ? velocity : DefaultVelocity;

    public void ToggleHit(int pad, int step, byte velocity = DefaultVelocity)
    {
        if (Hits.Remove((pad, step)))
        {
            return;
        }

        Hits[(pad, step)] = velocity;
    }

    public void Clear() => Hits.Clear();

    public DrumPattern Clone()
    {
        var copy = new DrumPattern
        {
            StepsPerBar = StepsPerBar,
            Bars = Bars,
            Bpm = Bpm,
            Name = Name,
        };

        foreach (var (key, velocity) in Hits)
        {
            copy.Hits[key] = velocity;
        }

        return copy;
    }
}
