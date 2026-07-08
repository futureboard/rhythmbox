using Rythmbox.Core.Formats;

namespace Rythmbox.Core.Engine;

/// <summary>Scans <c>shared/PRESETS/*.json</c> and <c>*.apak</c> for available drum kits.</summary>
public sealed class KitPresetService
{
    public IReadOnlyList<KitPresetEntry> Scan(string? presetDir)
    {
        if (presetDir is null || !Directory.Exists(presetDir))
        {
            return [];
        }

        var entries = new List<KitPresetEntry>();

        entries.AddRange(Directory.EnumerateFiles(presetDir, "*.json")
            .Where(path => !string.Equals(Path.GetFileName(path), "tempo.json", StringComparison.OrdinalIgnoreCase))
            .Select(path => new KitPresetEntry(Path.GetFileNameWithoutExtension(path), path, KitPresetKind.Json)));

        entries.AddRange(Directory.EnumerateFiles(presetDir, $"*{ApakCodec.Extension}")
            .Select(path => new KitPresetEntry(Path.GetFileNameWithoutExtension(path), path, KitPresetKind.Apak)));

        return entries
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

public enum KitPresetKind
{
    Json,
    Apak,
}

public sealed record KitPresetEntry(string Name, string FilePath, KitPresetKind Kind = KitPresetKind.Json);
