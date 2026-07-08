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
    private double _trimStart;

    [ObservableProperty]
    private double _trimEnd = 1.0;

    [ObservableProperty]
    private string _fileName = "(no sample)";

    public string GainLabel => $"{Gain:0.00}";

    public IReadOnlyList<float> WaveformPeaks { get; private set; } = [];

    partial void OnGainChanged(double value)
    {
        Sample.Gain = (float)value;
    }

    public void LoadFromFile(string path)
    {
        Sample.Samples = WavCodec.LoadMono(path);
        Sample.SampleRate = WavCodec.TargetSampleRate;
        Sample.FilePath = path;
        FileName = Path.GetFileName(path);
        TrimStart = 0;
        TrimEnd = 1;
        OnPropertyChanged(nameof(HasAudio));
        OnPropertyChanged(nameof(DurationLabel));
        RefreshWaveform();
        _creator.StatusText = $"Loaded {Label}: {FileName}";
    }

    public void RefreshWaveform()
    {
        WaveformPeaks = Sample.HasAudio
            ? WavCodec.BuildWaveformPeaks(Sample.Samples, 128)
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
        TrimStart = 0;
        TrimEnd = 1;
        OnPropertyChanged(nameof(HasAudio));
        OnPropertyChanged(nameof(DurationLabel));
        RefreshWaveform();
        _creator.StatusText = $"Cleared {Label}";
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
        _creator.StatusText = $"Normalized {Label}";
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
        _creator.StatusText = $"Trimmed {Label}";
    }
}
