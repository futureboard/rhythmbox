using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.App.Localization;
using Rythmbox.App.Services;
using Rythmbox.Core.Audio;
using Rythmbox.Core.Engine;
using Rythmbox.Core.Styles;
using Rythmbox.Editor.ViewModels;
using Rythmbox.SampleCreator.ViewModels;

namespace Rythmbox.App.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly PlaybackEngine _engine;
    private readonly KitSamplePlayer _kitPlayer;
    private readonly KitSession _kitSession;
    private readonly KitPresetService _kitPresets;
    private readonly PadMappingService _padMapping;
    private readonly PadMidiRouter _padRouter;
    private readonly MidiInputService _midiInput;
    private readonly MidiFootSwitchController _footSwitch;
    private readonly MidiFilePlayer _midiFilePlayer;
    private readonly LoopLibraryService _loopLibrary;
    private readonly SubLoopService _subLoopService;
    private readonly TempoPresetService _tempoPresets;
    private readonly AppPaths _paths;
    private readonly StyleBankService _styleBank;
    private readonly PatternArrangerEngine _arranger;
    private readonly IAudioBackend _audioBackend;
    private readonly DispatcherTimer _clockTimer;
    private readonly LocalizationService _localization;
    private readonly DispatcherTimer _beatTimer;

    public MainWindowViewModel()
    {
        _localization = new LocalizationService();
        _localization.SetLanguage(AppLanguage.English);

        _engine = new PlaybackEngine();
        _engine.Start();

        _paths = new AppPaths();
        _kitPlayer = new KitSamplePlayer(_engine);
        _kitSession = new KitSession(_kitPlayer, _paths);
        _kitPresets = new KitPresetService();
        _padMapping = new PadMappingService();
        _padRouter = new PadMidiRouter(_kitPlayer, _padMapping);
        _midiInput = new MidiInputService(_engine);
        _footSwitch = new MidiFootSwitchController();
        _midiFilePlayer = new MidiFilePlayer(_engine, _kitPlayer);
        _loopLibrary = new LoopLibraryService();
        _subLoopService = new SubLoopService();
        _tempoPresets = new TempoPresetService();
        _tempoPresets.Load(_paths.PresetDir);

        _styleBank = new StyleBankService();
        _arranger = new PatternArrangerEngine(_midiFilePlayer);
        _audioBackend = new MiniAudioPlaybackBackend(_engine);

        _engine.DeviceChanged += OnDeviceChanged;

        Status = new StatusViewModel();
        Localization = new LocalizationViewModel(_localization);

        FileManager = new FileManagerViewModel(_paths);
        FileManager.Initialize();
        FileDialog = new FileManagerDialogViewModel(new FileManagerViewModel(_paths), Localization);
        var fileDialogService = new AppFileDialogService(FileDialog);

        KitBrowser = new KitBrowserViewModel(_kitSession, _kitPresets, _midiInput, _padRouter, fileDialogService);
        Player = new PlayerViewModel(_midiFilePlayer);
        MasterStrip = new MasterStripViewModel(_engine);
        Mixer = new MixerViewModel(_engine, _kitPlayer, _audioBackend, _localization);
        LoopBrowser = new LoopBrowserViewModel(_loopLibrary, Player);
        PadGrid = new PercussionPadGridViewModel(_kitPlayer, Player);
        NowPlaying = new NowPlayingViewModel(LoopBrowser, Player);
        PadMixer = new PadMixerViewModel(_kitPlayer);
        BusMixer = new BusMixerViewModel(_kitPlayer);
        Settings = new SettingsViewModel(_padMapping, _padRouter, _midiInput, _footSwitch, _arranger, Status, _localization);
        Tempo = new TempoViewModel(_tempoPresets, Player, Status, _localization);
        SubLoops = new SubLoopViewModel(_subLoopService, Player, Status);
        BeatLeds = new BeatLedViewModel(Player);
        Machine = new MachineViewModel(
            _arranger,
            _midiFilePlayer,
            Player,
            KitBrowser,
            _audioBackend,
            _styleBank,
            _paths,
            _localization,
            Tempo);

        Editor = new EditorViewModel(fileDialogService);
        SampleCreator = new SampleCreatorViewModel(fileDialogService, _kitSession, _engine);

        // Foot switch: MIDI CC -> momentary time-signature switch. CC arrives on the MIDI thread,
        // so marshal the state change onto the UI thread before touching the arranger view models.
        _padRouter.ControlChangeReceived += (cc, value) => _footSwitch.ProcessControlChange(cc, value);
        _footSwitch.Pressed += () => Dispatcher.UIThread.Post(Machine.RequestTimeSignatureSwitch);

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => ClockText = DateTime.Now.ToString("HH:mm:ss");
        _clockTimer.Start();
        ClockText = DateTime.Now.ToString("HH:mm:ss");

        _beatTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _beatTimer.Tick += (_, _) => BeatLeds.Refresh();
        _beatTimer.Start();

        InitializeFromPaths();
    }

    public StatusViewModel Status { get; }

    public LocalizationViewModel Localization { get; }

    public KitBrowserViewModel KitBrowser { get; }

    public PlayerViewModel Player { get; }

    public MasterStripViewModel MasterStrip { get; }

    public MixerViewModel Mixer { get; }

    public LoopBrowserViewModel LoopBrowser { get; }

    public PercussionPadGridViewModel PadGrid { get; }

    public NowPlayingViewModel NowPlaying { get; }

    public PadMixerViewModel PadMixer { get; }

    public BusMixerViewModel BusMixer { get; }

    public SettingsViewModel Settings { get; }

    public TempoViewModel Tempo { get; }

    public SubLoopViewModel SubLoops { get; }

    public BeatLedViewModel BeatLeds { get; }

    public MachineViewModel Machine { get; }

    public EditorViewModel Editor { get; }

    public SampleCreatorViewModel SampleCreator { get; }

    public FileManagerViewModel FileManager { get; }

    public FileManagerDialogViewModel FileDialog { get; }

    [ObservableProperty]
    private AppPage _currentPage = AppPage.Home;

    [ObservableProperty]
    private string _clockText = string.Empty;

    public bool IsHomePage => CurrentPage == AppPage.Home;

    public bool IsMachinePage => CurrentPage == AppPage.Machine;

    public bool IsPadsPage => CurrentPage == AppPage.Pads;

    public bool IsMixerPage => CurrentPage == AppPage.Mixer;

    public bool IsSettingsPage => CurrentPage == AppPage.Settings;

    public bool IsEditorPage => CurrentPage == AppPage.Editor;

    public bool IsMacroPage => CurrentPage == AppPage.Macro;

    public bool IsFilesPage => CurrentPage == AppPage.Files;

    public bool IsChromePage => CurrentPage == AppPage.Chrome;

    partial void OnCurrentPageChanged(AppPage value)
    {
        OnPropertyChanged(nameof(IsHomePage));
        OnPropertyChanged(nameof(IsMachinePage));
        OnPropertyChanged(nameof(IsPadsPage));
        OnPropertyChanged(nameof(IsMixerPage));
        OnPropertyChanged(nameof(IsSettingsPage));
        OnPropertyChanged(nameof(IsEditorPage));
        OnPropertyChanged(nameof(IsMacroPage));
        OnPropertyChanged(nameof(IsFilesPage));
        OnPropertyChanged(nameof(IsChromePage));
    }

    [RelayCommand]
    private void Navigate(AppPage page) => CurrentPage = page;

    [RelayCommand]
    private void Quit()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    [RelayCommand]
    private void ShutdownComputer() => RunPowerCommand(restart: false);

    [RelayCommand]
    private void RestartComputer() => RunPowerCommand(restart: true);

    private void RunPowerCommand(bool restart)
    {
        try
        {
            var startInfo = CreatePowerProcessStartInfo(restart);
            if (startInfo is null)
            {
                Status.Show("Power command is not supported on this platform");
                return;
            }

            Process.Start(startInfo);
            Status.Show(restart ? "Restart requested" : "Shutdown requested");
        }
        catch (Exception ex)
        {
            Status.Show($"Power command failed: {ex.Message}");
        }
    }

    private static ProcessStartInfo? CreatePowerProcessStartInfo(bool restart)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new ProcessStartInfo
            {
                FileName = "shutdown.exe",
                Arguments = restart ? "/r /t 0" : "/s /t 0",
                UseShellExecute = false,
                CreateNoWindow = true,
            };
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new ProcessStartInfo
            {
                FileName = "systemctl",
                Arguments = restart ? "reboot" : "poweroff",
                UseShellExecute = false,
                CreateNoWindow = true,
            };
        }

        return null;
    }

    public void SetLoopFolder(string folder)
    {
        LoopBrowser.SetFolder(folder);
        Status.Show($"Scanned {LoopBrowser.TotalCount} loops");
    }

    public int? FindPadByKey(string keyName) => _padMapping.FindPadByKeyName(keyName);

    public void NudgeTempo(double delta, bool largeStep = false)
    {
        Tempo.Nudge(largeStep ? delta * 10 : delta);
    }

    private void InitializeFromPaths()
    {
        if (_paths.HasRythmLibrary && _paths.RythmDir is not null)
        {
            LoopBrowser.SetFolder(_paths.RythmDir);
            Status.Show($"{LoopBrowser.TotalCount} loops loaded from shared/RYTHM");
        }

        SubLoops.Scan(_paths.SubMidiDir);

        KitBrowser.SetPresetDirectory(_paths.PresetDir);
        _kitSession.TryLoadDefaultPreset();
        KitBrowser.SyncFromSession();

        if (_midiInput.ConnectByIndex(0, _padRouter))
        {
            KitBrowser.IsMidiConnected = true;
            Settings.RefreshMappings();
        }
    }

    private void OnDeviceChanged(object? sender, EventArgs e)
    {
        _kitPlayer.ReattachToMixer();
        _midiFilePlayer.ReattachToMixer();
        Machine.RefreshAudioStatus();
        Mixer.RefreshAudioStatus();
    }

    public void Dispose()
    {
        _clockTimer.Stop();
        _beatTimer.Stop();
        _engine.DeviceChanged -= OnDeviceChanged;
        Machine.Dispose();
        Mixer.Dispose();
        Editor.Dispose();
        SampleCreator.Dispose();
        Player.Dispose();
        MasterStrip.Dispose();
        _midiFilePlayer.Dispose();
        _midiInput.Dispose();
        _kitPlayer.Dispose();
        _engine.Dispose();
    }
}
