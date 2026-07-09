using Rythmbox.Core.Models;

namespace Rythmbox.Core.Samples;

/// <summary>Runtime pad playback: velocity layers, round-robin, and optional single-sample fallback.</summary>
public sealed class PadPlaybackState
{
    private int[] _roundRobinCounters = [];

    public float[] FallbackBuffer { get; private set; } = [];

    public IReadOnlyList<VelocityLayerPlayback> VelocityLayers { get; private set; } = [];

    public bool HasAudio =>
        FallbackBuffer.Length > 0 || VelocityLayers.Any(static layer => layer.Buffers.Length > 0);

    public static PadPlaybackState FromSample(DrumSample? sample)
    {
        var state = new PadPlaybackState();

        if (sample is null)
        {
            return state;
        }

        var layers = sample.VelocityLayers
            .Where(static layer => layer.HasSamples)
            .Select(layer => new VelocityLayerPlayback
            {
                VelocityLow = layer.VelocityLow,
                VelocityHigh = layer.VelocityHigh,
                Buffers = layer.RoundRobinSamples.ToArray(),
            })
            .ToArray();

        if (layers.Length > 0)
        {
            state.VelocityLayers = layers;
            state._roundRobinCounters = new int[layers.Length];
        }

        if (sample.HasAudio)
        {
            state.FallbackBuffer = sample.Samples;
        }

        return state;
    }

    public float[]? SelectBuffer(int midiVelocity)
    {
        midiVelocity = Math.Clamp(midiVelocity, 1, 127);

        for (var i = 0; i < VelocityLayers.Count; i++)
        {
            var layer = VelocityLayers[i];
            if (midiVelocity < layer.VelocityLow || midiVelocity > layer.VelocityHigh || layer.Buffers.Length == 0)
            {
                continue;
            }

            var index = _roundRobinCounters[i] % layer.Buffers.Length;
            _roundRobinCounters[i] = (index + 1) % layer.Buffers.Length;
            return layer.Buffers[index];
        }

        return FallbackBuffer.Length > 0 ? FallbackBuffer : null;
    }
}

public sealed class VelocityLayerPlayback
{
    public int VelocityLow { get; init; }

    public int VelocityHigh { get; init; }

    public float[][] Buffers { get; init; } = [];
}
