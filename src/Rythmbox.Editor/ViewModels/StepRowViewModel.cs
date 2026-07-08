using System.Collections.ObjectModel;
using Rythmbox.Core.Models;

namespace Rythmbox.Editor.ViewModels;

public sealed class StepRowViewModel : ViewModelBase
{
    public StepRowViewModel(PercussionPad pad, EditorViewModel editor)
    {
        Pad = pad;
        PadIndex = pad.Index;
        Label = pad.Label;
        Editor = editor;
    }

    public PercussionPad Pad { get; }

    public int PadIndex { get; }

    public string Label { get; }

    public EditorViewModel Editor { get; }

    public ObservableCollection<StepCellViewModel> Cells { get; } = new();

    public bool IsPerc => Pad.Category == PadCategory.Perc;
}
