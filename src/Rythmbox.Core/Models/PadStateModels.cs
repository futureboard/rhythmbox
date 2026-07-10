namespace Rythmbox.Core.Models;

/// <summary>How the mapping service handles one incoming note assigned to multiple pads.</summary>
public enum NoteConflictMode
{
    AllowLayers,
    WarnOnly,
    Exclusive,
}

/// <summary>Persistent pad configuration. MIDI assignment is intentionally separate from runtime state.</summary>
public sealed class PadConfig
{
    private List<int> _assignedNotes = [];

    public int PadId { get; init; }

    public string Label { get; set; } = string.Empty;

    /// <summary>All MIDI notes that trigger this physical pad slot.</summary>
    public List<int> AssignedNotes
    {
        get => _assignedNotes;
        set => _assignedNotes = NormalizeNotes(value);
    }

    /// <summary>Compatibility primary note. Setting it replaces the assignment list with one note.</summary>
    public int MidiNote
    {
        get => _assignedNotes.Count > 0 ? _assignedNotes[0] : -1;
        set => AssignedNotes = value is >= 0 and <= 127 ? [value] : [];
    }

    public string? SampleReference { get; set; }

    public float Gain { get; set; } = 1f;

    public float Pan { get; set; }

    public float PitchSemitones { get; set; }

    public int ChokeGroup { get; set; }

    public string ColorToken { get; set; } = "default";

    public DrumMixGroup OutputGroup { get; set; } = DrumMixGroup.Percussion;

    public bool IsEnabled { get; set; } = true;

    public string VelocityBehavior { get; set; } = "layered";

    public string PlaybackMode { get; set; } = "one-shot";

    public PadConfig Clone() => new()
    {
        PadId = PadId,
        Label = Label,
        AssignedNotes = [.. AssignedNotes],
        SampleReference = SampleReference,
        Gain = Gain,
        Pan = Pan,
        PitchSemitones = PitchSemitones,
        ChokeGroup = ChokeGroup,
        ColorToken = ColorToken,
        OutputGroup = OutputGroup,
        IsEnabled = IsEnabled,
        VelocityBehavior = VelocityBehavior,
        PlaybackMode = PlaybackMode,
    };

    public static List<int> NormalizeNotes(IEnumerable<int>? notes) => notes is null
        ? []
        : notes.Where(static note => note is >= 0 and <= 127).Distinct().OrderBy(static note => note).ToList();
}

/// <summary>
/// Ephemeral UI/runtime state. Every visual concept has its own field; playing
/// and render state are derived from real held-note and voice information.
/// </summary>
public sealed class PadRuntimeState
{
    private readonly Dictionary<int, int> _heldNoteCounts = new();

    public bool IsHovered { get; set; }

    public bool IsMouseDown { get; set; }

    public bool IsSelected { get; set; }

    public bool IsEditing { get; set; }

    public int ActiveVoiceCount { get; set; }

    public DateTimeOffset? LastTriggerTime { get; private set; }

    public DateTimeOffset? LastReleaseTime { get; private set; }

    public float LastVelocity { get; private set; }

    public float HitFlashPhase { get; private set; }

    public ulong HitFlashId { get; private set; }

    public int NoteDownCount { get; private set; }

    public HashSet<int> HeldNotes { get; } = [];

    public bool IsPlaying => NoteDownCount > 0 || ActiveVoiceCount > 0;

    public bool PreviewPending { get; set; }

    public bool HasDirtyTempEdit { get; set; }

    public void RegisterTrigger(DateTimeOffset now, float velocity)
    {
        LastTriggerTime = now;
        LastVelocity = Math.Clamp(velocity, 0f, 1f);
        HitFlashId++;
        HitFlashPhase = 1f;
    }

    public void RegisterNoteOn(int note, DateTimeOffset now, float velocity)
    {
        if (note is >= 0 and <= 127)
        {
            _heldNoteCounts.TryGetValue(note, out var count);
            _heldNoteCounts[note] = count + 1;
            HeldNotes.Add(note);
            NoteDownCount++;
        }

        RegisterTrigger(now, velocity);
    }

    public void RegisterNoteOff(int note, DateTimeOffset now)
    {
        if (_heldNoteCounts.TryGetValue(note, out var count))
        {
            if (count <= 1)
            {
                _heldNoteCounts.Remove(note);
                HeldNotes.Remove(note);
            }
            else
            {
                _heldNoteCounts[note] = count - 1;
            }

            NoteDownCount = Math.Max(0, NoteDownCount - 1);
        }

        LastReleaseTime = now;
    }

    public void ClearHeldNotes(DateTimeOffset now)
    {
        _heldNoteCounts.Clear();
        HeldNotes.Clear();
        NoteDownCount = 0;
        IsMouseDown = false;
        LastReleaseTime = now;
    }

    public void UpdateHitFlash(DateTimeOffset now, TimeSpan duration)
    {
        if (LastTriggerTime is not { } lastTrigger || duration <= TimeSpan.Zero)
        {
            HitFlashPhase = 0f;
            return;
        }

        var progress = (float)((now - lastTrigger).TotalMilliseconds / duration.TotalMilliseconds);
        if (progress >= 1f)
        {
            HitFlashPhase = 0f;
            return;
        }

        progress = Math.Clamp(progress, 0f, 1f);
        var smoothStep = progress * progress * (3f - (2f * progress));
        HitFlashPhase = 1f - smoothStep;
    }
}

/// <summary>Temporary edit state; nothing is written to <see cref="PadConfig"/> until commit.</summary>
public sealed class PadEditDraft
{
    public string Label { get; set; } = string.Empty;

    public List<int> AssignedNotes { get; set; } = [];

    /// <summary>Compatibility primary note for existing single-note editor controls.</summary>
    public int MidiNote
    {
        get => AssignedNotes.Count > 0 ? AssignedNotes[0] : -1;
        set => AssignedNotes = value is >= 0 and <= 127 ? [value] : [];
    }

    public string? SampleReference { get; set; }

    public float Gain { get; set; } = 1f;

    public float Pan { get; set; }

    public float PitchSemitones { get; set; }

    public int ChokeGroup { get; set; }

    public string ColorToken { get; set; } = "default";

    public DrumMixGroup OutputGroup { get; set; } = DrumMixGroup.Percussion;

    public bool IsEnabled { get; set; } = true;

    public string VelocityBehavior { get; set; } = "layered";

    public string PlaybackMode { get; set; } = "one-shot";

    public static PadEditDraft FromConfig(PadConfig config) => new()
    {
        Label = config.Label,
        AssignedNotes = [.. config.AssignedNotes],
        SampleReference = config.SampleReference,
        Gain = config.Gain,
        Pan = config.Pan,
        PitchSemitones = config.PitchSemitones,
        ChokeGroup = config.ChokeGroup,
        ColorToken = config.ColorToken,
        OutputGroup = config.OutputGroup,
        IsEnabled = config.IsEnabled,
        VelocityBehavior = config.VelocityBehavior,
        PlaybackMode = config.PlaybackMode,
    };

    public bool DiffersFrom(PadConfig config) =>
        Label != config.Label
        || !AssignedNotes.Order().SequenceEqual(config.AssignedNotes.Order())
        || SampleReference != config.SampleReference
        || Math.Abs(Gain - config.Gain) > 0.0001f
        || Math.Abs(Pan - config.Pan) > 0.0001f
        || Math.Abs(PitchSemitones - config.PitchSemitones) > 0.0001f
        || ChokeGroup != config.ChokeGroup
        || ColorToken != config.ColorToken
        || OutputGroup != config.OutputGroup
        || IsEnabled != config.IsEnabled
        || VelocityBehavior != config.VelocityBehavior
        || PlaybackMode != config.PlaybackMode;

    public void ApplyTo(PadConfig config)
    {
        config.Label = Label;
        config.AssignedNotes = AssignedNotes;
        config.SampleReference = SampleReference;
        config.Gain = Gain;
        config.Pan = Pan;
        config.PitchSemitones = PitchSemitones;
        config.ChokeGroup = ChokeGroup;
        config.ColorToken = ColorToken;
        config.OutputGroup = OutputGroup;
        config.IsEnabled = IsEnabled;
        config.VelocityBehavior = VelocityBehavior;
        config.PlaybackMode = PlaybackMode;
    }
}
