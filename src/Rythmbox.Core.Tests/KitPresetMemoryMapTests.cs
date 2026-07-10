using Rythmbox.Core.Samples;
using Rythmbox.Core.Models;
using Xunit;

namespace Rythmbox.Core.Tests;

public sealed class KitPresetMemoryMapTests
{
    [Fact]
    public void LoadWithDiagnostics_keeps_json_preset_samples_mapped_until_playback()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"kit_mapped_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var wavPath = Path.Combine(directory, "kick.wav");
        var presetPath = Path.Combine(directory, "kit.json");

        try
        {
            WavCodec.SaveMono(wavPath, [0.3f, 0.2f, 0.1f]);
            File.WriteAllText(presetPath, """
                {
                  "name": "Mapped Kit",
                  "pads": [
                    { "label": "Kick", "midi_note": 36, "sample": "kick.wav" }
                  ]
                }
                """);

            var result = KitPresetCodec.LoadWithDiagnostics(presetPath);
            var kick = Assert.Single(result.Kit.Pads, static pad => pad.MidiNote == 36);

            Assert.Empty(kick.Samples);
            Assert.NotNull(kick.MappedSample);
            Assert.Equal(1, result.MappedSampleCount);

            using var playback = PadPlaybackState.FromSample(kick);
            var source = playback.SelectSample(100);
            Assert.NotNull(source);
            Assert.InRange(source!.ReadFrame(0), 0.29f, 0.31f);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void LoadWithDiagnostics_skips_sources_that_exceed_the_mapping_budget()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"kit_budget_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var wavPath = Path.Combine(directory, "kick.wav");
        var presetPath = Path.Combine(directory, "kit.json");

        try
        {
            WavCodec.SaveMono(wavPath, [0.3f, 0.2f, 0.1f]);
            File.WriteAllText(presetPath, """
                {
                  "name": "Limited Kit",
                  "pads": [
                    { "label": "Kick", "midi_note": 36, "sample": "kick.wav" }
                  ]
                }
                """);

            var result = KitPresetCodec.LoadWithDiagnostics(presetPath, options: new KitLoadOptions
            {
                MaxMappedSampleCount = 0,
                MaxSingleMappedSampleBytes = 1_024,
                MaxTotalMappedSampleBytes = 1_024,
            });
            var kick = Assert.Single(result.Kit.Pads, static pad => pad.MidiNote == 36);

            Assert.Null(kick.MappedSample);
            Assert.False(kick.HasAudio);
            Assert.NotEmpty(result.Warnings);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void Json_round_trip_preserves_custom_pad_note_slot_and_mixer_output()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"kit_routing_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var presetPath = Path.Combine(directory, "routing.json");

        try
        {
            var kit = KitPresetCodec.CreateDefaultGmKit();
            kit.Pads[3].MidiNote = 100;
            kit.Pads[3].OutputGroup = DrumMixGroup.Cymbals;

            KitPresetCodec.Save(kit, presetPath, Path.Combine(directory, "SAMPLES"), exportWavs: false);
            var loaded = KitPresetCodec.Load(presetPath);

            var pad = loaded.Pads[3];
            Assert.Equal(3, pad.PadIndex);
            Assert.Equal(100, pad.MidiNote);
            Assert.Equal(DrumMixGroup.Cymbals, pad.OutputGroup);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
