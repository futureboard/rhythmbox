namespace Rythmbox.Core.Models.Mixer;

public enum MixerChannelKind
{
    Master,
    Group,
    DrumVoice,
}

public enum DrumGroup
{
    Drum,
    Percussion,
    Cymbal,
    Other,
}

public sealed class MixerMeterState
{
    public double RmsLeft { get; set; }

    public double RmsRight { get; set; }

    public double PeakLeft { get; set; }

    public double PeakRight { get; set; }

    public bool IsClipping { get; set; }

    public bool HasSignalData { get; set; }

    public static MixerMeterState Disabled { get; } = new();

    public static MixerMeterState FromMono(double rms, double peak, bool clipping = false) =>
        new()
        {
            RmsLeft = rms,
            RmsRight = rms,
            PeakLeft = peak,
            PeakRight = peak,
            IsClipping = clipping,
            HasSignalData = true,
        };
}

public sealed class MixerChannel
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string ShortName { get; init; } = string.Empty;

    public MixerChannelKind Kind { get; init; }

    public DrumGroup Group { get; init; } = DrumGroup.Other;

    public double Gain { get; set; } = 1.0;

    public double Pan { get; set; }

    public bool IsMuted { get; set; }

    public bool IsSoloed { get; set; }

    public bool IsSelected { get; set; }

    public bool IsEnabled { get; set; } = true;

    public bool IsPanEnabled { get; set; }

    public bool IsSoloEnabled { get; set; } = true;

    public string RouteName { get; set; } = "Main";

    public MixerMeterState Meter { get; set; } = MixerMeterState.Disabled;
}
