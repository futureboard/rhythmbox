using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.App.Localization;
using Rythmbox.Core.Audio;
using Rythmbox.Core.Engine;
using Rythmbox.Core.Models.Styles;
using Rythmbox.Core.Services;
using Rythmbox.Core.Styles;

namespace Rythmbox.App.ViewModels;

public sealed partial class MachineViewModel : ViewModelBase, IDisposable
{
    private static readonly string[] MidiExtensions = [".mid"];

    private readonly PatternArrangerEngine _arranger;
    private readonly MidiFilePlayer _midiPlayer;
    private readonly PlayerViewModel _player;
    private readonly KitBrowserViewModel _kitBrowser;
    private readonly IAudioBackend _audioBackend;
    private readonly AppPaths _paths;
    private readonly IFileDialogService _fileDialog;
    private readonly StatusViewModel _status;
    private readonly LocalizationService _i18n;
    private readonly TempoViewModel _tempo;
    private readonly DispatcherTimer _syncTimer;
    private bool _syncingMacros;
    private int _lastBarIndex = -1;

    public MachineViewModel(
        PatternArrangerEngine arranger,
        MidiFilePlayer midiPlayer,
        PlayerViewModel player,
        KitBrowserViewModel kitBrowser,
        IAudioBackend audioBackend,
        StyleBankService styleBank,
        AppPaths paths,
        IFileDialogService fileDialog,
        StatusViewModel status,
        LocalizationService i18n,
        TempoViewModel tempo)
    {
        _arranger = arranger;
        _midiPlayer = midiPlayer;
        _player = player;
        _kitBrowser = kitBrowser;
        _audioBackend = audioBackend;
        _paths = paths;
        _fileDialog = fileDialog;
        _status = status;
        _i18n = i18n;
        _tempo = tempo;
        _i18n.LanguageChanged += OnLanguageChanged;

        StyleBank = new StyleBankViewModel(styleBank, SelectStyle, _i18n);
        PatternPads = new ObservableCollection<PatternPadViewModel>(
            PatternPadLayout.Slots.Select(s => new PatternPadViewModel(s, OnPatternPadTriggered)));

        _arranger.SessionChanged += SyncFromSession;
        _kitBrowser.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(KitBrowserViewModel.LoadedKitName))
            {
                UpdateKitLabel();
            }
        };

        _syncTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _syncTimer.Tick += (_, _) =>
        {
            _arranger.SyncTransport(_midiPlayer, _player.UserTempo);
            AdvanceClock();
            SyncFromSession();
        };
        _syncTimer.Start();

        StyleBank.Scan(paths.StylesDir);
        UpdateKitLabel();
        SyncFromSession();
        RefreshAudioStatus();
    }

    public StyleBankViewModel StyleBank { get; }

    public ObservableCollection<PatternPadViewModel> PatternPads { get; }

    [ObservableProperty]
    private string _styleLabel = string.Empty;

    [ObservableProperty]
    private string _styleDetail = string.Empty;

    [ObservableProperty]
    private string _patternLabel = "—";

    [ObservableProperty]
    private string _kitLabel = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AudioStatusChip))]
    private string _backendLabel = PlatformAudioBackend.PreferredBackendId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AudioStatusChip))]
    private string _deviceLabel = "—";

    [ObservableProperty]
    private string _transportLabel = "STOPPED";

    /// <summary>Compact header chip: backend id, short device name, or Audio: Off.</summary>
    public string AudioStatusChip
    {
        get
        {
            if (string.IsNullOrWhiteSpace(DeviceLabel)
                || DeviceLabel == "—"
                || DeviceLabel == _i18n["machine.noDevice"])
            {
                return _i18n["header.audioOff"];
            }

            if (!string.IsNullOrWhiteSpace(BackendLabel))
            {
                return BackendLabel;
            }

            return ShortenDeviceName(DeviceLabel);
        }
    }

    private static string ShortenDeviceName(string name)
    {
        // Drop common Windows prefixes / parenthetical suffixes for a compact chip.
        var shortName = name;
        const string speakersPrefix = "Speakers (";
        if (shortName.StartsWith(speakersPrefix, StringComparison.OrdinalIgnoreCase) && shortName.EndsWith(')'))
        {
            shortName = shortName[speakersPrefix.Length..^1];
        }

        return shortName.Length <= 18 ? shortName : shortName[..17] + "…";
    }

    [ObservableProperty]
    private double _complexity;

    [ObservableProperty]
    private double _energy;

    [ObservableProperty]
    private double _swing;

    [ObservableProperty]
    private double _humanize;

    [ObservableProperty]
    private bool _hasStyleSelected;

    [ObservableProperty]
    private string? _lastError;

    [ObservableProperty]
    private bool _exportMidiEnabled;

    [ObservableProperty]
    private string _timeSignatureLabel = "4/4";

    [ObservableProperty]
    private bool _timeSignatureSwitchPending;

    public bool HasLastError => !string.IsNullOrWhiteSpace(LastError);

    partial void OnLastErrorChanged(string? value) => OnPropertyChanged(nameof(HasLastError));

    partial void OnComplexityChanged(double value)
    {
        if (!_syncingMacros)
        {
            PushMacros();
        }
    }

    partial void OnEnergyChanged(double value)
    {
        if (!_syncingMacros)
        {
            PushMacros();
        }
    }

    partial void OnSwingChanged(double value)
    {
        if (!_syncingMacros)
        {
            PushMacros();
        }
    }

    partial void OnHumanizeChanged(double value)
    {
        if (!_syncingMacros)
        {
            PushMacros();
        }
    }

    public PlayerViewModel Player => _player;

    public string PatternCurrentLabel => _i18n.Format("patternPads.current", PatternLabel);

    [RelayCommand]
    private async Task ExportMidiAsync()
    {
        var session = _arranger.Session;
        var pattern = session.GetPattern(session.SelectedPatternId);
        if (pattern is null || !pattern.HasMidiFile)
        {
            return;
        }

        var sourcePath = pattern.ResolvedMidiPath!;
        var defaultName = BuildExportFileName(pattern.Name);
        var destination = await _fileDialog.SaveFileAsync(
            _paths.PresetDir,
            _i18n["machine.exportMidi"],
            defaultName,
            MidiExtensions);
        if (string.IsNullOrEmpty(destination))
        {
            return;
        }

        try
        {
            File.Copy(sourcePath, destination, overwrite: true);
            _status.Show(_i18n.Format("status.midiExported", Path.GetFileName(destination)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _status.Show(_i18n.Format("status.midiExportFailed", ex.Message));
        }
    }

    private static string BuildExportFileName(string patternName)
    {
        var name = string.IsNullOrWhiteSpace(patternName) ? "pattern" : patternName.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return name + ".mid";
    }

    /// <summary>Drop a momentary short bar (e.g. 2/4), then return to the base groove. Bound to the SIG button.</summary>
    [RelayCommand]
    private void SwitchTimeSignature() => RequestTimeSignatureSwitch();

    /// <summary>Shared entry point for the UI button and the MIDI CC foot switch.</summary>
    public void RequestTimeSignatureSwitch()
    {
        _arranger.TriggerMomentarySwitch();
        SyncFromSession();
    }

    private void SelectStyle(StyleDefinition style)
    {
        _arranger.SelectStyle(style);
        _player.UserTempo = style.DefaultTempo;
        _player.Bpm = style.DefaultTempo;
        SyncFromSession();
        StyleBank.SyncFromSession(_arranger.Session);
    }

    private void OnPatternPadTriggered(PatternPadViewModel pad)
    {
        if (pad.IsTapTempo)
        {
            _tempo.TapTempoCommand.Execute(null);
            return;
        }

        if (pad.IsStop)
        {
            _player.StopCommand.Execute(null);
            _arranger.TriggerPad(pad.Slot);
            SyncFromSession();
            return;
        }

        _arranger.TriggerPad(pad.Slot);
        SyncFromSession();
    }

    private void PushMacros()
    {
        _arranger.UpdateMacros(new RhythmMacros
        {
            Complexity = (float)Complexity,
            Energy = (float)Energy,
            Swing = (float)Swing,
            Humanize = (float)Humanize,
        });
    }

    private void SyncFromSession()
    {
        var s = _arranger.Session;

        HasStyleSelected = s.SelectedStyle is not null;
        StyleLabel = s.SelectedStyle?.Name ?? _i18n["machine.noStyle"];
        StyleDetail = s.SelectedStyle is { } style
            ? $"{style.DefaultTempo:0} BPM · {style.TimeSignature}"
            : string.Empty;
        StyleBank.SyncFromSession(s);
        PatternLabel = s.SelectedPatternId is { } pid
            ? s.GetPattern(pid)?.Name ?? pid
            : "—";
        TransportLabel = s.TransportState.ToString().ToUpperInvariant();
        TimeSignatureLabel = s.CurrentTimeSignature.ToString();
        TimeSignatureSwitchPending = s.TimeSignatureOverridePending;
        LastError = s.LastError;

        _syncingMacros = true;
        Complexity = s.Macros.Complexity;
        Energy = s.Macros.Energy;
        Swing = s.Macros.Swing;
        Humanize = s.Macros.Humanize;
        _syncingMacros = false;

        foreach (var pad in PatternPads)
        {
            pad.SyncFromSession(s);
        }

        ExportMidiEnabled = s.GetPattern(s.SelectedPatternId)?.HasMidiFile == true;
        OnPropertyChanged(nameof(PatternCurrentLabel));
    }

    /// <summary>
    /// Derives bar boundaries from the playing position and the base meter so that momentary
    /// time-signature overrides expire after a bar of playback. The audio loop itself is metered by
    /// the pattern's MIDI file; this drives the arranger's logical bar counter and override lifetime.
    /// </summary>
    private void AdvanceClock()
    {
        var s = _arranger.Session;

        if (s.TransportState != ArrangerTransportState.Playing || _player.NativeBpm <= 0)
        {
            _lastBarIndex = -1;
            return;
        }

        var beatsPerBar = s.BaseTimeSignature.BeatsPerBar;
        if (beatsPerBar <= 0)
        {
            beatsPerBar = 4;
        }

        var totalBeats = _midiPlayer.Position.TotalSeconds * _player.NativeBpm / 60.0;
        var barIndex = (int)(totalBeats / beatsPerBar);

        // First observation after (re)start, or a loop wrap that rewinds the position.
        if (_lastBarIndex < 0 || barIndex < _lastBarIndex)
        {
            _lastBarIndex = barIndex;
            return;
        }

        while (_lastBarIndex < barIndex)
        {
            _arranger.OnBarAdvanced();
            _lastBarIndex++;
        }
    }

    private void UpdateKitLabel()
    {
        var name = _kitBrowser.LoadedKitName;
        KitLabel = string.IsNullOrWhiteSpace(name) ? _i18n["machine.notLoaded"] : name;
        _arranger.SetKitDisplay(null, KitLabel);
    }

    public void RefreshAudioStatus()
    {
        BackendLabel = _audioBackend.PlatformBackendId;
        DeviceLabel = _audioBackend.CurrentDeviceName ?? _i18n["machine.noDevice"];
        OnPropertyChanged(nameof(AudioStatusChip));
    }

    private void RefreshLocalizedLabels()
    {
        SyncFromSession();
        UpdateKitLabel();
        RefreshAudioStatus();
        StyleBank.RefreshLocalizedLabels();
        OnPropertyChanged(nameof(PatternCurrentLabel));
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => RefreshLocalizedLabels();

    public void Dispose()
    {
        _syncTimer.Stop();
        _i18n.LanguageChanged -= OnLanguageChanged;
        _arranger.SessionChanged -= SyncFromSession;
    }
}
