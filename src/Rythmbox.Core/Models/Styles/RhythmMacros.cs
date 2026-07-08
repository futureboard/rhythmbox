namespace Rythmbox.Core.Models.Styles;

/// <summary>Real macro values for the selected style/session (0..1).</summary>
public sealed class RhythmMacros
{
    public float Complexity { get; set; }

    public float Energy { get; set; }

    public float Swing { get; set; }

    public float Humanize { get; set; }

    public RhythmMacros Clone() => new()
    {
        Complexity = Complexity,
        Energy = Energy,
        Swing = Swing,
        Humanize = Humanize,
    };

    public static RhythmMacros Default => new()
    {
        Complexity = 0.45f,
        Energy = 0.55f,
        Swing = 0f,
        Humanize = 0.12f,
    };
}
