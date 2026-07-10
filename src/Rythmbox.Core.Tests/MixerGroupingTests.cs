using Rythmbox.Core.Models;
using Xunit;

namespace Rythmbox.Core.Tests;

public sealed class MixerGroupingTests
{
    [Theory]
    [InlineData(35, DrumMixGroup.Kick)]
    [InlineData(38, DrumMixGroup.Snare)]
    [InlineData(46, DrumMixGroup.HiHat)]
    [InlineData(48, DrumMixGroup.Toms)]
    [InlineData(51, DrumMixGroup.Cymbals)]
    [InlineData(60, DrumMixGroup.Percussion)]
    public void Gm_notes_resolve_to_musical_mixer_groups(int note, DrumMixGroup expected) =>
        Assert.Equal(expected, GmPercussionMap.GetMixGroup(note));

    [Fact]
    public void Mixer_exposes_six_musical_groups_not_one_strip_per_note()
    {
        Assert.Equal(6, GmPercussionMap.MixGroups.Count);
        Assert.Contains(DrumMixGroup.HiHat, GmPercussionMap.MixGroups);
    }
}
