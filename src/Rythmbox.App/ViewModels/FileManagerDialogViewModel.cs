using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Rythmbox.App.ViewModels;

public sealed partial class FileManagerDialogViewModel : ViewModelBase
{
    private readonly LocalizationViewModel _localization;
    private TaskCompletionSource<string?>? _tcs;
    private IReadOnlyList<string>? _extensions;

    public FileManagerDialogViewModel(FileManagerViewModel browser, LocalizationViewModel localization)
    {
        Browser = browser;
        _localization = localization;
    }

    public FileManagerViewModel Browser { get; }

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private FileManagerDialogMode _mode;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private bool _showFileName;

    [ObservableProperty]
    private string _confirmLabel = string.Empty;

    [ObservableProperty]
    private string _cancelLabel = string.Empty;

    public Task<string?> PickFolderAsync(string? startPath, string title) =>
        ShowAsync(FileManagerDialogMode.OpenFolder, startPath, title, null);

    public Task<string?> PickFileAsync(string? startPath, string title, IReadOnlyList<string>? extensions = null) =>
        ShowAsync(FileManagerDialogMode.OpenFile, startPath, title, extensions);

    public Task<string?> SaveFileAsync(string? startPath, string title, string defaultFileName, IReadOnlyList<string>? extensions = null)
    {
        FileName = defaultFileName;
        return ShowAsync(FileManagerDialogMode.SaveFile, startPath, title, extensions);
    }

    private Task<string?> ShowAsync(
        FileManagerDialogMode mode,
        string? startPath,
        string title,
        IReadOnlyList<string>? extensions)
    {
        _tcs = new TaskCompletionSource<string?>();
        Mode = mode;
        Title = title;
        _extensions = extensions;
        ShowFileName = mode == FileManagerDialogMode.SaveFile;
        ConfirmLabel = mode switch
        {
            FileManagerDialogMode.OpenFolder => _localization.FilesSelectFolder,
            FileManagerDialogMode.OpenFile => _localization.FilesOpen,
            FileManagerDialogMode.SaveFile => _localization.FilesSave,
            _ => "OK",
        };
        CancelLabel = _localization.FilesCancel;

        Browser.SetExtensionFilter(extensions);
        Browser.Prepare(startPath);
        IsOpen = true;
        return _tcs.Task;
    }

    [RelayCommand]
    private void Confirm()
    {
        string? result = Mode switch
        {
            FileManagerDialogMode.OpenFolder => Browser.CurrentPath,
            FileManagerDialogMode.OpenFile => Browser.SelectedEntry is { IsDirectory: false } entry
                ? entry.FullPath
                : null,
            FileManagerDialogMode.SaveFile => BuildSavePath(),
            _ => Browser.SelectedEntry?.FullPath,
        };

        if (result is null && Mode is FileManagerDialogMode.OpenFile or FileManagerDialogMode.SaveFile)
        {
            Browser.StatusText = Mode == FileManagerDialogMode.SaveFile
                ? "Enter a file name"
                : "Select a file";
            return;
        }

        Close(result);
    }

    [RelayCommand]
    private void Cancel() => Close(null);

    public void Close(string? result)
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;
        _tcs?.TrySetResult(result);
        _tcs = null;
    }

    private string? BuildSavePath()
    {
        if (string.IsNullOrWhiteSpace(FileName))
        {
            return null;
        }

        var name = FileName.Trim();
        if (_extensions is { Count: 1 } && !name.Contains('.'))
        {
            name += _extensions[0].StartsWith('.') ? _extensions[0] : $".{_extensions[0]}";
        }

        return Path.Combine(Browser.CurrentPath, name);
    }
}
