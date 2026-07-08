using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.Core.Editing;
using Rythmbox.Core.Engine;
using Rythmbox.Core.Models;

namespace Rythmbox.Editor.ViewModels;

public sealed partial class EditorViewModel : ViewModelBase, IDisposable
{
    private readonly PlaybackEngine _engine;
    private readonly KitSamplePlayer _kitPlayer;
    private readonly PatternPreviewEngine _preview;
    private readonly AppPaths _paths;
    private string? _currentFilePath;

    public EditorViewModel()
    {
        _engine = new PlaybackEngine();
        _engine.Start();
        _kitPlayer = new KitSamplePlayer(_engine);
        _preview = new PatternPreviewEngine(_kitPlayer);
        _paths = new AppPaths();
        Pattern = new DrumPattern();

        _preview.StepAdvanced += OnPreviewStep;

        RebuildGrid();
        TryLoadDefaultKit();
    }

    public DrumPattern Pattern { get; private set; }

    public ObservableCollection<PianoRollLaneViewModel> Lanes { get; } = new();

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
        Lanes.First(l => l.PadIndex == pad).Notes[step].IsActive = Pattern.HasHit(pad, step);
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
        IsKitLoaded = true;
        KitLabel = _kitPlayer.KitName;
        StatusText = $"Kit: {KitLabel}";
    }

    private void TryLoadDefaultKit()
    {
        if (_paths.PresetDir is null)
        {
            _kitPlayer.LoadProceduralGmKit();
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

        _kitPlayer.LoadProceduralGmKit();
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

        var pads = GmPercussionMap.Pads.OrderByDescending(p => p.Note).ToList();
        for (var i = 0; i < pads.Count; i++)
        {
            var pad = pads[i];
            var lane = new PianoRollLaneViewModel(pad, this, isAlternateRow: i % 2 == 0);
            for (var step = 0; step < Pattern.TotalSteps; step++)
            {
                var note = new PianoRollNoteViewModel(lane, this, step);
                note.SyncFromPattern();
                lane.Notes.Add(note);
            }

            Lanes.Add(lane);
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
                lane.Notes[step].IsPlaying = true;
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
