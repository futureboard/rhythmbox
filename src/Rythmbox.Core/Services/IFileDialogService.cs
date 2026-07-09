namespace Rythmbox.Core.Services;

/// <summary>Cross-platform in-app file/folder picker (implemented by the host application).</summary>
public interface IFileDialogService
{
    Task<string?> PickFolderAsync(string? startPath, string title);

    Task<string?> PickFileAsync(string? startPath, string title, IReadOnlyList<string>? extensions = null);

    Task<string?> SaveFileAsync(
        string? startPath,
        string title,
        string defaultFileName,
        IReadOnlyList<string>? extensions = null);
}
