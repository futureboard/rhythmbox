namespace Rythmbox.Core.Audio;

/// <summary>Platform-agnostic audio output backend (WASAPI / ALSA / CoreAudio via MiniAudio).</summary>
public interface IAudioBackend
{
    string BackendName { get; }

    /// <summary>Platform-native backend id: WASAPI, ALSA, or CoreAudio.</summary>
    string PlatformBackendId { get; }

    bool IsAvailable { get; }

    IReadOnlyList<AudioDeviceInfo> EnumerateDevices();

    AudioBackendStatus CurrentStatus { get; }

    string? CurrentDeviceName { get; }

    void RefreshDevices();

    void OpenDevice(AudioDeviceConfig config);

    void CloseDevice();

    void StartStream();

    void StopStream();
}

public static class PlatformAudioBackend
{
    public static string PreferredBackendId =>
        OperatingSystem.IsWindows() ? "WASAPI" :
        OperatingSystem.IsMacOS() ? "CoreAudio" :
        OperatingSystem.IsLinux() ? "ALSA" :
        "Unknown";
}
