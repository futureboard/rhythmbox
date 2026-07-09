using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Rythmbox.Core.Services;

namespace Rythmbox.Editor.Services;

/// <summary>Fallback picker for standalone Editor/Sample Creator executables (no embedded file manager).</summary>
public sealed class StorageFileDialogService(Func<TopLevel?> topLevelProvider) : IFileDialogService
{
    public async Task<string?> PickFolderAsync(string? startPath, string title)
    {
        var storage = topLevelProvider()?.StorageProvider;
        if (storage is null)
        {
            return null;
        }

        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickFileAsync(string? startPath, string title, IReadOnlyList<string>? extensions = null)
    {
        var storage = topLevelProvider()?.StorageProvider;
        if (storage is null)
        {
            return null;
        }

        var patterns = ToPatterns(extensions);
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(title) { Patterns = patterns },
            ],
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> SaveFileAsync(
        string? startPath,
        string title,
        string defaultFileName,
        IReadOnlyList<string>? extensions = null)
    {
        var storage = topLevelProvider()?.StorageProvider;
        if (storage is null)
        {
            return null;
        }

        var patterns = ToPatterns(extensions);
        var defaultExtension = extensions is { Count: > 0 } ext
            ? ext[0].TrimStart('.')
            : Path.GetExtension(defaultFileName).TrimStart('.');

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = defaultFileName,
            DefaultExtension = string.IsNullOrEmpty(defaultExtension) ? null : defaultExtension,
            SuggestedStartLocation = startPath is not null
                ? await storage.TryGetFolderFromPathAsync(startPath)
                : null,
            FileTypeChoices =
            [
                new FilePickerFileType(title) { Patterns = patterns },
            ],
        });

        return file?.TryGetLocalPath();
    }

    private static List<string> ToPatterns(IReadOnlyList<string>? extensions)
    {
        if (extensions is null or { Count: 0 })
        {
            return ["*"];
        }

        return extensions
            .Select(ext => ext.StartsWith('.') ? $"*{ext}" : ext.StartsWith('*') ? ext : $"*.{ext}")
            .ToList();
    }
}
