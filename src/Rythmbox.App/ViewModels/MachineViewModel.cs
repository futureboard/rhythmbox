using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    private readonly DispatcherTimer _syncTimer;
    private readonly List<DateTime> _tapTimes = [];
    private bool _syncingMacros;

    public MachineViewModel(
        PatternArrangerEngine arranger,
        MidiFilePlayer midiPlayer,
        PlayerViewModel player,
        KitBrowserViewModel kitBrowser,
        IAudioBackend audioBackend,
        StyleBankService styleBank,
        AppPaths paths)
    {
        _arranger = arranger;
        _midiPlayer = midiPlayer;
        _player = player;
        _kitBrowser = kitBrowser;
        _audioBackend = audioBackend;

        StyleBank = new StyleBankViewModel(styleBank, SelectStyle);
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
    private string _styleLabel = "No Style Selected";

    [ObservableProperty]
    private string _patternLabel = "—";

    [ObservableProperty]
    private string _kitLabel = "Not Loaded";

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
    private bool _showEmptyStylePrompt = true;

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

    [RelayCommand]
    private void BrowseStyles()
    {
        StyleBank.SelectedCategory ??= StyleBank.Categories.FirstOrDefault();
    }

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
    }

    private void OnPatternPadTriggered(PatternPadViewModel pad)
    {
        if (pad.IsTapTempo)
        {
            RegisterTapTempo();
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

    private void RegisterTapTempo()
    {
        var now = DateTime.UtcNow;
        _tapTimes.RemoveAll(t => (now - t).TotalSeconds > 2);
        _tapTimes.Add(now);

        if (_tapTimes.Count < 2)
        {
            return;
        }

        var intervals = new List<double>();
        for (var i = 1; i < _tapTimes.Count; i++)
        {
            intervals.Add((_tapTimes[i] - _tapTimes[i - 1]).TotalSeconds);
        }

        var avgInterval = intervals.Average();
        if (avgInterval <= 0)
        {
            return;
        }

        var bpm = 60.0 / avgInterval;
        _player.UserTempo = Math.Clamp(bpm, 40, 240);
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
        ShowEmptyStylePrompt = !HasStyleSelected;
        StyleLabel = s.SelectedStyle?.Name ?? "No Style Selected";
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
    }

    private void UpdateKitLabel()
    {
        var name = _kitBrowser.LoadedKitName;
        KitLabel = string.IsNullOrWhiteSpace(name) ? "Not Loaded" : name;
        _arranger.SetKitDisplay(null, KitLabel);
    }

    public void RefreshAudioStatus()
    {
        BackendLabel = _audioBackend.PlatformBackendId;
        DeviceLabel = _audioBackend.CurrentDeviceName ?? "No device";
    }

    public void Dispose()
    {
        _syncTimer.Stop();
        _arranger.SessionChanged -= SyncFromSession;
    }
}
