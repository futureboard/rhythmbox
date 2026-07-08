using System.Text.Json;
using System.Text.Json.Serialization;
using Rythmbox.Core.Models;

namespace Rythmbox.Core.Samples;

/// <summary>Loads and saves kit presets in the old DrumStage JSON format (<c>shared/PRESETS/*.json</c>).</summary>
public static class KitPresetCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static KitPreset CreateDefaultGmKit()
    {
        var kit = new KitPreset { Name = "GM Kit (empty)" };
        foreach (var pad in GmPercussionMap.Pads)
        {
            kit.Pads.Add(new DrumSample
            {
                Label = pad.Label,
                MidiNote = pad.Note,
            });
        }

        return kit;
    }

    public static KitPreset Load(string jsonPath, string? samplesRoot = null)
    {
        using var stream = File.OpenRead(jsonPath);
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        var kit = new KitPreset
        {
            Name = root.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "Kit" : "Kit",
        };

        if (!root.TryGetProperty("pads", out var padsEl) || padsEl.ValueKind != JsonValueKind.Array)
        {
            return CreateDefaultGmKit();
        }

        kit.Pads.Clear();
        var baseDir = Path.GetDirectoryName(jsonPath) ?? Environment.CurrentDirectory;

        foreach (var padEl in padsEl.EnumerateArray())
        {
            var sample = new DrumSample
            {
                Label = padEl.TryGetProperty("label", out var labelEl) ? labelEl.GetString() ?? "Pad" : "Pad",
                Gain = padEl.TryGetProperty("gain", out var gainEl) && gainEl.TryGetSingle(out var g) ? g : 1f,
                MidiNote = padEl.TryGetProperty("midi_note", out var noteEl) && noteEl.ValueKind == JsonValueKind.Number
                    ? noteEl.GetInt32()
                    : -1,
                ChokeGroup = padEl.TryGetProperty("choke_group", out var chokeEl) && chokeEl.ValueKind == JsonValueKind.Number
                    ? chokeEl.GetInt32()
                    : 0,
            };

            if (padEl.TryGetProperty("sample", out var sampleEl) && sampleEl.GetString() is { Length: > 0 } relPath)
            {
                var resolved = ResolveSamplePath(relPath, baseDir, samplesRoot);
                if (resolved is not null && File.Exists(resolved))
                {
                    sample.FilePath = resolved;
                    sample.Samples = WavCodec.LoadMono(resolved);
                    sample.SampleRate = WavCodec.TargetSampleRate;
                    if (sample.Gain != 1f)
                    {
                        WavCodec.ApplyGain(sample.Samples, sample.Gain);
                    }
                }
            }

            kit.Pads.Add(sample);
        }

        EnsurePadCount(kit);
        return kit;
    }

    public static void Save(KitPreset kit, string jsonPath, string samplesDir, bool exportWavs = true)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath) ?? ".");
        Directory.CreateDirectory(samplesDir);

        var padEntries = new List<object>();
        for (var i = 0; i < kit.Pads.Count; i++)
        {
            var pad = kit.Pads[i];
            string? relativeSample = null;

            if (exportWavs && pad.HasAudio)
            {
                var safeName = SanitizeFileName($"{pad.Label}_{pad.MidiNote}.wav");
                var wavPath = Path.Combine(samplesDir, safeName);
                WavCodec.SaveMono(wavPath, pad.Samples, pad.SampleRate);
                relativeSample = $"SAMPLES/{safeName}";
                pad.FilePath = wavPath;
            }
            else if (pad.FilePath is { Length: > 0 })
            {
                relativeSample = ToRelativeSamplePath(pad.FilePath);
            }

            padEntries.Add(new
            {
                label = pad.Label,
                gain = pad.Gain,
                midi_note = pad.MidiNote >= 0 ? pad.MidiNote : (int?)null,
                choke_group = pad.ChokeGroup > 0 ? pad.ChokeGroup : (int?)null,
                sample = relativeSample,
            });
        }

        var payload = new { name = kit.Name, pads = padEntries };
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        File.WriteAllText(jsonPath, json);
    }

    private static void EnsurePadCount(KitPreset kit)
    {
        if (kit.Pads.Count >= GmPercussionMap.Pads.Count)
        {
            return;
        }

        kit.Pads.Clear();
        foreach (var pad in GmPercussionMap.Pads)
        {
            kit.Pads.Add(new DrumSample { Label = pad.Label, MidiNote = pad.Note });
        }
    }

    private static string? ResolveSamplePath(string relative, string presetDir, string? samplesRoot)
    {
        var candidates = new List<string>
        {
            Path.GetFullPath(Path.Combine(presetDir, relative)),
            Path.GetFullPath(relative),
        };

        if (samplesRoot is not null)
        {
            candidates.Add(Path.Combine(samplesRoot, Path.GetFileName(relative)));
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string ToRelativeSamplePath(string absolutePath)
    {
        var fileName = Path.GetFileName(absolutePath);
        return $"SAMPLES/{fileName}";
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return name.Replace(' ', '_');
    }
}
