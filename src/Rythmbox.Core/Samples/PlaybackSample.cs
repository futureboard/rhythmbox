namespace Rythmbox.Core.Samples;

/// <summary>
/// A mono sample that can be read directly by the audio engine. Implementations
/// must not allocate or perform synchronous disk I/O from <see cref="ReadFrame"/>.
/// </summary>
public interface IPlaybackSample : IDisposable
{
    int FrameCount { get; }

    int SampleRate { get; }

    float ReadFrame(int frameIndex);
}

/// <summary>Adapter for samples that are already owned by managed memory.</summary>
public sealed class InMemoryPlaybackSample : IPlaybackSample
{
    public InMemoryPlaybackSample(float[] buffer, int sampleRate)
    {
        Buffer = buffer;
        SampleRate = sampleRate > 0 ? sampleRate : WavCodec.TargetSampleRate;
    }

    public float[] Buffer { get; }

    public int FrameCount => Buffer.Length;

    public int SampleRate { get; }

    public float ReadFrame(int frameIndex) => (uint)frameIndex < (uint)Buffer.Length ? Buffer[frameIndex] : 0f;

    public void Dispose()
    {
        // The array is owned by the kit/editor model, not the playback state.
    }
}
