namespace Rythmbox.Core.Audio;

public sealed class AudioDeviceInfo
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public bool IsDefault { get; init; }

    public uint InputChannels { get; init; }

    public uint OutputChannels { get; init; }

    public IReadOnlyList<uint> SupportedSampleRates { get; init; } = [48_000];

    public IReadOnlyList<uint> SupportedBufferSizes { get; init; } = [256, 512, 1024];
}

public sealed class AudioDeviceConfig
{
    public string? DeviceId { get; init; }

    public uint SampleRate { get; init; } = 48_000;

    public uint BufferSize { get; init; } = 512;

    public uint OutputChannels { get; init; } = 2;

    public uint InputChannels { get; init; }

    public bool ExclusiveMode { get; init; }
}

public enum AudioBackendStatusKind
{
    NotInitialized,
    Available,
    NoDevice,
    Opening,
    Running,
    Stopped,
    Error,
}

public sealed class AudioBackendStatus
{
    public AudioBackendStatusKind Kind { get; init; } = AudioBackendStatusKind.NotInitialized;

    public string Message { get; init; } = string.Empty;

    public static AudioBackendStatus NotInitialized() => new() { Kind = AudioBackendStatusKind.NotInitialized };

    public static AudioBackendStatus Running(string message) => new() { Kind = AudioBackendStatusKind.Running, Message = message };

    public static AudioBackendStatus NoDevice() => new() { Kind = AudioBackendStatusKind.NoDevice, Message = "No output device" };

    public static AudioBackendStatus Error(string message) => new() { Kind = AudioBackendStatusKind.Error, Message = message };
}

public enum AudioErrorKind
{
    BackendUnavailable,
    DeviceNotFound,
    DeviceOpenFailed,
    StreamStartFailed,
    StreamStoppedUnexpectedly,
    UnsupportedConfig,
}

public sealed class AudioError : Exception
{
    public AudioError(AudioErrorKind kind, string message) : base(message) => Kind = kind;

    public AudioErrorKind Kind { get; }
}
