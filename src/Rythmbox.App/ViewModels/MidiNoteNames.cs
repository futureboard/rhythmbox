namespace Rythmbox.App.ViewModels;

internal static class MidiNoteNames
{
    private static readonly string[] Names =
        ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    /// <summary>Drum editor style (C3 = MIDI 36).</summary>
    public static string Format(int note)
    {
        var clamped = Math.Clamp(note, 0, 127);
        var octave = clamped / 12;
        return $"{Names[clamped % 12]}{octave}";
    }
}
