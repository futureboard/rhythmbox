using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.Core.Engine;
using SoundFlow.Midi.Structs;
using SoundFlow.Synthesis.Banks;

namespace Rythmbox.App.ViewModels;

public sealed partial class SoundFontBrowserViewModel : ViewModelBase
{
    private const int AuditionChannel = 0;
    private const int AuditionVelocity = 100;

    private readonly SoundFontPlayer _soundFontPlayer;
    private readonly MidiInputService _midiInput;

    private List<PresetInfo> _allPresets = new();

    public SoundFontBrowserViewModel(SoundFontPlayer soundFontPlayer, MidiInputService midiInput)
    {
        _soundFontPlayer = soundFontPlayer;
        _midiInput = midiInput;
        RefreshMidiInputs();
    }

    public ObservableCollection<PresetInfo> Presets { get; } = new();

    public ObservableCollection<MidiDeviceInfo> MidiInputs { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoadButtonLabel))]
    private string? _loadedSoundFontName;

    public string LoadButtonLabel => LoadedSoundFontName ?? "Load SoundFont...";

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private PresetInfo? _selectedPreset;

    [ObservableProperty]
    private MidiDeviceInfo? _selectedMidiInput;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectLabel))]
    private bool _isMidiConnected;

    public string ConnectLabel => IsMidiConnected ? "Disconnect" : "Connect";

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedPresetChanged(PresetInfo? value)
    {
        if (value is not null && _soundFontPlayer.IsLoaded)
        {
            _soundFontPlayer.SelectPreset(AuditionChannel, value);
        }
    }

    public void LoadSoundFont(string path)
    {
        _soundFontPlayer.LoadSoundFont(path);
        LoadedSoundFontName = Path.GetFileName(path);

        _allPresets = _soundFontPlayer.Presets.ToList();
        ApplyFilter();
        SelectedPreset = _allPresets.FirstOrDefault();
    }

    public void PlayNote(int note) => _soundFontPlayer.NoteOn(AuditionChannel, note, AuditionVelocity);

    public void StopNote(int note) => _soundFontPlayer.NoteOff(AuditionChannel, note);

    [RelayCommand]
    private void RefreshMidiInputs()
    {
        _midiInput.RefreshDevices();

        MidiInputs.Clear();
        foreach (var device in _midiInput.AvailableInputs)
        {
            MidiInputs.Add(device);
        }
    }

    [RelayCommand]
    private void ToggleMidiConnection()
    {
        if (IsMidiConnected)
        {
            _midiInput.Disconnect();
            IsMidiConnected = false;
        }
        else if (SelectedMidiInput is { } device)
        {
            _midiInput.Connect(device, _soundFontPlayer);
            IsMidiConnected = true;
        }
    }

    private void ApplyFilter()
    {
        Presets.Clear();

        var query = string.IsNullOrWhiteSpace(SearchText)
            ? _allPresets
            : _allPresets.Where(preset => preset.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        foreach (var preset in query)
        {
            Presets.Add(preset);
        }
    }
}
