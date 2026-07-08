using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.App.Localization;
using Rythmbox.Core.Engine;
using Rythmbox.Core.Models;

namespace Rythmbox.App.ViewModels;

public sealed partial class TempoViewModel : ViewModelBase
{
    public const double TapTempoMin = 40;
    public const double TapTempoMax = 240;
    public const double TapResetSeconds = 2;

    private readonly TempoPresetService _presets;
    private readonly PlayerViewModel _player;
    private readonly StatusViewModel _status;
    private readonly LocalizationService _i18n;
    private readonly List<DateTime> _tapTimes = [];

    public TempoViewModel(
        TempoPresetService presets,
        PlayerViewModel player,
        StatusViewModel status,
        LocalizationService i18n)
    {
        _presets = presets;
        _player = player;
        _status = status;
        _i18n = i18n;
    }

    public IReadOnlyList<TempoPreset> Presets => _presets.Presets;

    [ObservableProperty]
    private bool _isPickerOpen;

    [ObservableProperty]
    private string _presetName = "User";

    [ObservableProperty]
    private bool _isAwaitingTap;

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

    [RelayCommand]
    private void TapTempo()
    {
        var now = DateTime.UtcNow;
        _tapTimes.RemoveAll(t => (now - t).TotalSeconds > TapResetSeconds);
        _tapTimes.Add(now);
        IsAwaitingTap = _tapTimes.Count == 1;

        if (_tapTimes.Count < 2)
        {
            _status.Show(_i18n["status.tapTempoKeepTapping"]);
            return;
        }

        var intervals = new List<double>();
        for (var i = 1; i < _tapTimes.Count; i++)
        {
            intervals.Add((_tapTimes[i] - _tapTimes[i - 1]).TotalSeconds);
        }

        var avgInterval = intervals.Average();
        if (avgInterval <= 0)
        {
            return;
        }

        var bpm = 60.0 / avgInterval;
        _player.UserTempo = Math.Clamp(bpm, TapTempoMin, TapTempoMax);
        PresetName = _i18n["tempo.tapPreset"];
        IsAwaitingTap = false;
        OnPropertyChanged(nameof(Tempo));
        OnPropertyChanged(nameof(TempoLabel));
        _status.Show(_i18n.Format("status.tapTempoSet", Tempo));
    }
}
