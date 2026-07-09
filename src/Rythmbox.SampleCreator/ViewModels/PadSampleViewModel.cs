using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.Core.Models;
using Rythmbox.Core.Samples;

namespace Rythmbox.SampleCreator.ViewModels;

public sealed partial class PadSampleViewModel : ViewModelBase
{
    private readonly SampleCreatorViewModel _creator;
    private bool _couplingTrim;
    private bool _syncingSelection;

    public PadSampleViewModel(SampleCreatorViewModel creator, DrumSample sample, int index)
    {
        _creator = creator;
        Sample = sample;
        Index = index;
        _gain = sample.Gain;
        _pitch = 0;
        RebuildLayers();
        EnsureSelection();
        RefreshFromSelection();
    }

    public int Index { get; }

    public DrumSample Sample { get; }

    public string Label => Sample.Label;

    public ObservableCollection<VelocityLayerViewModel> Layers { get; } = new();

    public bool HasAudio => Sample.HasAudio;

    public bool HasVelocityLayers => Sample.HasVelocityLayers;

    public bool IsSingleSample => Sample.Samples.Length > 0 && !Sample.HasVelocityLayers;

    public string DurationLabel
    {
        get
        {
            var buffer = ActiveBuffer;
            if (buffer.Length > 0)
            {
                var rate = Sample.SampleRate > 0 ? Sample.SampleRate : WavCodec.TargetSampleRate;
                return $"{TimeSpan.FromSeconds((double)buffer.Length / rate).TotalMilliseconds:0} ms";
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

    public string GainLabel => $"{Gain:0.00}";

    public string PitchLabel => Math.Abs(Pitch) < 0.05
        ? "0 st"
        : $"{Pitch:+0.0;-0.0} st";

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

    private float[] ActiveBuffer =>
        SelectedRoundRobin is { Samples.Length: > 0 } rr
            ? rr.Samples
            : Sample.Samples;

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

        OnPropertyChanged(nameof(HasVelocityLayers));
        OnPropertyChanged(nameof(IsSingleSample));
        OnPropertyChanged(nameof(HasAudio));
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
    }

    public void NotifyLayerEdited(string? status = null)
    {
        UpdateFileNameSummary();
        OnPropertyChanged(nameof(HasAudio));
        OnPropertyChanged(nameof(HasVelocityLayers));
        OnPropertyChanged(nameof(DurationLabel));
        OnPropertyChanged(nameof(SelectionLabel));
        _creator.NotifyKitEdited(status);
    }

    public void PreviewRoundRobin(RoundRobinSlotViewModel slot)
    {
        SelectRoundRobin(slot);
        _creator.PreviewBuffer(slot.Samples, Sample.Gain, Sample.PitchSemitones, $"{Label} RR{slot.Index + 1}");
    }

    public void LoadFromFile(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var samples = WavCodec.DecodeMono(bytes, out var sourceRate, WavCodec.TargetSampleRate);

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
        Sample.SampleRate = WavCodec.TargetSampleRate;
        Sample.PitchSemitones = 0f;
        Pitch = 0;
        SourceSampleRate = sourceRate;
        Sample.FilePath = path;
        FileName = Path.GetFileName(path);
        TrimStart = 0;
        TrimEnd = 1;

        var status = $"Loaded {Label}: {FileName}";
        if (WavCodec.TryReadSamplerRootNote(bytes, out var wavRootNote))
        {
            status += $" (ignored WAV root note {wavRootNote})";
        }

        status += $" — {SampleRateLabel}";
        RefreshFromSelection();
        _creator.NotifyKitEdited(status);
    }

    public void RefreshWaveform()
    {
        var buffer = ActiveBuffer;
        WaveformPeaks = buffer.Length > 0
            ? WavCodec.BuildWaveformEnvelope(buffer, 256)
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

        var bytes = File.ReadAllBytes(path);
        var samples = WavCodec.DecodeMono(bytes, out var sourceRate, WavCodec.TargetSampleRate);
        SourceSampleRate = sourceRate;
        Sample.SampleRate = WavCodec.TargetSampleRate;
        SelectedLayer.AddRoundRobin(samples, path);
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
