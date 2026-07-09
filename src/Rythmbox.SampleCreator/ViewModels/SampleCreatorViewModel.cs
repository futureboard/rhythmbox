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
    private readonly PlaybackEngine _engine;
    private readonly SamplePreviewPlayer _preview;
    private readonly AppPaths _paths;
    private readonly IFileDialogService _fileDialog;
    private KitPreset _kit;
    private string? _presetPath;

    public SampleCreatorViewModel(IFileDialogService fileDialog)
    {
        _fileDialog = fileDialog;
        _engine = new PlaybackEngine();
        _engine.Start();
        _preview = new SamplePreviewPlayer(_engine);
        _paths = new AppPaths();
        _kit = KitPresetCodec.CreateDefaultGmKit();
        RebuildPads();
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

    partial void OnSelectedPadChanged(PadSampleViewModel? value)
    {
        foreach (var pad in Pads)
        {
            pad.IsSelected = ReferenceEquals(pad, value);
        }
    }

    public void OpenPreset(string path)
    {
        try
        {
            _kit = KitPresetCodec.Load(path, _paths.SamplesDir);
            _presetPath = path;
            KitName = _kit.Name;
            Title = $"Rythmbox Sample Creator — {_kit.Name}";
            RebuildPads();
            StatusText = $"Opened {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            StatusText = $"Open failed: {ex.Message}";
        }
    }

    public void SavePreset(string jsonPath)
    {
        _kit.Name = KitName;
        SyncKitFromPads();

        var samplesDir = _paths.SamplesDir ?? Path.Combine(Path.GetDirectoryName(jsonPath) ?? ".", "SAMPLES");
        KitPresetCodec.Save(_kit, jsonPath, samplesDir);
        _presetPath = jsonPath;
        Title = $"Rythmbox Sample Creator — {_kit.Name}";
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

    public void PreviewPad(PadSampleViewModel pad)
    {
        if (!pad.HasAudio)
        {
            StatusText = $"{pad.Label} has no sample";
            return;
        }

        _preview.Play(pad.Sample);
        StatusText = $"Previewing {pad.Label}";
    }

    [RelayCommand]
    private void NewKit()
    {
        _preview.Stop();
        _kit = KitPresetCodec.CreateDefaultGmKit();
        _presetPath = null;
        KitName = _kit.Name;
        Title = "Rythmbox Sample Creator";
        RebuildPads();
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
            OpenPreset(path);
        }
    }

    [RelayCommand]
    private async Task BrowseSaveKitAsync()
    {
        var defaultName = _presetPath is not null
            ? Path.GetFileName(_presetPath)
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

        var path = await _fileDialog.PickFileAsync(
            SuggestSamplesFolder(),
            "Import WAV sample",
            [".wav", ".wave"]);

        if (path is not null)
        {
            ImportWavToSelected(path);
        }
    }

    public string? SuggestPresetFolder() => _paths.PresetDir;

    public string? SuggestSamplesFolder() => _paths.SamplesDir;

    private void RebuildPads()
    {
        Pads.Clear();
        for (var i = 0; i < _kit.Pads.Count; i++)
        {
            var vm = new PadSampleViewModel(this, _kit.Pads[i], i);
            vm.Gain = _kit.Pads[i].Gain;
            if (_kit.Pads[i].FilePath is { } fp)
            {
                vm.FileName = Path.GetFileName(fp);
            }

            Pads.Add(vm);
        }

        SelectedPad = Pads.FirstOrDefault();
    }

    private void SyncKitFromPads()
    {
        for (var i = 0; i < Pads.Count && i < _kit.Pads.Count; i++)
        {
            _kit.Pads[i] = Pads[i].Sample;
        }
    }

    public void Dispose()
    {
        _preview.Dispose();
        _engine.Dispose();
    }
}
