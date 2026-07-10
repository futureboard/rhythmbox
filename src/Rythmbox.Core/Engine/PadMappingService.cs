using System.Threading;
using Rythmbox.Core.Models;

namespace Rythmbox.Core.Engine;

/// <summary>
/// Per-pad keyboard and multi-note MIDI assignments. MIDI lookup uses a
/// copy-on-write note-to-pad index, so realtime input never scans the pad list.
/// </summary>
public sealed class PadMappingService
{
    private readonly int[][] _notesByPad;
    private int[][] _padsByMidiNote = CreateEmptyNoteIndex();
    private readonly string[] _keyboardKeyNames;

    public PadMappingService()
    {
        _notesByPad = GmPercussionMap.Pads.Select(pad => new[] { pad.Note }).ToArray();
        _keyboardKeyNames = new string[GmPercussionMap.Pads.Count];
        ApplyNumberRowMapping();
        RebuildMidiIndex();
    }

    public int PadCount => GmPercussionMap.Pads.Count;

    public bool UseHomeRowMapping { get; private set; }

    public int? LearnPadIndex { get; set; }

    public NoteConflictMode ConflictMode { get; set; } = NoteConflictMode.AllowLayers;

    /// <summary>Compatibility event for existing single-note consumers.</summary>
    public event Action<int, int>? MidiNoteChanged;

    /// <summary>Raised after a pad's whole assignment list and reverse index are updated.</summary>
    public event Action<int, IReadOnlyList<int>>? MidiNotesChanged;

    /// <summary>Raised in <see cref="NoteConflictMode.WarnOnly"/> when a note will layer pads.</summary>
    public event Action<int, int, IReadOnlyList<int>>? MidiNoteConflictDetected;

    public int GetMidiNote(int padIndex) => GetMidiNotes(padIndex).FirstOrDefault(-1);

    public IReadOnlyList<int> GetMidiNotes(int padIndex) =>
        (uint)padIndex < (uint)_notesByPad.Length ? _notesByPad[padIndex] : Array.Empty<int>();

    /// <summary>Compatibility setter that replaces the pad's assignments with one note.</summary>
    public void SetMidiNote(int padIndex, int note) => SetMidiNotes(padIndex, [note]);

    public bool SetMidiNotes(int padIndex, IEnumerable<int> notes)
    {
        if ((uint)padIndex >= (uint)_notesByPad.Length)
        {
            return false;
        }

        var normalized = PadConfig.NormalizeNotes(notes).ToArray();
        var conflicts = GetConflictingNotes(padIndex, normalized);
        if (ConflictMode == NoteConflictMode.Exclusive && conflicts.Count > 0)
        {
            return false;
        }

        var previous = _notesByPad[padIndex];
        if (previous.SequenceEqual(normalized))
        {
            return true;
        }

        _notesByPad[padIndex] = normalized;
        RebuildMidiIndex();

        var previousPrimary = previous.FirstOrDefault(-1);
        var primary = normalized.FirstOrDefault(-1);
        if (previousPrimary != primary)
        {
            MidiNoteChanged?.Invoke(padIndex, primary);
        }

        MidiNotesChanged?.Invoke(padIndex, normalized);

        if (ConflictMode == NoteConflictMode.WarnOnly)
        {
            foreach (var note in conflicts)
            {
                MidiNoteConflictDetected?.Invoke(padIndex, note, GetPadIndicesForMidiNote(note));
            }
        }

        return true;
    }

    public bool AddMidiNote(int padIndex, int note) =>
        SetMidiNotes(padIndex, GetMidiNotes(padIndex).Append(note));

    public bool RemoveMidiNote(int padIndex, int note) =>
        SetMidiNotes(padIndex, GetMidiNotes(padIndex).Where(existing => existing != note));

    public void NudgeMidiNote(int padIndex, int delta)
    {
        if ((uint)padIndex >= (uint)_notesByPad.Length)
        {
            return;
        }

        var notes = GetMidiNotes(padIndex).ToArray();
        if (notes.Length == 0)
        {
            SetMidiNotes(padIndex, [Math.Clamp(delta, 0, 127)]);
            return;
        }

        notes[0] = Math.Clamp(notes[0] + delta, 0, 127);
        SetMidiNotes(padIndex, notes);
    }

    /// <summary>Realtime lookup for all pads layered under an incoming note.</summary>
    public IReadOnlyList<int> GetPadIndicesForMidiNote(int note)
    {
        var index = Volatile.Read(ref _padsByMidiNote);
        return note is >= 0 and < 128 ? index[note] : Array.Empty<int>();
    }

    /// <summary>Compatibility lookup; layered mappings return the first pad index.</summary>
    public int? FindPadByMidiNote(int note)
    {
        var padIndices = GetPadIndicesForMidiNote(note);
        return padIndices.Count > 0 ? padIndices[0] : null;
    }

    public string GetKeyboardKeyName(int padIndex) =>
        (uint)padIndex < (uint)_keyboardKeyNames.Length ? _keyboardKeyNames[padIndex] : "--";

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
        var idx = Array.FindIndex(keys, key => string.Equals(key, current, StringComparison.OrdinalIgnoreCase));
        _keyboardKeyNames[padIndex] = keys[idx < 0 ? 0 : (idx + 1) % keys.Length];
    }

    private List<int> GetConflictingNotes(int padIndex, IReadOnlyList<int> notes) => notes
        .Where(note => GetPadIndicesForMidiNote(note).Any(existingPad => existingPad != padIndex))
        .ToList();

    private void RebuildMidiIndex()
    {
        var buckets = Enumerable.Range(0, 128).Select(_ => new List<int>()).ToArray();
        for (var padIndex = 0; padIndex < _notesByPad.Length; padIndex++)
        {
            foreach (var note in _notesByPad[padIndex])
            {
                buckets[note].Add(padIndex);
            }
        }

        Volatile.Write(ref _padsByMidiNote, buckets.Select(static bucket => bucket.ToArray()).ToArray());
    }

    private static int[][] CreateEmptyNoteIndex() => Enumerable.Range(0, 128).Select(_ => Array.Empty<int>()).ToArray();

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
