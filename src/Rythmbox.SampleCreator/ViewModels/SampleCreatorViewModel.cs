using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.Core.Engine;
using Rythmbox.Core.Models;
using Rythmbox.Core.Samples;
using Rythmbox.Core.Services;

namespace Rythmbox.SampleCreator.ViewModels;

public sealed partial class SampleCreatorViewModel : ViewModelBase, IDisposable
{
    private readonly KitSession _kitSession;
    private readonly SamplePreviewPlayer _preview;
    private readonly IFileDialogService _fileDialog;

    public SampleCreatorViewModel(IFileDialogService fileDialog, KitSession kitSession, PlaybackEngine engine)
    {
        _fileDialog = fileDialog;
        _kitSession = kitSession;
        _preview = new SamplePreviewPlayer(engine);
        _kitSession.StructureChanged += OnKitStructureChanged;
        _kitSession.LiveKitUpdated += OnLiveKitUpdated;
        RebuildPads();
        SyncFromSession();
    }

    public ObservableCollection<PadSampleViewModel> Pads { get; } = new();

    [ObservableProperty]
    private PadSampleViewModel? _selectedPad;

    [ObservableProperty]
    private string _kitName = "GM Kit (empty)";

    [ObservableProperty]
    private string _statusText = "Import WAV samples and export a kit preset";

    [ObservableProperty]
    private string _title = "Rythmbox Sample Creator";

    [ObservableProperty]
    private bool _isPresetLoading;

    [ObservableProperty]
    private string _presetLoadingMessage = "Preparing kit…";

    [ObservableProperty]
    private double _presetLoadingProgress;

    partial void OnSelectedPadChanged(PadSampleViewModel? value)
    {
        foreach (var pad in Pads)
        {
            pad.IsSelected = ReferenceEquals(pad, value);
        }

        value?.ActivateForEditing();
    }

    partial void OnKitNameChanged(string value)
    {
        if (!string.Equals(_kitSession.WorkingKit.Name, value, StringComparison.Ordinal))
        {
            _kitSession.SetKitName(value);
        }
    }

    public void OpenPreset(string path)
    {
        try
        {
            _kitSession.LoadFromFile(path);
            StatusText = $"Opened {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            StatusText = $"Open failed: {ex.Message}";
        }
    }

    public async Task OpenPresetAsync(string path)
    {
        if (IsPresetLoading)
        {
            return;
        }

        IsPresetLoading = true;
        PresetLoadingProgress = 0;
        PresetLoadingMessage = $"Reading {Path.GetFileName(path)}…";

        try
        {
            var progress = new Progress<KitLoadProgress>(update =>
            {
                PresetLoadingMessage = update.Message;
                PresetLoadingProgress = update.Fraction;
            });
            var result = await Task.Run(() => KitPresetCodec.LoadWithDiagnostics(
                path,
                _kitSession.SamplesDir,
                progress: progress));

            _kitSession.LoadKitPreset(result.Kit, path);
            StatusText = result.Warnings.Count == 0
                ? $"Opened {Path.GetFileName(path)}"
                : $"Opened {Path.GetFileName(path)} with {result.Warnings.Count} skipped sample(s)";
        }
        catch (Exception ex)
        {
            StatusText = $"Open failed: {ex.Message}";
        }
        finally
        {
            IsPresetLoading = false;
        }
    }

    public void SavePreset(string jsonPath)
    {
        _kitSession.WorkingKit.Name = KitName;
        SyncKitFromPads();
        _kitSession.SaveToFile(jsonPath);
        StatusText = $"Saved kit + WAVs to {Path.GetFileName(jsonPath)}";
    }

    public void ImportWavToSelected(string path)
    {
        if (SelectedPad is null)
        {
            StatusText = "Select a pad first";
            return;
        }

        SelectedPad.LoadFromFile(path);
    }

    public void ImportDroppedFiles(IEnumerable<string> paths)
    {
        var wavPaths = paths
            .Where(static p => p.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)
                               || p.EndsWith(".wave", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (wavPaths.Count == 0)
        {
            StatusText = "Drop WAV files to import";
            return;
        }

        if (wavPaths.Count == 1)
        {
            if (SelectedPad is null)
            {
                StatusText = "Select a pad first";
                return;
            }

            ImportWavToSelected(wavPaths[0]);
            return;
        }

        var startIndex = SelectedPad?.Index ?? 0;
        var imported = 0;

        for (var i = 0; i < wavPaths.Count && startIndex + i < Pads.Count; i++)
        {
            Pads[startIndex + i].LoadFromFile(wavPaths[i]);
            imported++;
        }

        SelectedPad = Pads[Math.Min(startIndex + imported - 1, Pads.Count - 1)];
        StatusText = imported == wavPaths.Count
            ? $"Imported {imported} samples"
            : $"Imported {imported} of {wavPaths.Count} (ran out of pads)";
    }

    public void PreviewPad(PadSampleViewModel pad) => pad.PreviewCommand.Execute(null);

    public void PreviewBuffer(float[] samples, float gain, float pitchSemitones, string name)
    {
        if (samples.Length == 0)
        {
            StatusText = $"{name} has no sample";
            return;
        }

        _preview.Play(samples, gain, pitchSemitones, name);
        StatusText = $"Previewing {name}";
    }

    public Task<string?> PickWavAsync() =>
        _fileDialog.PickFileAsync(SuggestSamplesFolder(), "Import WAV sample", [".wav", ".wave"]);

    internal void NotifyKitEdited(string? status = null)
    {
        SyncKitFromPads();
        _kitSession.PushToPlayer();
        if (status is not null)
        {
            StatusText = status;
        }
    }

    [RelayCommand]
    private void NewKit()
    {
        _preview.Stop();
        _kitSession.ResetToEmptyGmKit();
        StatusText = "New kit";
    }

    [RelayCommand]
    private void StopPreview() => _preview.Stop();

    [RelayCommand]
    private async Task BrowseOpenPresetAsync()
    {
        var path = await _fileDialog.PickFileAsync(
            SuggestPresetFolder(),
            "Open kit preset",
            [".json"]);

        if (path is not null)
        {
            await OpenPresetAsync(path);
        }
    }

    [RelayCommand]
    private async Task BrowseSaveKitAsync()
    {
        var defaultName = _kitSession.PresetPath is not null
            ? Path.GetFileName(_kitSession.PresetPath)
            : $"{KitName}.json";

        var path = await _fileDialog.SaveFileAsync(
            SuggestPresetFolder(),
            "Save kit preset",
            defaultName,
            [".json"]);

        if (path is not null)
        {
            SavePreset(path);
        }
    }

    [RelayCommand]
    private async Task BrowseImportWavAsync()
    {
        if (SelectedPad is null)
        {
            StatusText = "Select a pad first";
            return;
        }

        var path = await PickWavAsync();
        if (path is not null)
        {
            ImportWavToSelected(path);
        }
    }

    public string? SuggestPresetFolder() => _kitSession.PresetDir;

    public string? SuggestSamplesFolder() => _kitSession.SamplesDir;

    private void OnKitStructureChanged()
    {
        RebuildPads();
        SyncFromSession();
        var loaded = _kitSession.PresetPath is { } path
            ? Path.GetFileName(path)
            : _kitSession.KitName;
        StatusText = $"Loaded {loaded}";
    }

    private void OnLiveKitUpdated() => SyncFromSession();

    private void SyncFromSession()
    {
        KitName = _kitSession.KitName;
        Title = $"Rythmbox Sample Creator — {_kitSession.KitName}";
    }

    private void RebuildPads()
    {
        var previousNote = SelectedPad?.Sample.MidiNote;
        Pads.Clear();
        var kit = _kitSession.WorkingKit;
        for (var i = 0; i < kit.Pads.Count; i++)
        {
            kit.Pads[i].PitchSemitones = 0f;
            var sample = kit.Pads[i];
            var vm = new PadSampleViewModel(this, sample, i);
            Pads.Add(vm);
        }

        SelectedPad = previousNote is { } note
            ? Pads.FirstOrDefault(p => p.Sample.MidiNote == note) ?? Pads.FirstOrDefault()
            : Pads.FirstOrDefault();
    }

    private void SyncKitFromPads()
    {
        var kit = _kitSession.WorkingKit;
        for (var i = 0; i < Pads.Count && i < kit.Pads.Count; i++)
        {
            Pads[i].Sample.PitchSemitones = 0f;
            kit.Pads[i] = Pads[i].Sample;
        }
    }

    [RelayCommand]
    private void ResetAllPitch()
    {
        foreach (var pad in Pads)
        {
            pad.Pitch = 0;
            pad.Sample.PitchSemitones = 0f;
        }

        NotifyKitEdited("Reset pitch on all pads to 0 st");
    }

    public void Dispose()
    {
        _kitSession.StructureChanged -= OnKitStructureChanged;
        _kitSession.LiveKitUpdated -= OnLiveKitUpdated;
        _preview.Dispose();
    }
}
