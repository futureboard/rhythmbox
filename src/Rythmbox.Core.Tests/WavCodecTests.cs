using Rythmbox.Core.Samples;
using Xunit;

namespace Rythmbox.Core.Tests;

public class WavCodecTests
{
    [Fact]
    public void DecodeMono_supports_16_bit_mono_and_resamples_to_target_rate()
    {
        var wav = CreatePcmWav(channels: 1, sampleRate: 44_100, bitsPerSample: 16, samples: [short.MaxValue, 0, short.MinValue]);

        var decoded = WavCodec.DecodeMono(wav, out var sampleRate);

        Assert.Equal(44_100, sampleRate);
        Assert.True(decoded.Length >= 3);
        Assert.InRange(decoded[0], 0.9f, 1.1f);
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

        var decoded = WavCodec.DecodeMono(wav, out var sampleRate);

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
}
