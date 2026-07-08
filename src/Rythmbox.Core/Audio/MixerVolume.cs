namespace Rythmbox.Core.Audio;

/// <summary>
/// Normalized mixer fader mapping ported from Futureboard SphereUIComponents
/// <c>timeline::state::volume</c> (-60 dB … +6 dB linear-in-dB).
/// </summary>
public static class MixerVolume
{
    public const double MinDb = -60.0;
    public const double MaxDb = 6.0;

    public static readonly double UnityNorm = DbToNorm(0.0);

    public static readonly (double Db, string Label)[] ScaleMarks =
    [
        (MaxDb, "+6"),
        (0.0, "0"),
        (-6.0, "6"),
        (-12.0, "12"),
        (-24.0, "24"),
        (-36.0, "36"),
        (-48.0, "48"),
        (MinDb, "∞"),
    ];

    public static double NormToDb(double norm)
    {
        var n = Math.Clamp(norm, 0, 1);
        return MinDb + n * (MaxDb - MinDb);
    }

    public static double DbToNorm(double db) =>
        Math.Clamp((db - MinDb) / (MaxDb - MinDb), 0, 1);

    public static string FormatDb(double norm)
    {
        var db = NormToDb(norm);
        if (norm <= 0.001 || db <= MinDb + 0.05)
        {
            return "-∞";
        }

        return db >= 0 ? $"+{db:0.0}" : $"{db:0.0}";
    }

    public static string FormatDbWithUnit(double norm) => $"{FormatDb(norm)} dB";

    /// <summary>Top-of-rail fraction for a dB tick (0 = top, 1 = bottom).</summary>
    public static double DbToTopFraction(double db) => 1.0 - DbToNorm(db);

    public static double LinearToNorm(double linear)
    {
        if (linear <= 0.000001)
        {
            return 0;
        }

        var db = 20.0 * Math.Log10(linear);
        return DbToNorm(db);
    }

    public static double NormToLinear(double norm)
    {
        if (norm <= 0.001)
        {
            return 0;
        }

        var db = NormToDb(norm);
        return Math.Pow(10, db / 20.0);
    }

    public static double PointerYToNorm(double pointerY, double boundsY, double boundsHeight, double thumbRadius = 11)
    {
        var railTop = thumbRadius;
        var railHeight = Math.Max(1, boundsHeight - thumbRadius * 2);
        var railY = Math.Clamp(pointerY - boundsY - railTop, 0, railHeight);
        return 1.0 - railY / railHeight;
    }
}
