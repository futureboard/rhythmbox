namespace Rythmbox.Core.Engine;

/// <summary>
/// Resolves RhythmLive content folders under Documents, with dev-repo fallbacks.
/// </summary>
public sealed class AppPaths
{
    public const string ProductFolderName = "Futureboard Studio";
    public const string AppFolderName = "Rhythmlive";

    public AppPaths()
    {
        var rhythmliveDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            ProductFolderName,
            AppFolderName);

        StylesDir = EnsureDirectory(Path.Combine(rhythmliveDir, "Styles"));
        PresetDir = EnsureDirectory(Path.Combine(rhythmliveDir, "Presets"));
        SamplesDir = EnsureDirectory(Path.Combine(rhythmliveDir, "Samplepacks"));
        KitsDir = SamplesDir;
        UserStylesDir = StylesDir;

        RootDir = FindDevRootDirectory();
        if (RootDir is not null)
        {
            SharedDir = Path.Combine(RootDir, "shared");
            ContentDir = Path.Combine(RootDir, "Content");
            RythmDir = Path.Combine(SharedDir, "RYTHM");
            SubMidiDir = Path.Combine(SharedDir, "SUBMIDI");

            if (!HasStyleContent(StylesDir))
            {
                var devStyles = Path.Combine(ContentDir, "Styles");
                if (Directory.Exists(devStyles))
                {
                    StylesDir = devStyles;
                }
            }

            if (!HasPresetContent(PresetDir))
            {
                var devPresets = Path.Combine(SharedDir, "PRESETS");
                if (Directory.Exists(devPresets))
                {
                    PresetDir = devPresets;
                }
            }

            if (!HasSampleContent(SamplesDir))
            {
                var devSamples = Path.Combine(SharedDir, "SAMPLES");
                if (Directory.Exists(devSamples))
                {
                    SamplesDir = devSamples;
                    KitsDir = SamplesDir;
                }
            }
        }
        else
        {
            SharedDir = null;
            ContentDir = null;
            RythmDir = null;
            SubMidiDir = null;
        }
    }

    public string? RootDir { get; }

    public string? SharedDir { get; }

    public string? ContentDir { get; }

    public string? RythmDir { get; }

    public string? SubMidiDir { get; }

    public string PresetDir { get; }

    public string SamplesDir { get; }

    public string StylesDir { get; }

    public string KitsDir { get; }

    public string UserStylesDir { get; }

    public bool HasRythmLibrary => RythmDir is not null && Directory.Exists(RythmDir);

    public bool HasStyleLibrary => Directory.Exists(StylesDir) && HasStyleContent(StylesDir);

    public static string RhythmliveRoot =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            ProductFolderName,
            AppFolderName);

    private static string EnsureDirectory(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }

    private static bool HasStyleContent(string dir) =>
        Directory.Exists(dir)
        && (Directory.EnumerateFiles(dir, "style.json", SearchOption.AllDirectories).Any()
            || Directory.EnumerateFiles(dir, "*.rhmsty", SearchOption.AllDirectories).Any());

    private static bool HasPresetContent(string dir) =>
        Directory.Exists(dir)
        && Directory.EnumerateFiles(dir, "*.json").Any();

    private static bool HasSampleContent(string dir) =>
        Directory.Exists(dir)
        && (Directory.EnumerateFiles(dir, "*.wav", SearchOption.AllDirectories).Any()
            || Directory.EnumerateFiles(dir, "*.apak", SearchOption.AllDirectories).Any());

    private static string? FindDevRootDirectory()
    {
        var dir = AppContext.BaseDirectory;

        for (var i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
        {
            if (Directory.Exists(Path.Combine(dir, "shared", "RYTHM"))
                || Directory.Exists(Path.Combine(dir, "Content", "Styles")))
            {
                return dir;
            }

            var parent = Directory.GetParent(dir)?.FullName;
            if (parent is null || parent == dir)
            {
                break;
            }

            dir = parent;
        }

        return null;
    }
}
