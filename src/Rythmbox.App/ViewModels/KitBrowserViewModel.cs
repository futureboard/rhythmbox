using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.Core.Engine;
using Rythmbox.Core.Models;
using SoundFlow.Midi.Structs;

namespace Rythmbox.App.ViewModels;

public sealed partial class KitBrowserViewModel : ViewModelBase
{
    private readonly KitSamplePlayer _kitPlayer;
    private readonly KitPresetService _presetService;
    private readonly MidiInputService _midiInput;
    private readonly PadMidiRouter _padRouter;
    private string? _presetDir;

    public KitBrowserViewModel(
        KitSamplePlayer kitPlayer,
        KitPresetService presetService,
        MidiInputService midiInput,
        PadMidiRouter padRouter)
    {
        _kitPlayer = kitPlayer;
        _presetService = presetService;
        _midiInput = midiInput;
        _padRouter = padRouter;
        RefreshMidiInputs();
    }

    public ObservableCollection<KitPresetEntry> Kits { get; } = new();

    public ObservableCollection<PadAuditionViewModel> Pads { get; } = new();

    public ObservableCollection<MidiDeviceInfo> MidiInputs { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoadButtonLabel))]
    private string? _loadedKitName;

    public string LoadButtonLabel => LoadedKitName ?? "Load Kit...";

    [ObservableProperty]
    private KitPresetEntry? _selectedKit;

    [ObservableProperty]
    private MidiDeviceInfo? _selectedMidiInput;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectLabel))]
    private bool _isMidiConnected;

    public string ConnectLabel => IsMidiConnected ? "Disconnect" : "Connect";

    partial void OnSelectedKitChanged(KitPresetEntry? value)
    {
        if (value is not null)
        {
            LoadKit(value.FilePath);
        }
    }

    public void SetPresetDirectory(string? presetDir)
    {
        _presetDir = presetDir;
        Rescan();
    }

    public void LoadKit(string path)
    {
        _kitPlayer.LoadKit(path, Path.GetDirectoryName(path));
        LoadedKitName = _kitPlayer.KitName;
        SelectedKit = Kits.FirstOrDefault(k => string.Equals(k.FilePath, path, StringComparison.OrdinalIgnoreCase));
        RefreshPads();
    }

    public void TryLoadDefault(string? presetDir)
    {
        if (presetDir is null || !Directory.Exists(presetDir))
        {
            _kitPlayer.LoadProceduralGmKit();
            LoadedKitName = _kitPlayer.KitName;
            RefreshPads();
            return;
        }

        var defaultPath = Path.Combine(presetDir, "default.json");
        if (File.Exists(defaultPath))
        {
            LoadKit(defaultPath);
            return;
        }

        var first = Directory.EnumerateFiles(presetDir, "*.json")
            .FirstOrDefault(p => !string.Equals(Path.GetFileName(p), "tempo.json", StringComparison.OrdinalIgnoreCase));

        if (first is not null)
        {
            LoadKit(first);
            return;
        }

        _kitPlayer.LoadProceduralGmKit();
        LoadedKitName = _kitPlayer.KitName;
        RefreshPads();
    }

    [RelayCommand]
    private void Rescan()
    {
        Kits.Clear();
        foreach (var entry in _presetService.Scan(_presetDir))
        {
            Kits.Add(entry);
        }

        if (SelectedKit is null && _kitPlayer.LoadedKitPath is { } loaded)
        {
            SelectedKit = Kits.FirstOrDefault(k => string.Equals(k.FilePath, loaded, StringComparison.OrdinalIgnoreCase));
        }
    }

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
            _midiInput.Connect(device, _padRouter);
            IsMidiConnected = true;
        }
        else if (_midiInput.AvailableInputs.Count > 0)
        {
            _midiInput.ConnectByIndex(0, _padRouter);
            IsMidiConnected = true;
        }
    }

    private void RefreshPads()
    {
        Pads.Clear();
        var hasSample = _kitPlayer.PadHasSample;

        for (var i = 0; i < GmPercussionMap.Pads.Count; i++)
        {
            Pads.Add(new PadAuditionViewModel(GmPercussionMap.Pads[i], hasSample[i], _kitPlayer));
        }
    }
}

public sealed partial class PadAuditionViewModel : ViewModelBase
{
    private readonly KitSamplePlayer _kitPlayer;

    public PadAuditionViewModel(PercussionPad pad, bool hasSample, KitSamplePlayer kitPlayer)
    {
        Pad = pad;
        HasSample = hasSample;
        _kitPlayer = kitPlayer;
    }

    public PercussionPad Pad { get; }

    public string Label => Pad.Label;

    public bool HasSample { get; }

    [RelayCommand]
    private void Audition() => _kitPlayer.TriggerPad(Pad.Index);
}
