namespace Rythmbox.Core.Samples;

using System.Buffers.Binary;
using System.Text;

/// <summary>Loads and saves mono PCM WAV files for drum kit samples (compatible with old DrumStage SDL_LoadWAV kits).</summary>
public static class WavCodec
{
    public const int TargetSampleRate = 48_000;

    public static float[] LoadMono(string path, int targetSampleRate = TargetSampleRate)
    {
        return DecodeMono(File.ReadAllBytes(path), out _, targetSampleRate);
    }

    public static float[] DecodeMono(ReadOnlySpan<byte> wavBytes, out int sampleRate, int targetSampleRate = TargetSampleRate)
    {
        if (wavBytes.Length < 44
            || Encoding.ASCII.GetString(wavBytes.Slice(0, 4)) != "RIFF"
            || Encoding.ASCII.GetString(wavBytes.Slice(8, 4)) != "WAVE")
        {
            throw new InvalidDataException("Not a RIFF/WAVE file.");
        }

        int channels = 1;
        sampleRate = TargetSampleRate;
        short bitsPerSample = 16;
        short blockAlign = 2;
        byte[]? data = null;

        var offset = 12;
        while (offset + 8 <= wavBytes.Length)
        {
            var chunkId = Encoding.ASCII.GetString(wavBytes.Slice(offset, 4));
            var chunkSize = BinaryPrimitives.ReadInt32LittleEndian(wavBytes.Slice(offset + 4, 4));
            offset += 8;

            if (chunkSize < 0 || offset + chunkSize > wavBytes.Length)
            {
                break;
            }

            switch (chunkId)
            {
                case "fmt ":
                    channels = BinaryPrimitives.ReadInt16LittleEndian(wavBytes.Slice(offset + 2, 2));
                    sampleRate = BinaryPrimitives.ReadInt32LittleEndian(wavBytes.Slice(offset + 4, 4));
                    blockAlign = BinaryPrimitives.ReadInt16LittleEndian(wavBytes.Slice(offset + 12, 2));
                    bitsPerSample = BinaryPrimitives.ReadInt16LittleEndian(wavBytes.Slice(offset + 14, 2));
                    offset += chunkSize;
                    break;

                case "data":
                    data = wavBytes.Slice(offset, chunkSize).ToArray();
                    offset += chunkSize;
                    break;

                default:
                    offset += chunkSize;
                    break;
            }

            if (data is not null)
            {
                break;
            }

            if ((chunkSize & 1) != 0)
            {
                offset++;
            }
        }

        if (data is null)
        {
            sampleRate = targetSampleRate;
            return [];
        }

        var mono = DecodeToMono(data, channels, bitsPerSample, blockAlign);
        return sampleRate == targetSampleRate
            ? mono
            : Resample(mono, sampleRate, targetSampleRate);
    }

    public static byte[] EncodeMono(ReadOnlySpan<float> samples, int sampleRate = TargetSampleRate)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        var pcm = new byte[samples.Length * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            var clamped = Math.Clamp(samples[i], -1f, 1f);
            var sample16 = (short)(clamped * short.MaxValue);
            pcm[i * 2] = (byte)(sample16 & 0xFF);
            pcm[i * 2 + 1] = (byte)((sample16 >> 8) & 0xFF);
        }

        const short channels = 1;
        const short bitsPerSample = 16;
        var byteRate = sampleRate * channels * bitsPerSample / 8;
        var blockAlign = channels * bitsPerSample / 8;

        writer.Write("RIFF"u8);
        writer.Write(36 + pcm.Length);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write((short)blockAlign);
        writer.Write(bitsPerSample);
        writer.Write("data"u8);
        writer.Write(pcm.Length);
        writer.Write(pcm);

        return stream.ToArray();
    }

    public static void SaveMono(string path, ReadOnlySpan<float> samples, int sampleRate = TargetSampleRate)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        File.WriteAllBytes(path, EncodeMono(samples, sampleRate));
    }

    private static float[] DecodeToMono(byte[] data, int channels, short bitsPerSample, short blockAlign)
    {
        if (channels <= 0)
        {
            throw new InvalidDataException("WAV channel count must be positive.");
        }

        if (blockAlign <= 0)
        {
            blockAlign = (short)(channels * ((bitsPerSample + 7) / 8));
        }

        if (data.Length < blockAlign)
        {
            return [];
        }

        var frameCount = data.Length / blockAlign;
        var bytesPerSample = blockAlign / channels;
        var mono = new float[frameCount];

        for (var i = 0; i < frameCount; i++)
        {
            var frameOffset = i * blockAlign;
            float sum = 0;

            for (var ch = 0; ch < channels; ch++)
            {
                var sampleOffset = frameOffset + (ch * bytesPerSample);
                sum += ReadPcmSample(data, sampleOffset, bitsPerSample, bytesPerSample);
            }

            mono[i] = sum / channels;
        }

        return mono;
    }

    private static float ReadPcmSample(byte[] data, int offset, short bitsPerSample, int bytesPerSample)
    {
        return bitsPerSample switch
        {
            8 => (data[offset] - 128) / 128f,
            16 => BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset, 2)) / (float)short.MaxValue,
            24 => ReadInt24LittleEndian(data.AsSpan(offset, Math.Min(3, bytesPerSample))) / 8_388_608f,
            32 => BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, Math.Min(4, bytesPerSample))) / (float)int.MaxValue,
            _ => throw new NotSupportedException($"Only 8/16/24/32-bit PCM WAV is supported (got {bitsPerSample}-bit)."),
        };
    }

    private static int ReadInt24LittleEndian(ReadOnlySpan<byte> data)
    {
        var value = data[0] | (data[1] << 8) | (data[2] << 16);
        if ((value & 0x800000) != 0)
        {
            value |= unchecked((int)0xFF000000);
        }

        return value;
    }

    private static float[] Resample(float[] input, int fromRate, int toRate)
    {
        if (fromRate == toRate)
        {
            return input;
        }

        var outputLength = (int)((long)input.Length * toRate / fromRate);
        var output = new float[Math.Max(1, outputLength)];

        for (var i = 0; i < output.Length; i++)
        {
            var srcPos = (double)i * fromRate / toRate;
            var idx = (int)srcPos;
            var frac = (float)(srcPos - idx);
            var a = input[Math.Min(idx, input.Length - 1)];
            var b = input[Math.Min(idx + 1, input.Length - 1)];
            output[i] = a + (b - a) * frac;
        }

        return output;
    }

    /// <summary>Downsamples float PCM to peak values for waveform display (0..1).</summary>
    public static float[] BuildWaveformPeaks(ReadOnlySpan<float> samples, int peakCount)
    {
        if (samples.Length == 0 || peakCount <= 0)
        {
            return [];
        }

        var peaks = new float[peakCount];
        var block = Math.Max(1, samples.Length / peakCount);

        for (var i = 0; i < peakCount; i++)
        {
            var start = i * block;
            var end = Math.Min(samples.Length, start + block);
            var peak = 0f;
            for (var s = start; s < end; s++)
            {
                peak = Math.Max(peak, Math.Abs(samples[s]));
            }

            peaks[i] = peak;
        }

        return peaks;
    }

    public static void Normalize(Span<float> samples, float targetPeak = 0.95f)
    {
        var peak = 0f;
        foreach (var s in samples)
        {
            peak = Math.Max(peak, Math.Abs(s));
        }

        if (peak < 1e-6f)
        {
            return;
        }

        var scale = targetPeak / peak;
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] *= scale;
        }
    }

    public static float[] Trim(float[] samples, double startFraction, double endFraction)
    {
        if (samples.Length == 0)
        {
            return [];
        }

        startFraction = Math.Clamp(startFraction, 0, 1);
        endFraction = Math.Clamp(endFraction, startFraction, 1);

        var start = (int)(samples.Length * startFraction);
        var end = (int)(samples.Length * endFraction);
        var length = Math.Max(1, end - start);
        var trimmed = new float[length];
        Array.Copy(samples, start, trimmed, 0, length);
        return trimmed;
    }

    public static void ApplyGain(Span<float> samples, float gain)
    {
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] *= gain;
        }
    }
}
