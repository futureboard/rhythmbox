using System.Diagnostics;
using System.Text;
using System.Threading;

namespace Rythmbox.Core.Audio;

/// <summary>
/// Lock-free development counters for proving the sample path from kit source
/// to the device-bound master mixer buffer. Calls from the audio callback only
/// update atomics; formatting and bypass inspection happen on the caller thread.
/// </summary>
public sealed class AudioGraphTrace
{
    private const float NonZeroThreshold = 0.000001f;
    private readonly string[] _channelIds;
    private readonly int[] _channelPeakBits;

    private long _sourceBlocksRendered;
    private long _sourceNonzeroBlocks;
    private long _mixerInputBlocks;
    private long _mixerInputNonzeroBlocks;
    private long _mixerOutputBlocks;
    private long _mixerOutputNonzeroBlocks;
    private long _masterInputBlocks;
    private long _masterInputNonzeroBlocks;
    private long _masterOutputBlocks;
    private long _masterOutputNonzeroBlocks;
    private long _deviceOutputBlocks;
    private long _deviceOutputNonzeroBlocks;
    private long _meterUpdateCount;
    private long _meterNonzeroCount;
    private int _lastPeakSourceBits;
    private int _lastPeakMixerInputBits;
    private int _lastPeakMixerOutputBits;
    private int _lastPeakMasterInputBits;
    private int _lastPeakMasterBits;
    private int _lastPeakDeviceBits;
    private int _lastPeakMeterBits;
    private int _lastSourcePadIndex = -1;
    private int _lastSourceMidiNote = -1;

    public AudioGraphTrace(IEnumerable<string> channelIds)
    {
        _channelIds = channelIds.ToArray();
        _channelPeakBits = new int[_channelIds.Length];
    }

    [Conditional("DEBUG")]
    public void RecordSource(int padIndex, int midiNote, float peak)
    {
        RecordStage(ref _sourceBlocksRendered, ref _sourceNonzeroBlocks, peak);
        SetLastPeak(ref _lastPeakSourceBits, peak);
        if (IsNonZero(peak) && padIndex >= 0)
        {
            Volatile.Write(ref _lastSourcePadIndex, padIndex);
            Volatile.Write(ref _lastSourceMidiNote, midiNote);
        }
    }

    [Conditional("DEBUG")]
    public void RecordMixerInput(float peak)
    {
        RecordStage(ref _mixerInputBlocks, ref _mixerInputNonzeroBlocks, peak);
        SetLastPeak(ref _lastPeakMixerInputBits, peak);
    }

    [Conditional("DEBUG")]
    public void RecordMixerOutput(float peak)
    {
        RecordStage(ref _mixerOutputBlocks, ref _mixerOutputNonzeroBlocks, peak);
        SetLastPeak(ref _lastPeakMixerOutputBits, peak);
    }

    [Conditional("DEBUG")]
    public void RecordMasterInput(float peak)
    {
        RecordStage(ref _masterInputBlocks, ref _masterInputNonzeroBlocks, peak);
        SetLastPeak(ref _lastPeakMasterInputBits, peak);
    }

    [Conditional("DEBUG")]
    public void RecordMasterOutput(float peak)
    {
        RecordStage(ref _masterOutputBlocks, ref _masterOutputNonzeroBlocks, peak);
        SetLastPeak(ref _lastPeakMasterBits, peak);
    }

    /// <summary>
    /// Records the exact post-master-fader buffer handed from SoundFlow's master
    /// mixer to the backend callback, unless SoundFlow's explicit engine-solo
    /// mode bypasses that mixer.
    /// </summary>
    [Conditional("DEBUG")]
    public void RecordDeviceOutput(float peak)
    {
        RecordStage(ref _deviceOutputBlocks, ref _deviceOutputNonzeroBlocks, peak);
        SetLastPeak(ref _lastPeakDeviceBits, peak);
        RecordMeterTap(peak);
    }

    [Conditional("DEBUG")]
    public void RecordMeterTap(float peak)
    {
        Interlocked.Increment(ref _meterUpdateCount);
        SetLastPeak(ref _lastPeakMeterBits, peak);
        if (IsNonZero(peak))
        {
            Interlocked.Increment(ref _meterNonzeroCount);
        }
    }

    [Conditional("DEBUG")]
    public void RecordChannelPeak(int channelIndex, float peak)
    {
        if ((uint)channelIndex >= (uint)_channelPeakBits.Length)
        {
            return;
        }

        SetLastPeak(ref _channelPeakBits[channelIndex], peak);
    }

    public AudioGraphTraceSnapshot Snapshot(string? bypassReason = null)
    {
        var peaks = new Dictionary<string, float>(_channelIds.Length, StringComparer.Ordinal);
        for (var i = 0; i < _channelIds.Length; i++)
        {
            peaks[_channelIds[i]] = Read(_channelPeakBits[i]);
        }

        return new AudioGraphTraceSnapshot(
            SourceBlocksRendered: Interlocked.Read(ref _sourceBlocksRendered),
            SourceNonzeroBlocks: Interlocked.Read(ref _sourceNonzeroBlocks),
            MixerInputBlocks: Interlocked.Read(ref _mixerInputBlocks),
            MixerInputNonzeroBlocks: Interlocked.Read(ref _mixerInputNonzeroBlocks),
            MixerOutputBlocks: Interlocked.Read(ref _mixerOutputBlocks),
            MixerOutputNonzeroBlocks: Interlocked.Read(ref _mixerOutputNonzeroBlocks),
            MasterInputBlocks: Interlocked.Read(ref _masterInputBlocks),
            MasterInputNonzeroBlocks: Interlocked.Read(ref _masterInputNonzeroBlocks),
            MasterOutputBlocks: Interlocked.Read(ref _masterOutputBlocks),
            MasterOutputNonzeroBlocks: Interlocked.Read(ref _masterOutputNonzeroBlocks),
            DeviceOutputBlocks: Interlocked.Read(ref _deviceOutputBlocks),
            DeviceOutputNonzeroBlocks: Interlocked.Read(ref _deviceOutputNonzeroBlocks),
            MeterUpdateCount: Interlocked.Read(ref _meterUpdateCount),
            MeterNonzeroCount: Interlocked.Read(ref _meterNonzeroCount),
            LastPeakPerChannel: peaks,
            LastPeakSource: Read(_lastPeakSourceBits),
            LastPeakMixerInput: Read(_lastPeakMixerInputBits),
            LastPeakMixerOutput: Read(_lastPeakMixerOutputBits),
            LastPeakMasterInput: Read(_lastPeakMasterInputBits),
            LastPeakMaster: Read(_lastPeakMasterBits),
            LastPeakDevice: Read(_lastPeakDeviceBits),
            LastPeakMeter: Read(_lastPeakMeterBits),
            LastSourcePadIndex: Volatile.Read(ref _lastSourcePadIndex),
            LastSourceMidiNote: Volatile.Read(ref _lastSourceMidiNote),
            LastAudioBypassReason: bypassReason ?? string.Empty);
    }

    public string FormatTruthTable(string? sourceId, string? bypassReason = null)
    {
        var snapshot = Snapshot(bypassReason);
        var rows = new StringBuilder();
        rows.AppendLine("source_id | pad_id | note | source_peak | mixer_channel | mixer_in_peak | mixer_out_peak | master_peak | device_peak | meter_peak | route_status | bypass");

        var routeStatus = snapshot.DeviceOutputNonzeroBlocks > 0 && snapshot.MasterOutputNonzeroBlocks > 0
            ? "routed"
            : "no_verified_audio";
        var bypass = string.IsNullOrEmpty(snapshot.LastAudioBypassReason) ? "no" : snapshot.LastAudioBypassReason;
        var meterPeak = snapshot.MeterNonzeroCount > 0 ? snapshot.LastPeakMeter : 0f;

        var padId = snapshot.LastSourcePadIndex >= 0 ? $"pad_{snapshot.LastSourcePadIndex + 1:00}" : "-";
        var note = snapshot.LastSourceMidiNote >= 0 ? snapshot.LastSourceMidiNote.ToString() : "-";

        rows.Append(sourceId ?? "kit")
            .Append(" | ").Append(padId)
            .Append(" | ").Append(note)
            .Append(" | ").Append(snapshot.LastPeakSource.ToString("0.000"))
            .Append(" | ").Append("mixed")
            .Append(" | ").Append(snapshot.MixerInputNonzeroBlocks > 0 ? snapshot.LastPeakMixerInput.ToString("0.000") : "0.000")
            .Append(" | ").Append(snapshot.MixerOutputNonzeroBlocks > 0 ? snapshot.LastPeakMixerOutput.ToString("0.000") : "0.000")
            .Append(" | ").Append(snapshot.LastPeakMaster.ToString("0.000"))
            .Append(" | ").Append(snapshot.DeviceOutputNonzeroBlocks > 0 ? snapshot.LastPeakDevice.ToString("0.000") : "0.000")
            .Append(" | ").Append(meterPeak.ToString("0.000"))
            .Append(" | ").Append(routeStatus)
            .Append(" | ").Append(bypass)
            .AppendLine();

        return rows.ToString();
    }

    private static void RecordStage(ref long blocks, ref long nonzeroBlocks, float peak)
    {
        Interlocked.Increment(ref blocks);
        if (IsNonZero(peak))
        {
            Interlocked.Increment(ref nonzeroBlocks);
        }
    }

    private static bool IsNonZero(float peak) => float.IsFinite(peak) && MathF.Abs(peak) > NonZeroThreshold;

    private static void SetLastPeak(ref int targetBits, float candidate)
    {
        candidate = IsNonZero(candidate) ? MathF.Abs(candidate) : 0f;
        Volatile.Write(ref targetBits, BitConverter.SingleToInt32Bits(candidate));
    }

    private static float Read(int bits) => BitConverter.Int32BitsToSingle(Volatile.Read(ref bits));
}

public sealed record AudioGraphTraceSnapshot(
    long SourceBlocksRendered,
    long SourceNonzeroBlocks,
    long MixerInputBlocks,
    long MixerInputNonzeroBlocks,
    long MixerOutputBlocks,
    long MixerOutputNonzeroBlocks,
    long MasterInputBlocks,
    long MasterInputNonzeroBlocks,
    long MasterOutputBlocks,
    long MasterOutputNonzeroBlocks,
    long DeviceOutputBlocks,
    long DeviceOutputNonzeroBlocks,
    long MeterUpdateCount,
    long MeterNonzeroCount,
    IReadOnlyDictionary<string, float> LastPeakPerChannel,
    float LastPeakSource,
    float LastPeakMixerInput,
    float LastPeakMixerOutput,
    float LastPeakMasterInput,
    float LastPeakMaster,
    float LastPeakDevice,
    float LastPeakMeter,
    int LastSourcePadIndex,
    int LastSourceMidiNote,
    string LastAudioBypassReason);
