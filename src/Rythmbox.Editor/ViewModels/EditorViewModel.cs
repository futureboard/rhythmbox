using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.Core.Editing;
using Rythmbox.Core.Engine;
using Rythmbox.Core.Models;
using Rythmbox.Core.Services;

namespace Rythmbox.Editor.ViewModels;

public sealed partial class EditorViewModel : ViewModelBase, IDisposable
{
    private static readonly string[] HotloadSlotNames = ["A", "B", "C", "D"];

    private const int MinDrumNote = 1;
    private const int MaxDrumNote = 127;

    private readonly PlaybackEngine _engine;
    private readonly KitSamplePlayer _kitPlayer;
    private readonly PatternPreviewEngine _preview;
    private readonly AppPaths _paths;
    private readonly IFileDialogService _fileDialog;
    private string? _currentFilePath;
    private string? _loadedKitPath;

    public EditorViewModel(IFileDialogService fileDialog)
    {
        _fileDialog = fileDialog;
        _engine = new PlaybackEngine();
        _engine.Start();
        _kitPlayer = new KitSamplePlayer(_engine);
        _preview = new PatternPreviewEngine(_kitPlayer);
        _paths = new AppPaths();

        var stored = KitHotloadSlotStore.Load(HotloadSlotNames);
        HotloadSlots = new ObservableCollection<EditorKitHotloadSlotViewModel>(
            HotloadSlotNames.Select(name => new EditorKitHotloadSlotViewModel(
                name,
                stored.GetValueOrDefault(name),
                ActivateHotloadSlot,
                ChangeHotloadSlot,
                ClearHotloadSlot)));

        Pattern = new DrumPattern();

        _preview.StepAdvanced += OnPreviewStep;

        RebuildGrid();
        TryLoadDefaultKit();
    }

    public DrumPattern Pattern { get; private set; }

    public ObservableCollection<PianoRollLaneViewModel> Lanes { get; } = new();

    public ObservableCollection<EditorKitHotloadSlotViewModel> HotloadSlots { get; }

    public IReadOnlyList<PianoRollRulerTickViewModel> RulerTicks { get; private set; } = [];

    [ObservableProperty]
    private string _title = "Rythmbox Editor";

    [ObservableProperty]
    private string _statusText = "New pattern";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BpmLabel))]
    private double _bpm = 120;

    [ObservableProperty]
    private int _bars = 1;

    [ObservableProperty]
    private int _stepsPerBar = DrumPattern.DefaultStepsPerBar;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isKitLoaded;

    [ObservableProperty]
    private string _kitLabel = "GM Kit";

    public string BpmLabel => $"{Bpm:0.0} BPM";

    public string FileLabel => _currentFilePath is null
        ? "Unsaved pattern"
        : Path.GetFileName(_currentFilePath);

    partial void OnBpmChanged(double value)
    {
        Pattern.Bpm = value;
        OnPropertyChanged(nameof(BpmLabel));
    }

    partial void OnBarsChanged(int value) => RebuildPatternDimensions();

    partial void OnStepsPerBarChanged(int value) => RebuildPatternDimensions();

    public void ToggleCell(int pad, int step)
    {
        Pattern.ToggleHit(pad, step);
        var lane = Lanes.FirstOrDefault(l => l.PadIndex == pad);
        if (lane is null)
        {
            return;
        }

        lane.Notes[step].IsActive = Pattern.HasHit(pad, step);
        StatusText = $"{Pattern.Hits.Count} notes";
    }

    public void OpenFile(string path)
    {
        try
        {
            Pattern = DrumPatternCodec.Import(path);
            _currentFilePath = path;
            Bpm = Pattern.Bpm;
            Bars = Pattern.Bars;
            StepsPerBar = Pattern.StepsPerBar;
            RebuildGrid();
            Title = $"Rythmbox Editor — {Pattern.Name}";
            StatusText = $"Opened {Path.GetFileName(path)}";
            OnPropertyChanged(nameof(FileLabel));
        }
        catch (Exception ex)
        {
            StatusText = $"Open failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void NewPattern()
    {
        StopPreview();
        Pattern = new DrumPattern();
        _currentFilePath = null;
        Bpm = 120;
        Bars = 1;
        StepsPerBar = DrumPattern.DefaultStepsPerBar;
        RebuildGrid();
        Title = "Rythmbox Editor";
        StatusText = "New pattern";
        OnPropertyChanged(nameof(FileLabel));
    }

    [RelayCommand]
    private void ClearPattern()
    {
        Pattern.Clear();
        foreach (var lane in Lanes)
        {
            foreach (var note in lane.Notes)
            {
                note.IsActive = false;
            }
        }

        StatusText = "Pattern cleared";
    }

    [RelayCommand]
    private async Task BrowseOpenMidiAsync()
    {
        var path = await _fileDialog.PickFileAsync(
            SuggestSaveFolder() ?? _paths.PresetDir,
            "Open MIDI pattern",
            [".mid", ".midi"]);

        if (path is not null)
        {
            OpenFile(path);
        }
    }

    [RelayCommand]
    private async Task BrowseSaveMidiAsync()
    {
        var defaultName = _currentFilePath is not null
            ? Path.GetFileName(_currentFilePath)
            : "pattern.mid";

        var path = await _fileDialog.SaveFileAsync(
            SuggestSaveFolder() ?? _paths.PresetDir,
            "Save MIDI pattern",
            defaultName,
            [".mid"]);

        if (path is not null)
        {
            SaveTo(path);
        }
    }

    [RelayCommand]
    private async Task BrowseLoadKitAsync()
    {
        var path = await _fileDialog.PickFileAsync(
            _paths.PresetDir,
            "Load Drum Kit",
            [".json", ".apak"]);

        if (path is not null)
        {
            LoadKit(path);
        }
    }

    [RelayCommand]
    private async Task PlayPreview()
    {
        if (!_kitPlayer.IsLoaded)
        {
            StatusText = "No kit loaded";
            return;
        }

        if (IsPlaying)
        {
            StopPreview();
            return;
        }

        IsPlaying = true;
        Pattern.Bpm = Bpm;
        StatusText = "Preview playing…";
        await _preview.PlayAsync(Pattern, loop: true);
        IsPlaying = false;
        StatusText = "Preview stopped";
    }

    [RelayCommand]
    private void StopPreview()
    {
        _preview.Stop();
        IsPlaying = false;
        ClearPlayingHighlight();
    }

    public void LoadKit(string path)
    {
        _kitPlayer.LoadKit(path, _paths.SamplesDir);
        _loadedKitPath = path;
        IsKitLoaded = true;
        KitLabel = _kitPlayer.KitName;
        StatusText = $"Kit: {KitLabel}";
        RefreshHotloadActiveState();
    }

    private async Task ActivateHotloadSlot(EditorKitHotloadSlotViewModel slot)
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

        LoadKit(slot.KitPath!);
    }

    private async Task ChangeHotloadSlot(EditorKitHotloadSlotViewModel slot)
    {
        var path = await _fileDialog.PickFileAsync(
            _paths.PresetDir,
            $"Select kit for slot {slot.SlotName}",
            [".json", ".apak"]);

        if (path is null)
        {
            return;
        }

        slot.KitPath = path;
        PersistHotloadSlots();
        LoadKit(path);
    }

    private void ClearHotloadSlot(EditorKitHotloadSlotViewModel slot)
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
        foreach (var slot in HotloadSlots)
        {
            slot.IsActive = !slot.IsEmpty
                && _loadedKitPath is not null
                && string.Equals(slot.KitPath, _loadedKitPath, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void TryLoadDefaultKit()
    {
        if (!Directory.Exists(_paths.PresetDir))
        {
            _kitPlayer.LoadEmptyGmKit();
            IsKitLoaded = true;
            KitLabel = _kitPlayer.KitName;
            return;
        }

        var defaultPath = Path.Combine(_paths.PresetDir, "default.json");
        if (File.Exists(defaultPath))
        {
            LoadKit(defaultPath);
            return;
        }

        var first = Directory.EnumerateFiles(_paths.PresetDir, "*.json")
            .FirstOrDefault(p => !string.Equals(Path.GetFileName(p), "tempo.json", StringComparison.OrdinalIgnoreCase));

        if (first is not null)
        {
            LoadKit(first);
            return;
        }

        _kitPlayer.LoadEmptyGmKit();
        IsKitLoaded = true;
        KitLabel = _kitPlayer.KitName;
    }

    public string? SuggestSaveFolder() =>
        _paths.RythmDir is { } dir && Directory.Exists(dir) ? dir : null;

    public void SaveTo(string path)
    {
        Pattern.Bpm = Bpm;
        Pattern.Bars = Bars;
        Pattern.StepsPerBar = StepsPerBar;
        Pattern.Name = Path.GetFileNameWithoutExtension(path);
        DrumPatternCodec.Export(Pattern, path);
        _currentFilePath = path;
        Title = $"Rythmbox Editor — {Pattern.Name}";
        StatusText = $"Saved {Path.GetFileName(path)}";
        OnPropertyChanged(nameof(FileLabel));
    }

    private void RebuildPatternDimensions()
    {
        var copy = Pattern.Clone();
        Pattern = new DrumPattern
        {
            Bpm = Bpm,
            Bars = Math.Clamp(Bars, 1, 8),
            StepsPerBar = Math.Clamp(StepsPerBar, 4, 32),
            Name = copy.Name,
        };
        Bars = Pattern.Bars;
        StepsPerBar = Pattern.StepsPerBar;

        foreach (var ((pad, step), velocity) in copy.Hits)
        {
            if (step < Pattern.TotalSteps)
            {
                Pattern.Hits[(pad, step)] = velocity;
            }
        }

        RebuildGrid();
    }

    private void RebuildGrid()
    {
        Lanes.Clear();
        BuildRuler();

        var gmPadsByNote = GmPercussionMap.Pads.ToDictionary(p => p.Note);
        var laneIndex = 0;
        for (var note = MaxDrumNote; note >= MinDrumNote; note--)
        {
            var pad = gmPadsByNote.TryGetValue(note, out var gmPad)
                ? gmPad with { Index = note }
                : new PercussionPad(note, $"Note {note}", note, PadCategory.Drum, PadBus.Drum);
            var lane = new PianoRollLaneViewModel(pad, this, isAlternateRow: laneIndex % 2 == 0);
            for (var step = 0; step < Pattern.TotalSteps; step++)
            {
                var cell = new PianoRollNoteViewModel(lane, this, step);
                cell.SyncFromPattern();
                lane.Notes.Add(cell);
            }

            Lanes.Add(lane);
            laneIndex++;
        }
    }

    private void BuildRuler()
    {
        var ticks = new List<PianoRollRulerTickViewModel>();
        var stepsPerBeat = Math.Max(1, Pattern.StepsPerBar / 4);

        for (var step = 0; step < Pattern.TotalSteps; step++)
        {
            var isBarStart = step % Pattern.StepsPerBar == 0;
            var isBeatStart = step % stepsPerBeat == 0;
            string label = string.Empty;

            if (isBarStart)
            {
                label = $"{step / Pattern.StepsPerBar + 1}";
            }
            else if (isBeatStart && Pattern.StepsPerBar >= 8)
            {
                label = "·";
            }

            ticks.Add(new PianoRollRulerTickViewModel
            {
                Label = label,
                IsBarStart = isBarStart,
                IsBeatStart = isBeatStart,
            });
        }

        RulerTicks = ticks;
        OnPropertyChanged(nameof(RulerTicks));
    }

    private void OnPreviewStep(int step)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ClearPlayingHighlight();
            if (step < 0)
            {
                return;
            }

            foreach (var lane in Lanes)
            {
                if (step < lane.Notes.Count)
                {
                    lane.Notes[step].IsPlaying = true;
                }
            }
        });
    }

    private void ClearPlayingHighlight()
    {
        foreach (var lane in Lanes)
        {
            foreach (var note in lane.Notes)
            {
                note.IsPlaying = false;
            }
        }
    }

    public void Dispose()
    {
        _preview.StepAdvanced -= OnPreviewStep;
        _preview.Dispose();
        _kitPlayer.Dispose();
        _engine.Dispose();
    }
}

public sealed partial class EditorKitHotloadSlotViewModel : ViewModelBase
{
    private readonly Func<EditorKitHotloadSlotViewModel, Task> _activate;
    private readonly Func<EditorKitHotloadSlotViewModel, Task> _change;
    private readonly Action<EditorKitHotloadSlotViewModel> _clear;

    public EditorKitHotloadSlotViewModel(
        string slotName,
        string? kitPath,
        Func<EditorKitHotloadSlotViewModel, Task> activate,
        Func<EditorKitHotloadSlotViewModel, Task> change,
        Action<EditorKitHotloadSlotViewModel> clear)
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
        ? "Select…"
        : Path.GetFileNameWithoutExtension(KitPath!) ?? "Kit";

    public string DisplayName => IsEmpty ? SlotName : $"{SlotName}: {KitLabel}";

    public string ToolTip => IsEmpty
        ? $"Click to select a kit for slot {SlotName}"
        : $"Load {KitLabel}";

    [RelayCommand]
    private Task Activate() => _activate(this);

    [RelayCommand]
    private Task Change() => _change(this);

    [RelayCommand]
    private void Clear() => _clear(this);
}
