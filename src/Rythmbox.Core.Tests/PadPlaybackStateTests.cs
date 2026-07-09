using Rythmbox.Core.Models;
using Rythmbox.Core.Samples;
using Xunit;

namespace Rythmbox.Core.Tests;

public sealed class PadPlaybackStateTests
{
    [Fact]
    public void SelectBuffer_picks_velocity_layer_and_cycles_round_robin()
    {
        var sample = new DrumSample
        {
            Label = "Kick",
            MidiNote = 36,
            VelocityLayers =
            [
                new VelocityLayer
                {
                    VelocityLow = 1,
                    VelocityHigh = 60,
                    RoundRobinSamples = [CreateTone(0.1f), CreateTone(0.2f)],
                },
                new VelocityLayer
                {
                    VelocityLow = 61,
                    VelocityHigh = 127,
                    RoundRobinSamples = [CreateTone(0.9f)],
                },
            ],
        };

        var state = PadPlaybackState.FromSample(sample, "Kick");

        var softA = state.SelectBuffer(20);
        var softB = state.SelectBuffer(20);
        var hard = state.SelectBuffer(100);

        Assert.NotNull(softA);
        Assert.NotNull(softB);
        Assert.NotSame(softA, softB);
        Assert.Equal(0.1f, softA![0], 3);
        Assert.Equal(0.2f, softB![0], 3);
        Assert.Equal(0.9f, hard![0], 3);
    }

    [Fact]
    public void SelectBuffer_falls_back_to_single_sample_when_no_layer_matches()
    {
        var sample = new DrumSample
        {
            Label = "Tamb",
            Samples = CreateTone(0.5f),
            VelocityLayers =
            [
                new VelocityLayer
                {
                    VelocityLow = 1,
                    VelocityHigh = 40,
                    RoundRobinSamples = [CreateTone(0.1f)],
                },
            ],
        };

        var state = PadPlaybackState.FromSample(sample, "Tamb");
        var buffer = state.SelectBuffer(100);

        Assert.NotNull(buffer);
        Assert.Equal(0.5f, buffer![0], 3);
    }

    private static float[] CreateTone(float value) => [value, value, value];
}
