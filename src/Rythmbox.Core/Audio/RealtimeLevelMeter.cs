using System.Threading;
using Rythmbox.Core.Models.Mixer;

namespace Rythmbox.Core.Audio;

/// <summary>
/// A lock-free, peak-hold meter shared by the audio callback and UI thread.
/// Samples are written only by the audio path; UI polling merely reads and
/// decays the held value.
/// </summary>
internal sealed class RealtimeLevelMeter
{
    private const float SilenceThreshold = 0.0001f;
    private int _peakBits;
    private int _rmsBits;

    public void RecordBlock(float peak, float rms)
    {
        SetMaximum(ref _peakBits, Sanitize(peak));
        SetMaximum(ref _rmsBits, Sanitize(rms));
    }

    public MixerMeterState Poll()
    {
        var peak = Read(ref _peakBits);
        var rms = Read(ref _rmsBits);

        Decay(ref _peakBits, peak, 0.82f);
        Decay(ref _rmsBits, rms, 0.72f);

        return peak <= SilenceThreshold && rms <= SilenceThreshold
            ? MixerMeterState.Disabled
            : MixerMeterState.FromMono(rms, peak, peak >= 0.98f);
    }

    /// <summary>Reads the currently held sample-derived value without consuming its decay.</summary>
    public MixerMeterState Peek()
    {
        var peak = Read(ref _peakBits);
        var rms = Read(ref _rmsBits);
        return peak <= SilenceThreshold && rms <= SilenceThreshold
            ? MixerMeterState.Disabled
            : MixerMeterState.FromMono(rms, peak, peak >= 0.98f);
    }

    private static void SetMaximum(ref int targetBits, float value)
    {
        while (true)
        {
            var currentBits = Volatile.Read(ref targetBits);
            var current = BitConverter.Int32BitsToSingle(currentBits);
            if (value <= current)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref targetBits, BitConverter.SingleToInt32Bits(value), currentBits) == currentBits)
            {
                return;
            }
        }
    }

    private static void Decay(ref int targetBits, float observed, float multiplier)
    {
        if (observed <= SilenceThreshold)
        {
            return;
        }

        var decayedBits = BitConverter.SingleToInt32Bits(observed * multiplier);
        Interlocked.CompareExchange(ref targetBits, decayedBits, BitConverter.SingleToInt32Bits(observed));
    }

    private static float Read(ref int bits) => BitConverter.Int32BitsToSingle(Volatile.Read(ref bits));

    private static float Sanitize(float value) => float.IsFinite(value) ? MathF.Max(0f, value) : 0f;
}
