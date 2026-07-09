using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.Core.Models;

namespace Rythmbox.SampleCreator.ViewModels;

public sealed partial class VelocityLayerViewModel : ViewModelBase
{
    public VelocityLayerViewModel(PadSampleViewModel pad, VelocityLayer layer, int index)
    {
        Pad = pad;
        Layer = layer;
        Index = index;
        _velocityLow = layer.VelocityLow;
        _velocityHigh = layer.VelocityHigh;
        RebuildRoundRobins();
    }

    public PadSampleViewModel Pad { get; }

    public VelocityLayer Layer { get; }

    public int Index { get; }

    public ObservableCollection<RoundRobinSlotViewModel> RoundRobins { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RangeLabel))]
    [NotifyPropertyChangedFor(nameof(Summary))]
    private decimal _velocityLow;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RangeLabel))]
    [NotifyPropertyChangedFor(nameof(Summary))]
    private decimal _velocityHigh;

    public string RangeLabel => $"{(int)VelocityLow}–{(int)VelocityHigh}";

    public string Summary => $"{RangeLabel} · {RoundRobins.Count} RR";

    partial void OnVelocityLowChanged(decimal value)
    {
        var clamped = Math.Clamp((int)value, 1, 127);
        if (clamped != value)
        {
            VelocityLow = clamped;
            return;
        }

        if (VelocityHigh < clamped)
        {
            VelocityHigh = clamped;
        }

        if (Layer.VelocityLow == clamped && Layer.VelocityHigh == (int)VelocityHigh)
        {
            return;
        }

        Layer.VelocityLow = clamped;
        Layer.VelocityHigh = (int)VelocityHigh;
        OnPropertyChanged(nameof(Summary));
        Pad.NotifyLayerEdited($"Layer {Index + 1} vel {RangeLabel}");
    }

    partial void OnVelocityHighChanged(decimal value)
    {
        var clamped = Math.Clamp((int)value, 1, 127);
        if (clamped != value)
        {
            VelocityHigh = clamped;
            return;
        }

        if (clamped < VelocityLow)
        {
            VelocityLow = clamped;
        }

        if (Layer.VelocityHigh == clamped && Layer.VelocityLow == (int)VelocityLow)
        {
            return;
        }

        Layer.VelocityHigh = clamped;
        Layer.VelocityLow = (int)VelocityLow;
        OnPropertyChanged(nameof(Summary));
        Pad.NotifyLayerEdited($"Layer {Index + 1} vel {RangeLabel}");
    }

    public void RebuildRoundRobins()
    {
        RoundRobins.Clear();
        for (var i = 0; i < Layer.RoundRobinSamples.Count; i++)
        {
            var path = i < Layer.RoundRobinPaths.Count ? Layer.RoundRobinPaths[i] : null;
            RoundRobins.Add(new RoundRobinSlotViewModel(this, i, Layer.RoundRobinSamples[i], path));
        }

        OnPropertyChanged(nameof(Summary));
    }

    public void AddRoundRobin(float[] samples, string? path)
    {
        Layer.RoundRobinSamples.Add(samples);
        Layer.RoundRobinPaths.Add(path ?? string.Empty);
        RebuildRoundRobins();
        Pad.SelectRoundRobin(RoundRobins[^1]);
        Pad.NotifyLayerEdited($"Added RR to layer {Index + 1}");
    }

    public void ReplaceRoundRobin(RoundRobinSlotViewModel slot, float[] samples, string? path, string? status = null)
    {
        if ((uint)slot.Index >= (uint)Layer.RoundRobinSamples.Count)
        {
            return;
        }

        Layer.RoundRobinSamples[slot.Index] = samples;
        while (Layer.RoundRobinPaths.Count <= slot.Index)
        {
            Layer.RoundRobinPaths.Add(string.Empty);
        }

        Layer.RoundRobinPaths[slot.Index] = path ?? string.Empty;
        slot.Replace(samples, path);
        Pad.RefreshFromSelection();
        Pad.NotifyLayerEdited(status ?? $"Updated RR {slot.Index + 1} on layer {Index + 1}");
    }

    public void RemoveRoundRobin(RoundRobinSlotViewModel slot)
    {
        if ((uint)slot.Index >= (uint)Layer.RoundRobinSamples.Count)
        {
            return;
        }

        Layer.RoundRobinSamples.RemoveAt(slot.Index);
        if (slot.Index < Layer.RoundRobinPaths.Count)
        {
            Layer.RoundRobinPaths.RemoveAt(slot.Index);
        }

        RebuildRoundRobins();
        Pad.OnRoundRobinRemoved(this);
        Pad.NotifyLayerEdited($"Removed RR from layer {Index + 1}");
    }

    [RelayCommand]
    private void Remove() => Pad.RemoveLayer(this);
}
