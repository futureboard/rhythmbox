using Rythmbox.Core.Models;
using Rythmbox.Core.Samples;
using Rythmbox.Core.Formats;

namespace Rythmbox.Core.Engine;

/// <summary>Shared working kit for Sample Creator, Pads, and Machine — single source of truth.</summary>
public sealed class KitSession
{
    private readonly KitSamplePlayer _player;
    private readonly AppPaths _paths;

    public KitSession(KitSamplePlayer player, AppPaths? paths = null)
    {
        _player = player;
        _paths = paths ?? new AppPaths();
        WorkingKit = KitPresetCodec.CreateDefaultGmKit();
    }

    public KitPreset WorkingKit { get; private set; }

    public string? PresetPath { get; private set; }

    public string KitName => WorkingKit.Name;

    public KitSamplePlayer Player => _player;

    public string? PresetDir => _paths.PresetDir;

    public string? SamplesDir => _paths.SamplesDir;

    /// <summary>Fired when kit structure changes (open/new/save) — UI should rebuild pad lists.</summary>
    public event Action? StructureChanged;

    /// <summary>Fired when samples are pushed to the live player (pads/machine hear updates).</summary>
    public event Action? LiveKitUpdated;

    public void LoadFromFile(string path)
    {
        WorkingKit = string.Equals(Path.GetExtension(path), ApakCodec.Extension, StringComparison.OrdinalIgnoreCase)
            ? ApakCodec.LoadFactory(path)
            : KitPresetCodec.Load(path, _paths.SamplesDir);
        PresetPath = path;
        PushToPlayer();
        StructureChanged?.Invoke();
    }

    public void LoadKitPreset(KitPreset kit, string? presetPath = null)
    {
        WorkingKit = kit;
        PresetPath = presetPath;
        PushToPlayer();
        StructureChanged?.Invoke();
    }

    public void ResetToEmptyGmKit()
    {
        WorkingKit = KitPresetCodec.CreateDefaultGmKit();
        PresetPath = null;
        PushToPlayer();
        StructureChanged?.Invoke();
    }

    public void SetKitName(string name)
    {
        WorkingKit.Name = name;
        _player.LoadKitPreset(WorkingKit, PresetPath);
        LiveKitUpdated?.Invoke();
    }

    public void SaveToFile(string jsonPath)
    {
        var samplesDir = _paths.SamplesDir ?? Path.Combine(Path.GetDirectoryName(jsonPath) ?? ".", "SAMPLES");
        KitPresetCodec.Save(WorkingKit, jsonPath, samplesDir);
        PresetPath = jsonPath;
        WorkingKit = KitPresetCodec.Load(jsonPath, _paths.SamplesDir);
        PushToPlayer();
        StructureChanged?.Invoke();
    }

    public void PushToPlayer()
    {
        _player.LoadKitPreset(WorkingKit, PresetPath);
        LiveKitUpdated?.Invoke();
    }

    public void TryLoadDefaultPreset()
    {
        if (_paths.PresetDir is not { } presetDir || !Directory.Exists(presetDir))
        {
            ResetToEmptyGmKit();
            return;
        }

        var defaultPath = Path.Combine(presetDir, "default.json");
        if (File.Exists(defaultPath))
        {
            LoadFromFile(defaultPath);
            return;
        }

        var first = Directory.EnumerateFiles(presetDir, "*.json")
            .FirstOrDefault(p => !string.Equals(Path.GetFileName(p), "tempo.json", StringComparison.OrdinalIgnoreCase));

        if (first is not null)
        {
            LoadFromFile(first);
            return;
        }

        ResetToEmptyGmKit();
    }
}
