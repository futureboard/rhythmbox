using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.Core.Engine;
using Rythmbox.Core.Models;

namespace Rythmbox.App.ViewModels;

public sealed partial class LoopBankViewModel : ViewModelBase
{
    public LoopBankViewModel(LoopBank bank)
    {
        Bank = bank;
    }

    public LoopBank Bank { get; }

    public string Name => Bank.Name;

    [ObservableProperty]
    private bool _isSelected;
}

public sealed partial class LoopBrowserViewModel : ViewModelBase
{
    private readonly LoopLibraryService _library;
    private readonly PlayerViewModel _player;
    private string? _rootFolder;

    public LoopBrowserViewModel(LoopLibraryService library, PlayerViewModel player)
    {
        _library = library;
        _player = player;
    }

    public ObservableCollection<LoopBankViewModel> Banks { get; } = new();

    public ObservableCollection<LoopEntryViewModel> Loops { get; } = new();

    [ObservableProperty]
    private string _folderLabel = "(no folder selected)";

    [ObservableProperty]
    private LoopBankViewModel? _selectedBank;

    [ObservableProperty]
    private LoopEntryViewModel? _selectedLoop;

    public int SelectedIndexDisplay => SelectedLoop is null ? 0 : Loops.IndexOf(SelectedLoop) + 1;

    public int TotalCount => Loops.Count;

    partial void OnSelectedBankChanged(LoopBankViewModel? value)
    {
        foreach (var bank in Banks)
        {
            bank.IsSelected = ReferenceEquals(bank, value);
        }

        if (value is not null)
        {
            RescanBank(value.Bank.Path);
        }
    }

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

    public void SetFolder(string folder)
    {
        _rootFolder = folder;
        var banks = _library.ScanBanks(folder);

        Banks.Clear();
        foreach (var bank in banks)
        {
            Banks.Add(new LoopBankViewModel(bank));
        }

        FolderLabel = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar)) is { Length: > 0 } name
            ? name
            : folder;

        SelectedBank = Banks.FirstOrDefault();
    }

    [RelayCommand]
    private void Rescan()
    {
        if (_rootFolder is not null)
        {
            SetFolder(_rootFolder);
        }
        else if (_library.CurrentFolder is { } folder)
        {
            RescanBank(folder);
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

    private void RescanBank(string folder)
    {
        var results = _library.Scan(folder);

        Loops.Clear();
        foreach (var info in results)
        {
            Loops.Add(new LoopEntryViewModel(info));
        }

        OnPropertyChanged(nameof(TotalCount));
        SelectedLoop = Loops.FirstOrDefault();
    }
}
