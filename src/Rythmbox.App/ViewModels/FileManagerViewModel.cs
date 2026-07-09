using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.Core.Engine;

namespace Rythmbox.App.ViewModels;

public sealed partial class FileManagerViewModel : ViewModelBase
{
    private readonly AppPaths _paths;
    private IReadOnlyList<string>? _extensionFilter;

    public FileManagerViewModel(AppPaths paths)
    {
        _paths = paths;
    }

    public ObservableCollection<FileEntryViewModel> Entries { get; } = new();

    public ObservableCollection<FileShortcutViewModel> Shortcuts { get; } = new();

    [ObservableProperty]
    private string _currentPath = string.Empty;

    [ObservableProperty]
    private FileEntryViewModel? _selectedEntry;

    [ObservableProperty]
    private FileShortcutViewModel? _selectedShortcut;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _canGoUp;

    public void Initialize() => Prepare(null);

    public void Prepare(string? startPath)
    {
        if (Shortcuts.Count == 0)
        {
            BuildShortcuts();
        }

        NavigateTo(startPath ?? GetDefaultStartPath());
    }

    public string GetDefaultStartPath()
    {
        if (_paths.SharedDir is not null && Directory.Exists(_paths.SharedDir))
        {
            return _paths.SharedDir;
        }

        var appDir = AppContext.BaseDirectory;
        if (Directory.Exists(appDir))
        {
            return appDir;
        }

        return AppPaths.RhythmliveRoot;
    }

    public void SetExtensionFilter(IReadOnlyList<string>? extensions)
    {
        _extensionFilter = extensions?.Select(NormalizeExtension).ToList();
        if (!string.IsNullOrEmpty(CurrentPath))
        {
            LoadDirectory(CurrentPath);
        }
    }

    public void NavigateTo(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!Directory.Exists(fullPath))
            {
                StatusText = "Folder not found";
                return;
            }

            LoadDirectory(fullPath);
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoUp))]
    private void GoUp()
    {
        var parent = Directory.GetParent(CurrentPath)?.FullName;
        if (parent is not null)
        {
            NavigateTo(parent);
        }
    }

    [RelayCommand]
    private void Refresh() => NavigateTo(CurrentPath);

    [RelayCommand]
    private void OpenSelected()
    {
        if (SelectedEntry is { IsDirectory: true } entry)
        {
            NavigateTo(entry.FullPath);
        }
    }

    partial void OnSelectedShortcutChanged(FileShortcutViewModel? value)
    {
        if (value is null)
        {
            return;
        }

        NavigateTo(value.Path);
        SelectedShortcut = null;
    }

    partial void OnCanGoUpChanged(bool value) => GoUpCommand.NotifyCanExecuteChanged();

    private void BuildShortcuts()
    {
        Shortcuts.Clear();
        AddShortcut("RhythmLive", AppPaths.RhythmliveRoot);

        if (_paths.SharedDir is { } shared && Directory.Exists(shared))
        {
            AddShortcut("shared", shared);
        }

        if (_paths.PresetDir is { } presets && Directory.Exists(presets))
        {
            AddShortcut("Presets", presets);
        }

        if (_paths.SamplesDir is { } samples && Directory.Exists(samples))
        {
            AddShortcut("Samples", samples);
        }

        if (_paths.StylesDir is { } styles && Directory.Exists(styles))
        {
            AddShortcut("Styles", styles);
        }

        if (_paths.RythmDir is { } rythm && Directory.Exists(rythm))
        {
            AddShortcut("RYTHM", rythm);
        }

        var appDir = AppContext.BaseDirectory;
        if (Directory.Exists(appDir))
        {
            AddShortcut("App", appDir);
        }
    }

    private void AddShortcut(string label, string path)
    {
        if (Shortcuts.Any(s => string.Equals(s.Path, path, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        Shortcuts.Add(new FileShortcutViewModel(label, path));
    }

    private void LoadDirectory(string fullPath)
    {
        Entries.Clear();
        CurrentPath = fullPath;
        CanGoUp = Directory.GetParent(fullPath) is not null;

        var directories = new List<FileEntryViewModel>();
        var files = new List<FileEntryViewModel>();

        try
        {
            foreach (var dir in Directory.EnumerateDirectories(fullPath))
            {
                var info = new DirectoryInfo(dir);
                directories.Add(new FileEntryViewModel(
                    info.Name,
                    info.FullName,
                    isDirectory: true,
                    sizeBytes: 0,
                    modifiedUtc: info.LastWriteTimeUtc));
            }

            foreach (var file in Directory.EnumerateFiles(fullPath))
            {
                if (_extensionFilter is { Count: > 0 }
                    && !_extensionFilter.Contains(NormalizeExtension(Path.GetExtension(file))))
                {
                    continue;
                }

                var info = new FileInfo(file);
                files.Add(new FileEntryViewModel(
                    info.Name,
                    info.FullName,
                    isDirectory: false,
                    sizeBytes: info.Length,
                    modifiedUtc: info.LastWriteTimeUtc));
            }

            directories.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            files.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            foreach (var entry in directories)
            {
                Entries.Add(entry);
            }

            foreach (var entry in files)
            {
                Entries.Add(entry);
            }

            StatusText = $"{directories.Count} folders, {files.Count} files";
        }
        catch (UnauthorizedAccessException)
        {
            StatusText = "Access denied";
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
    }

    private static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        return extension.StartsWith('.') ? extension.ToLowerInvariant() : $".{extension.ToLowerInvariant()}";
    }
}
