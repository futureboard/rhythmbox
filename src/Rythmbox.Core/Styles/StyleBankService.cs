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

        foreach (var styleJson in Directory.EnumerateFiles(stylesRoot, "style.json", SearchOption.AllDirectories))
        {
            TryAddStyle(styles, categories, () => StyleBankCodec.Load(styleJson), styleJson);
        }

        foreach (var packedStyle in Directory.EnumerateFiles(stylesRoot, $"*{RhmStyleCodec.Extension}", SearchOption.AllDirectories))
        {
            TryAddStyle(styles, categories, () => RhmStyleCodec.Load(packedStyle), packedStyle);
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
}
