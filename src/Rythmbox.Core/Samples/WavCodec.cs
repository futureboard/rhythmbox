using System.Buffers.Binary;
using System.Text;

namespace Rythmbox.Core.Samples;

/// <summary>Loads and saves mono PCM WAV files for drum kit samples (compatible with old DrumStage SDL_LoadWAV kits).</summary>
public static class WavCodec
{
    public const int TargetSampleRate = 48_000;

    private const short WaveFormatPcm = 1;
    private const short WaveFormatIeeeFloat = 3;
    private const short WaveFormatExtensible = unchecked((short)0xFFFE);

    private static readonly Guid SubFormatPcm = new("00000001-0000-0010-8000-00AA00389B71");
    private static readonly Guid SubFormatFloat = new("00000003-0000-0010-8000-00AA00389B71");

    public static float[] LoadMono(string path, int targetSampleRate = TargetSampleRate)
    {
        return LoadMono(path, out _, targetSampleRate);
    }

    /// <summary>Loads a file through a memory map and returns its original rate for editor metadata.</summary>
    public static float[] LoadMono(string path, out int sourceSampleRate, int targetSampleRate = TargetSampleRate)
    {
        // Avoid File.ReadAllBytes here: a normal editor import only needs one
        // PCM output buffer, not a second managed copy of the complete WAV file.
        var mapped = MemoryMappedWavSample.Open(path);
        sourceSampleRate = mapped.SampleRate;
        return mapped.DecodeMono(targetSampleRate);
    }

    public static float[] DecodeMono(ReadOnlySpan<byte> wavBytes, out int sampleRate, int targetSampleRate = TargetSampleRate)
    {
        if (wavBytes.Length < 44
            || Encoding.ASCII.GetString(wavBytes.Slice(0, 4)) != "RIFF"
            || Encoding.ASCII.GetString(wavBytes.Slice(8, 4)) != "WAVE")
        {
            throw new InvalidDataException("Not a RIFF/WAVE file.");
        }

        var format = new WavFormat();
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
                    format = ParseFmtChunk(wavBytes.Slice(offset, chunkSize));
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

        sampleRate = format.SampleRate;
        var mono = DecodeToMono(data, format);
        return sampleRate == targetSampleRate
            ? mono
            : WavResampler.Resample(mono, sampleRate, targetSampleRate);
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

    /// <summary>Downsamples float PCM to min/max envelope columns for waveform display.</summary>
    public static WaveformPeak[] BuildWaveformEnvelope(ReadOnlySpan<float> samples, int peakCount)
    {
        if (samples.Length == 0 || peakCount <= 0)
        {
            return [];
        }

        // Preserve every source range. A fixed block count could append empty
        // peaks for short samples and drop the final remainder of a waveform.
        var resolvedPeakCount = Math.Min(peakCount, samples.Length);
        var peaks = new WaveformPeak[resolvedPeakCount];

        for (var i = 0; i < resolvedPeakCount; i++)
        {
            var start = (int)((long)i * samples.Length / resolvedPeakCount);
            var end = (int)((long)(i + 1) * samples.Length / resolvedPeakCount);
            var min = 0f;
            var max = 0f;

            for (var s = start; s < end; s++)
            {
                var sample = samples[s];
                min = Math.Min(min, sample);
                max = Math.Max(max, sample);
            }

            peaks[i] = new WaveformPeak(min, max);
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

    /// <summary>Reads the MIDI root note from a WAV <c>smpl</c> chunk, if present. We intentionally do not apply it.</summary>
    public static bool TryReadSamplerRootNote(ReadOnlySpan<byte> wavBytes, out int midiNote)
    {
        midiNote = -1;

        if (wavBytes.Length < 44
            || Encoding.ASCII.GetString(wavBytes.Slice(0, 4)) != "RIFF"
            || Encoding.ASCII.GetString(wavBytes.Slice(8, 4)) != "WAVE")
        {
            return false;
        }

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

            if (chunkId == "smpl" && chunkSize >= 36)
            {
                midiNote = BinaryPrimitives.ReadInt32LittleEndian(wavBytes.Slice(offset + 20, 4));
                return midiNote is >= 0 and <= 127;
            }

            offset += chunkSize;
            if ((chunkSize & 1) != 0)
            {
                offset++;
            }
        }

        return false;
    }

    /// <summary>Pitch-shift by resampling (drum-machine style). Positive semitones = higher pitch.</summary>
    public static float[] PitchShift(ReadOnlySpan<float> samples, float semitones)
    {
        if (samples.Length == 0 || MathF.Abs(semitones) < 0.001f)
        {
            return samples.ToArray();
        }

        var ratio = Math.Pow(2, semitones / 12.0);
        var outLength = Math.Max(1, (int)(samples.Length / ratio));
        var output = new float[outLength];

        for (var i = 0; i < outLength; i++)
        {
            var srcPos = i * ratio;
            var idx = (int)srcPos;
            var frac = (float)(srcPos - idx);
            var a = samples[Math.Min(idx, samples.Length - 1)];
            var b = samples[Math.Min(idx + 1, samples.Length - 1)];
            output[i] = a + (b - a) * frac;
        }

        return output;
    }

    public static float PitchToPlaybackRatio(float semitones) =>
        (float)Math.Pow(2, semitones / 12.0);

    private static WavFormat ParseFmtChunk(ReadOnlySpan<byte> chunk)
    {
        if (chunk.Length < 16)
        {
            throw new InvalidDataException("WAV fmt chunk is too small.");
        }

        var formatTag = BinaryPrimitives.ReadInt16LittleEndian(chunk);
        var channels = BinaryPrimitives.ReadInt16LittleEndian(chunk.Slice(2, 2));
        var sampleRate = BinaryPrimitives.ReadInt32LittleEndian(chunk.Slice(4, 4));
        var blockAlign = BinaryPrimitives.ReadInt16LittleEndian(chunk.Slice(12, 2));
        var bitsPerSample = BinaryPrimitives.ReadInt16LittleEndian(chunk.Slice(14, 2));
        var isFloat = formatTag == WaveFormatIeeeFloat;

        if (formatTag == WaveFormatExtensible && chunk.Length >= 40)
        {
            bitsPerSample = BinaryPrimitives.ReadInt16LittleEndian(chunk.Slice(18, 2));
            var subFormat = ReadGuid(chunk.Slice(24, 16));
            isFloat = subFormat == SubFormatFloat;
            if (subFormat != SubFormatPcm && subFormat != SubFormatFloat)
            {
                throw new NotSupportedException($"Unsupported WAV sub-format {subFormat}.");
            }
        }
        else if (formatTag != WaveFormatPcm && formatTag != WaveFormatIeeeFloat)
        {
            throw new NotSupportedException($"Unsupported WAV format tag {formatTag}.");
        }

        return new WavFormat(channels, sampleRate, blockAlign, bitsPerSample, isFloat);
    }

    private static Guid ReadGuid(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 16)
        {
            return Guid.Empty;
        }

        return new Guid(bytes.Slice(0, 16));
    }

    private static float[] DecodeToMono(byte[] data, WavFormat format)
    {
        if (format.Channels <= 0)
        {
            throw new InvalidDataException("WAV channel count must be positive.");
        }

        var blockAlign = format.BlockAlign > 0
            ? format.BlockAlign
            : (short)(format.Channels * ((format.BitsPerSample + 7) / 8));

        if (data.Length < blockAlign)
        {
            return [];
        }

        var frameCount = data.Length / blockAlign;
        var bytesPerSample = blockAlign / format.Channels;
        var mono = new float[frameCount];

        for (var i = 0; i < frameCount; i++)
        {
            var frameOffset = i * blockAlign;
            float sum = 0;

            for (var ch = 0; ch < format.Channels; ch++)
            {
                var sampleOffset = frameOffset + (ch * bytesPerSample);
                sum += ReadSample(data, sampleOffset, format, bytesPerSample);
            }

            mono[i] = sum / format.Channels;
        }

        return mono;
    }

    private static float ReadSample(byte[] data, int offset, WavFormat format, int bytesPerSample)
    {
        if (format.IsFloat && format.BitsPerSample == 32)
        {
            return BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(offset, 4));
        }

        return format.BitsPerSample switch
        {
            8 => (data[offset] - 128) / 128f,
            16 => BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset, 2)) / (float)short.MaxValue,
            24 => ReadInt24LittleEndian(data.AsSpan(offset, Math.Min(3, bytesPerSample))) / 8_388_608f,
            32 => BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, Math.Min(4, bytesPerSample))) / (float)int.MaxValue,
            _ => throw new NotSupportedException($"Unsupported WAV bit depth ({format.BitsPerSample}-bit)."),
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

    private readonly record struct WavFormat(int Channels, int SampleRate, short BlockAlign, short BitsPerSample, bool IsFloat);
}
