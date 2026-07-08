namespace Rythmbox.Editor.ViewModels;

public sealed class PianoRollRulerTickViewModel
{
    public required string Label { get; init; }

    public bool IsBarStart { get; init; }

    public bool IsBeatStart { get; init; }
}
