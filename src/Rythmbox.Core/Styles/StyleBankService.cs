using Rythmbox.Core.Formats;
using Rythmbox.Core.Models.Styles;

namespace Rythmbox.Core.Styles;

/// <summary>Scans <c>Content/Styles</c> for arranger style definitions.</summary>
public sealed class StyleBankService
{
    public static readonly IReadOnlyList<string> FactoryCategories =
    [
        "Pop",
        "Rock",
        "Funk / Soul",
        "EDM",
        "Hip-Hop",
        "Latin",
        "Jazz",
        "Thai / Asian",
        "User Styles",
    ];

    public IReadOnlyList<StyleDefinition> AllStyles { get; private set; } = [];

    public IReadOnlyList<string> Categories { get; private set; } = FactoryCategories.ToList();

    public void Scan(string? stylesRoot)
    {
        if (stylesRoot is null || !Directory.Exists(stylesRoot))
        {
            AllStyles = [];
            Categories = FactoryCategories.ToList();
            return;
        }

        var styles = new List<StyleDefinition>();
        var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var styleJson in EnumerateFiles(stylesRoot, "style.json"))
        {
            TryAddStyle(styles, categories, () => StyleBankCodec.Load(styleJson), styleJson);
        }

        foreach (var packedStyle in EnumerateFiles(stylesRoot, $"*{RhmStyleCodec.Extension}"))
        {
            TryAddStyle(styles, categories, () => RhmStyleCodec.Load(packedStyle), packedStyle);
        }

        foreach (var sourcePath in EnumerateFiles(stylesRoot, "*.*")
                     .Where(IsRawStyleSource))
        {
            // A manifest is authoritative for its own directory. Standalone
            // scan remains recursive for Factory and Users trees without
            // duplicating simple, manifest-backed style folders.
            var sourceDirectory = Path.GetDirectoryName(sourcePath) ?? stylesRoot;
            if (File.Exists(Path.Combine(sourceDirectory, "style.json")))
            {
                continue;
            }

            var style = CreateStandaloneStyle(stylesRoot, sourcePath);
            styles.Add(style);
            categories.Add(style.Category);
        }

        AllStyles = styles.OrderBy(s => s.Category).ThenBy(s => s.Name).ToList();

        foreach (var cat in FactoryCategories)
        {
            categories.Add(cat);
        }

        Categories = categories.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public IReadOnlyList<StyleDefinition> GetStylesInCategory(string category) =>
        AllStyles.Where(s => string.Equals(s.Category, category, StringComparison.OrdinalIgnoreCase)).ToList();

    public StyleDefinition? FindById(string id) =>
        AllStyles.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));

    private static void TryAddStyle(
        List<StyleDefinition> styles,
        HashSet<string> categories,
        Func<StyleDefinition> load,
        string sourcePath)
    {
        try
        {
            var style = load();
            styles.Add(style);
            categories.Add(style.Category);
        }
        catch (StyleLoadException ex)
        {
            AddBrokenStyle(styles, sourcePath, ex.Message);
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or FormatException)
        {
            AddBrokenStyle(styles, sourcePath, ex.Message);
        }
    }

    private static void AddBrokenStyle(List<StyleDefinition> styles, string sourcePath, string message)
    {
        var name = Path.GetFileNameWithoutExtension(sourcePath);
        styles.Add(new StyleDefinition
        {
            Id = name,
            Name = name,
            Category = "User Styles",
            StyleDirectory = Path.GetDirectoryName(sourcePath) ?? sourcePath,
            Patterns = new Dictionary<string, StylePattern>(),
            ValidationErrors = [message],
        });
    }

    private static bool IsRawStyleSource(string path) =>
        Path.GetExtension(path) is var extension
        && (extension.Equals(".mid", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".rhythm", StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> EnumerateFiles(string root, string pattern) =>
        Directory.EnumerateFiles(root, pattern, new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
        });

    private static StyleDefinition CreateStandaloneStyle(string stylesRoot, string sourcePath)
    {
        var relative = Path.GetRelativePath(stylesRoot, sourcePath);
        var category = InferCategory(relative);
        var name = Path.GetFileNameWithoutExtension(sourcePath);
        var extension = Path.GetExtension(sourcePath);
        var id = "raw_" + relative
            .Replace(Path.DirectorySeparatorChar, '_')
            .Replace(Path.AltDirectorySeparatorChar, '_')
            .Replace(' ', '_');

        return new StyleDefinition
        {
            Id = id,
            Name = name,
            Category = category,
            StyleDirectory = Path.GetDirectoryName(sourcePath) ?? stylesRoot,
            Tags = ["raw", extension.TrimStart('.').ToLowerInvariant()],
            Patterns = new Dictionary<string, StylePattern>(StringComparer.OrdinalIgnoreCase)
            {
                // Raw files have no manifest. Map them to Verse A so the
                // existing Machine pad layout can trigger them immediately.
                ["verse_a"] = new StylePattern
                {
                    Id = "verse_a",
                    Name = name,
                    Type = PatternType.Custom,
                    PlaybackMode = PatternPlaybackMode.Loop,
                    MidiRelativePath = Path.GetFileName(sourcePath),
                    ResolvedMidiPath = sourcePath,
                },
            },
        };
    }

    private static string InferCategory(string relativePath)
    {
        var parts = relativePath.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && parts[0].Equals("Factory", StringComparison.OrdinalIgnoreCase))
        {
            return parts[1] switch
            {
                "Funk-Soul" => "Funk / Soul",
                "Thai-Asian" => "Thai / Asian",
                _ => parts[1],
            };
        }

        return parts.Length > 1 && parts[0].Equals("Users", StringComparison.OrdinalIgnoreCase)
            ? "User Styles"
            : "User Styles";
    }
}
