using System.Text.Json;
using Rythmbox.Core.Models.Styles;

namespace Rythmbox.Core.Styles;

public static class StyleBankCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static StyleDefinition Load(string styleJsonPath)
    {
        var styleDir = Path.GetDirectoryName(styleJsonPath)
            ?? throw new StyleLoadException("Invalid style path.");

        if (!File.Exists(styleJsonPath))
        {
            throw new StyleLoadException($"Style file not found: {styleJsonPath}");
        }

        using var stream = File.OpenRead(styleJsonPath);
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        var errors = new List<string>();
        var warnings = new List<string>();

        var id = GetRequiredString(root, "id", errors) ?? Path.GetFileName(styleDir);
        var name = GetRequiredString(root, "name", errors) ?? id;
        var category = GetRequiredString(root, "category", errors) ?? "User Styles";

        var defaultTempo = GetDouble(root, "default_tempo", 120);
        var tempoRange = GetDoubleRange(root, "tempo_range", defaultTempo - 30, defaultTempo + 20);

        var patterns = ParsePatterns(root, styleDir, warnings, errors);

        var macros = ParseMacros(root);

        if (errors.Count > 0)
        {
            return new StyleDefinition
            {
                Id = id,
                Name = name,
                Category = category,
                StyleDirectory = styleDir,
                Patterns = patterns,
                Macros = macros,
                ValidationErrors = errors,
                ValidationWarnings = warnings,
            };
        }

        return new StyleDefinition
        {
            Id = id,
            Name = name,
            Category = category,
            Description = GetString(root, "description") ?? string.Empty,
            DefaultTempo = defaultTempo,
            TempoMin = tempoRange.min,
            TempoMax = tempoRange.max,
            TimeSignature = GetString(root, "time_signature") ?? "4/4",
            Feel = GetString(root, "feel") ?? "straight",
            DefaultKit = GetString(root, "default_kit") ?? "gm_standard",
            Tags = ParseTags(root),
            Patterns = patterns,
            Macros = macros,
            StyleDirectory = styleDir,
            ValidationWarnings = warnings,
            ValidationErrors = errors,
        };
    }

    private static IReadOnlyDictionary<string, StylePattern> ParsePatterns(
        JsonElement root,
        string styleDir,
        List<string> warnings,
        List<string> errors)
    {
        var result = new Dictionary<string, StylePattern>(StringComparer.OrdinalIgnoreCase);

        if (!root.TryGetProperty("patterns", out var patternsEl) || patternsEl.ValueKind != JsonValueKind.Object)
        {
            errors.Add("Missing required 'patterns' object.");
            return result;
        }

        foreach (var prop in patternsEl.EnumerateObject())
        {
            var id = prop.Name;
            var el = prop.Value;

            var name = GetString(el, "name") ?? id;
            var type = ParsePatternType(GetString(el, "type"), warnings, id);
            var variation = GetString(el, "variation") ?? string.Empty;
            var bars = el.TryGetProperty("bars", out var barsEl) && barsEl.TryGetInt32(out var b) ? b : 4;
            var oneShot = el.TryGetProperty("one_shot", out var oneShotEl) && oneShotEl.ValueKind == JsonValueKind.True;
            var midiRel = GetString(el, "midi");

            string? resolved = null;
            if (!string.IsNullOrWhiteSpace(midiRel))
            {
                resolved = ResolveMidiPath(styleDir, midiRel);
                if (resolved is null || !File.Exists(resolved))
                {
                    warnings.Add($"Pattern '{id}': MIDI file missing ({midiRel}).");
                }
            }
            else
            {
                warnings.Add($"Pattern '{id}': no MIDI path specified.");
            }

            var playbackMode = oneShot || type is PatternType.Fill or PatternType.Ending
                ? PatternPlaybackMode.OneShot
                : type is PatternType.Break
                    ? PatternPlaybackMode.OneShot
                    : PatternPlaybackMode.Loop;

            result[id] = new StylePattern
            {
                Id = id,
                Name = name,
                Type = type,
                Variation = variation,
                Bars = bars,
                OneShot = oneShot,
                PlaybackMode = playbackMode,
                MidiRelativePath = midiRel,
                ResolvedMidiPath = resolved,
            };
        }

        return result;
    }

    private static RhythmMacros ParseMacros(JsonElement root)
    {
        if (!root.TryGetProperty("macros", out var macrosEl) || macrosEl.ValueKind != JsonValueKind.Object)
        {
            return RhythmMacros.Default;
        }

        return new RhythmMacros
        {
            Complexity = GetFloat(macrosEl, "complexity", 0.45f),
            Energy = GetFloat(macrosEl, "energy", 0.55f),
            Swing = GetFloat(macrosEl, "swing", 0f),
            Humanize = GetFloat(macrosEl, "humanize", 0.12f),
        };
    }

    private static PatternType ParsePatternType(string? raw, List<string> warnings, string patternId)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            warnings.Add($"Pattern '{patternId}': missing type, using Custom.");
            return PatternType.Custom;
        }

        return raw.ToLowerInvariant() switch
        {
            "intro" => PatternType.Intro,
            "verse" => PatternType.Verse,
            "chorus" => PatternType.Chorus,
            "fill" => PatternType.Fill,
            "break" => PatternType.Break,
            "ending" => PatternType.Ending,
            _ => PatternType.Custom,
        };
    }

    private static string? ResolveMidiPath(string styleDir, string relative)
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(styleDir, relative)),
            Path.GetFullPath(relative),
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static IReadOnlyList<string> ParseTags(JsonElement root)
    {
        if (!root.TryGetProperty("tags", out var tagsEl) || tagsEl.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return tagsEl.EnumerateArray()
            .Select(t => t.GetString())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Cast<string>()
            .ToList();
    }

    private static string? GetRequiredString(JsonElement root, string name, List<string> errors)
    {
        var value = GetString(root, name);
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"Missing required field '{name}'.");
            return null;
        }

        return value;
    }

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    private static double GetDouble(JsonElement root, string name, double fallback) =>
        root.TryGetProperty(name, out var el) && el.TryGetDouble(out var v) ? v : fallback;

    private static (double min, double max) GetDoubleRange(JsonElement root, string name, double defaultMin, double defaultMax)
    {
        if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Array)
        {
            return (defaultMin, defaultMax);
        }

        var values = el.EnumerateArray().Select(e => e.GetDouble()).ToList();
        if (values.Count < 2)
        {
            return (defaultMin, defaultMax);
        }

        return (values[0], values[1]);
    }

    private static float GetFloat(JsonElement root, string name, float fallback) =>
        root.TryGetProperty(name, out var el) && el.TryGetSingle(out var v) ? v : fallback;
}

public sealed class StyleLoadException : Exception
{
    public StyleLoadException(string message) : base(message) { }
}
