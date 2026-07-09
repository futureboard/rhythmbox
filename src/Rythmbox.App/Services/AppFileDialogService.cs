using Rythmbox.App.ViewModels;
using Rythmbox.Core.Services;

namespace Rythmbox.App.Services;

public sealed class AppFileDialogService(FileManagerDialogViewModel dialog) : IFileDialogService
{
    public Task<string?> PickFolderAsync(string? startPath, string title) =>
        dialog.PickFolderAsync(startPath, title);

    public Task<string?> PickFileAsync(string? startPath, string title, IReadOnlyList<string>? extensions = null) =>
        dialog.PickFileAsync(startPath, title, extensions);

    public Task<string?> SaveFileAsync(
        string? startPath,
        string title,
        string defaultFileName,
        IReadOnlyList<string>? extensions = null) =>
        dialog.SaveFileAsync(startPath, title, defaultFileName, extensions);
}
