using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.Core.Engine;
using Rythmbox.Core.Models;

namespace Rythmbox.App.ViewModels;

/// <summary>
/// One physical percussion pad. Persistent assignment, input holds, audio
/// playback, and short hit animation deliberately remain separate states.
/// </summary>
public sealed partial class PadViewModel : ViewModelBase
{
    private static readonly TimeSpan HitFlashDuration = TimeSpan.FromMilliseconds(180);

    private readonly KitSamplePlayer? _kitPlayer;
    private readonly PadMappingService? _mapping;

    private PadViewModel(PercussionPad pad, KitSamplePlayer? kitPlayer, PadMappingService? mapping, bool isPlaceholder)
    {
        Pad = pad;
        _kitPlayer = kitPlayer;
        _mapping = mapping;
        IsPlaceholder = isPlaceholder;
        Config = new PadConfig
        {
            PadId = pad.Index,
            Label = pad.Label,
            MidiNote = pad.Note,
            OutputGroup = pad.Note >= 0 ? GmPercussionMap.GetMixGroup(pad.Note) : DrumMixGroup.Percussion,
        };
        EditDraft = PadEditDraft.FromConfig(Config);
    }

    public PadViewModel(PercussionPad pad, KitSamplePlayer kitPlayer, PadMappingService mapping)
        : this(pad, kitPlayer, mapping, isPlaceholder: false)
    {
    }

    public static PadViewModel CreatePlaceholder(int slotIndex) =>
        new(new PercussionPad(-1, string.Empty, -1, PadCategory.Drum, PadBus.Drum), null, null, isPlaceholder: true)
        {
            PlaceholderSlot = slotIndex,
        };

    public PercussionPad Pad { get; }

    public PadConfig Config { get; }

    public PadRuntimeState Runtime { get; } = new();

    public PadEditDraft EditDraft { get; private set; }

    public bool IsPlaceholder { get; }

    public int PlaceholderSlot { get; private init; }

    public int Number => IsPlaceholder ? 0 : Pad.Index + 1;

    public string Label => IsPlaceholder ? string.Empty : Pad.Label;

    /// <summary>Primary note retained for older callers; use <see cref="AssignedNotes"/> for complete routing.</summary>
    public int MidiNote => AssignedNotes.FirstOrDefault(-1);

    public IReadOnlyList<int> AssignedNotes => IsPlaceholder
        ? Array.Empty<int>()
        : _mapping?.GetMidiNotes(Pad.Index)
            ?? _kitPlayer?.GetPadMidiNotes(Pad.Index)
            ?? Config.AssignedNotes;

    public DrumMixGroup OutputGroup => IsPlaceholder
        ? DrumMixGroup.Percussion
        : _kitPlayer?.GetPadOutputGroup(Pad.Index) ?? GmPercussionMap.GetMixGroup(Pad.Note);

    public string OutputLabel => IsPlaceholder ? string.Empty : GmPercussionMap.GetMixGroupLabel(OutputGroup);

    public string NoteName => IsPlaceholder || MidiNote < 0 ? string.Empty : MidiNoteNames.Format(MidiNote);

    /// <summary>Full note list for tooltips and edit detail.</summary>
    public string NoteDetail => IsPlaceholder ? string.Empty : string.Join(", ", AssignedNotes.Select(note => $"{MidiNoteNames.Format(note)} / {note}"));

    /// <summary>Compact note list shown on the pad face.</summary>
    public string AssignedNotesLabel
    {
        get
        {
            if (AssignedNotes.Count == 0)
            {
                return "Click to assign";
            }

            if (AssignedNotes.Count == 1)
            {
                return $"{MidiNoteNames.Format(AssignedNotes[0])} / {AssignedNotes[0]}";
            }

            var compact = string.Join(", ", AssignedNotes.Take(2).Select(MidiNoteNames.Format));
            return AssignedNotes.Count > 2 ? $"{compact} +{AssignedNotes.Count - 2}" : compact;
        }
    }

    public string SampleDetail => IsPlaceholder ? string.Empty : HasSample ? AssignedNotesLabel : "Click to assign";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SampleDetail))]
    [NotifyPropertyChangedFor(nameof(IsAssigned))]
    private bool _hasSample;

    [ObservableProperty]
    private bool _isUsedInLoop;

    public bool IsHovered => Runtime.IsHovered;

    public bool IsMouseDown => Runtime.IsMouseDown;

    public bool IsSelected => Runtime.IsSelected;

    public bool IsEditing => Runtime.IsEditing;

    public bool IsPlaying => Runtime.IsPlaying;

    public bool IsHitFlashing => Runtime.HitFlashPhase > 0.001f;

    public double HitFlashIntensity => Runtime.HitFlashPhase * (0.35d + (Runtime.LastVelocity * 0.65d));

    public bool IsAssigned => HasSample || AssignedNotes.Count > 0;

    public string RenderState => !Config.IsEnabled
        ? "disabled"
        : IsEditing
            ? "editing"
            : IsSelected
                ? "selected"
                : IsMouseDown
                    ? "mouse-down"
                    : IsHitFlashing
                        ? "hit-flash"
                        : IsHovered
                            ? "hover"
                            : IsAssigned ? "assigned" : "empty";

    public bool IsDebugPadInspectorVisible
    {
        get
        {
#if DEBUG
            return IsHovered || IsSelected;
#else
            return false;
#endif
        }
    }

    public string DebugPadInspector
    {
        get
        {
#if DEBUG
            var (sourcePeak, meterPeak) = GetDebugAudioPeaks();
            return $"pad={Pad.Index} {Label}\nnotes=[{string.Join(',', AssignedNotes)}] held=[{string.Join(',', Runtime.HeldNotes.Order())}] voices={Runtime.ActiveVoiceCount}\nselected={IsSelected} mouseDown={IsMouseDown} hover={IsHovered} editing={IsEditing} playing={IsPlaying}\ntrigger={Runtime.LastTriggerTime:HH:mm:ss.fff} flash={Runtime.HitFlashPhase:0.000} velocity={Runtime.LastVelocity:0.00}\nrender={RenderState} route={OutputLabel} source={sourcePeak:0.000} meter={meterPeak:0.000}";
#else
            return string.Empty;
#endif
        }
    }

    public bool IsDirty => Runtime.HasDirtyTempEdit;

    public void Press()
    {
        if (!IsPlaceholder && _kitPlayer is not null)
        {
            _kitPlayer.TriggerPad(Pad.Index, 110f / 127f);
        }
    }

    public void PointerDown()
    {
        Runtime.IsMouseDown = true;
        NotifyVisualStateChanged();
    }

    public void Release()
    {
        Runtime.IsMouseDown = false;
        NotifyVisualStateChanged();
    }

    public void SetHovered(bool hovered)
    {
        Runtime.IsHovered = hovered;
        NotifyVisualStateChanged();
    }

    public void ClearPointerState() => Release();

    /// <summary>Receives a real accepted trigger from the player, including sequencer and MIDI paths.</summary>
    public void OnPadTriggered(int sourceNote, int velocity)
    {
        var now = DateTimeOffset.UtcNow;
        if (sourceNote >= 0)
        {
            Runtime.RegisterNoteOn(sourceNote, now, velocity / 127f);
        }
        else
        {
            Runtime.RegisterTrigger(now, velocity / 127f);
        }

        NotifyVisualStateChanged();
    }

    public void OnPadNoteReleased(int sourceNote)
    {
        Runtime.RegisterNoteOff(sourceNote, DateTimeOffset.UtcNow);
        NotifyVisualStateChanged();
    }

    public void ClearHeldRuntimeState()
    {
        Runtime.ClearHeldNotes(DateTimeOffset.UtcNow);
        NotifyVisualStateChanged();
    }

    /// <summary>Runs on the UI timer; it reads actual voice counts and decays only visual flash state.</summary>
    public void UpdateRuntime(DateTimeOffset now)
    {
        if (IsPlaceholder)
        {
            return;
        }

        var oldPhase = Runtime.HitFlashPhase;
        var oldVoices = Runtime.ActiveVoiceCount;
        Runtime.ActiveVoiceCount = _kitPlayer?.GetActiveVoiceCount(Pad.Index) ?? 0;
        Runtime.UpdateHitFlash(now, HitFlashDuration);

        if (Math.Abs(oldPhase - Runtime.HitFlashPhase) > 0.0001f || oldVoices != Runtime.ActiveVoiceCount)
        {
            NotifyVisualStateChanged();
        }
    }

    public void RefreshRouting()
    {
        Config.AssignedNotes = [.. AssignedNotes];
        Config.OutputGroup = OutputGroup;
        EditDraft = PadEditDraft.FromConfig(Config);
        Runtime.HasDirtyTempEdit = false;
        OnPropertyChanged(nameof(AssignedNotes));
        OnPropertyChanged(nameof(MidiNote));
        OnPropertyChanged(nameof(OutputGroup));
        OnPropertyChanged(nameof(OutputLabel));
        OnPropertyChanged(nameof(NoteName));
        OnPropertyChanged(nameof(NoteDetail));
        OnPropertyChanged(nameof(AssignedNotesLabel));
        OnPropertyChanged(nameof(SampleDetail));
        OnPropertyChanged(nameof(EditDraft));
        OnPropertyChanged(nameof(IsDirty));
        NotifyVisualStateChanged();
    }

    public void BeginEdit()
    {
        Runtime.IsEditing = true;
        Runtime.IsSelected = true;
        EditDraft = PadEditDraft.FromConfig(Config);
        Runtime.HasDirtyTempEdit = false;
        OnPropertyChanged(nameof(EditDraft));
        OnPropertyChanged(nameof(IsDirty));
        NotifyVisualStateChanged();
    }

    public void PreviewMidiNote(int midiNote)
    {
        EditDraft.MidiNote = Math.Clamp(midiNote, 0, 127);
        Runtime.PreviewPending = true;
        Runtime.HasDirtyTempEdit = EditDraft.DiffersFrom(Config);
        OnPropertyChanged(nameof(EditDraft));
        OnPropertyChanged(nameof(IsDirty));
    }

    public void AddDraftMidiNote(int midiNote)
    {
        EditDraft.AssignedNotes = PadConfig.NormalizeNotes(EditDraft.AssignedNotes.Append(midiNote));
        Runtime.HasDirtyTempEdit = EditDraft.DiffersFrom(Config);
        OnPropertyChanged(nameof(EditDraft));
        OnPropertyChanged(nameof(IsDirty));
    }

    public void RemoveDraftMidiNote(int midiNote)
    {
        EditDraft.AssignedNotes = EditDraft.AssignedNotes.Where(note => note != midiNote).ToList();
        Runtime.HasDirtyTempEdit = EditDraft.DiffersFrom(Config);
        OnPropertyChanged(nameof(EditDraft));
        OnPropertyChanged(nameof(IsDirty));
    }

    public void CommitEdit()
    {
        EditDraft.ApplyTo(Config);
        _mapping?.SetMidiNotes(Pad.Index, Config.AssignedNotes);
        Runtime.IsEditing = false;
        Runtime.PreviewPending = false;
        Runtime.HasDirtyTempEdit = false;
        RefreshRouting();
    }

    public void CancelEdit()
    {
        EditDraft = PadEditDraft.FromConfig(Config);
        Runtime.IsEditing = false;
        Runtime.PreviewPending = false;
        Runtime.HasDirtyTempEdit = false;
        OnPropertyChanged(nameof(EditDraft));
        OnPropertyChanged(nameof(IsDirty));
        NotifyVisualStateChanged();
    }

    [RelayCommand]
    private void Hit() => Press();

    private (float SourcePeak, float MeterPeak) GetDebugAudioPeaks()
    {
        if (_kitPlayer is null || IsPlaceholder)
        {
            return (0f, 0f);
        }

        var trace = _kitPlayer.GetAudioGraphTraceSnapshot();
        var sourcePeak = trace.LastSourcePadIndex == Pad.Index ? trace.LastPeakSource : 0f;
        var meter = _kitPlayer.PeekPadMeter(Pad.Index);
        return (sourcePeak, (float)meter.PeakLeft);
    }

    private void NotifyVisualStateChanged()
    {
        OnPropertyChanged(nameof(IsHovered));
        OnPropertyChanged(nameof(IsMouseDown));
        OnPropertyChanged(nameof(IsSelected));
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(IsPlaying));
        OnPropertyChanged(nameof(IsHitFlashing));
        OnPropertyChanged(nameof(HitFlashIntensity));
        OnPropertyChanged(nameof(IsAssigned));
        OnPropertyChanged(nameof(RenderState));
        OnPropertyChanged(nameof(IsDebugPadInspectorVisible));
        OnPropertyChanged(nameof(DebugPadInspector));
    }
}
