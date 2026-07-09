namespace Rythmbox.Core.Models.Styles;

/// <summary>
/// A musical time signature (e.g. 4/4, 2/4, 6/8). Immutable value type used by the arranger to
/// meter bars and to momentarily switch feel (drop a short 2/4 bar, then return to 4/4).
/// </summary>
public readonly record struct TimeSignature(int Numerator, int Denominator)
{
    public static readonly TimeSignature FourFour = new(4, 4);

    /// <summary>Number of beats counted per bar (the numerator).</summary>
    public int BeatsPerBar => Numerator;

    /// <summary>Length of one bar measured in quarter notes (4/4 = 4, 2/4 = 2, 6/8 = 3).</summary>
    public double QuarterNotesPerBar => Numerator * 4.0 / Denominator;

    public bool IsValid =>
        Numerator is > 0 and <= 32 &&
        Denominator is 1 or 2 or 4 or 8 or 16 or 32;

    public override string ToString() => $"{Numerator}/{Denominator}";

    /// <summary>Parses "n/d"; falls back to 4/4 when the text is missing or malformed.</summary>
    public static TimeSignature Parse(string? text) =>
        TryParse(text, out var signature) ? signature : FourFour;

    public static bool TryParse(string? text, out TimeSignature result)
    {
        result = FourFour;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Split('/');
        if (parts.Length != 2 ||
            !int.TryParse(parts[0].Trim(), out var numerator) ||
            !int.TryParse(parts[1].Trim(), out var denominator))
        {
            return false;
        }

        var candidate = new TimeSignature(numerator, denominator);
        if (!candidate.IsValid)
        {
            return false;
        }

        result = candidate;
        return true;
    }
}
