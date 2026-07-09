using System.Buffers.Binary;
using Rythmbox.Core.Samples;
using Xunit;

namespace Rythmbox.Core.Tests;

public class WavCodecTests
{
    [Fact]
    public void DecodeMono_supports_16_bit_mono()
    {
        var wav = CreatePcmWav(channels: 1, sampleRate: 44_100, bitsPerSample: 16, samples: [short.MaxValue, 0, short.MinValue]);

        var decoded = WavCodec.DecodeMono(wav, out var sampleRate, targetSampleRate: 44_100);

        Assert.Equal(44_100, sampleRate);
        Assert.Equal(3, decoded.Length);
        Assert.InRange(decoded[0], 0.9f, 1.1f);
    }

    [Fact]
    public void DecodeMono_resamples_to_target_sample_rate()
    {
        var wav = CreatePcmWav(channels: 1, sampleRate: 44_100, bitsPerSample: 16, samples: Enumerable.Repeat((short)10_000, 441).ToArray());

        var decoded = WavCodec.DecodeMono(wav, out var sourceRate, targetSampleRate: 48_000);

        Assert.Equal(44_100, sourceRate);
        Assert.InRange(decoded.Length, 470, 490);
    }

    [Fact]
    public void DecodeMono_resample_preserves_duration_for_sine_tone()
    {
        var sourceRate = 44_100;
        var samples = new float[sourceRate];
        for (var i = 0; i < samples.Length; i++)
        {
            var t = (double)i / sourceRate;
            samples[i] = (float)Math.Sin(2 * Math.PI * 440 * t);
        }

        var pcm = EncodeFloatMonoPcm16(samples, sourceRate);
        var decoded = WavCodec.DecodeMono(pcm, out _, targetSampleRate: 48_000);

        Assert.InRange(decoded.Length, 47_500, 48_500);

        var sourceZeroCrossings = CountZeroCrossings(samples);
        var decodedZeroCrossings = CountZeroCrossings(decoded);
        Assert.InRange(decodedZeroCrossings, sourceZeroCrossings - 4, sourceZeroCrossings + 4);
    }

    [Fact]
    public void DecodeMono_supports_ieee_float_32_bit_mono()
    {
        var wav = CreateFloatWav(channels: 1, sampleRate: 48_000, samples: [1f, -1f, 0.5f, -0.5f]);

        var decoded = WavCodec.DecodeMono(wav, out var sampleRate);

        Assert.Equal(48_000, sampleRate);
        Assert.Equal(4, decoded.Length);
        Assert.InRange(decoded[0], 0.99f, 1.01f);
        Assert.InRange(decoded[1], -1.01f, -0.99f);
    }

    [Fact]
    public void BuildWaveformEnvelope_returns_min_and_max_per_block()
    {
        var samples = new float[] { -0.8f, 0.2f, 0.5f, -0.1f, 0.9f, -0.4f };
        var peaks = WavCodec.BuildWaveformEnvelope(samples, peakCount: 2);

        Assert.Equal(2, peaks.Length);
        Assert.InRange(peaks[0].Min, -0.81f, -0.79f);
        Assert.InRange(peaks[0].Max, 0.49f, 0.51f);
        Assert.InRange(peaks[1].Min, -0.41f, -0.39f);
        Assert.InRange(peaks[1].Max, 0.89f, 0.91f);
    }

    [Fact]
    public void TryReadSamplerRootNote_reads_smpl_chunk_but_is_not_applied_by_decode()
    {
        var wav = CreatePcmWavWithSmplRootNote(sampleRate: 48_000, rootNote: 60);
        Assert.True(WavCodec.TryReadSamplerRootNote(wav, out var root));
        Assert.Equal(60, root);

        var decoded = WavCodec.DecodeMono(wav, out _, targetSampleRate: 48_000);
        Assert.Equal(3, decoded.Length);
    }

    [Fact]
    public void PitchShift_octave_up_halves_sample_length()
    {
        var samples = Enumerable.Repeat(0.5f, 480).ToArray();
        var shifted = WavCodec.PitchShift(samples, semitones: 12);
        Assert.Equal(240, shifted.Length);
    }

    [Fact]
    public void DecodeMono_supports_24_bit_stereo_and_downmixes_to_mono()
    {
        var wav = CreatePcmWav(
            channels: 2,
            sampleRate: 44_100,
            bitsPerSample: 24,
            samples24:
            [
                4_194_304, -4_194_304,
                2_097_152, 2_097_152,
            ]);

        var decoded = WavCodec.DecodeMono(wav, out var sampleRate, targetSampleRate: 44_100);

        Assert.Equal(44_100, sampleRate);
        Assert.Equal(2, decoded.Length);
        Assert.InRange(decoded[0], -0.01f, 0.01f);
        Assert.InRange(decoded[1], 0.2f, 0.3f);
    }

    [Fact]
    public void LoadMono_reads_published_gm_kick_without_throwing()
    {
        var kickPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "out", "publish", "win-x64", "shared", "PRESETS", "GM", "SAMPLES", "Kick_36.wav"));

        if (!File.Exists(kickPath))
        {
            return;
        }

        var decoded = WavCodec.LoadMono(kickPath);

        Assert.NotEmpty(decoded);
        Assert.Contains(decoded, sample => Math.Abs(sample) > 0.001f);
    }

    private static byte[] CreatePcmWav(int channels, int sampleRate, short bitsPerSample, short[]? samples = null, int[]? samples24 = null)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        var bytesPerSample = (bitsPerSample + 7) / 8;
        var blockAlign = channels * bytesPerSample;
        byte[] pcm;

        if (bitsPerSample == 16)
        {
            pcm = new byte[(samples?.Length ?? 0) * 2];
            for (var i = 0; i < samples!.Length; i++)
            {
                pcm[i * 2] = (byte)(samples[i] & 0xFF);
                pcm[i * 2 + 1] = (byte)((samples[i] >> 8) & 0xFF);
            }
        }
        else if (bitsPerSample == 24)
        {
            pcm = new byte[(samples24?.Length ?? 0) * 3];
            for (var i = 0; i < samples24!.Length; i++)
            {
                var sample = samples24[i];
                pcm[i * 3] = (byte)(sample & 0xFF);
                pcm[i * 3 + 1] = (byte)((sample >> 8) & 0xFF);
                pcm[i * 3 + 2] = (byte)((sample >> 16) & 0xFF);
            }
        }
        else
        {
            throw new NotSupportedException("Test helper only builds 16- or 24-bit PCM.");
        }

        var byteRate = sampleRate * blockAlign;

        writer.Write("RIFF"u8);
        writer.Write(36 + pcm.Length);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write((short)blockAlign);
        writer.Write(bitsPerSample);
        writer.Write("data"u8);
        writer.Write(pcm.Length);
        writer.Write(pcm);

        return stream.ToArray();
    }

    private static byte[] CreatePcmWavWithSmplRootNote(int sampleRate, int rootNote)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        var pcm = new byte[] { 0x00, 0x40, 0x00, 0x00, 0x00, 0xC0 };
        var smpl = new byte[36];
        BinaryPrimitives.WriteInt32LittleEndian(smpl.AsSpan(20, 4), rootNote);

        const short channels = 1;
        const short bitsPerSample = 16;
        var blockAlign = channels * bitsPerSample / 8;
        var byteRate = sampleRate * blockAlign;

        writer.Write("RIFF"u8);
        writer.Write(4 + 24 + 44 + 14);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write((short)blockAlign);
        writer.Write(bitsPerSample);
        writer.Write("smpl"u8);
        writer.Write(smpl.Length);
        writer.Write(smpl);
        writer.Write("data"u8);
        writer.Write(pcm.Length);
        writer.Write(pcm);

        return stream.ToArray();
    }

    private static byte[] EncodeFloatMonoPcm16(float[] samples, int sampleRate)
    {
        var shorts = new short[samples.Length];
        for (var i = 0; i < samples.Length; i++)
        {
            shorts[i] = (short)(Math.Clamp(samples[i], -1f, 1f) * short.MaxValue);
        }

        return CreatePcmWav(1, sampleRate, 16, shorts);
    }

    private static int CountZeroCrossings(float[] samples)
    {
        var count = 0;
        for (var i = 1; i < samples.Length; i++)
        {
            if ((samples[i - 1] >= 0 && samples[i] < 0) || (samples[i - 1] < 0 && samples[i] >= 0))
            {
                count++;
            }
        }

        return count;
    }

    private static byte[] CreateFloatWav(int channels, int sampleRate, float[] samples)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        var pcm = new byte[samples.Length * 4];
        for (var i = 0; i < samples.Length; i++)
        {
            var bytes = BitConverter.GetBytes(samples[i]);
            pcm[i * 4] = bytes[0];
            pcm[i * 4 + 1] = bytes[1];
            pcm[i * 4 + 2] = bytes[2];
            pcm[i * 4 + 3] = bytes[3];
        }

        const short formatTag = 3;
        const short bitsPerSample = 32;
        var blockAlign = channels * bitsPerSample / 8;
        var byteRate = sampleRate * blockAlign;

        writer.Write("RIFF"u8);
        writer.Write(36 + pcm.Length);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write(formatTag);
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write((short)blockAlign);
        writer.Write(bitsPerSample);
        writer.Write("data"u8);
        writer.Write(pcm.Length);
        writer.Write(pcm);

        return stream.ToArray();
    }
}
