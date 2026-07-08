using System.Collections.Concurrent;
using System.Text.Json;
using Avalonia.Platform;

namespace Rythmbox.App.Localization;

public sealed class LocalizationService
{
    public const string BarlowAnuphanFont =
        "avares://Rythmbox.App/Assets/Fonts#Barlow, avares://Rythmbox.App/Assets/Fonts#Anuphan";

    private readonly ConcurrentDictionary<string, string> _strings = new(StringComparer.Ordinal);

    public event EventHandler? LanguageChanged;

    public AppLanguage Language { get; private set; } = AppLanguage.English;

    public string FontFamily => BarlowAnuphanFont;

    public string this[string key] => Get(key);

    public string Get(string key) =>
        _strings.TryGetValue(key, out var value) ? value : key;

    public string Format(string key, params object[] args)
    {
        var template = Get(key);
        try
        {
            return string.Format(template, args);
        }
        catch
        {
            return template;
        }
    }

    public void SetLanguage(AppLanguage language)
    {
        if (language == Language && _strings.Count > 0)
        {
            return;
        }

        Language = language;
        Load(language);
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Load(AppLanguage language)
    {
        _strings.Clear();
        var code = language == AppLanguage.Thai ? "th" : "en";
        var uri = new Uri($"avares://Rythmbox.App/Localization/Strings.{code}.json");

        using var stream = AssetLoader.Open(uri);
        using var doc = JsonDocument.Parse(stream);

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            _strings[prop.Name] = prop.Value.GetString() ?? prop.Name;
        }
    }
}
