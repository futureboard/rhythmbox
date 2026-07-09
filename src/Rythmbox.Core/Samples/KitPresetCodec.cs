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
                PitchSemitones = 0f,
                MidiNote = padEl.TryGetProperty("midi_note", out var noteEl) && noteEl.ValueKind == JsonValueKind.Number
                    ? noteEl.GetInt32()
                    : -1,
                ChokeGroup = padEl.TryGetProperty("choke_group", out var chokeEl) && chokeEl.ValueKind == JsonValueKind.Number
                    ? chokeEl.GetInt32()
                    : 0,
            };

            if (padEl.TryGetProperty("velocity_layers", out var layersEl) && layersEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var layerEl in layersEl.EnumerateArray())
                {
                    var layer = new VelocityLayer
                    {
                        VelocityLow = layerEl.TryGetProperty("velocity_low", out var lowEl) && lowEl.TryGetInt32(out var low)
                            ? low
                            : 1,
                        VelocityHigh = layerEl.TryGetProperty("velocity_high", out var highEl) && highEl.TryGetInt32(out var high)
                            ? high
                            : 127,
                    };

                    if (layerEl.TryGetProperty("round_robin", out var rrEl) && rrEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var pathEl in rrEl.EnumerateArray())
                        {
                            if (pathEl.GetString() is not { Length: > 0 } layerPath)
                            {
                                continue;
                            }

                            layer.RoundRobinPaths.Add(layerPath);
                            var resolved = ResolveSamplePath(layerPath, baseDir, samplesRoot);
                            if (resolved is null || !File.Exists(resolved))
                            {
                                continue;
                            }

                            try
                            {
                                layer.RoundRobinSamples.Add(WavCodec.LoadMono(resolved));
                            }
                            catch (Exception ex) when (ex is InvalidDataException or NotSupportedException)
                            {
                            }
                        }
                    }

                    if (layer.HasSamples)
                    {
                        sample.VelocityLayers.Add(layer);
                    }
                }

                if (sample.HasVelocityLayers && sample.SampleRate <= 0)
                {
                    sample.SampleRate = WavCodec.TargetSampleRate;
                }
            }

            if (padEl.TryGetProperty("sample", out var sampleEl) && sampleEl.GetString() is { Length: > 0 } relPath)
            {
                var resolved = ResolveSamplePath(relPath, baseDir, samplesRoot);
                if (resolved is not null && File.Exists(resolved))
                {
                    try
                    {
                        sample.FilePath = resolved;
                        sample.Samples = WavCodec.LoadMono(resolved);
                        sample.SampleRate = WavCodec.TargetSampleRate;
                    }
                    catch (Exception ex) when (ex is InvalidDataException or NotSupportedException)
                    {
                        sample.FilePath = null;
                        sample.Samples = [];
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
            object[]? velocityLayers = null;

            if (pad.HasVelocityLayers)
            {
                if (exportWavs)
                {
                    foreach (var layer in pad.VelocityLayers.Where(static layer => layer.HasSamples))
                    {
                        for (var rr = 0; rr < layer.RoundRobinSamples.Count; rr++)
                        {
                            var samples = layer.RoundRobinSamples[rr];
                            if (samples.Length == 0)
                            {
                                continue;
                            }

                            while (layer.RoundRobinPaths.Count <= rr)
                            {
                                layer.RoundRobinPaths.Add(string.Empty);
                            }

                            var existingRel = layer.RoundRobinPaths[rr];
                            string relative;
                            if (!string.IsNullOrWhiteSpace(existingRel))
                            {
                                relative = existingRel.Replace('\\', '/');
                            }
                            else
                            {
                                var safeName = SanitizeFileName($"{pad.Label}_{pad.MidiNote}_v{layer.VelocityLow}-{layer.VelocityHigh}_rr{rr + 1}.wav");
                                relative = $"SAMPLES/{safeName}";
                            }

                            var wavPath = ResolveExportWavPath(relative, samplesDir, jsonPath);
                            Directory.CreateDirectory(Path.GetDirectoryName(wavPath) ?? samplesDir);
                            WavCodec.SaveMono(wavPath, samples, pad.SampleRate > 0 ? pad.SampleRate : WavCodec.TargetSampleRate);
                            layer.RoundRobinPaths[rr] = relative;
                        }
                    }
                }

                velocityLayers = pad.VelocityLayers
                    .Where(static layer => layer.HasSamples)
                    .Select(layer => new
                    {
                        velocity_low = layer.VelocityLow,
                        velocity_high = layer.VelocityHigh,
                        round_robin = Enumerable.Range(0, layer.RoundRobinSamples.Count)
                            .Select(index =>
                            {
                                if (index < layer.RoundRobinPaths.Count && !string.IsNullOrWhiteSpace(layer.RoundRobinPaths[index]))
                                {
                                    return layer.RoundRobinPaths[index].Replace('\\', '/');
                                }

                                return $"SAMPLES/{SanitizeFileName($"{pad.Label}_{pad.MidiNote}_rr{index + 1}.wav")}";
                            })
                            .ToArray(),
                    })
                    .ToArray();
            }

            if (exportWavs && pad.Samples.Length > 0 && !pad.HasVelocityLayers)
            {
                var safeName = SanitizeFileName($"{pad.Label}_{pad.MidiNote}.wav");
                var wavPath = Path.Combine(samplesDir, safeName);
                WavCodec.SaveMono(wavPath, pad.Samples, pad.SampleRate);
                relativeSample = $"SAMPLES/{safeName}";
                pad.FilePath = wavPath;
            }
            else if (!pad.HasVelocityLayers && pad.FilePath is { Length: > 0 })
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
                velocity_layers = velocityLayers,
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

        var byNote = kit.Pads
            .Where(static pad => pad.MidiNote >= 0)
            .GroupBy(static pad => pad.MidiNote)
            .ToDictionary(static group => group.Key, static group => group.First());

        kit.Pads.Clear();
        foreach (var pad in GmPercussionMap.Pads)
        {
            kit.Pads.Add(byNote.TryGetValue(pad.Note, out var existing)
                ? existing
                : new DrumSample { Label = pad.Label, MidiNote = pad.Note });
        }
    }

    private static string ResolveExportWavPath(string relative, string samplesDir, string jsonPath)
    {
        relative = relative.Replace('\\', '/');
        if (relative.StartsWith("SAMPLES/", StringComparison.OrdinalIgnoreCase))
        {
            relative = relative["SAMPLES/".Length..];
        }

        var underSamples = Path.Combine(samplesDir, relative.Replace('/', Path.DirectorySeparatorChar));
        if (relative.Contains('/'))
        {
            return underSamples;
        }

        // Flat SAMPLES/name.wav — also try next to the preset for older layouts.
        var nextToPreset = Path.Combine(Path.GetDirectoryName(jsonPath) ?? ".", "SAMPLES", relative);
        return File.Exists(nextToPreset) ? nextToPreset : underSamples;
    }

    public static string? ResolveSamplePath(string relative, string presetDir, string? samplesRoot)
    {
        var candidates = new List<string>
        {
            Path.GetFullPath(Path.Combine(presetDir, relative)),
            Path.GetFullPath(Path.Combine(presetDir, "SAMPLES", relative)),
            Path.GetFullPath(relative),
        };

        var presetParent = Directory.GetParent(presetDir)?.FullName;
        if (presetParent is not null)
        {
            candidates.Add(Path.GetFullPath(Path.Combine(presetParent, "SAMPLES", relative)));
            candidates.Add(Path.GetFullPath(Path.Combine(presetParent, relative)));
        }

        if (samplesRoot is not null)
        {
            candidates.Add(Path.GetFullPath(Path.Combine(samplesRoot, relative)));
            candidates.Add(Path.GetFullPath(Path.Combine(samplesRoot, Path.GetFileName(relative))));
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
