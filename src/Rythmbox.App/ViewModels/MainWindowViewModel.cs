using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.App.Localization;
using Rythmbox.Core.Audio;
using Rythmbox.Core.Engine;
using Rythmbox.Core.Styles;

namespace Rythmbox.App.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly PlaybackEngine _engine;
    private readonly KitSamplePlayer _kitPlayer;
    private readonly KitPresetService _kitPresets;
    private readonly PadMappingService _padMapping;
    private readonly PadMidiRouter _padRouter;
    private readonly MidiInputService _midiInput;
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
        _kitPresets = new KitPresetService();
        _padMapping = new PadMappingService();
        _padRouter = new PadMidiRouter(_kitPlayer, _padMapping);
        _midiInput = new MidiInputService(_engine);
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
        KitBrowser = new KitBrowserViewModel(_kitPlayer, _kitPresets, _midiInput, _padRouter);
        Player = new PlayerViewModel(_midiFilePlayer);
        MasterStrip = new MasterStripViewModel(_engine);
        Mixer = new MixerViewModel(_engine, _kitPlayer, _audioBackend, _localization);
        LoopBrowser = new LoopBrowserViewModel(_loopLibrary, Player);
        PadGrid = new PercussionPadGridViewModel(_kitPlayer, Player);
        NowPlaying = new NowPlayingViewModel(LoopBrowser, Player);
        PadMixer = new PadMixerViewModel(_kitPlayer);
        BusMixer = new BusMixerViewModel(_kitPlayer);
        Settings = new SettingsViewModel(_padMapping, _padRouter, _midiInput, Status, _localization);
        Tempo = new TempoViewModel(_tempoPresets, Player, Status);
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
            _localization);

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

    [ObservableProperty]
    private AppPage _currentPage = AppPage.Machine;

    [ObservableProperty]
    private string _clockText = string.Empty;

    public bool IsMachinePage => CurrentPage == AppPage.Machine;

    public bool IsPadsPage => CurrentPage == AppPage.Pads;

    public bool IsMixerPage => CurrentPage == AppPage.Mixer;

    public bool IsSettingsPage => CurrentPage == AppPage.Settings;

    public bool IsEditorPage => CurrentPage == AppPage.Editor;

    public bool IsMacroPage => CurrentPage == AppPage.Macro;

    public bool IsChromePage => CurrentPage == AppPage.Chrome;

    partial void OnCurrentPageChanged(AppPage value)
    {
        OnPropertyChanged(nameof(IsMachinePage));
        OnPropertyChanged(nameof(IsPadsPage));
        OnPropertyChanged(nameof(IsMixerPage));
        OnPropertyChanged(nameof(IsSettingsPage));
        OnPropertyChanged(nameof(IsEditorPage));
        OnPropertyChanged(nameof(IsMacroPage));
        OnPropertyChanged(nameof(IsChromePage));
    }

    [RelayCommand]
    private void Navigate(AppPage page)
    {
        if (page == AppPage.Editor)
        {
            LaunchEditor();
            return;
        }

        if (page == AppPage.Macro)
        {
            LaunchSampleCreator();
            return;
        }

        CurrentPage = page;
    }

    private void LaunchEditor()
    {
        var editorExe = Path.Combine(AppContext.BaseDirectory, "Rythmbox.Editor.exe");
        if (!File.Exists(editorExe))
        {
            editorExe = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Rythmbox.Editor", "bin", "Debug", "net10.0", "Rythmbox.Editor.exe"));
        }

        if (!File.Exists(editorExe))
        {
            Status.Show("Rythmbox.Editor not found — build the Editor project first");
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = editorExe,
                UseShellExecute = true,
            });
            Status.Show("Launched Rythmbox Editor");
        }
        catch (Exception ex)
        {
            Status.Show($"Failed to launch Editor: {ex.Message}");
        }
    }

    private void LaunchSampleCreator()
    {
        var exe = Path.Combine(AppContext.BaseDirectory, "Rythmbox.SampleCreator.exe");
        if (!File.Exists(exe))
        {
            exe = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Rythmbox.SampleCreator", "bin", "Debug", "net10.0", "Rythmbox.SampleCreator.exe"));
        }

        if (!File.Exists(exe))
        {
            Status.Show("Rythmbox.SampleCreator not found — build the SampleCreator project first");
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = true,
            });
            Status.Show("Launched Rythmbox Sample Creator");
        }
        catch (Exception ex)
        {
            Status.Show($"Failed to launch Sample Creator: {ex.Message}");
        }
    }

    [RelayCommand]
    private void Quit()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
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
        KitBrowser.TryLoadDefault(_paths.PresetDir);

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
        Player.Dispose();
        MasterStrip.Dispose();
        _midiFilePlayer.Dispose();
        _midiInput.Dispose();
        _kitPlayer.Dispose();
        _engine.Dispose();
    }
}
