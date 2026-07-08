using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.App.Localization;
using Rythmbox.Core.Audio;
using Rythmbox.Core.Engine;
using Rythmbox.Core.Models.Styles;
using Rythmbox.Core.Styles;

namespace Rythmbox.App.ViewModels;

public sealed partial class MachineViewModel : ViewModelBase, IDisposable
{
    private readonly PatternArrangerEngine _arranger;
    private readonly MidiFilePlayer _midiPlayer;
    private readonly PlayerViewModel _player;
    private readonly KitBrowserViewModel _kitBrowser;
    private readonly IAudioBackend _audioBackend;
    private readonly LocalizationService _i18n;
    private readonly TempoViewModel _tempo;
    private readonly DispatcherTimer _syncTimer;
    private bool _syncingMacros;

    public MachineViewModel(
        PatternArrangerEngine arranger,
        MidiFilePlayer midiPlayer,
        PlayerViewModel player,
        KitBrowserViewModel kitBrowser,
        IAudioBackend audioBackend,
        StyleBankService styleBank,
        AppPaths paths,
        LocalizationService i18n,
        TempoViewModel tempo)
    {
        _arranger = arranger;
        _midiPlayer = midiPlayer;
        _player = player;
        _kitBrowser = kitBrowser;
        _audioBackend = audioBackend;
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
    private string _backendLabel = PlatformAudioBackend.PreferredBackendId;

    [ObservableProperty]
    private string _deviceLabel = "—";

    [ObservableProperty]
    private string _transportLabel = "STOPPED";

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
    private void ExportMidi()
    {
        // TODO: export selected pattern MIDI to file.
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
