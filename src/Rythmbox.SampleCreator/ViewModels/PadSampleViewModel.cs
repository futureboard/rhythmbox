using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.Core.Models;
using Rythmbox.Core.Samples;

namespace Rythmbox.SampleCreator.ViewModels;

public sealed partial class PadSampleViewModel : ViewModelBase
{
    // The canvas resamples this high-resolution envelope to the available pixels.
    // At 32 KB per pad it stays inexpensive while retaining transient detail.
    private const int WaveformPeakCount = 4096;

    private readonly SampleCreatorViewModel _creator;
    private bool _couplingTrim;
    private bool _syncingSelection;
    private bool _isEditorActive;

    public PadSampleViewModel(SampleCreatorViewModel creator, DrumSample sample, int index)
    {
        _creator = creator;
        Sample = sample;
        Index = index;
        _midiNote = sample.MidiNote is >= 0 and <= 127 ? sample.MidiNote : GmPercussionMap.Pads[index].Note;
        _outputGroup = sample.OutputGroup;
        _gain = sample.Gain;
        _pitch = 0;
        _attackMs = sample.Envelope.AttackMs;
        _decayMs = sample.Envelope.DecayMs;
        _sustain = sample.Envelope.SustainLevel;
        _releaseMs = sample.Envelope.ReleaseMs;
        RebuildLayers();
        EnsureSelection();
        RefreshFromSelection();
    }

    public int Index { get; }

    public DrumSample Sample { get; }

    public string Label => Sample.Label;

    public IReadOnlyList<DrumMixGroup> OutputGroups => GmPercussionMap.MixGroups;

    public ObservableCollection<VelocityLayerViewModel> Layers { get; } = new();

    public bool HasAudio => Sample.HasAudio;

    public bool HasVelocityLayers => Sample.HasVelocityLayers;

    public bool IsSingleSample => (Sample.Samples.Length > 0 || Sample.MappedSample is not null) && !Sample.HasVelocityLayers;

    public string DurationLabel
    {
        get
        {
            var frameCount = ActiveBuffer.Length > 0 ? ActiveBuffer.Length : Sample.FrameCount;
            if (frameCount > 0)
            {
                var rate = ActiveBuffer.Length > 0
                    ? Sample.SampleRate > 0 ? Sample.SampleRate : WavCodec.TargetSampleRate
                    : Sample.EffectiveSampleRate;
                return $"{TimeSpan.FromSeconds((double)frameCount / rate).TotalMilliseconds:0} ms";
            }

            return Sample.HasVelocityLayers ? "layers" : "no sample";
        }
    }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GainLabel))]
    private double _gain = 1.0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PitchLabel))]
    private double _pitch;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MidiNoteLabel))]
    private int _midiNote;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OutputGroupLabel))]
    private DrumMixGroup _outputGroup;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AttackLabel))]
    private double _attackMs;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DecayLabel))]
    private double _decayMs;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SustainLabel))]
    private double _sustain = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReleaseLabel))]
    private double _releaseMs;

    [ObservableProperty]
    private double _trimStart;

    [ObservableProperty]
    private double _trimEnd = 1.0;

    [ObservableProperty]
    private string _fileName = "(no sample — import WAV)";

    [ObservableProperty]
    private int _sourceSampleRate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectionLabel))]
    private VelocityLayerViewModel? _selectedLayer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectionLabel))]
    private RoundRobinSlotViewModel? _selectedRoundRobin;

    // Single selection enum for the Layer Engine inspector. Defaults to Pad so a
    // freshly selected pad shows the pad-level inspector until the user drills in.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPadFocus))]
    [NotifyPropertyChangedFor(nameof(IsLayerFocus))]
    [NotifyPropertyChangedFor(nameof(IsSampleFocus))]
    [NotifyPropertyChangedFor(nameof(InspectorTitle))]
    private LayerEngineFocus _focus = LayerEngineFocus.Pad;

    public bool IsPadFocus => Focus == LayerEngineFocus.Pad;

    public bool IsLayerFocus => Focus == LayerEngineFocus.Layer && SelectedLayer is not null;

    public bool IsSampleFocus => Focus == LayerEngineFocus.Sample && SelectedRoundRobin is not null;

    public string InspectorTitle => Focus switch
    {
        LayerEngineFocus.Layer when SelectedLayer is not null => "LAYER INSPECTOR",
        LayerEngineFocus.Sample when SelectedRoundRobin is not null => "SAMPLE INSPECTOR",
        _ => "PAD INSPECTOR",
    };

    public bool ShowLayerEmptyState => !HasVelocityLayers;

    public string GainLabel => $"{Gain:0.00}";

    public string PitchLabel => Math.Abs(Pitch) < 0.05
        ? "0 st"
        : $"{Pitch:+0.0;-0.0} st";

    public string MidiNoteLabel => $"{FormatMidiNote(MidiNote)} / {MidiNote}";

    public string OutputGroupLabel => GmPercussionMap.GetMixGroupLabel(OutputGroup);

    public string AttackLabel => $"{AttackMs:0} ms";

    public string DecayLabel => $"{DecayMs:0} ms";

    public string SustainLabel => $"{Sustain * 100:0}%";

    public string ReleaseLabel => $"{ReleaseMs:0} ms";

    public string SampleRateLabel => HasAudio
        ? SourceSampleRate > 0 && SourceSampleRate != Sample.SampleRate
            ? $"{Sample.SampleRate / 1000.0:0.#} kHz (from {SourceSampleRate / 1000.0:0.#} kHz)"
            : $"{(Sample.SampleRate > 0 ? Sample.SampleRate : WavCodec.TargetSampleRate) / 1000.0:0.#} kHz"
        : string.Empty;

    public string SelectionLabel
    {
        get
        {
            if (SelectedLayer is null || SelectedRoundRobin is null)
            {
                return IsSingleSample ? "Single sample" : "No layer selected";
            }

            return $"Layer {SelectedLayer.Index + 1} · RR {SelectedRoundRobin.Index + 1} · vel {SelectedLayer.RangeLabel}";
        }
    }

    public IReadOnlyList<WaveformPeak> WaveformPeaks { get; private set; } = [];

    private float[] ActiveBuffer
    {
        get
        {
            if (!_isEditorActive)
            {
                return [];
            }

            if (SelectedRoundRobin is { } roundRobin && SelectedLayer is { } layer)
            {
                var samples = layer.Layer.EnsureEditableRoundRobinSamples(roundRobin.Index);
                if (!ReferenceEquals(samples, roundRobin.Samples) || samples.Length != roundRobin.Samples.Length)
                {
                    roundRobin.Replace(samples, roundRobin.Path);
                }

                return samples;
            }

            return Sample.EnsureEditableSamples();
        }
    }

    /// <summary>Called only for the selected pad, keeping the remaining kit mapped.</summary>
    public void ActivateForEditing()
    {
        if (_isEditorActive)
        {
            return;
        }

        _isEditorActive = true;
        RefreshFromSelection();
    }

    partial void OnGainChanged(double value)
    {
        Sample.Gain = (float)value;
        _creator.NotifyKitEdited();
    }

    partial void OnPitchChanged(double value)
    {
        if (Math.Abs(value) < 0.25)
        {
            if (Math.Abs(value) > 0.001)
            {
                Pitch = 0;
            }

            Sample.PitchSemitones = 0f;
            return;
        }

        Sample.PitchSemitones = (float)value;
    }

    partial void OnMidiNoteChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 127);
        if (clamped != value)
        {
            MidiNote = clamped;
            return;
        }

        Sample.MidiNote = clamped;
        Sample.MidiNotes = [clamped];
        _creator.NotifyKitEdited($"{Label}: MIDI {FormatMidiNote(clamped)}");
    }

    partial void OnOutputGroupChanged(DrumMixGroup value)
    {
        Sample.OutputGroup = value;
        _creator.NotifyKitEdited($"{Label}: routed to {GmPercussionMap.GetMixGroupLabel(value)}");
    }

    private static string FormatMidiNote(int note)
    {
        var names = new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        var clamped = Math.Clamp(note, 0, 127);
        return $"{names[clamped % 12]}{clamped / 12}";
    }

    partial void OnAttackMsChanged(double value)
    {
        Sample.Envelope.AttackMs = (float)Math.Clamp(value, 0, 5_000);
        _creator.NotifyKitEdited();
    }

    partial void OnDecayMsChanged(double value)
    {
        Sample.Envelope.DecayMs = (float)Math.Clamp(value, 0, 10_000);
        _creator.NotifyKitEdited();
    }

    partial void OnSustainChanged(double value)
    {
        Sample.Envelope.SustainLevel = (float)Math.Clamp(value, 0, 1);
        _creator.NotifyKitEdited();
    }

    partial void OnReleaseMsChanged(double value)
    {
        Sample.Envelope.ReleaseMs = (float)Math.Clamp(value, 0, 10_000);
        _creator.NotifyKitEdited();
    }

    partial void OnTrimStartChanged(double value)
    {
        if (_couplingTrim)
        {
            return;
        }

        if (TrimEnd < value)
        {
            _couplingTrim = true;
            TrimEnd = value;
            _couplingTrim = false;
        }
    }

    partial void OnTrimEndChanged(double value)
    {
        if (_couplingTrim)
        {
            return;
        }

        if (value < TrimStart)
        {
            _couplingTrim = true;
            TrimStart = value;
            _couplingTrim = false;
        }
    }

    partial void OnSelectedLayerChanged(VelocityLayerViewModel? value)
    {
        UpdateLayerHighlight();

        if (_syncingSelection || value is null)
        {
            return;
        }

        if (SelectedRoundRobin is null || !value.RoundRobins.Contains(SelectedRoundRobin))
        {
            _syncingSelection = true;
            SelectedRoundRobin = value.RoundRobins.FirstOrDefault();
            _syncingSelection = false;
        }

        RefreshFromSelection();
    }

    partial void OnSelectedRoundRobinChanged(RoundRobinSlotViewModel? value)
    {
        UpdateSampleHighlight();

        if (_syncingSelection)
        {
            return;
        }

        if (value is not null)
        {
            var owner = Layers.FirstOrDefault(layer => layer.RoundRobins.Contains(value));
            if (owner is not null && !ReferenceEquals(SelectedLayer, owner))
            {
                _syncingSelection = true;
                SelectedLayer = owner;
                _syncingSelection = false;
            }
        }

        RefreshFromSelection();
    }

    private void UpdateLayerHighlight()
    {
        foreach (var layer in Layers)
        {
            layer.IsSelected = ReferenceEquals(layer, SelectedLayer);
        }

        OnPropertyChanged(nameof(IsLayerFocus));
    }

    private void UpdateSampleHighlight()
    {
        foreach (var layer in Layers)
        {
            foreach (var slot in layer.RoundRobins)
            {
                slot.IsSelected = ReferenceEquals(slot, SelectedRoundRobin);
            }
        }

        OnPropertyChanged(nameof(IsSampleFocus));
    }

    /// <summary>Select a velocity layer and switch the inspector to layer mode.</summary>
    [RelayCommand]
    private void FocusLayer(VelocityLayerViewModel? layer)
    {
        if (layer is null)
        {
            return;
        }

        SelectedLayer = layer;
        layer.IsExpanded = true;
        Focus = LayerEngineFocus.Layer;
    }

    /// <summary>Select a round-robin sample and switch the inspector to sample mode.</summary>
    [RelayCommand]
    private void FocusSample(RoundRobinSlotViewModel? slot)
    {
        if (slot is null)
        {
            return;
        }

        SelectRoundRobin(slot);
        Focus = LayerEngineFocus.Sample;
    }

    /// <summary>Return the inspector to pad-level actions.</summary>
    [RelayCommand]
    private void FocusPad() => Focus = LayerEngineFocus.Pad;

    [RelayCommand]
    private static void ToggleLayerExpanded(VelocityLayerViewModel? layer)
    {
        if (layer is not null)
        {
            layer.IsExpanded = !layer.IsExpanded;
        }
    }

    public void RebuildLayers()
    {
        var previousLow = SelectedLayer is null ? (int?)null : (int)SelectedLayer.VelocityLow;
        var previousRr = SelectedRoundRobin?.Index;

        Layers.Clear();
        for (var i = 0; i < Sample.VelocityLayers.Count; i++)
        {
            Layers.Add(new VelocityLayerViewModel(this, Sample.VelocityLayers[i], i));
        }

        _syncingSelection = true;
        SelectedLayer = previousLow is { } low
            ? Layers.FirstOrDefault(layer => (int)layer.VelocityLow == low) ?? Layers.FirstOrDefault()
            : Layers.FirstOrDefault();

        SelectedRoundRobin = SelectedLayer is null
            ? null
            : previousRr is { } rrIndex && rrIndex < SelectedLayer.RoundRobins.Count
                ? SelectedLayer.RoundRobins[rrIndex]
                : SelectedLayer.RoundRobins.FirstOrDefault();
        _syncingSelection = false;

        UpdateLayerHighlight();
        UpdateSampleHighlight();

        // A pad with no layers can only show pad-level actions.
        if (Layers.Count == 0)
        {
            Focus = LayerEngineFocus.Pad;
        }

        OnPropertyChanged(nameof(HasVelocityLayers));
        OnPropertyChanged(nameof(IsSingleSample));
        OnPropertyChanged(nameof(HasAudio));
        OnPropertyChanged(nameof(ShowLayerEmptyState));
    }

    public void EnsureSelection()
    {
        if (SelectedLayer is null && Layers.Count > 0)
        {
            SelectedLayer = Layers[0];
        }

        if (SelectedLayer is not null && SelectedRoundRobin is null)
        {
            SelectedRoundRobin = SelectedLayer.RoundRobins.FirstOrDefault();
        }
    }

    public void SelectRoundRobin(RoundRobinSlotViewModel slot)
    {
        var owner = Layers.FirstOrDefault(layer => layer.RoundRobins.Contains(slot));
        _syncingSelection = true;
        if (owner is not null)
        {
            SelectedLayer = owner;
        }

        SelectedRoundRobin = slot;
        _syncingSelection = false;
        RefreshFromSelection();
    }

    public void OnRoundRobinRemoved(VelocityLayerViewModel layer)
    {
        if (ReferenceEquals(SelectedLayer, layer))
        {
            SelectedRoundRobin = layer.RoundRobins.FirstOrDefault();
        }

        RefreshFromSelection();
        UpdateFileNameSummary();
    }

    public void RefreshFromSelection()
    {
        if (Sample.SampleRate <= 0)
        {
            Sample.SampleRate = WavCodec.TargetSampleRate;
        }

        UpdateFileNameSummary();
        TrimStart = 0;
        TrimEnd = 1;
        RefreshWaveform();
        OnPropertyChanged(nameof(HasAudio));
        OnPropertyChanged(nameof(DurationLabel));
        OnPropertyChanged(nameof(SampleRateLabel));
        OnPropertyChanged(nameof(SelectionLabel));
        OnPropertyChanged(nameof(HasVelocityLayers));
        OnPropertyChanged(nameof(IsSingleSample));
        OnPropertyChanged(nameof(ShowLayerEmptyState));
        OnPropertyChanged(nameof(IsLayerFocus));
        OnPropertyChanged(nameof(IsSampleFocus));
        OnPropertyChanged(nameof(InspectorTitle));
    }

    public void NotifyLayerEdited(string? status = null)
    {
        UpdateFileNameSummary();
        OnPropertyChanged(nameof(HasAudio));
        OnPropertyChanged(nameof(HasVelocityLayers));
        OnPropertyChanged(nameof(DurationLabel));
        OnPropertyChanged(nameof(SelectionLabel));
        OnPropertyChanged(nameof(ShowLayerEmptyState));
        _creator.NotifyKitEdited(status);
    }

    public void PreviewRoundRobin(RoundRobinSlotViewModel slot)
    {
        SelectRoundRobin(slot);
        _creator.PreviewBuffer(ActiveBuffer, Sample.Gain, Sample.PitchSemitones, $"{Label} RR{slot.Index + 1}");
    }

    public void LoadFromFile(string path)
    {
        var samples = WavCodec.LoadMono(path, out var sourceRate);

        if (SelectedLayer is not null && Sample.HasVelocityLayers)
        {
            if (SelectedRoundRobin is not null)
            {
                SelectedLayer.ReplaceRoundRobin(SelectedRoundRobin, samples, path);
            }
            else
            {
                SelectedLayer.AddRoundRobin(samples, path);
            }

            SourceSampleRate = sourceRate;
            Sample.SampleRate = WavCodec.TargetSampleRate;
            return;
        }

        Sample.VelocityLayers.Clear();
        RebuildLayers();
        Sample.Samples = samples;
        Sample.MappedSample = null;
        Sample.SampleRate = WavCodec.TargetSampleRate;
        Sample.PitchSemitones = 0f;
        Pitch = 0;
        SourceSampleRate = sourceRate;
        Sample.FilePath = path;
        FileName = Path.GetFileName(path);
        TrimStart = 0;
        TrimEnd = 1;

        var status = $"Loaded {Label}: {FileName}";
        status += $" — {SampleRateLabel}";
        RefreshFromSelection();
        _creator.NotifyKitEdited(status);
    }

    public void RefreshWaveform()
    {
        var buffer = ActiveBuffer;
        WaveformPeaks = buffer.Length > 0
            ? WavCodec.BuildWaveformEnvelope(buffer, WaveformPeakCount)
            : [];
        OnPropertyChanged(nameof(WaveformPeaks));
    }

    [RelayCommand]
    private void Preview()
    {
        var buffer = ActiveBuffer;
        if (buffer.Length == 0)
        {
            _creator.StatusText = $"{Label} has no sample";
            return;
        }

        _creator.PreviewBuffer(buffer, Sample.Gain, Sample.PitchSemitones, Label);
    }

    [RelayCommand]
    private void Clear()
    {
        if (SelectedLayer is not null && SelectedRoundRobin is not null && Sample.HasVelocityLayers)
        {
            SelectedLayer.RemoveRoundRobin(SelectedRoundRobin);
            return;
        }

        Sample.Samples = [];
        Sample.MappedSample = null;
        Sample.FilePath = null;
        Sample.VelocityLayers.Clear();
        RebuildLayers();
        FileName = "(no sample)";
        SourceSampleRate = 0;
        Pitch = 0;
        Sample.PitchSemitones = 0f;
        TrimStart = 0;
        TrimEnd = 1;
        RefreshFromSelection();
        _creator.NotifyKitEdited($"Cleared {Label}");
    }

    [RelayCommand]
    private void Normalize()
    {
        if (SelectedRoundRobin is not null && Sample.HasVelocityLayers)
        {
            if (SelectedRoundRobin.Samples.Length == 0)
            {
                return;
            }

            WavCodec.Normalize(SelectedRoundRobin.Samples);
            SelectedLayer?.ReplaceRoundRobin(
                SelectedRoundRobin,
                SelectedRoundRobin.Samples,
                SelectedRoundRobin.Path,
                $"Normalized {Label} RR{SelectedRoundRobin.Index + 1}");
            return;
        }

        if (Sample.Samples.Length == 0)
        {
            return;
        }

        WavCodec.Normalize(Sample.Samples);
        RefreshWaveform();
        _creator.NotifyKitEdited($"Normalized {Label}");
    }

    [RelayCommand]
    private void ApplyTrim()
    {
        if (SelectedRoundRobin is not null && Sample.HasVelocityLayers)
        {
            if (SelectedRoundRobin.Samples.Length == 0)
            {
                return;
            }

            var trimmed = WavCodec.Trim(SelectedRoundRobin.Samples, TrimStart, TrimEnd);
            SelectedLayer?.ReplaceRoundRobin(
                SelectedRoundRobin,
                trimmed,
                SelectedRoundRobin.Path,
                $"Trimmed {Label} RR{SelectedRoundRobin.Index + 1}");
            return;
        }

        if (Sample.Samples.Length == 0)
        {
            return;
        }

        Sample.Samples = WavCodec.Trim(Sample.Samples, TrimStart, TrimEnd);
        TrimStart = 0;
        TrimEnd = 1;
        RefreshFromSelection();
        _creator.NotifyKitEdited($"Trimmed {Label}");
    }

    [RelayCommand]
    private void ApplyPitch()
    {
        if (MathF.Abs(Sample.PitchSemitones) < 0.001f)
        {
            return;
        }

        if (SelectedRoundRobin is not null && Sample.HasVelocityLayers)
        {
            if (SelectedRoundRobin.Samples.Length == 0)
            {
                return;
            }

            var shifted = WavCodec.PitchShift(SelectedRoundRobin.Samples, Sample.PitchSemitones);
            SelectedLayer?.ReplaceRoundRobin(
                SelectedRoundRobin,
                shifted,
                SelectedRoundRobin.Path,
                $"Pitch applied to {Label} RR{SelectedRoundRobin.Index + 1}");
            Pitch = 0;
            Sample.PitchSemitones = 0f;
            return;
        }

        if (Sample.Samples.Length == 0)
        {
            return;
        }

        Sample.Samples = WavCodec.PitchShift(Sample.Samples, Sample.PitchSemitones);
        Pitch = 0;
        Sample.PitchSemitones = 0f;
        RefreshFromSelection();
        _creator.NotifyKitEdited($"Pitch applied to {Label}");
    }

    [RelayCommand]
    private void AddLayer()
    {
        if (Sample.Samples.Length > 0 && !Sample.HasVelocityLayers)
        {
            Sample.VelocityLayers.Add(new VelocityLayer
            {
                VelocityLow = 1,
                VelocityHigh = 127,
                RoundRobinPaths = [Sample.FilePath ?? string.Empty],
                RoundRobinSamples = [Sample.Samples],
            });
            Sample.Samples = [];
            Sample.MappedSample = null;
            Sample.FilePath = null;
            RebuildLayers();
            EnsureSelection();
            RefreshFromSelection();
            NotifyLayerEdited($"Converted {Label} to velocity layers");
            return;
        }

        var low = Sample.VelocityLayers.Count > 0
            ? Math.Min(127, Sample.VelocityLayers.Max(static layer => layer.VelocityHigh) + 1)
            : 1;
        var high = Math.Min(127, low + 5);

        Sample.VelocityLayers.Add(new VelocityLayer
        {
            VelocityLow = low,
            VelocityHigh = high,
        });
        RebuildLayers();
        SelectedLayer = Layers[^1];
        NotifyLayerEdited($"Added layer {Layers.Count} ({low}–{high})");
    }

    [RelayCommand]
    private async Task AddRoundRobinAsync()
    {
        if (SelectedLayer is null)
        {
            if (Layers.Count == 0)
            {
                AddLayer();
            }

            if (SelectedLayer is null)
            {
                _creator.StatusText = "Add a velocity layer first";
                return;
            }
        }

        var path = await _creator.PickWavAsync();
        if (path is null)
        {
            return;
        }

        var samples = WavCodec.LoadMono(path, out var sourceRate);
        SourceSampleRate = sourceRate;
        Sample.SampleRate = WavCodec.TargetSampleRate;
        SelectedLayer.AddRoundRobin(samples, path);
        RefreshFromSelection();
    }

    /// <summary>Open a file picker and append the chosen WAV as a round robin on <paramref name="layer"/>.</summary>
    [RelayCommand]
    private async Task ImportToLayerAsync(VelocityLayerViewModel? layer)
    {
        layer ??= SelectedLayer;
        if (layer is null)
        {
            return;
        }

        FocusLayer(layer);
        var path = await _creator.PickWavAsync();
        if (path is null)
        {
            return;
        }

        var samples = WavCodec.LoadMono(path, out var sourceRate);
        SourceSampleRate = sourceRate;
        Sample.SampleRate = WavCodec.TargetSampleRate;
        layer.AddRoundRobin(samples, path);
        RefreshFromSelection();
    }

    /// <summary>Drop-target entry point: append every dropped WAV as a round robin on <paramref name="layer"/>.</summary>
    public void ImportFilesToLayer(VelocityLayerViewModel layer, IEnumerable<string> paths)
    {
        var wavs = paths
            .Where(static p => p.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)
                               || p.EndsWith(".wave", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (wavs.Count == 0)
        {
            return;
        }

        FocusLayer(layer);
        foreach (var path in wavs)
        {
            var samples = WavCodec.LoadMono(path, out var sourceRate);
            SourceSampleRate = sourceRate;
            Sample.SampleRate = WavCodec.TargetSampleRate;
            layer.AddRoundRobin(samples, path);
        }

        RefreshFromSelection();
    }

    public void RemoveLayer(VelocityLayerViewModel layer)
    {
        if ((uint)layer.Index >= (uint)Sample.VelocityLayers.Count)
        {
            return;
        }

        Sample.VelocityLayers.RemoveAt(layer.Index);
        RebuildLayers();
        EnsureSelection();
        RefreshFromSelection();
        NotifyLayerEdited($"Removed layer from {Label}");
    }

    private void UpdateFileNameSummary()
    {
        if (SelectedRoundRobin is not null)
        {
            FileName = SelectedRoundRobin.FileName;
            return;
        }

        if (Sample.FilePath is { } fp)
        {
            FileName = Path.GetFileName(fp);
            return;
        }

        if (Sample.HasVelocityLayers)
        {
            var layerCount = Sample.VelocityLayers.Count(static layer => layer.HasSamples);
            var rrCount = Sample.VelocityLayers.Sum(static layer => layer.RoundRobinSamples.Count);
            FileName = $"{layerCount} vel · {rrCount} samples";
            return;
        }

        FileName = "(no sample)";
    }
}
