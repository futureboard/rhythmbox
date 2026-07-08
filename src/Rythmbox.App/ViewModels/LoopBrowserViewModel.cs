using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.Core.Engine;

namespace Rythmbox.App.ViewModels;

public sealed partial class LoopBrowserViewModel : ViewModelBase
{
    private readonly LoopLibraryService _library;
    private readonly PlayerViewModel _player;

    public LoopBrowserViewModel(LoopLibraryService library, PlayerViewModel player)
    {
        _library = library;
        _player = player;
    }

    public ObservableCollection<LoopEntryViewModel> Loops { get; } = new();

    [ObservableProperty]
    private string _folderLabel = "(no folder selected)";

    [ObservableProperty]
    private LoopEntryViewModel? _selectedLoop;

    public int SelectedIndexDisplay => SelectedLoop is null ? 0 : Loops.IndexOf(SelectedLoop) + 1;

    public int TotalCount => Loops.Count;

    partial void OnSelectedLoopChanged(LoopEntryViewModel? value)
    {
        foreach (var loop in Loops)
        {
            loop.IsSelected = ReferenceEquals(loop, value);
        }

        OnPropertyChanged(nameof(SelectedIndexDisplay));

        if (value is not null)
        {
            _player.OpenFile(value.Info.FilePath);
        }
    }

    public void SetFolder(string folder) => Rescan(folder);

    [RelayCommand]
    private void Rescan()
    {
        if (_library.CurrentFolder is { } folder)
        {
            Rescan(folder);
        }
    }

    public void SelectNext() => Shift(1);

    public void SelectPrevious() => Shift(-1);

    private void Shift(int delta)
    {
        if (Loops.Count == 0)
        {
            return;
        }

        var currentIndex = SelectedLoop is null ? -1 : Loops.IndexOf(SelectedLoop);
        var nextIndex = ((currentIndex + delta) % Loops.Count + Loops.Count) % Loops.Count;
        SelectedLoop = Loops[nextIndex];
    }

    private void Rescan(string folder)
    {
        var results = _library.Scan(folder);

        Loops.Clear();
        foreach (var info in results)
        {
            Loops.Add(new LoopEntryViewModel(info));
        }

        FolderLabel = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar)) is { Length: > 0 } name
            ? name
            : folder;

        OnPropertyChanged(nameof(TotalCount));
        SelectedLoop = Loops.FirstOrDefault();
    }
}
