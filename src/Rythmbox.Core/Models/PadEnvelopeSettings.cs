namespace Rythmbox.Core.Models;

/// <summary>ADSR envelope applied to each triggered pad voice.</summary>
public sealed class PadEnvelopeSettings
{
    public float AttackMs { get; set; }

    public float DecayMs { get; set; }

    public float SustainLevel { get; set; } = 1f;

    public float ReleaseMs { get; set; }

    public bool IsDefault => AttackMs <= 0.001f
        && DecayMs <= 0.001f
        && SustainLevel >= 0.999f
        && ReleaseMs <= 0.001f;

    public PadEnvelopeSettings Clone() => new()
    {
        AttackMs = AttackMs,
        DecayMs = DecayMs,
        SustainLevel = SustainLevel,
        ReleaseMs = ReleaseMs,
    };
}
