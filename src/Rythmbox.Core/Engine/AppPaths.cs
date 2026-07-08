namespace Rythmbox.Core.Engine;

/// <summary>
/// Resolves shared asset folders from the repo root (mirrors the old C++ <c>shared/</c> layout).
/// Walks upward from the app base directory until it finds a <c>shared/RYTHM</c> folder.
/// </summary>
public sealed class AppPaths
{
    public AppPaths()
    {
        RootDir = FindRootDirectory();
        if (RootDir is null)
        {
            return;
        }

        SharedDir = Path.Combine(RootDir, "shared");
        ContentDir = Path.Combine(RootDir, "Content");
        RythmDir = Path.Combine(SharedDir, "RYTHM");
        SubMidiDir = Path.Combine(SharedDir, "SUBMIDI");
        PresetDir = Path.Combine(SharedDir, "PRESETS");
        SamplesDir = Path.Combine(SharedDir, "SAMPLES");
        StylesDir = Path.Combine(ContentDir, "Styles");
        KitsDir = Path.Combine(ContentDir, "Kits");
        UserStylesDir = Path.Combine(ContentDir, "User", "Styles");
    }

    public string? RootDir { get; }

    public string? SharedDir { get; }

    public string? ContentDir { get; }

    public string? RythmDir { get; }

    public string? SubMidiDir { get; }

    public string? PresetDir { get; }

    public string? SamplesDir { get; }

    public string? StylesDir { get; }

    public string? KitsDir { get; }

    public string? UserStylesDir { get; }

    public bool HasRythmLibrary => RythmDir is not null && Directory.Exists(RythmDir);

    public bool HasStyleLibrary => StylesDir is not null && Directory.Exists(StylesDir);

    private static string? FindRootDirectory()
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
