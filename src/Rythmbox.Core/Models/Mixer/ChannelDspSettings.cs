namespace Rythmbox.Core.Models.Mixer;

public enum MixerFxSlot
{
    Eq,
    Compressor,
    Delay,
    Reverb,
}

/// <summary>Per-channel insert FX parameters for the drum mixer.</summary>
public sealed class ChannelDspSettings
{
    /// <summary>Additional trim gain after the fader (linear).</summary>
    public float TrimGain { get; set; } = 1f;

    public bool EqEnabled { get; set; } = true;

    public float LowGainDb { get; set; }

    public float MidGainDb { get; set; }

    public float HighGainDb { get; set; }

    public float MidFrequencyHz { get; set; } = 1_200f;

    public bool CompressorEnabled { get; set; }

    public float CompressorThresholdDb { get; set; } = -18f;

    public float CompressorRatio { get; set; } = 3f;

    public float CompressorMakeupDb { get; set; }

    public bool DelayEnabled { get; set; } = true;

    public float DelayTimeMs { get; set; }

    public float DelayFeedback { get; set; } = 0.35f;

    public float DelayMix { get; set; }

    public bool ReverbEnabled { get; set; } = true;

    public float ReverbMix { get; set; }

    public float ReverbSize { get; set; } = 0.45f;

    public ChannelDspSettings Clone() => new()
    {
        TrimGain = TrimGain,
        EqEnabled = EqEnabled,
        LowGainDb = LowGainDb,
        MidGainDb = MidGainDb,
        HighGainDb = HighGainDb,
        MidFrequencyHz = MidFrequencyHz,
        CompressorEnabled = CompressorEnabled,
        CompressorThresholdDb = CompressorThresholdDb,
        CompressorRatio = CompressorRatio,
        CompressorMakeupDb = CompressorMakeupDb,
        DelayEnabled = DelayEnabled,
        DelayTimeMs = DelayTimeMs,
        DelayFeedback = DelayFeedback,
        DelayMix = DelayMix,
        ReverbEnabled = ReverbEnabled,
        ReverbMix = ReverbMix,
        ReverbSize = ReverbSize,
    };
}
