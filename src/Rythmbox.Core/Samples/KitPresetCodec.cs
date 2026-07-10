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
                MidiNotes = [pad.Note],
                PadIndex = pad.Index,
                OutputGroup = GmPercussionMap.GetMixGroup(pad.Note),
            });
        }

        return kit;
    }

    public static KitPreset Load(string jsonPath, string? samplesRoot = null) =>
        LoadWithDiagnostics(jsonPath, samplesRoot).Kit;

    /// <summary>
    /// Opens a JSON preset without decoding every WAV into managed PCM. Each
    /// valid source is represented by a memory-map descriptor and is paged by
    /// the OS only while it is actually played.
    /// </summary>
    public static KitLoadResult LoadWithDiagnostics(
        string jsonPath,
        string? samplesRoot = null,
        KitLoadOptions? options = null,
        IProgress<KitLoadProgress>? progress = null)
    {
        options ??= new KitLoadOptions();
        using var stream = new FileStream(
            jsonPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan);
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        var kit = new KitPreset
        {
            Name = root.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "Kit" : "Kit",
        };

        if (!root.TryGetProperty("pads", out var padsEl) || padsEl.ValueKind != JsonValueKind.Array)
        {
            return new KitLoadResult(CreateDefaultGmKit(), [], 0, 0);
        }

        kit.Pads.Clear();
        var baseDir = Path.GetDirectoryName(jsonPath) ?? Environment.CurrentDirectory;
        var warnings = new List<string>();
        var mappedByPath = new Dictionary<string, MemoryMappedWavSample>(StringComparer.OrdinalIgnoreCase);
        var totalSources = CountSampleReferences(padsEl);
        var completedSources = 0;
        long mappedBytes = 0;

        progress?.Report(new KitLoadProgress(0, totalSources, "Reading preset metadata…", 0, 0));

        MemoryMappedWavSample? OpenMappedSample(string relativePath)
        {
            completedSources++;
            var resolved = ResolveSamplePath(relativePath, baseDir, samplesRoot);
            if (resolved is null)
            {
                warnings.Add($"Missing sample: {relativePath}");
                ReportProgress($"Missing {Path.GetFileName(relativePath)}");
                return null;
            }

            if (mappedByPath.TryGetValue(resolved, out var existing))
            {
                ReportProgress($"Linked {Path.GetFileName(resolved)}");
                return existing;
            }

            var size = new FileInfo(resolved).Length;
            if (size > options.MaxSingleMappedSampleBytes)
            {
                warnings.Add($"Skipped {Path.GetFileName(resolved)}: file exceeds the per-sample limit.");
                ReportProgress($"Skipped oversized {Path.GetFileName(resolved)}");
                return null;
            }

            if (mappedByPath.Count >= options.MaxMappedSampleCount || mappedBytes + size > options.MaxTotalMappedSampleBytes)
            {
                warnings.Add($"Skipped {Path.GetFileName(resolved)}: preset memory-map limit reached.");
                ReportProgress($"Skipped {Path.GetFileName(resolved)} (limit reached)");
                return null;
            }

            try
            {
                var mapped = MemoryMappedWavSample.Open(resolved);
                mapped.WarmStart();
                mappedByPath.Add(resolved, mapped);
                mappedBytes += mapped.FileLength;
                ReportProgress($"Mapped {Path.GetFileName(resolved)}");
                return mapped;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
            {
                warnings.Add($"Skipped {Path.GetFileName(resolved)}: {ex.Message}");
                ReportProgress($"Skipped invalid {Path.GetFileName(resolved)}");
                return null;
            }
        }

        void ReportProgress(string message) => progress?.Report(new KitLoadProgress(
            completedSources,
            totalSources,
            message,
            mappedBytes,
            warnings.Count));

        var sourcePadIndex = 0;
        foreach (var padEl in padsEl.EnumerateArray())
        {
            var midiNote = padEl.TryGetProperty("midi_note", out var noteEl) && noteEl.ValueKind == JsonValueKind.Number
                ? noteEl.GetInt32()
                : -1;
            var midiNotes = ReadMidiNotes(padEl, midiNote);
            var sample = new DrumSample
            {
                Label = padEl.TryGetProperty("label", out var labelEl) ? labelEl.GetString() ?? "Pad" : "Pad",
                Gain = padEl.TryGetProperty("gain", out var gainEl) && gainEl.TryGetSingle(out var g) ? g : 1f,
                PitchSemitones = 0f,
                Envelope = ReadEnvelope(padEl),
                MidiNote = midiNotes.FirstOrDefault(midiNote),
                MidiNotes = midiNotes,
                PadIndex = ReadPadIndex(padEl),
                OutputGroup = ReadOutputGroup(padEl, midiNote, sourcePadIndex),
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

                            var mapped = OpenMappedSample(layerPath);
                            if (mapped is not null)
                            {
                                layer.RoundRobinPaths.Add(layerPath);
                                layer.RoundRobinSamples.Add([]);
                                layer.RoundRobinMappedSamples.Add(mapped);
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
                var mapped = OpenMappedSample(relPath);
                if (mapped is not null)
                {
                    sample.FilePath = mapped.FilePath;
                    sample.MappedSample = mapped;
                    sample.SampleRate = mapped.SampleRate;
                }
            }

            kit.Pads.Add(sample);
            sourcePadIndex++;
        }

        EnsurePadCount(kit);
        progress?.Report(new KitLoadProgress(totalSources, totalSources, "Preset ready", mappedBytes, warnings.Count));
        return new KitLoadResult(kit, warnings, mappedBytes, mappedByPath.Count);
    }

    public static void Save(KitPreset kit, string jsonPath, string samplesDir, bool exportWavs = true)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath) ?? ".");
        Directory.CreateDirectory(samplesDir);

        var padEntries = new List<object>();
        for (var i = 0; i < kit.Pads.Count; i++)
        {
            var pad = kit.Pads[i];
            var midiNotes = pad.ResolveMidiNotes();
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
                            var mapped = rr < layer.RoundRobinMappedSamples.Count
                                ? layer.RoundRobinMappedSamples[rr]
                                : null;
                            if (samples.Length == 0 && mapped is null)
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
                            if (samples.Length > 0)
                            {
                                WavCodec.SaveMono(wavPath, samples, pad.SampleRate > 0 ? pad.SampleRate : WavCodec.TargetSampleRate);
                            }
                            else
                            {
                                CopyMappedSample(mapped!, wavPath);
                            }

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
            else if (exportWavs && pad.MappedSample is { } mapped && !pad.HasVelocityLayers)
            {
                var safeName = SanitizeFileName($"{pad.Label}_{pad.MidiNote}.wav");
                var wavPath = Path.Combine(samplesDir, safeName);
                CopyMappedSample(mapped, wavPath);
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
                pad_index = pad.PadIndex >= 0 ? pad.PadIndex : i,
                midi_note = pad.MidiNote >= 0 ? pad.MidiNote : (int?)null,
                midi_notes = midiNotes.Length > 0 ? midiNotes : null,
                output_group = pad.OutputGroup.ToString(),
                choke_group = pad.ChokeGroup > 0 ? pad.ChokeGroup : (int?)null,
                adsr = pad.Envelope.IsDefault
                    ? null
                    : new
                    {
                        attack_ms = pad.Envelope.AttackMs,
                        decay_ms = pad.Envelope.DecayMs,
                        sustain = pad.Envelope.SustainLevel,
                        release_ms = pad.Envelope.ReleaseMs,
                    },
                sample = relativeSample,
                velocity_layers = velocityLayers,
            });
        }

        var payload = new { name = kit.Name, pads = padEntries };
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        File.WriteAllText(jsonPath, json);
    }

    internal static void EnsurePadCount(KitPreset kit)
    {
        var sourcePads = kit.Pads.ToArray();
        var hasExplicitPadIndices = sourcePads.Any(static pad => pad.PadIndex >= 0);
        var byIndex = new Dictionary<int, DrumSample>();

        for (var sourceIndex = 0; sourceIndex < sourcePads.Length; sourceIndex++)
        {
            var sample = sourcePads[sourceIndex];
            var index = sample.PadIndex;
            if (index < 0 && !hasExplicitPadIndices)
            {
                index = sourcePads.Length < GmPercussionMap.Pads.Count
                    ? GmPercussionMap.Pads.FirstOrDefault(pad => pad.Note == sample.MidiNote)?.Index ?? -1
                    : sourceIndex;
            }

            if (index is >= 0 && index < GmPercussionMap.Pads.Count && !byIndex.ContainsKey(index))
            {
                byIndex[index] = sample;
            }
        }

        kit.Pads.Clear();
        foreach (var pad in GmPercussionMap.Pads)
        {
            if (byIndex.TryGetValue(pad.Index, out var existing))
            {
                existing.PadIndex = pad.Index;
                kit.Pads.Add(existing);
                continue;
            }

            kit.Pads.Add(new DrumSample
            {
                Label = pad.Label,
                MidiNote = pad.Note,
                MidiNotes = [pad.Note],
                PadIndex = pad.Index,
                OutputGroup = GmPercussionMap.GetMixGroup(pad.Note),
            });
        }
    }

    private static int ReadPadIndex(JsonElement pad) =>
        pad.TryGetProperty("pad_index", out var indexEl)
        && indexEl.ValueKind == JsonValueKind.Number
        && indexEl.TryGetInt32(out var index)
        && index is >= 0 and < 128
            ? index
            : -1;

    private static List<int> ReadMidiNotes(JsonElement pad, int legacyNote)
    {
        var notes = new List<int>();
        if (pad.TryGetProperty("midi_notes", out var notesEl) && notesEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var noteEl in notesEl.EnumerateArray())
            {
                if (noteEl.TryGetInt32(out var note) && note is >= 0 and <= 127)
                {
                    notes.Add(note);
                }
            }
        }

        if (notes.Count == 0 && legacyNote is >= 0 and <= 127)
        {
            notes.Add(legacyNote);
        }

        return notes.Distinct().Order().ToList();
    }

    private static DrumMixGroup ReadOutputGroup(JsonElement pad, int midiNote, int fallbackIndex)
    {
        if (pad.TryGetProperty("output_group", out var groupEl))
        {
            if (groupEl.ValueKind == JsonValueKind.String
                && Enum.TryParse<DrumMixGroup>(groupEl.GetString(), ignoreCase: true, out var named))
            {
                return named;
            }

            if (groupEl.ValueKind == JsonValueKind.Number
                && groupEl.TryGetInt32(out var numeric)
                && Enum.IsDefined((DrumMixGroup)numeric))
            {
                return (DrumMixGroup)numeric;
            }
        }

        if (midiNote is >= GmPercussionMap.FirstNote and <= GmPercussionMap.LastNote)
        {
            return GmPercussionMap.GetMixGroup(midiNote);
        }

        var defaultPad = GmPercussionMap.Pads.ElementAtOrDefault(fallbackIndex);
        return defaultPad is null ? DrumMixGroup.Percussion : GmPercussionMap.GetMixGroup(defaultPad.Note);
    }

    private static int CountSampleReferences(JsonElement pads)
    {
        var count = 0;
        foreach (var pad in pads.EnumerateArray())
        {
            if (pad.TryGetProperty("sample", out var sample) && sample.GetString() is { Length: > 0 })
            {
                count++;
            }

            if (!pad.TryGetProperty("velocity_layers", out var layers) || layers.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var layer in layers.EnumerateArray())
            {
                if (!layer.TryGetProperty("round_robin", out var roundRobin) || roundRobin.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                count += roundRobin.EnumerateArray().Count(static item => item.GetString() is { Length: > 0 });
            }
        }

        return count;
    }

    private static PadEnvelopeSettings ReadEnvelope(JsonElement pad)
    {
        if (!pad.TryGetProperty("adsr", out var adsr) || adsr.ValueKind != JsonValueKind.Object)
        {
            return new PadEnvelopeSettings();
        }

        return new PadEnvelopeSettings
        {
            AttackMs = ReadEnvelopeValue(adsr, "attack_ms", 0f, 0f, 5_000f),
            DecayMs = ReadEnvelopeValue(adsr, "decay_ms", 0f, 0f, 10_000f),
            SustainLevel = ReadEnvelopeValue(adsr, "sustain", 1f, 0f, 1f),
            ReleaseMs = ReadEnvelopeValue(adsr, "release_ms", 0f, 0f, 10_000f),
        };
    }

    private static float ReadEnvelopeValue(JsonElement adsr, string property, float fallback, float min, float max) =>
        adsr.TryGetProperty(property, out var value) && value.TryGetSingle(out var parsed)
            ? Math.Clamp(parsed, min, max)
            : fallback;

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

    private static void CopyMappedSample(MemoryMappedWavSample source, string destinationPath)
    {
        var sourcePath = Path.GetFullPath(source.FilePath);
        var destination = Path.GetFullPath(destinationPath);
        if (string.Equals(sourcePath, destination, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        File.Copy(sourcePath, destination, overwrite: true);
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
