namespace Rythmbox.App.ViewModels;

public sealed class FileEntryViewModel
{
    public FileEntryViewModel(string name, string fullPath, bool isDirectory, long sizeBytes, DateTime modifiedUtc)
    {
        Name = name;
        FullPath = fullPath;
        IsDirectory = isDirectory;
        SizeLabel = isDirectory ? "—" : FormatSize(sizeBytes);
        ModifiedLabel = modifiedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    }

    public string Name { get; }

    public string FullPath { get; }

    public bool IsDirectory { get; }

    public string SizeLabel { get; }

    public string ModifiedLabel { get; }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:0.#} KB";
        }

        if (bytes < 1024L * 1024 * 1024)
        {
            return $"{bytes / (1024.0 * 1024):0.#} MB";
        }

        return $"{bytes / (1024.0 * 1024 * 1024):0.#} GB";
    }
}
