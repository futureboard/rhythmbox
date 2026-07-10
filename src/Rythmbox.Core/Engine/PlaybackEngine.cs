using Rythmbox.Core.Audio;
using Rythmbox.Core.Models;
using Rythmbox.Core.Models.Mixer;
using SoundFlow.Abstracts;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Midi.PortMidi;
using SoundFlow.Midi.Routing;
using SoundFlow.Midi.Structs;
using SoundFlow.Structs;
using SoundFlow.Visualization;

namespace Rythmbox.Core.Engine;

/// <summary>
/// Owns the SoundFlow <see cref="MiniAudioEngine"/> and the currently active playback device.
/// This is the single audio/MIDI I/O root that every other engine service is built on top of.
/// </summary>
public sealed class PlaybackEngine : IDisposable
{
    private readonly MiniAudioEngine _engine;
    private AudioPlaybackDevice? _device;

    public PlaybackEngine()
    {
        _engine = new MiniAudioEngine();
        _engine.UsePortMidi();
    }

    /// <summary>Audio format used for the whole graph (48kHz stereo float, matching SoundFlow's own MIDI/SoundFont examples).</summary>
    public AudioFormat Format { get; } = AudioFormat.DvdHq;

    public AudioEngine RawEngine => _engine;

    public bool IsRunning => _device?.IsRunning ?? false;

    public AudioPlaybackDevice Device => _device ?? throw new InvalidOperationException("Call Start() before using the playback device.");

    public Mixer MasterMixer => Device.MasterMixer;

    public LevelMeterAnalyzer MasterLevelMeter { get; private set; } = null!;

    /// <summary>Post-master-fader meter for the exact normal-playback buffer bound for the device.</summary>
    public MixerMeterState PollMasterOutputMeter() => _deviceOutputMeter?.Poll() ?? MixerMeterState.Disabled;

    /// <summary>Development-only sample-path counters. Release builds leave these counters inert.</summary>
    public AudioGraphTrace AudioGraphTrace { get; } = new(GmPercussionMap.MixGroups.Select(group => group.ToString()));

    public AudioGraphTraceSnapshot GetAudioGraphTraceSnapshot()
    {
#if DEBUG
        var bypassReason = RawEngine.GetSoloedComponent() is { } component
            ? $"SoundFlow engine solo bypass: {component.Name}"
            : string.Empty;
        return AudioGraphTrace.Snapshot(bypassReason);
#else
        return AudioGraphTrace.Snapshot();
#endif
    }

    public IReadOnlyList<DeviceInfo> PlaybackDevices => _engine.PlaybackDevices;

    public DeviceInfo? CurrentDevice => _device?.Info;

    public MidiManager MidiManager => _engine.MidiManager;

    public IReadOnlyList<MidiDeviceInfo> MidiInputDevices => _engine.MidiManager.AvailableInputs;

    /// <summary>Raised whenever the underlying device (and therefore MasterMixer instance) changes.</summary>
    public event EventHandler? DeviceChanged;

    public void Start(DeviceInfo? device = null)
    {
        StopInternal();
        _device = _engine.InitializePlaybackDevice(device, Format);
        _device.Start();
        AttachMasterLevelMeter();
        DeviceChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetOutputDevice(DeviceInfo device)
    {
        if (_device is null)
        {
            Start(device);
            return;
        }

        // The switch creates a fresh MasterMixer, so carry the master strip state
        // (volume + mute) across to the new device to avoid a silent reset on switch.
        var masterVolume = MasterMixer.Volume;
        var masterMuted = MasterMixer.Mute;

        var newDevice = _engine.SwitchDevice(_device, device, _device.Config);
        _device = newDevice;
        _device.Start();
        AttachMasterLevelMeter();

        MasterMixer.Volume = masterVolume;
        MasterMixer.Mute = masterMuted;

        DeviceChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RefreshDevices() => _engine.UpdateAudioDevicesInfo();

    private void AttachMasterLevelMeter()
    {
        MasterLevelMeter = new LevelMeterAnalyzer(Format, new NullVisualizer());
        MasterMixer.AddAnalyzer(MasterLevelMeter);

        _deviceOutputMeter = new DeviceOutputMeterAnalyzer(Format, _masterOutputMeter, AudioGraphTrace);
        MasterMixer.AddAnalyzer(_deviceOutputMeter);
    }

    /// <summary>
    /// Development command requested by the audio truth audit. It formats a
    /// snapshot outside the audio callback and identifies SoundFlow's only
    /// device-level bypass: an explicit engine-solo component.
    /// </summary>
    public string rhythmbox_debug_audio_truth()
    {
#if DEBUG
        return AudioGraphTrace.FormatTruthTable("kit_sample_player", GetAudioGraphTraceSnapshot().LastAudioBypassReason);
#else
        return "Audio truth tracing is available in Debug builds only.";
#endif
    }

    private void StopInternal()
    {
        if (_device is null)
        {
            return;
        }

        _device.Stop();
        _device.Dispose();
        _device = null;
    }

    public void Dispose()
    {
        StopInternal();
        _engine.Dispose();
    }

    private readonly RealtimeLevelMeter _masterOutputMeter = new();
    private DeviceOutputMeterAnalyzer? _deviceOutputMeter;
}
