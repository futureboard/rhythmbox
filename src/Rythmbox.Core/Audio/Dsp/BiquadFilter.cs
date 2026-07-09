namespace Rythmbox.Core.Audio.Dsp;

/// <summary>Single biquad filter (RBJ cookbook).</summary>
public sealed class BiquadFilter
{
    private double _b0 = 1;
    private double _b1;
    private double _b2;
    private double _a1;
    private double _a2;
    private double _z1;
    private double _z2;

    public void SetLowShelf(float sampleRate, float frequencyHz, float gainDb)
    {
        Configure(sampleRate, frequencyHz, gainDb, 0.707f, FilterKind.LowShelf);
    }

    public void SetPeaking(float sampleRate, float frequencyHz, float gainDb, float q = 1f)
    {
        Configure(sampleRate, frequencyHz, gainDb, q, FilterKind.Peaking);
    }

    public void SetHighShelf(float sampleRate, float frequencyHz, float gainDb)
    {
        Configure(sampleRate, frequencyHz, gainDb, 0.707f, FilterKind.HighShelf);
    }

    public float Process(float input)
    {
        var x = input;
        var y = _b0 * x + _z1;
        _z1 = _b1 * x - _a1 * y + _z2;
        _z2 = _b2 * x - _a2 * y;
        return (float)y;
    }

    public void Reset()
    {
        _z1 = 0;
        _z2 = 0;
    }

    private enum FilterKind
    {
        LowShelf,
        Peaking,
        HighShelf,
    }

    private void Configure(float sampleRate, float frequencyHz, float gainDb, float q, FilterKind kind)
    {
        var a = Math.Pow(10, gainDb / 40.0);
        var w0 = 2 * Math.PI * Math.Clamp(frequencyHz, 20f, sampleRate * 0.45f) / sampleRate;
        var cos = Math.Cos(w0);
        var sin = Math.Sin(w0);
        var alpha = sin / (2 * Math.Max(0.1, q));

        double b0, b1, b2, a0, a1, a2;
        switch (kind)
        {
            case FilterKind.LowShelf:
            {
                var amp = 2 * Math.Sqrt(a) * alpha;
                b0 = a * ((a + 1) - (a - 1) * cos + amp);
                b1 = 2 * a * ((a - 1) - (a + 1) * cos);
                b2 = a * ((a + 1) - (a - 1) * cos - amp);
                a0 = (a + 1) + (a - 1) * cos + amp;
                a1 = -2 * ((a - 1) + (a + 1) * cos);
                a2 = (a + 1) + (a - 1) * cos - amp;
                break;
            }
            case FilterKind.Peaking:
            {
                b0 = 1 + alpha * a;
                b1 = -2 * cos;
                b2 = 1 - alpha * a;
                a0 = 1 + alpha / a;
                a1 = -2 * cos;
                a2 = 1 - alpha / a;
                break;
            }
            default:
            {
                var amp = 2 * Math.Sqrt(a) * alpha;
                b0 = a * ((a + 1) + (a - 1) * cos + amp);
                b1 = -2 * a * ((a - 1) + (a + 1) * cos);
                b2 = a * ((a + 1) + (a - 1) * cos - amp);
                a0 = (a + 1) - (a - 1) * cos + amp;
                a1 = 2 * ((a - 1) - (a + 1) * cos);
                a2 = (a + 1) - (a - 1) * cos - amp;
                break;
            }
        }

        _b0 = b0 / a0;
        _b1 = b1 / a0;
        _b2 = b2 / a0;
        _a1 = a1 / a0;
        _a2 = a2 / a0;
    }
}
