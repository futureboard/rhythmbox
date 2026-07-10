using Rythmbox.Core.Models;

namespace Rythmbox.Core.Samples;

/// <summary>Runtime pad playback: velocity layers, round-robin, and optional single-sample fallback.</summary>
public sealed class PadPlaybackState : IDisposable
{
    private int[] _roundRobinCounters = [];
    private IPlaybackSample? _fallbackSample;
    private bool _disposed;

    /// <summary>Kept for editor/tests that inspect an in-memory fallback buffer.</summary>
    public float[] FallbackBuffer { get; private set; } = [];

    public IReadOnlyList<VelocityLayerPlayback> VelocityLayers { get; private set; } = [];

    public bool HasAudio =>
        _fallbackSample is { FrameCount: > 0 } || VelocityLayers.Any(static layer => layer.HasAudio);

    public static PadPlaybackState FromSample(DrumSample? sample)
    {
        var state = new PadPlaybackState();
        if (sample is null)
        {
            return state;
        }

        var layers = sample.VelocityLayers
            .Where(static layer => layer.HasSamples)
            .Select(CreateLayer)
            .Where(static layer => layer.HasAudio)
            .ToArray();

        if (layers.Length > 0)
        {
            state.VelocityLayers = layers;
            state._roundRobinCounters = new int[layers.Length];
        }

        // Edited/imported PCM takes precedence over the original mapped file.
        // The descriptor remains available for metadata/save-as until the edit
        // replaces it, but must never hide the user's newer buffer.
        if (sample.Samples.Length > 0)
        {
            state.FallbackBuffer = sample.Samples;
            state._fallbackSample = new InMemoryPlaybackSample(sample.Samples, sample.SampleRate);
        }
        else if (sample.MappedSample is { FrameCount: > 0 } mapped)
        {
            state._fallbackSample = mapped.CreatePlaybackSample();
        }

        return state;
    }

    /// <summary>Selects the source for the next voice without allocating on the audio/MIDI path.</summary>
    public IPlaybackSample? SelectSample(int midiVelocity)
    {
        if (_disposed)
        {
            return null;
        }

        midiVelocity = Math.Clamp(midiVelocity, 1, 127);

        for (var i = 0; i < VelocityLayers.Count; i++)
        {
            var layer = VelocityLayers[i];
            if (midiVelocity < layer.VelocityLow || midiVelocity > layer.VelocityHigh || !layer.HasAudio)
            {
                continue;
            }

            var index = _roundRobinCounters[i] % layer.Samples.Length;
            _roundRobinCounters[i] = (index + 1) % layer.Samples.Length;
            return layer.Samples[index];
        }

        return _fallbackSample is { FrameCount: > 0 } ? _fallbackSample : null;
    }

    /// <summary>
    /// Compatibility helper for callers that only support managed buffers. The
    /// real-time player uses <see cref="SelectSample"/> so mapped files stay mapped.
    /// </summary>
    public float[]? SelectBuffer(int midiVelocity) =>
        SelectSample(midiVelocity) is InMemoryPlaybackSample memory ? memory.Buffer : null;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _fallbackSample?.Dispose();
        _fallbackSample = null;
        foreach (var layer in VelocityLayers)
        {
            layer.Dispose();
        }

        VelocityLayers = [];
        FallbackBuffer = [];
        _roundRobinCounters = [];
    }

    private static VelocityLayerPlayback CreateLayer(VelocityLayer layer)
    {
        var count = Math.Max(layer.RoundRobinSamples.Count, layer.RoundRobinMappedSamples.Count);
        var sources = new List<IPlaybackSample>(count);
        var buffers = new List<float[]>(count);

        for (var i = 0; i < count; i++)
        {
            var mapped = i < layer.RoundRobinMappedSamples.Count ? layer.RoundRobinMappedSamples[i] : null;
            var buffer = i < layer.RoundRobinSamples.Count ? layer.RoundRobinSamples[i] : [];

            if (buffer.Length > 0)
            {
                sources.Add(new InMemoryPlaybackSample(buffer, WavCodec.TargetSampleRate));
                buffers.Add(buffer);
            }
            else if (mapped is { FrameCount: > 0 })
            {
                sources.Add(mapped.CreatePlaybackSample());
                buffers.Add([]);
            }
        }

        return new VelocityLayerPlayback
        {
            VelocityLow = layer.VelocityLow,
            VelocityHigh = layer.VelocityHigh,
            Samples = [.. sources],
            Buffers = [.. buffers],
        };
    }
}

public sealed class VelocityLayerPlayback : IDisposable
{
    public int VelocityLow { get; init; }

    public int VelocityHigh { get; init; }

    public IPlaybackSample[] Samples { get; init; } = [];

    /// <summary>Managed buffers only; mapped entries are represented by an empty buffer.</summary>
    public float[][] Buffers { get; init; } = [];

    public bool HasAudio => Samples.Any(static sample => sample.FrameCount > 0);

    public void Dispose()
    {
        foreach (var sample in Samples)
        {
            sample.Dispose();
        }
    }
}
