using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.Core.Engine;
using Rythmbox.Core.Models;

namespace Rythmbox.App.ViewModels;

public sealed partial class PercussionPadGridViewModel : ViewModelBase
{
    public const int PadsPerPage = 20;
    public const int GridColumns = 4;
    public const int GridRows = 5;

    private readonly PlayerViewModel _player;

    public PercussionPadGridViewModel(KitSamplePlayer kitPlayer, PlayerViewModel player)
    {
        _player = player;
        Pads = GmPercussionMap.Pads.Select(pad => new PadViewModel(pad, kitPlayer)).ToList();
        CurrentPagePads = new ObservableCollection<PadViewModel>(BuildCurrentPagePads());

        _player.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlayerViewModel.UsedNoteNumbers))
            {
                RefreshUsage();
            }
        };
    }

    public IReadOnlyList<PadViewModel> Pads { get; }

    public ObservableCollection<PadViewModel> CurrentPagePads { get; }

    public int PageCount => (Pads.Count + PadsPerPage - 1) / PadsPerPage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageLabel))]
    [NotifyPropertyChangedFor(nameof(PageNoteRange))]
    [NotifyPropertyChangedFor(nameof(CanGoToPreviousPage))]
    [NotifyPropertyChangedFor(nameof(CanGoToNextPage))]
    private int _currentPage;

    public bool CanGoToPreviousPage => CurrentPage > 0;

    public bool CanGoToNextPage => CurrentPage < PageCount - 1;

    public string PageLabel => $"{CurrentPage + 1} / {PageCount}";

    public string PageNoteRange
    {
        get
        {
            var start = CurrentPage * PadsPerPage;
            if (start >= Pads.Count)
            {
                return string.Empty;
            }

            var end = Math.Min(start + PadsPerPage, Pads.Count) - 1;
            return $"{Pads[start].NoteName}–{Pads[end].NoteName}";
        }
    }

    public void TriggerPad(int index)
    {
        if ((uint)index < (uint)Pads.Count)
        {
            Pads[index].Press();
        }
    }

    public void ReleasePad(int index)
    {
        if ((uint)index < (uint)Pads.Count)
        {
            Pads[index].Release();
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
    private void PreviousPage()
    {
        if (CanGoToPreviousPage)
        {
            CurrentPage--;
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
    private void NextPage()
    {
        if (CanGoToNextPage)
        {
            CurrentPage++;
        }
    }

    partial void OnCurrentPageChanged(int value)
    {
        RefreshCurrentPagePads();
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    private void RefreshCurrentPagePads()
    {
        CurrentPagePads.Clear();
        foreach (var pad in BuildCurrentPagePads())
        {
            CurrentPagePads.Add(pad);
        }
    }

    private IEnumerable<PadViewModel> BuildCurrentPagePads()
    {
        var start = CurrentPage * PadsPerPage;
        for (var slot = 0; slot < PadsPerPage; slot++)
        {
            var index = start + slot;
            yield return index < Pads.Count ? Pads[index] : PadViewModel.CreatePlaceholder(slot);
        }
    }

    private void RefreshUsage()
    {
        var used = _player.UsedNoteNumbers;
        foreach (var pad in Pads.Where(static p => !p.IsPlaceholder))
        {
            pad.IsUsedInLoop = used.Contains(pad.Pad.Note);
        }
    }
}
