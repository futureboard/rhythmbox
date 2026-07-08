using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Rythmbox.Editor.ViewModels;

public sealed partial class PianoRollNoteViewModel : ViewModelBase
{
    private readonly PianoRollLaneViewModel _lane;
    private readonly EditorViewModel _editor;

    public PianoRollNoteViewModel(PianoRollLaneViewModel lane, EditorViewModel editor, int stepIndex)
    {
        _lane = lane;
        _editor = editor;
        StepIndex = stepIndex;
    }

    public int StepIndex { get; }

    public bool IsBarStart => StepIndex % _editor.Pattern.StepsPerBar == 0;

    public bool IsBeatStart => StepIndex % (_editor.Pattern.StepsPerBar / 4) == 0
                               && _editor.Pattern.StepsPerBar >= 4;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isPlaying;

    public void SyncFromPattern()
    {
        IsActive = _editor.Pattern.HasHit(_lane.PadIndex, StepIndex);
    }

    [RelayCommand]
    private void Toggle() => _editor.ToggleCell(_lane.PadIndex, StepIndex);
}
