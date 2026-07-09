namespace Rythmbox.Core.Samples;

/// <summary>High-quality mono resampling via Cockos WDL (NAudio output-driven pattern).</summary>
internal static class WavResampler
{
    public static float[] Resample(ReadOnlySpan<float> input, int fromRate, int toRate)
    {
        if (fromRate <= 0 || toRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fromRate), "Sample rate must be positive.");
        }

        if (fromRate == toRate)
        {
            return input.ToArray();
        }

        if (input.Length == 0)
        {
            return [];
        }

        var resampler = new WdlResampler();
        resampler.SetMode(interp: true, filtercnt: 2, sinc: false);
        resampler.SetFilterParms();
        resampler.SetFeedMode(wantInputDriven: false);
        resampler.SetRates(fromRate, toRate);

        var inputArray = input.ToArray();
        var inOffset = 0;
        var estimated = (int)((long)inputArray.Length * toRate / fromRate) + 128;
        var output = new List<float>(estimated);

        while (true)
        {
            const int framesRequested = 1024;
            var needed = resampler.ResamplePrepare(framesRequested, 1, out var inBuffer, out var inBufferOffset);

            var inAvailable = 0;
            if (inOffset < inputArray.Length)
            {
                inAvailable = Math.Min(needed, inputArray.Length - inOffset);
                Array.Copy(inputArray, inOffset, inBuffer, inBufferOffset, inAvailable);
                inOffset += inAvailable;
            }

            var outBuffer = new float[framesRequested];
            var produced = resampler.ResampleOut(outBuffer, 0, inAvailable, framesRequested, 1);
            if (produced <= 0)
            {
                break;
            }

            for (var i = 0; i < produced; i++)
            {
                output.Add(outBuffer[i]);
            }

            if (inOffset >= inputArray.Length && produced < framesRequested)
            {
                break;
            }
        }

        return output.Count == 0 ? new float[1] : output.ToArray();
    }
}
