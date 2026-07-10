using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.Core.Engine;
using Rythmbox.Core.Models;

namespace Rythmbox.App.ViewModels;

/// <summary>A single percussion pad button: a fixed GM note that can be triggered live or highlighted when used by the loaded loop.</summary>
public sealed partial class PadViewModel : ViewModelBase
{
    private static readonly TimeSpan FlashDuration = TimeSpan.FromMilliseconds(90);

    private readonly KitSamplePlayer? _kitPlayer;
    private DispatcherTimer? _flashTimer;

    private PadViewModel(PercussionPad pad, KitSamplePlayer? kitPlayer, bool isPlaceholder)
    {
        Pad = pad;
        _kitPlayer = kitPlayer;
        IsPlaceholder = isPlaceholder;
    }

    public PadViewModel(PercussionPad pad, KitSamplePlayer kitPlayer)
        : this(pad, kitPlayer, isPlaceholder: false)
    {
    }

    public static PadViewModel CreatePlaceholder(int slotIndex) =>
        new(new PercussionPad(-1, string.Empty, -1, PadCategory.Drum, PadBus.Drum), null, isPlaceholder: true)
        {
            PlaceholderSlot = slotIndex,
        };

    public PercussionPad Pad { get; }

    public bool IsPlaceholder { get; }

    public int PlaceholderSlot { get; private init; }

    public int Number => IsPlaceholder ? 0 : Pad.Index + 1;

    public string Label => IsPlaceholder ? string.Empty : Pad.Label;

    public int MidiNote => IsPlaceholder ? -1 : _kitPlayer?.GetPadMidiNote(Pad.Index) ?? Pad.Note;

    public DrumMixGroup OutputGroup => IsPlaceholder
        ? DrumMixGroup.Percussion
        : _kitPlayer?.GetPadOutputGroup(Pad.Index) ?? GmPercussionMap.GetMixGroup(Pad.Note);

    public string OutputLabel => IsPlaceholder ? string.Empty : GmPercussionMap.GetMixGroupLabel(OutputGroup);

    public string NoteName => IsPlaceholder ? string.Empty : MidiNoteNames.Format(MidiNote);

    public string NoteDetail => IsPlaceholder ? string.Empty : $"{NoteName} / {MidiNote}";

    public string SampleDetail => IsPlaceholder
        ? string.Empty
        : HasSample
            ? NoteDetail
            : "Click to assign";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SampleDetail))]
    private bool _hasSample;

    [ObservableProperty]
    private bool _isUsedInLoop;

    [ObservableProperty]
    private bool _isPressed;

    public void Press()
    {
        if (IsPlaceholder || _kitPlayer is null)
        {
            return;
        }

        Flash();
        _kitPlayer.TriggerPad(Pad.Index, 110f / 127f);
    }

    public void Release()
    {
        StopFlashTimer();
        IsPressed = false;
    }

    /// <summary>Visual hit feedback only (audio already handled by MIDI/router).</summary>
    public void AnimateHit()
    {
        if (IsPlaceholder)
        {
            return;
        }

        Flash();
    }

    public void RefreshRouting()
    {
        OnPropertyChanged(nameof(MidiNote));
        OnPropertyChanged(nameof(OutputGroup));
        OnPropertyChanged(nameof(OutputLabel));
        OnPropertyChanged(nameof(NoteName));
        OnPropertyChanged(nameof(NoteDetail));
        OnPropertyChanged(nameof(SampleDetail));
    }

    [RelayCommand]
    private void Hit()
    {
        if (IsPlaceholder)
        {
            return;
        }

        Press();
    }

    private void Flash()
    {
        IsPressed = true;
        StopFlashTimer();
        _flashTimer = new DispatcherTimer { Interval = FlashDuration };
        _flashTimer.Tick += OnFlashTick;
        _flashTimer.Start();
    }

    private void OnFlashTick(object? sender, EventArgs e)
    {
        StopFlashTimer();
        IsPressed = false;
    }

    private void StopFlashTimer()
    {
        if (_flashTimer is null)
        {
            return;
        }

        _flashTimer.Stop();
        _flashTimer.Tick -= OnFlashTick;
        _flashTimer = null;
    }
}
