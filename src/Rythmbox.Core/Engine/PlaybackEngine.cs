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

        var newDevice = _engine.SwitchDevice(_device, device, _device.Config);
        _device = newDevice;
        _device.Start();
        AttachMasterLevelMeter();
        DeviceChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RefreshDevices() => _engine.UpdateAudioDevicesInfo();

    private void AttachMasterLevelMeter()
    {
        MasterLevelMeter = new LevelMeterAnalyzer(Format, new NullVisualizer());
        MasterMixer.AddAnalyzer(MasterLevelMeter);
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
}
