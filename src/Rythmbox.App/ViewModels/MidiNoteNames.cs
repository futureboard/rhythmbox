namespace Rythmbox.App.ViewModels;

internal static class MidiNoteNames
{
    private static readonly string[] Names =
        ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    /// <summary>GM drum map style (C1 = MIDI 36).</summary>
    public static string Format(int note)
    {
        var octave = note / 12 - 2;
        return $"{Names[note % 12]}{octave}";
    }
}
