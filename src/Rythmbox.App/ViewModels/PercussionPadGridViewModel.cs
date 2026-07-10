using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.Core.Engine;
using Rythmbox.Core.Models;

namespace Rythmbox.App.ViewModels;

public sealed partial class PercussionPadGridViewModel : ViewModelBase, IDisposable
{
    public const int PadsPerPage = 20;
    public const int GridColumns = 4;
    public const int GridRows = 5;

    private readonly KitSamplePlayer _kitPlayer;
    private readonly PlayerViewModel _player;
    private readonly DispatcherTimer _runtimeTimer;

    public PercussionPadGridViewModel(KitSamplePlayer kitPlayer, PlayerViewModel player, PadMappingService mapping)
    {
        _kitPlayer = kitPlayer;
        _player = player;
        Pads = GmPercussionMap.Pads.Select(pad => new PadViewModel(pad, kitPlayer, mapping)).ToList();
        CurrentPagePads = new ObservableCollection<PadViewModel>(BuildCurrentPagePads());
        RefreshPadState();

        _runtimeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _runtimeTimer.Tick += (_, _) => UpdateRuntimeState();
        _runtimeTimer.Start();

        _player.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlayerViewModel.UsedNoteNumbers))
            {
                RefreshUsage();
            }

            if (e.PropertyName == nameof(PlayerViewModel.IsPlaying) && !_player.IsPlaying)
            {
                ClearHeldRuntimeState();
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

    public void AnimatePad(int index)
    {
        if ((uint)index < (uint)Pads.Count)
        {
            Pads[index].OnPadTriggered(sourceNote: -1, velocity: 127);
        }
    }

    public void HandlePadTriggered(int padIndex, int sourceNote, int velocity)
    {
        if ((uint)padIndex < (uint)Pads.Count)
        {
            Pads[padIndex].OnPadTriggered(sourceNote, velocity);
        }
    }

    public void HandlePadReleased(int padIndex, int sourceNote)
    {
        if ((uint)padIndex < (uint)Pads.Count)
        {
            Pads[padIndex].OnPadNoteReleased(sourceNote);
        }
    }

    public void ClearHeldRuntimeState()
    {
        foreach (var pad in Pads)
        {
            pad.ClearHeldRuntimeState();
        }
    }

    public void ClearPointerStates()
    {
        foreach (var pad in Pads)
        {
            pad.ClearPointerState();
        }
    }

    public void RefreshPadRouting(int padIndex)
    {
        if ((uint)padIndex < (uint)Pads.Count)
        {
            Pads[padIndex].RefreshRouting();
            RefreshUsage();
        }
    }

    public void RefreshSampleState()
        => RefreshPadState();

    public void RefreshPadState()
    {
        var hasSample = _kitPlayer.PadHasSample;
        for (var i = 0; i < Pads.Count && i < hasSample.Count; i++)
        {
            Pads[i].HasSample = hasSample[i];
            Pads[i].RefreshRouting();
        }

        RefreshUsage();
        OnPropertyChanged(nameof(PageNoteRange));
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
            pad.IsUsedInLoop = pad.AssignedNotes.Any(used.Contains);
        }
    }

    private void UpdateRuntimeState()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var pad in Pads)
        {
            pad.UpdateRuntime(now);
        }
    }

    public void Dispose() => _runtimeTimer.Stop();
}
