using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.Core.Models;
using Rythmbox.Core.Samples;

namespace Rythmbox.SampleCreator.ViewModels;

public sealed partial class PadSampleViewModel : ViewModelBase
{
    private readonly SampleCreatorViewModel _creator;

    public PadSampleViewModel(SampleCreatorViewModel creator, DrumSample sample, int index)
    {
        _creator = creator;
        Sample = sample;
        Index = index;
        _gain = sample.Gain;
        _pitch = 0;
        RefreshWaveform();
    }

    public int Index { get; }

    public DrumSample Sample { get; }

    public string Label => Sample.Label;

    public bool HasAudio => Sample.HasAudio;

    public string DurationLabel => Sample.HasAudio
        ? $"{Sample.Duration.TotalMilliseconds:0} ms"
        : "empty";

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

    private bool _couplingTrim;

    [ObservableProperty]
    private string _fileName = "(no sample)";

    [ObservableProperty]
    private int _sourceSampleRate;

    public string GainLabel => $"{Gain:0.00}";

    public string PitchLabel => Math.Abs(Pitch) < 0.05
        ? "0 st"
        : $"{Pitch:+0.0;-0.0} st";

    public string SampleRateLabel => HasAudio
        ? SourceSampleRate > 0 && SourceSampleRate != Sample.SampleRate
            ? $"{Sample.SampleRate / 1000.0:0.#} kHz (from {SourceSampleRate / 1000.0:0.#} kHz)"
            : $"{Sample.SampleRate / 1000.0:0.#} kHz"
        : string.Empty;

    public IReadOnlyList<WaveformPeak> WaveformPeaks { get; private set; } = [];

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

    public void LoadFromFile(string path)
    {
        var bytes = File.ReadAllBytes(path);
        Sample.Samples = WavCodec.DecodeMono(bytes, out var sourceRate, WavCodec.TargetSampleRate);
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
        OnPropertyChanged(nameof(HasAudio));
        OnPropertyChanged(nameof(DurationLabel));
        OnPropertyChanged(nameof(SampleRateLabel));
        RefreshWaveform();
        _creator.NotifyKitEdited(status);
    }

    public void RefreshWaveform()
    {
        WaveformPeaks = Sample.HasAudio
            ? WavCodec.BuildWaveformEnvelope(Sample.Samples, 256)
            : [];
        OnPropertyChanged(nameof(WaveformPeaks));
    }

    [RelayCommand]
    private void Preview() => _creator.PreviewPad(this);

    [RelayCommand]
    private void Clear()
    {
        Sample.Samples = [];
        Sample.FilePath = null;
        FileName = "(no sample)";
        SourceSampleRate = 0;
        Pitch = 0;
        Sample.PitchSemitones = 0f;
        TrimStart = 0;
        TrimEnd = 1;
        OnPropertyChanged(nameof(HasAudio));
        OnPropertyChanged(nameof(DurationLabel));
        OnPropertyChanged(nameof(SampleRateLabel));
        RefreshWaveform();
        _creator.NotifyKitEdited($"Cleared {Label}");
    }

    [RelayCommand]
    private void Normalize()
    {
        if (!Sample.HasAudio)
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
        if (!Sample.HasAudio)
        {
            return;
        }

        Sample.Samples = WavCodec.Trim(Sample.Samples, TrimStart, TrimEnd);
        TrimStart = 0;
        TrimEnd = 1;
        OnPropertyChanged(nameof(HasAudio));
        OnPropertyChanged(nameof(DurationLabel));
        RefreshWaveform();
        _creator.NotifyKitEdited($"Trimmed {Label}");
    }

    [RelayCommand]
    private void ApplyPitch()
    {
        if (!Sample.HasAudio || MathF.Abs(Sample.PitchSemitones) < 0.001f)
        {
            return;
        }

        Sample.Samples = WavCodec.PitchShift(Sample.Samples, Sample.PitchSemitones);
        Pitch = 0;
        Sample.PitchSemitones = 0f;
        OnPropertyChanged(nameof(HasAudio));
        OnPropertyChanged(nameof(DurationLabel));
        RefreshWaveform();
        _creator.NotifyKitEdited($"Pitch applied to {Label}");
    }
}
