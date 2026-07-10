using System.Text;
using Rythmbox.Core.Formats;
using Rythmbox.Core.Models;
using Rythmbox.Core.Samples;
using Rythmbox.Core.Styles;
using Xunit;

namespace Rythmbox.Core.Tests;

public class BinaryFormatTests
{
    [Fact]
    public void Apak_round_trip_preserves_kit_name_and_audio()
    {
        var kit = new KitPreset
        {
            Name = "Test Kit",
            Pads =
            [
                new DrumSample
                {
                    Label = "Kick",
                    MidiNote = 36,
                    Samples = [0.5f, 0.25f, 0f],
                    SampleRate = WavCodec.TargetSampleRate,
                    Envelope = new PadEnvelopeSettings
                    {
                        AttackMs = 12,
                        DecayMs = 80,
                        SustainLevel = 0.65f,
                        ReleaseMs = 120,
                    },
                },
            ],
        };

        Assert.Equal(3, kit.Pads[0].Samples.Length);
        var path = Path.Combine(Path.GetTempPath(), "test_" + Guid.NewGuid().ToString("N") + ApakCodec.Extension);
        try
        {
            ApakCodec.Save(kit, path, new ApakCodec.PackOptions { Key32 = RhythmAes256.FactoryKey });
            var loaded = ApakCodec.LoadFactory(path);

            Assert.Equal("Test Kit", loaded.Name);
            Assert.True(loaded.Pads[0].HasAudio);
            Assert.Equal(3, loaded.Pads[0].Samples.Length);
            Assert.Equal(12, loaded.Pads[0].Envelope.AttackMs);
            Assert.Equal(0.65f, loaded.Pads[0].Envelope.SustainLevel);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Rhmsty_round_trip_preserves_style_metadata()
    {
        var dir = Path.Combine(Path.GetTempPath(), "rhmsty_pack_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        var styleDir = Path.Combine(dir, "test_style");
        Directory.CreateDirectory(styleDir);

        var midiBytes = CreateMinimalSmf();
        File.WriteAllBytes(Path.Combine(styleDir, "verse_a.mid"), midiBytes);
        File.WriteAllText(Path.Combine(styleDir, "style.json"), """
            {
              "id": "test_style",
              "name": "Test Style",
              "category": "Pop",
              "patterns": {
                "verse_a": {
                  "name": "Verse A",
                  "type": "verse",
                  "bars": 4,
                  "midi": "verse_a.mid"
                }
              }
            }
            """);

        var packed = Path.Combine(dir, "test_style" + RhmStyleCodec.Extension);
        try
        {
            var pack = RhmStyleCodec.PackDirectory(styleDir, packed);
            Assert.Equal(1, pack.BlobCount);

            var style = RhmStyleCodec.Load(packed);
            Assert.Equal("test_style", style.Id);
            Assert.True(style.Patterns["verse_a"].HasMidiFile);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    private static byte[] CreateMinimalSmf()
    {
        // MThd + MTrk with end-of-track — enough for StyleBankCodec file existence checks.
        return
        [
            0x4D, 0x54, 0x68, 0x64, 0x00, 0x00, 0x00, 0x06,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x60,
            0x4D, 0x54, 0x72, 0x6B, 0x00, 0x00, 0x00, 0x04,
            0x00, 0xFF, 0x2F, 0x00,
        ];
    }
}
