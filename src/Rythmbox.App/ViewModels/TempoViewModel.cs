using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.Core.Engine;
using Rythmbox.Core.Models;

namespace Rythmbox.App.ViewModels;

public sealed partial class TempoViewModel : ViewModelBase
{
    private readonly TempoPresetService _presets;
    private readonly PlayerViewModel _player;
    private readonly StatusViewModel _status;

    public TempoViewModel(TempoPresetService presets, PlayerViewModel player, StatusViewModel status)
    {
        _presets = presets;
        _player = player;
        _status = status;
    }

    public IReadOnlyList<TempoPreset> Presets => _presets.Presets;

    [ObservableProperty]
    private bool _isPickerOpen;

    [ObservableProperty]
    private string _presetName = "User";

    public double Tempo
    {
        get => _player.UserTempo;
        set
        {
            _player.UserTempo = Math.Clamp(value, 1, 999);
            PresetName = "User";
            OnPropertyChanged();
            OnPropertyChanged(nameof(TempoLabel));
        }
    }

    public string TempoLabel => $"{Tempo:0.0}";

    [RelayCommand]
    private void TogglePicker() => IsPickerOpen = !IsPickerOpen;

    [RelayCommand]
    private void SelectPreset(TempoPreset preset)
    {
        Tempo = preset.Bpm;
        PresetName = preset.Name;
        IsPickerOpen = false;
        _status.Show($"Tempo: {preset.Name} ({preset.Bpm:0.0} BPM)");
    }

    partial void OnSelectedPresetInPickerChanged(TempoPreset? value)
    {
        if (value is not null && IsPickerOpen)
        {
            SelectPreset(value);
        }
    }

    [ObservableProperty]
    private TempoPreset? _selectedPresetInPicker;

    public void Nudge(double delta)
    {
        Tempo += delta;
        _status.Show($"Tempo: {Tempo:0.0} BPM");
    }
}
