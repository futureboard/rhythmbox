using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.Core.Engine;

namespace Rythmbox.App.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly PlaybackEngine _engine;
    private readonly SoundFontPlayer _soundFontPlayer;
    private readonly MidiInputService _midiInput;
    private readonly MidiFilePlayer _midiFilePlayer;
    private readonly LoopLibraryService _loopLibrary;
    private readonly DispatcherTimer _clockTimer;

    public MainWindowViewModel()
    {
        _engine = new PlaybackEngine();
        _engine.Start();

        _soundFontPlayer = new SoundFontPlayer(_engine);
        _midiInput = new MidiInputService(_engine);
        _midiFilePlayer = new MidiFilePlayer(_engine, _soundFontPlayer);
        _loopLibrary = new LoopLibraryService();

        // The audio device (and its MasterMixer) can be swapped at runtime; re-attach
        // whatever is currently loaded so playback keeps working after the switch.
        _engine.DeviceChanged += OnDeviceChanged;

        SoundFontBrowser = new SoundFontBrowserViewModel(_soundFontPlayer, _midiInput);
        Player = new PlayerViewModel(_midiFilePlayer);
        MasterStrip = new MasterStripViewModel(_engine);
        LoopBrowser = new LoopBrowserViewModel(_loopLibrary, Player);
        PadGrid = new PercussionPadGridViewModel(_soundFontPlayer, Player);
        NowPlaying = new NowPlayingViewModel(LoopBrowser, Player);

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => ClockText = DateTime.Now.ToString("HH:mm:ss");
        _clockTimer.Start();
        ClockText = DateTime.Now.ToString("HH:mm:ss");
    }

    public SoundFontBrowserViewModel SoundFontBrowser { get; }

    public PlayerViewModel Player { get; }

    public MasterStripViewModel MasterStrip { get; }

    public LoopBrowserViewModel LoopBrowser { get; }

    public PercussionPadGridViewModel PadGrid { get; }

    public NowPlayingViewModel NowPlaying { get; }

    [ObservableProperty]
    private string _clockText = string.Empty;

    [ObservableProperty]
    private bool _isMixerOpen;

    [RelayCommand]
    private void ToggleMixer() => IsMixerOpen = !IsMixerOpen;

    [RelayCommand]
    private void Quit()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    public void SetLoopFolder(string folder) => LoopBrowser.SetFolder(folder);

    private void OnDeviceChanged(object? sender, EventArgs e)
    {
        _soundFontPlayer.ReattachToMixer();
        _midiFilePlayer.ReattachToMixer();
    }

    public void Dispose()
    {
        _clockTimer.Stop();
        _engine.DeviceChanged -= OnDeviceChanged;
        Player.Dispose();
        MasterStrip.Dispose();
        _midiFilePlayer.Dispose();
        _midiInput.Dispose();
        _soundFontPlayer.Dispose();
        _engine.Dispose();
    }
}
