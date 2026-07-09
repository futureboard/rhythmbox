namespace Rythmbox.App.ViewModels;

public sealed class FileShortcutViewModel
{
    public FileShortcutViewModel(string label, string path)
    {
        Label = label;
        Path = path;
    }

    public string Label { get; }

    public string Path { get; }
}
