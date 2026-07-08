using Rythmbox.Core.Models;

namespace Rythmbox.Core.Engine;

/// <summary>
/// Per-pad keyboard and MIDI note assignments, mirroring the old Settings page mapping options.
/// </summary>
public sealed class PadMappingService
{
    private readonly int[] _midiNotes;
    private readonly string[] _keyboardKeyNames;

    public PadMappingService()
    {
        _midiNotes = GmPercussionMap.Pads.Select(p => p.Note).ToArray();
        _keyboardKeyNames = new string[GmPercussionMap.Pads.Count];
        ApplyNumberRowMapping();
    }

    public int PadCount => GmPercussionMap.Pads.Count;

    public bool UseHomeRowMapping { get; private set; }

    public int? LearnPadIndex { get; set; }

    public int GetMidiNote(int padIndex) =>
        (uint)padIndex < (uint)_midiNotes.Length ? _midiNotes[padIndex] : -1;

    public void SetMidiNote(int padIndex, int note)
    {
        if ((uint)padIndex < (uint)_midiNotes.Length)
        {
            _midiNotes[padIndex] = Math.Clamp(note, 0, 127);
        }
    }

    public void NudgeMidiNote(int padIndex, int delta)
    {
        if ((uint)padIndex < (uint)_midiNotes.Length)
        {
            _midiNotes[padIndex] = Math.Clamp(_midiNotes[padIndex] + delta, 0, 127);
        }
    }

    public string GetKeyboardKeyName(int padIndex) =>
        (uint)padIndex < (uint)_keyboardKeyNames.Length ? _keyboardKeyNames[padIndex] : "--";

    public int? FindPadByMidiNote(int note)
    {
        for (var i = 0; i < _midiNotes.Length; i++)
        {
            if (_midiNotes[i] == note)
            {
                return i;
            }
        }

        return null;
    }

    public int? FindPadByKeyName(string keyName)
    {
        for (var i = 0; i < _keyboardKeyNames.Length; i++)
        {
            if (string.Equals(_keyboardKeyNames[i], keyName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return null;
    }

    public void ToggleKeyboardMapping()
    {
        if (UseHomeRowMapping)
        {
            ApplyNumberRowMapping();
        }
        else
        {
            ApplyHomeRowMapping();
        }
    }

    public void CycleKeyboardKey(int padIndex)
    {
        if ((uint)padIndex >= (uint)_keyboardKeyNames.Length)
        {
            return;
        }

        var keys = UseHomeRowMapping ? HomeRowKeys : NumberRowKeys;
        var current = _keyboardKeyNames[padIndex];
        var idx = Array.FindIndex(keys, k => string.Equals(k, current, StringComparison.OrdinalIgnoreCase));
        var next = idx < 0 ? 0 : (idx + 1) % keys.Length;
        _keyboardKeyNames[padIndex] = keys[next];
    }

    private void ApplyNumberRowMapping()
    {
        UseHomeRowMapping = false;
        var keys = new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=", "[", "]", "\\", ";", "'", ",", "." };
        for (var i = 0; i < _keyboardKeyNames.Length; i++)
        {
            _keyboardKeyNames[i] = i < keys.Length ? keys[i] : $"F{i - keys.Length + 1}";
        }
    }

    private void ApplyHomeRowMapping()
    {
        UseHomeRowMapping = true;
        var keys = new[] { "A", "S", "D", "F", "J", "K", "L", ";", "Q", "W", "E", "R", "U", "I", "O", "P", "Z", "X", "C" };
        for (var i = 0; i < _keyboardKeyNames.Length; i++)
        {
            _keyboardKeyNames[i] = i < keys.Length ? keys[i] : $"F{i - keys.Length + 1}";
        }
    }

    private static readonly string[] NumberRowKeys =
        ["1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=", "[", "]", "\\", ";", "'", ",", "."];

    private static readonly string[] HomeRowKeys =
        ["A", "S", "D", "F", "J", "K", "L", ";", "Q", "W", "E", "R", "U", "I", "O", "P", "Z", "X", "C"];
}
