namespace Rythmbox.Core.Audio.Dsp;

/// <summary>Stereo-compatible mono delay with feedback.</summary>
public sealed class DelayEffect
{
    private float[] _buffer = [];
    private int _writeIndex;

    public float TimeMs { get; set; }

    public float Feedback { get; set; } = 0.35f;

    public float Mix { get; set; }

    public float Process(float input, float sampleRate)
    {
        if (Mix <= 0.0001f || TimeMs <= 0.1f)
        {
            return input;
        }

        var delaySamples = (int)(sampleRate * TimeMs / 1000f);
        if (delaySamples < 1)
        {
            return input;
        }

        EnsureBuffer(delaySamples + 1);
        var readIndex = (_writeIndex - delaySamples + _buffer.Length) % _buffer.Length;
        var delayed = _buffer[readIndex];
        _buffer[_writeIndex] = input + delayed * Math.Clamp(Feedback, 0f, 0.95f);
        _writeIndex = (_writeIndex + 1) % _buffer.Length;

        return input * (1f - Mix) + delayed * Mix;
    }

    public void Reset()
    {
        Array.Clear(_buffer);
        _writeIndex = 0;
    }

    private void EnsureBuffer(int size)
    {
        if (_buffer.Length >= size)
        {
            return;
        }

        _buffer = new float[Math.Max(size, 48_000 * 2)];
        _writeIndex = 0;
    }
}
