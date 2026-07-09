using System.Collections.ObjectModel;
using Rythmbox.Core.Models;

namespace Rythmbox.Editor.ViewModels;

public sealed class PianoRollLaneViewModel : ViewModelBase
{
    public PianoRollLaneViewModel(PercussionPad pad, EditorViewModel editor, bool isAlternateRow)
    {
        Pad = pad;
        PadIndex = pad.Index;
        Label = pad.Label;
        NoteNumber = pad.Note;
        Editor = editor;
        IsAlternateRow = isAlternateRow;
    }

    public PercussionPad Pad { get; }

    public int PadIndex { get; }

    public string Label { get; }

    public int NoteNumber { get; }

    public string NoteLabel => $"{FormatNoteName(NoteNumber)} · {NoteNumber}";

    public EditorViewModel Editor { get; }

    public bool IsAlternateRow { get; }

    public bool IsPerc => Pad.Category == PadCategory.Perc;

    public ObservableCollection<PianoRollNoteViewModel> Notes { get; } = new();

    private static string FormatNoteName(int note)
    {
        string[] names = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
        return $"{names[Math.Clamp(note, 0, 127) % 12]}{Math.Clamp(note, 0, 127) / 12}";
    }
}
