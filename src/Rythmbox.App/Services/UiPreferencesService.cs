using System.Text.Json;
using Rythmbox.Core.Engine;

namespace Rythmbox.App.Services;

/// <summary>
/// Persists lightweight UI preferences (currently the in-app scale factor) to a
/// JSON file under the application data directory. This is the application's own
/// zoom level — distinct from the operating-system / monitor DPI scaling — so the
/// same layout can be tuned to fit embedded panels, kiosks or high-density screens.
/// </summary>
public sealed class UiPreferencesService
{
    public const double MinScale = 0.75;
    public const double MaxScale = 2.0;
    public const double ScaleStep = 0.05;
    public const double DefaultScale = 1.0;

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _filePath;

    public UiPreferencesService()
    {
        _filePath = Path.Combine(AppPaths.ApplicationDataRoot, "ui-preferences.json");
    }

    public double AppScale { get; private set; } = DefaultScale;

    public void Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return;
            }

            var json = File.ReadAllText(_filePath);
            var data = JsonSerializer.Deserialize<UiPreferencesData>(json);
            if (data is not null)
            {
                AppScale = ClampScale(data.AppScale);
            }
        }
        catch
        {
            // Corrupt or unreadable preferences fall back to defaults.
            AppScale = DefaultScale;
        }
    }

    public void SetAppScale(double value)
    {
        AppScale = ClampScale(value);
        Save();
    }

    public static double ClampScale(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return DefaultScale;
        }

        return Math.Round(Math.Clamp(value, MinScale, MaxScale), 2);
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            var data = new UiPreferencesData { AppScale = AppScale };
            File.WriteAllText(_filePath, JsonSerializer.Serialize(data, SerializerOptions));
        }
        catch
        {
            // Preferences are best-effort; ignore write failures (read-only media, etc.).
        }
    }

    private sealed class UiPreferencesData
    {
        public double AppScale { get; set; } = DefaultScale;
    }
}
