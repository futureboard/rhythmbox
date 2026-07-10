using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.Core.Samples;

namespace Rythmbox.SampleCreator.ViewModels;

public sealed partial class RoundRobinSlotViewModel : ViewModelBase
{
    private readonly VelocityLayerViewModel _layer;

    public RoundRobinSlotViewModel(VelocityLayerViewModel layer, int index, float[] samples, string? path)
    {
        _layer = layer;
        Index = index;
        Samples = samples;
        Path = path;
        FileName = string.IsNullOrWhiteSpace(path) ? $"(rr {index + 1})" : System.IO.Path.GetFileName(path);
        DurationLabel = samples.Length > 0
            ? $"{TimeSpan.FromSeconds((double)samples.Length / WavCodec.TargetSampleRate).TotalMilliseconds:0} ms"
            : "no sample";
    }

    public int Index { get; }

    /// <summary>Owning velocity layer — lets the sample row reach pad-level focus commands.</summary>
    public VelocityLayerViewModel Layer => _layer;

    /// <summary>Highlight flag driven by <see cref="PadSampleViewModel.SelectedRoundRobin"/>.</summary>
    [ObservableProperty]
    private bool _isSelected;

    public float[] Samples { get; private set; }

    public string? Path { get; private set; }

    public string FileName { get; private set; }

    public string DurationLabel { get; private set; }

    public string DisplayName => $"{Index + 1}. {FileName}";

    public void Replace(float[] samples, string? path)
    {
        Samples = samples;
        Path = path;
        FileName = string.IsNullOrWhiteSpace(path) ? $"(rr {Index + 1})" : System.IO.Path.GetFileName(path);
        DurationLabel = samples.Length > 0
            ? $"{TimeSpan.FromSeconds((double)samples.Length / WavCodec.TargetSampleRate).TotalMilliseconds:0} ms"
            : "no sample";
        OnPropertyChanged(nameof(Samples));
        OnPropertyChanged(nameof(Path));
        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(DurationLabel));
        OnPropertyChanged(nameof(DisplayName));
    }

    [RelayCommand]
    private void Preview() => _layer.Pad.PreviewRoundRobin(this);

    [RelayCommand]
    private void Remove() => _layer.RemoveRoundRobin(this);
}
