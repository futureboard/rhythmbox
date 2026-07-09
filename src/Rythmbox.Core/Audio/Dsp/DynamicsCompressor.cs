namespace Rythmbox.Core.Audio.Dsp;

/// <summary>Simple feed-forward compressor with envelope follower.</summary>
public sealed class DynamicsCompressor
{
    private float _envelope;

    public bool Enabled { get; set; }

    public float ThresholdDb { get; set; } = -18f;

    public float Ratio { get; set; } = 3f;

    public float MakeupDb { get; set; }

    public float Process(float input, float sampleRate)
    {
        if (!Enabled)
        {
            return input;
        }

        var attack = MathF.Exp(-1f / (0.003f * sampleRate));
        var release = MathF.Exp(-1f / (0.08f * sampleRate));
        var abs = MathF.Abs(input);
        _envelope = abs > _envelope
            ? attack * _envelope + (1f - attack) * abs
            : release * _envelope + (1f - release) * abs;

        var levelDb = 20f * MathF.Log10(MathF.Max(_envelope, 1e-6f));
        var overDb = levelDb - ThresholdDb;
        if (overDb <= 0f)
        {
            return input * DbToLinear(MakeupDb);
        }

        var gainDb = -overDb + (overDb / MathF.Max(1f, Ratio));
        return input * DbToLinear(gainDb + MakeupDb);
    }

    private static float DbToLinear(float db) => MathF.Pow(10f, db / 20f);
}
