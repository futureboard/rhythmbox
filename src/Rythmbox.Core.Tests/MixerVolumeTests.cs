using Rythmbox.Core.Audio;
using Xunit;

namespace Rythmbox.Core.Tests;

public sealed class MixerVolumeTests
{
    [Fact]
    public void DbToNorm_Unity_IsExpected()
    {
        var norm = MixerVolume.DbToNorm(0.0);
        Assert.InRange(norm, 0.90, 0.92);
    }

    [Fact]
    public void NormToDb_RoundTrip_IsStable()
    {
        const double norm = 0.75;
        var db = MixerVolume.NormToDb(norm);
        Assert.InRange(MixerVolume.DbToNorm(db), norm - 0.001, norm + 0.001);
    }

    [Fact]
    public void LinearToNorm_Unity_IsZeroDbNorm()
    {
        Assert.InRange(MixerVolume.LinearToNorm(1.0), MixerVolume.UnityNorm - 0.001, MixerVolume.UnityNorm + 0.001);
    }

    [Fact]
    public void PointerMapping_TopIsMaxBottomIsMin()
    {
        var h = 210.0;
        var pad = 15.0;
        Assert.InRange(MixerVolume.PointerYToNorm(pad, 0, h, pad), 0.99, 1.01);
        Assert.InRange(MixerVolume.PointerYToNorm(h - pad, 0, h, pad), -0.01, 0.01);
    }

    [Fact]
    public void FormatDb_Silence_IsInfinitySymbol()
    {
        Assert.Equal("-∞", MixerVolume.FormatDb(0));
    }

    [Fact]
    public void NudgeDb_MovesByRequestedDecibels()
    {
        var start = MixerVolume.DbToNorm(-12.0);
        var nudged = MixerVolume.NudgeDb(start, 3.0);
        Assert.InRange(MixerVolume.NormToDb(nudged), -9.0 - 0.001, -9.0 + 0.001);
    }

    [Fact]
    public void NudgeDb_ClampsAtRangeEnds()
    {
        Assert.Equal(1.0, MixerVolume.NudgeDb(1.0, 12.0));
        Assert.Equal(0.0, MixerVolume.NudgeDb(0.0, -12.0));
    }
}
