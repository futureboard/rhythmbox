using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.Core.Engine;
using Rythmbox.Core.Models;

namespace Rythmbox.App.ViewModels;

public sealed partial class SubLoopViewModel : ViewModelBase
{
    private readonly SubLoopService _subLoops;
    private readonly PlayerViewModel _player;
    private readonly StatusViewModel _status;

    public SubLoopViewModel(SubLoopService subLoops, PlayerViewModel player, StatusViewModel status)
    {
        _subLoops = subLoops;
        _player = player;
        _status = status;
    }

    public ObservableCollection<LoopEntryViewModel> SubLoops { get; } = new();

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private LoopEntryViewModel? _selectedSubLoop;

    partial void OnSelectedSubLoopChanged(LoopEntryViewModel? value)
    {
        if (value is not null)
        {
            _player.OpenFile(value.Info.FilePath);
            _status.Show($"Sub loop: {value.Name}");
            IsOpen = false;
        }
    }

    public void Scan(string? folder)
    {
        var results = _subLoops.Scan(folder);
        SubLoops.Clear();
        foreach (var info in results)
        {
            SubLoops.Add(new LoopEntryViewModel(info));
        }
    }

    [RelayCommand]
    private void Toggle() => IsOpen = !IsOpen;

    [RelayCommand]
    private void Close() => IsOpen = false;
}
