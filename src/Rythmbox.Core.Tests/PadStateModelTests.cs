using Rythmbox.Core.Models;
using Xunit;

namespace Rythmbox.Core.Tests;

public sealed class PadStateModelTests
{
    [Fact]
    public void Draft_tracks_dirty_state_before_commit()
    {
        var config = new PadConfig
        {
            PadId = 3,
            Label = "Snare",
            MidiNote = 38,
            OutputGroup = DrumMixGroup.Snare,
        };

        var draft = PadEditDraft.FromConfig(config);
        Assert.False(draft.DiffersFrom(config));

        draft.MidiNote = 40;
        Assert.True(draft.DiffersFrom(config));

        draft.ApplyTo(config);
        Assert.Equal(40, config.MidiNote);
        Assert.False(draft.DiffersFrom(config));
    }

    [Fact]
    public void Config_clone_preserves_persistent_pad_fields()
    {
        var config = new PadConfig
        {
            PadId = 1,
            Label = "Kick",
            MidiNote = 36,
            SampleReference = "Kick.wav",
            Gain = 0.8f,
            Pan = -0.2f,
            PitchSemitones = -1f,
            ChokeGroup = 2,
            ColorToken = "drum",
            OutputGroup = DrumMixGroup.Kick,
            IsEnabled = false,
            VelocityBehavior = "fixed",
            PlaybackMode = "gate",
        };

        var copy = config.Clone();

        Assert.NotSame(config, copy);
        Assert.Equal(config.PadId, copy.PadId);
        Assert.Equal(config.Label, copy.Label);
        Assert.Equal(config.MidiNote, copy.MidiNote);
        Assert.Equal(config.SampleReference, copy.SampleReference);
        Assert.Equal(config.OutputGroup, copy.OutputGroup);
        Assert.Equal(config.PlaybackMode, copy.PlaybackMode);
    }

    [Fact]
    public void Runtime_state_keeps_duplicate_note_holds_until_the_matching_note_off()
    {
        var state = new PadRuntimeState();
        var now = DateTimeOffset.UtcNow;

        state.RegisterNoteOn(42, now, 0.5f);
        state.RegisterNoteOn(42, now, 0.75f);
        state.RegisterNoteOff(42, now.AddMilliseconds(10));

        Assert.True(state.IsPlaying);
        Assert.Equal(1, state.NoteDownCount);
        Assert.Contains(42, state.HeldNotes);

        state.RegisterNoteOff(42, now.AddMilliseconds(20));

        Assert.False(state.IsPlaying);
        Assert.Empty(state.HeldNotes);
        Assert.Equal(0, state.NoteDownCount);
    }

    [Fact]
    public void Runtime_hit_flash_decays_from_time_not_a_dispatcher_timer()
    {
        var state = new PadRuntimeState();
        var now = DateTimeOffset.UtcNow;
        state.RegisterTrigger(now, 1f);

        state.UpdateHitFlash(now.AddMilliseconds(90), TimeSpan.FromMilliseconds(180));
        Assert.InRange(state.HitFlashPhase, 0.49f, 0.51f);

        state.UpdateHitFlash(now.AddMilliseconds(181), TimeSpan.FromMilliseconds(180));
        Assert.Equal(0f, state.HitFlashPhase);
    }

    [Fact]
    public void Draft_commit_preserves_multiple_assigned_notes()
    {
        var config = new PadConfig { AssignedNotes = [36, 42] };
        var draft = PadEditDraft.FromConfig(config);
        draft.AssignedNotes = [42, 49];

        draft.ApplyTo(config);

        Assert.Equal([42, 49], config.AssignedNotes);
    }
}
