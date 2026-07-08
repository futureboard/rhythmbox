using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.Core.Models.Styles;
using Rythmbox.Core.Styles;

namespace Rythmbox.App.ViewModels;

public sealed partial class PatternPadViewModel : ViewModelBase
{
    public PatternPadViewModel(PatternPadSlot slot, Action<PatternPadViewModel> onTrigger)
    {
        Slot = slot;
        _onTrigger = onTrigger;
    }

    private readonly Action<PatternPadViewModel> _onTrigger;

    public PatternPadSlot Slot { get; }

    public string Label => Slot.Label;

    public bool IsStop => Slot.Kind == PatternPadKind.Stop;

    public bool IsTapTempo => Slot.Kind == PatternPadKind.TapTempo;

    public bool IsUtility => IsStop || IsTapTempo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateClass))]
    [NotifyPropertyChangedFor(nameof(IsPlayingState))]
    [NotifyPropertyChangedFor(nameof(IsSelectedState))]
    [NotifyPropertyChangedFor(nameof(IsQueuedState))]
    [NotifyPropertyChangedFor(nameof(IsMissingState))]
    private PatternPadVisualState _visualState = PatternPadVisualState.Idle;

    [ObservableProperty]
    private bool _isEnabled = true;

    public string StateClass => VisualState switch
    {
        PatternPadVisualState.Playing => "playing",
        PatternPadVisualState.Selected => "selected",
        PatternPadVisualState.Queued => "queued",
        PatternPadVisualState.Missing => "missing",
        PatternPadVisualState.Disabled => "disabled",
        _ => string.Empty,
    };

    public bool IsPlayingState => VisualState == PatternPadVisualState.Playing;

    public bool IsSelectedState => VisualState == PatternPadVisualState.Selected;

    public bool IsQueuedState => VisualState == PatternPadVisualState.Queued;

    public bool IsMissingState => VisualState == PatternPadVisualState.Missing;

    public string ToolTipText => VisualState switch
    {
        PatternPadVisualState.Missing => "Missing MIDI pattern",
        PatternPadVisualState.Disabled => "Not available",
        PatternPadVisualState.Queued => "Queued for next section",
        PatternPadVisualState.Playing => "Playing",
        PatternPadVisualState.Selected => "Selected",
        _ => Label,
    };

    [RelayCommand]
    private void Trigger()
    {
        if (!IsEnabled && Slot.Kind == PatternPadKind.Pattern)
        {
            return;
        }

        _onTrigger(this);
    }

    public void SyncFromSession(ArrangerSession session)
    {
        VisualState = session.GetPadVisualState(Slot);
        IsEnabled = Slot.Kind is PatternPadKind.Stop or PatternPadKind.TapTempo
                    || VisualState is not PatternPadVisualState.Missing and not PatternPadVisualState.Disabled;
    }
}
