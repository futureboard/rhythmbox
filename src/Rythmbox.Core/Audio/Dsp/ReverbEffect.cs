namespace Rythmbox.Core.Audio.Dsp;

/// <summary>Lightweight comb/allpass reverb for drum sends.</summary>
public sealed class ReverbEffect
{
    private readonly CombFilter[] _combs = Enumerable.Range(0, 4).Select(_ => new CombFilter()).ToArray();
    private readonly AllpassFilter[] _allpasses = Enumerable.Range(0, 2).Select(_ => new AllpassFilter()).ToArray();

    public float Mix { get; set; }

    public float Size { get; set; } = 0.45f;

    public float Process(float input)
    {
        if (Mix <= 0.0001f)
        {
            return 0f;
        }

        var room = 0.2f + Size * 0.75f;
        var outSample = 0f;
        foreach (var comb in _combs)
        {
            outSample += comb.Process(input, room);
        }

        foreach (var allpass in _allpasses)
        {
            outSample = allpass.Process(outSample);
        }

        return outSample * Mix;
    }

    public void Reset()
    {
        foreach (var comb in _combs)
        {
            comb.Reset();
        }

        foreach (var allpass in _allpasses)
        {
            allpass.Reset();
        }
    }

    private sealed class CombFilter
    {
        private float[] _buffer = new float[1557];
        private int _index;

        public float Process(float input, float feedback)
        {
            var output = _buffer[_index];
            _buffer[_index] = input + output * feedback;
            _index = (_index + 1) % _buffer.Length;
            return output;
        }

        public void Reset() => Array.Clear(_buffer);
    }

    private sealed class AllpassFilter
    {
        private float[] _buffer = new float[441];
        private int _index;

        public float Process(float input)
        {
            const float feedback = 0.5f;
            var buffered = _buffer[_index];
            var output = -input + buffered;
            _buffer[_index] = input + buffered * feedback;
            _index = (_index + 1) % _buffer.Length;
            return output;
        }

        public void Reset() => Array.Clear(_buffer);
    }
}
