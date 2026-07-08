using Rythmbox.Core.Engine;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Structs;

namespace Rythmbox.Core.Audio;

/// <summary>MiniAudio adapter — maps to WASAPI (Windows), ALSA (Linux), CoreAudio (macOS).</summary>
public sealed class MiniAudioPlaybackBackend : IAudioBackend
{
    private readonly PlaybackEngine _engine;
    private AudioBackendStatus _status = AudioBackendStatus.NotInitialized();

    public MiniAudioPlaybackBackend(PlaybackEngine engine)
    {
        _engine = engine;
        _engine.DeviceChanged += (_, _) => RefreshStatusFromEngine();
        RefreshStatusFromEngine();
    }

    public string BackendName => "MiniAudio";

    public string PlatformBackendId => PlatformAudioBackend.PreferredBackendId;

    public bool IsAvailable => true;

    public AudioBackendStatus CurrentStatus => _status;

    public string? CurrentDeviceName => _engine.CurrentDevice?.Name;

    public IReadOnlyList<AudioDeviceInfo> EnumerateDevices() =>
        _engine.PlaybackDevices.Select(MapDevice).ToList();

    public void RefreshDevices() => _engine.RefreshDevices();

    public void OpenDevice(AudioDeviceConfig config)
    {
        _status = new AudioBackendStatus { Kind = AudioBackendStatusKind.Opening, Message = "Opening device…" };

        try
        {
            DeviceInfo? device = null;
            if (config.DeviceId is not null)
            {
                device = _engine.PlaybackDevices.FirstOrDefault(d => d.Name == config.DeviceId || d.Id.ToString() == config.DeviceId);
                if (device is null)
                {
                    throw new AudioError(AudioErrorKind.DeviceNotFound, $"Device not found: {config.DeviceId}");
                }
            }

            if (_engine.IsRunning && device is DeviceInfo selectedDevice)
            {
                _engine.SetOutputDevice(selectedDevice);
            }
            else
            {
                _engine.Start(device);
            }

            RefreshStatusFromEngine();
        }
        catch (AudioError)
        {
            throw;
        }
        catch (Exception ex)
        {
            _status = AudioBackendStatus.Error(ex.Message);
            throw new AudioError(AudioErrorKind.DeviceOpenFailed, ex.Message);
        }
    }

    public void CloseDevice()
    {
        StopStream();
        _status = new AudioBackendStatus { Kind = AudioBackendStatusKind.Stopped, Message = "Device closed" };
    }

    public void StartStream()
    {
        if (!_engine.IsRunning)
        {
            _engine.Start();
        }

        RefreshStatusFromEngine();
    }

    public void StopStream()
    {
        // PlaybackEngine owns lifecycle; mark stopped for UI.
        _status = new AudioBackendStatus { Kind = AudioBackendStatusKind.Stopped, Message = "Stopped" };
    }

    private void RefreshStatusFromEngine()
    {
        if (!_engine.IsRunning)
        {
            _status = _engine.PlaybackDevices.Count == 0
                ? AudioBackendStatus.NoDevice()
                : new AudioBackendStatus { Kind = AudioBackendStatusKind.Available, Message = "Ready" };
            return;
        }

        var name = _engine.CurrentDevice?.Name ?? "Default";
        _status = AudioBackendStatus.Running(name);
    }

    private static AudioDeviceInfo MapDevice(DeviceInfo device) => new()
    {
        Id = device.Id.ToString(),
        Name = device.Name,
        IsDefault = device.IsDefault,
        OutputChannels = 2,
    };
}
