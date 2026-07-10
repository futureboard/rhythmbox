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
        if (Directory.Exists(_paths.ApplicationDataDir))
        {
            return _paths.ApplicationDataDir;
        }

        var userDirectory = GetUserDirectory();
        if (Directory.Exists(userDirectory))
        {
            return userDirectory;
        }

        return AppContext.BaseDirectory;
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
        AddShortcut("User Folder", GetUserDirectory());
        AddShortcut("Application Data", _paths.ApplicationDataDir);

        if (OperatingSystem.IsWindows())
        {
            AddWindowsDriveShortcuts();
        }
        else if (OperatingSystem.IsLinux())
        {
            AddShortcutIfExists("Media", "/run/media");
        }
        else if (OperatingSystem.IsMacOS())
        {
            AddShortcutIfExists("Volumes", "/Volumes");
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

    private void AddWindowsDriveShortcuts()
    {
        foreach (var drive in DriveInfo.GetDrives().OrderBy(drive => drive.Name, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (!drive.IsReady)
                {
                    continue;
                }

                var label = drive.Name.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                AddShortcut(label, drive.RootDirectory.FullName);
            }
            catch (IOException)
            {
                // Removable and optical drives can disappear while the list is built.
            }
            catch (UnauthorizedAccessException)
            {
                // Keep the remaining drives usable when one root is restricted.
            }
        }
    }

    private static string GetUserDirectory()
    {
        var userDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrWhiteSpace(userDirectory)
            ? Environment.GetEnvironmentVariable("HOME") ?? AppContext.BaseDirectory
            : userDirectory;
    }

    private void AddShortcutIfExists(string label, string path)
    {
        if (Directory.Exists(path))
        {
            AddShortcut(label, path);
        }
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
