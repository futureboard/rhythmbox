using Rythmbox.Core.Engine;
using Rythmbox.Core.Models;
using Xunit;

namespace Rythmbox.Core.Tests;

public sealed class PadMappingServiceTests
{
    [Fact]
    public void SetMidiNote_updates_lookup_and_unmaps_old_note()
    {
        var mapping = new PadMappingService();
        var padIndex = 3;
        var oldNote = mapping.GetMidiNote(padIndex);

        mapping.SetMidiNote(padIndex, 72);

        Assert.NotEqual(padIndex, mapping.FindPadByMidiNote(oldNote));
        Assert.Equal(padIndex, mapping.FindPadByMidiNote(72));
    }

    [Fact]
    public void SetMidiNote_raises_change_only_when_value_changes()
    {
        var mapping = new PadMappingService();
        var padIndex = 4;
        var seen = new List<(int PadIndex, int Note)>();
        mapping.MidiNoteChanged += (idx, note) => seen.Add((idx, note));

        mapping.SetMidiNote(padIndex, 60);
        mapping.SetMidiNote(padIndex, 60);

        var change = Assert.Single(seen);
        Assert.Equal((padIndex, 60), change);
    }

    [Fact]
    public void SetMidiNotes_indexes_every_note_for_one_pad()
    {
        var mapping = new PadMappingService();

        Assert.True(mapping.SetMidiNotes(2, [101, 100, 101]));

        Assert.Equal([100, 101], mapping.GetMidiNotes(2));
        Assert.Equal([2], mapping.GetPadIndicesForMidiNote(100));
        Assert.Equal([2], mapping.GetPadIndicesForMidiNote(101));
    }

    [Fact]
    public void AllowLayers_indexes_all_pads_for_a_shared_note()
    {
        var mapping = new PadMappingService();

        Assert.True(mapping.SetMidiNotes(1, [100]));
        Assert.True(mapping.SetMidiNotes(6, [100]));

        Assert.Equal([1, 6], mapping.GetPadIndicesForMidiNote(100));
    }

    [Fact]
    public void Exclusive_mode_rejects_conflicting_note_without_changing_index()
    {
        var mapping = new PadMappingService { ConflictMode = NoteConflictMode.Exclusive };
        Assert.True(mapping.SetMidiNotes(1, [100]));

        Assert.False(mapping.SetMidiNotes(6, [100]));
        Assert.Equal([1], mapping.GetPadIndicesForMidiNote(100));
    }

    [Fact]
    public void Warn_only_mode_keeps_layer_and_reports_the_conflict()
    {
        var mapping = new PadMappingService { ConflictMode = NoteConflictMode.WarnOnly };
        mapping.SetMidiNotes(1, [100]);
        var seen = new List<(int PadIndex, int Note, IReadOnlyList<int> Pads)>();
        mapping.MidiNoteConflictDetected += (pad, note, pads) => seen.Add((pad, note, pads));

        Assert.True(mapping.SetMidiNotes(6, [100]));

        var conflict = Assert.Single(seen);
        Assert.Equal(6, conflict.PadIndex);
        Assert.Equal(100, conflict.Note);
        Assert.Equal([1, 6], conflict.Pads);
    }
}
