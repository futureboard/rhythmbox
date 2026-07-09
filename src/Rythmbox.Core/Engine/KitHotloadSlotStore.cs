using System.Text.Json;

namespace Rythmbox.Core.Engine;

/// <summary>Persists user-assigned drum kit hotload slots (A–D) under Documents/Rhythmlive.</summary>
public static class KitHotloadSlotStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string StorePath => Path.Combine(AppPaths.RhythmliveRoot, "hotload-slots.json");

    public static IReadOnlyDictionary<string, string?> Load(IReadOnlyList<string> slotNames)
    {
        var result = slotNames.ToDictionary(static name => name, static _ => (string?)null, StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(StorePath))
        {
            return result;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(StorePath));
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (!result.ContainsKey(prop.Name))
                {
                    continue;
                }

                result[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                    ? prop.Value.GetString()
                    : null;
            }
        }
        catch (JsonException)
        {
        }

        return result;
    }

    public static void Save(IReadOnlyDictionary<string, string?> slots)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StorePath) ?? AppPaths.RhythmliveRoot);
        File.WriteAllText(StorePath, JsonSerializer.Serialize(slots, JsonOptions));
    }
}
