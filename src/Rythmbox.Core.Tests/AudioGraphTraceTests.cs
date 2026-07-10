using Rythmbox.Core.Audio;
using Xunit;

namespace Rythmbox.Core.Tests;

public sealed class AudioGraphTraceTests
{
    [Fact]
    public void Audible_device_output_has_a_verified_mixer_master_and_meter_path()
    {
        var trace = new AudioGraphTrace(["Kick"]);

        trace.RecordSource(padIndex: 0, midiNote: 36, peak: 0.82f);
        trace.RecordMixerInput(0.82f);
        trace.RecordMixerOutput(0.61f);
        trace.RecordMasterInput(0.60f);
        trace.RecordMasterOutput(0.60f);
        trace.RecordChannelPeak(channelIndex: 0, peak: 0.61f);
        trace.RecordMeterTap(0.60f);
        trace.RecordDeviceOutput(0.60f);

        var snapshot = trace.Snapshot();

        Assert.True(snapshot.DeviceOutputNonzeroBlocks > 0);
        Assert.True(snapshot.MixerOutputNonzeroBlocks > 0);
        Assert.True(snapshot.MasterInputNonzeroBlocks > 0);
        Assert.True(snapshot.MeterNonzeroCount > 0);
        Assert.Equal(0, snapshot.LastSourcePadIndex);
        Assert.Equal(36, snapshot.LastSourceMidiNote);
        Assert.InRange(snapshot.LastPeakPerChannel["Kick"], 0.60f, 0.62f);
        Assert.Contains("source_id | pad_id | note | source_peak", trace.FormatTruthTable("kit_sample_player"));
    }

    [Fact]
    public void Silent_blocks_do_not_claim_nonzero_audio()
    {
        var trace = new AudioGraphTrace(["Kick"]);

        trace.RecordSource(padIndex: -1, midiNote: -1, peak: 0f);
        trace.RecordMixerInput(0f);
        trace.RecordMixerOutput(0f);
        trace.RecordMasterInput(0f);
        trace.RecordMasterOutput(0f);
        trace.RecordDeviceOutput(0f);

        var snapshot = trace.Snapshot();

        Assert.Equal(0, snapshot.SourceNonzeroBlocks);
        Assert.Equal(0, snapshot.MixerInputNonzeroBlocks);
        Assert.Equal(0, snapshot.MixerOutputNonzeroBlocks);
        Assert.Equal(0, snapshot.MasterInputNonzeroBlocks);
        Assert.Equal(0, snapshot.MasterOutputNonzeroBlocks);
        Assert.Equal(0, snapshot.DeviceOutputNonzeroBlocks);
        Assert.Equal(0, snapshot.MeterNonzeroCount);
    }
}
