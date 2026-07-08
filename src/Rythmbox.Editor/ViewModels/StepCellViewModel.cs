using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.Core.Models;

namespace Rythmbox.Editor.ViewModels;

public sealed partial class StepCellViewModel : ViewModelBase
{
    private readonly StepRowViewModel _row;
    private readonly EditorViewModel _editor;

    public StepCellViewModel(StepRowViewModel row, EditorViewModel editor, int stepIndex)
    {
        _row = row;
        _editor = editor;
        StepIndex = stepIndex;
    }

    public int StepIndex { get; }

    public bool IsBarStart => StepIndex % _editor.Pattern.StepsPerBar == 0;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isPlaying;

    public void SyncFromPattern()
    {
        IsActive = _editor.Pattern.HasHit(_row.PadIndex, StepIndex);
    }

    [RelayCommand]
    private void Toggle() => _editor.ToggleCell(_row.PadIndex, StepIndex);
}
