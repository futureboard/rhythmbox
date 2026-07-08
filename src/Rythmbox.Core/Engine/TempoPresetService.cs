using System.Text.Json;
using Rythmbox.Core.Models;

namespace Rythmbox.Core.Engine;

/// <summary>Loads named tempo presets from <c>shared/PRESETS/tempo.json</c> (old DrumStage format).</summary>
public sealed class TempoPresetService
{
    public IReadOnlyList<TempoPreset> Presets { get; private set; } = [];

    public void Load(string? presetDir)
    {
        if (presetDir is null)
        {
            Presets = [];
            return;
        }

        var path = Path.Combine(presetDir, "tempo.json");
        if (!File.Exists(path))
        {
            Presets = DefaultPresets();
            return;
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var doc = JsonDocument.Parse(stream);
            var list = new List<TempoPreset>();

            if (doc.RootElement.TryGetProperty("presets", out var presets) &&
                presets.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in presets.EnumerateArray())
                {
                    var name = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
                    var value = item.TryGetProperty("value", out var valEl) && valEl.TryGetDouble(out var bpm)
                        ? bpm
                        : 0;

                    if (!string.IsNullOrWhiteSpace(name) && value is >= 1 and <= 999)
                    {
                        list.Add(new TempoPreset(name!, value));
                    }
                }
            }

            Presets = list.Count > 0 ? list : DefaultPresets();
        }
        catch
        {
            Presets = DefaultPresets();
        }
    }

    private static IReadOnlyList<TempoPreset> DefaultPresets() =>
    [
        new("Ballad", 80),
        new("Rock", 120),
        new("Dance", 128),
        new("Fast", 160),
    ];
}
