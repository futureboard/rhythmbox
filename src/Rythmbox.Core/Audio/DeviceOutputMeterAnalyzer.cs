using Rythmbox.Core.Models.Mixer;
using SoundFlow.Abstracts;
using SoundFlow.Structs;

namespace Rythmbox.Core.Audio;

/// <summary>
/// Meter tap on SoundFlow's MasterMixer. SoundFlow invokes analyzers after a
/// component's volume and mute handling, so this reads the post-fader buffer
/// that is handed to the backend callback for normal playback.
/// </summary>
internal sealed class DeviceOutputMeterAnalyzer : AudioAnalyzer
{
    private readonly RealtimeLevelMeter _meter;
    private readonly AudioGraphTrace _trace;

    public DeviceOutputMeterAnalyzer(AudioFormat format, RealtimeLevelMeter meter, AudioGraphTrace trace)
        : base(format)
    {
        _meter = meter;
        _trace = trace;
        Name = "Rythmbox device-bound master meter";
    }

    protected override void Analyze(ReadOnlySpan<float> buffer, int channels)
    {
        var peak = 0f;
        var sumSquares = 0d;

        foreach (var sample in buffer)
        {
            var absolute = MathF.Abs(sample);
            peak = MathF.Max(peak, absolute);
            sumSquares += sample * sample;
        }

        var rms = buffer.IsEmpty ? 0f : (float)Math.Sqrt(sumSquares / buffer.Length);
        _meter.RecordBlock(peak, rms);
        _trace.RecordDeviceOutput(peak);
    }

    public MixerMeterState Poll() => _meter.Poll();
}
