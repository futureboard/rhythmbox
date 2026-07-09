using Rythmbox.Core.Audio.Dsp;
using Xunit;

namespace Rythmbox.Core.Tests;

public sealed class ChannelDspChainTests
{
    [Fact]
    public void Eq_changes_sample_level()
    {
        var chain = new ChannelDspChain
        {
            Settings = new Models.Mixer.ChannelDspSettings
            {
                LowGainDb = 6f,
            },
        };

        var dry = 0.2f;
        var sample = dry;
        chain.Process(ref sample, 48_000f);

        Assert.NotEqual(dry, sample);
    }

    [Fact]
    public void Delay_processes_without_nan()
    {
        var chain = new ChannelDspChain
        {
            Settings = new Models.Mixer.ChannelDspSettings
            {
                DelayTimeMs = 20f,
                DelayFeedback = 0.2f,
                DelayMix = 1f,
            },
        };

        for (var i = 0; i < 2_000; i++)
        {
            var sample = i == 0 ? 1f : 0f;
            chain.Process(ref sample, 48_000f);
            Assert.False(float.IsNaN(sample));
        }
    }
}
