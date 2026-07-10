using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.Core.Engine;
using Rythmbox.Core.Formats;
using Rythmbox.Core.Models;
using Rythmbox.Core.Samples;
using Rythmbox.Core.Services;
using SoundFlow.Midi.Structs;

namespace Rythmbox.App.ViewModels;

public sealed partial class KitBrowserViewModel : ViewModelBase
{
    private static readonly string[] SlotNames = ["A", "B", "C", "D"];

    private readonly KitSession _kitSession;
    private readonly KitPresetService _presetService;
    private readonly MidiInputService _midiInput;
    private readonly PadMidiRouter _padRouter;
    private readonly IFileDialogService _fileDialog;
    private readonly StatusViewModel _status;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private string? _presetDir;
    private bool _syncingFromSession;

    public KitBrowserViewModel(
        KitSession kitSession,
        KitPresetService presetService,
        MidiInputService midiInput,
        PadMidiRouter padRouter,
        IFileDialogService fileDialog,
        StatusViewModel status)
    {
        _kitSession = kitSession;
        _presetService = presetService;
        _midiInput = midiInput;
        _padRouter = padRouter;
        _fileDialog = fileDialog;
        _status = status;

        var stored = KitHotloadSlotStore.Load(SlotNames);
        HotloadSlots = new ObservableCollection<KitHotloadSlotViewModel>(
            SlotNames.Select(name => new KitHotloadSlotViewModel(
                name,
                stored.GetValueOrDefault(name),
                ActivateHotloadSlot,
                ChangeHotloadSlot,
                ClearHotloadSlot)));

        _kitSession.LiveKitUpdated += SyncFromSession;
        _kitSession.StructureChanged += SyncFromSession;
        SyncFromSession();
        RefreshMidiInputs();
    }

    public ObservableCollection<KitPresetEntry> Kits { get; } = new();

    public ObservableCollection<KitHotloadSlotViewModel> HotloadSlots { get; }

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
    private bool _isLoading;

    [ObservableProperty]
    private string _loadingMessage = "Preparing kit…";

    [ObservableProperty]
    private double _loadingProgress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectLabel))]
    private bool _isMidiConnected;

    public string ConnectLabel => IsMidiConnected ? "Disconnect" : "Connect";

    partial void OnSelectedKitChanged(KitPresetEntry? value)
    {
        if (!_syncingFromSession && value is not null)
        {
            _ = LoadKitAsync(value.FilePath);
        }
    }

    public void SetPresetDirectory(string? presetDir)
    {
        _presetDir = presetDir;
        Rescan();
    }

    public async Task LoadKitAsync(string path)
    {
        if (!await _loadGate.WaitAsync(0))
        {
            return;
        }

        IsLoading = true;
        LoadingProgress = 0;
        LoadingMessage = $"Reading {Path.GetFileName(path)}…";

        try
        {
            KitLoadResult result;
            if (string.Equals(Path.GetExtension(path), ApakCodec.Extension, StringComparison.OrdinalIgnoreCase))
            {
                // APAK is an encrypted legacy archive, so its payload must be
                // decrypted before it can be read. It remains isolated to this
                // one background operation; normal JSON presets use memory maps.
                result = await Task.Run(() => new KitLoadResult(ApakCodec.LoadFactory(path), [], 0, 0));
                LoadingProgress = 1;
            }
            else
            {
                var progress = new Progress<KitLoadProgress>(update =>
                {
                    LoadingMessage = update.Message;
                    LoadingProgress = update.Fraction;
                });

                result = await Task.Run(() => KitPresetCodec.LoadWithDiagnostics(path, _kitSession.SamplesDir, progress: progress));
            }

            _kitSession.LoadKitPreset(result.Kit, path);
            var mapped = result.MappedSampleCount > 0
                ? $" ({result.MappedSampleCount} mapped samples)"
                : string.Empty;
            _status.Show(result.Warnings.Count == 0
                ? $"Loaded {_kitSession.KitName}{mapped}"
                : $"Loaded {_kitSession.KitName} with {result.Warnings.Count} skipped sample(s)");
        }
        catch (Exception ex)
        {
            _status.Show($"Could not load kit: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            _loadGate.Release();
        }
    }

    public void TryLoadDefault(string? presetDir)
    {
        if (presetDir is not null)
        {
            _presetDir = presetDir;
        }

        _kitSession.TryLoadDefaultPreset();
    }

    public void SyncFromSession()
    {
        LoadedKitName = _kitSession.KitName;
        _syncingFromSession = true;
        try
        {
            SelectedKit = _kitSession.PresetPath is { } loaded
                ? Kits.FirstOrDefault(k => string.Equals(k.FilePath, loaded, StringComparison.OrdinalIgnoreCase))
                : SelectedKit;
        }
        finally
        {
            _syncingFromSession = false;
        }
        RefreshHotloadActiveState();
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

        SyncFromSession();
    }

    [RelayCommand]
    private async Task BrowseLoadKitAsync()
    {
        var path = await _fileDialog.PickFileAsync(
            _presetDir,
            "Load Drum Kit",
            [".json", ".apak"]);

        if (path is not null)
        {
            await LoadKitAsync(path);
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

    private async Task ActivateHotloadSlot(KitHotloadSlotViewModel slot)
    {
        if (slot.IsEmpty)
        {
            await ChangeHotloadSlot(slot);
            return;
        }

        if (!File.Exists(slot.KitPath))
        {
            slot.KitPath = null;
            PersistHotloadSlots();
            await ChangeHotloadSlot(slot);
            return;
        }

        await LoadKitAsync(slot.KitPath!);
    }

    private async Task ChangeHotloadSlot(KitHotloadSlotViewModel slot)
    {
        var path = await _fileDialog.PickFileAsync(
            _presetDir,
            $"Select kit for slot {slot.SlotName}",
            [".json", ".apak"]);

        if (path is null)
        {
            return;
        }

        slot.KitPath = path;
        PersistHotloadSlots();
        await LoadKitAsync(path);
    }

    private void ClearHotloadSlot(KitHotloadSlotViewModel slot)
    {
        slot.KitPath = null;
        PersistHotloadSlots();
    }

    private void PersistHotloadSlots()
    {
        KitHotloadSlotStore.Save(HotloadSlots.ToDictionary(static s => s.SlotName, static s => s.KitPath));
    }

    private void RefreshHotloadActiveState()
    {
        var loaded = _kitSession.PresetPath;
        foreach (var slot in HotloadSlots)
        {
            slot.IsActive = !slot.IsEmpty
                && loaded is not null
                && string.Equals(slot.KitPath, loaded, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void RefreshPads()
    {
        Pads.Clear();
        var hasSample = _kitSession.Player.PadHasSample;

        for (var i = 0; i < GmPercussionMap.Pads.Count; i++)
        {
            Pads.Add(new PadAuditionViewModel(GmPercussionMap.Pads[i], hasSample[i], _kitSession.Player));
        }
    }
}

public sealed partial class KitHotloadSlotViewModel : ViewModelBase
{
    private readonly Func<KitHotloadSlotViewModel, Task> _activate;
    private readonly Func<KitHotloadSlotViewModel, Task> _change;
    private readonly Action<KitHotloadSlotViewModel> _clear;

    public KitHotloadSlotViewModel(
        string slotName,
        string? kitPath,
        Func<KitHotloadSlotViewModel, Task> activate,
        Func<KitHotloadSlotViewModel, Task> change,
        Action<KitHotloadSlotViewModel> clear)
    {
        SlotName = slotName;
        _kitPath = kitPath;
        _activate = activate;
        _change = change;
        _clear = clear;
    }

    public string SlotName { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(KitLabel))]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    [NotifyPropertyChangedFor(nameof(ToolTip))]
    private string? _kitPath;

    [ObservableProperty]
    private bool _isActive;

    public bool IsEmpty => string.IsNullOrWhiteSpace(KitPath);

    public string KitLabel => IsEmpty
        ? "Select kit…"
        : Path.GetFileNameWithoutExtension(KitPath!) ?? "Kit";

    public string DisplayName => IsEmpty ? SlotName : $"{SlotName}: {KitLabel}";

    public string ToolTip => IsEmpty
        ? $"Click to select a kit for slot {SlotName}"
        : $"Load {KitLabel}. Right-click to change or clear.";

    [RelayCommand]
    private Task Activate() => _activate(this);

    [RelayCommand]
    private Task Change() => _change(this);

    [RelayCommand]
    private void Clear() => _clear(this);
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
