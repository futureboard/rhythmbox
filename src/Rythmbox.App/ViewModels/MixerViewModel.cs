using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.App.Localization;
using Rythmbox.Core.Audio;
using Rythmbox.Core.Engine;
using Rythmbox.Core.Models;
using Rythmbox.Core.Models.Mixer;
using SoundFlow.Structs;

namespace Rythmbox.App.ViewModels;

/// <summary>
/// Rhythm-machine mixer surface — groups, drum voices, and master.
/// Ported from Futureboard SphereUIComponents mixer panel layout.
/// </summary>
public sealed partial class MixerViewModel : ViewModelBase, IDisposable
{
    public const int DrumStripWidth = 84;
    public const int GroupStripWidth = 96;
    public const int MasterStripWidth = 180;
    public const double StripMinHeight = 340;

    private readonly PlaybackEngine _engine;
    private readonly KitSamplePlayer _kitPlayer;
    private readonly IAudioBackend _audioBackend;
    private readonly LocalizationService _i18n;
    private readonly DispatcherTimer _meterTimer;
    private readonly Dictionary<string, MixerChannelStripViewModel> _stripById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, MixerChannelStripViewModel> _padStripByNote = new();
    private readonly Dictionary<PadBus, MixerChannelStripViewModel> _busStripByBus = new();
    private bool _suppressDeviceChange;

    public MixerViewModel(PlaybackEngine engine, KitSamplePlayer kitPlayer, IAudioBackend audioBackend, LocalizationService i18n)
    {
        _engine = engine;
        _kitPlayer = kitPlayer;
        _audioBackend = audioBackend;
        _i18n = i18n;
        _i18n.LanguageChanged += OnLanguageChanged;

        MasterChannel = CreateMasterStrip();
        BuildChannels();
        BindFxPanel(MasterChannel);

        RefreshDevices();
        BackendLabel = _audioBackend.PlatformBackendId;
        DeviceLabel = _audioBackend.CurrentDeviceName ?? _i18n["mixer.noDevice"];

        _meterTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _meterTimer.Tick += (_, _) => PollMeters();
        _meterTimer.Start();
    }

    public MixerChannelStripViewModel MasterChannel { get; }

    public ChannelDspViewModel SelectedChannelFx { get; } = new();

    public ObservableCollection<MixerChannelStripViewModel> Channels { get; } = new();

    public ObservableCollection<DeviceInfo> OutputDevices { get; } = new();

    [ObservableProperty]
    private MixerChannelStripViewModel? _selectedChannel;

    [ObservableProperty]
    private DeviceInfo? _selectedOutputDevice;

    [ObservableProperty]
    private string _backendLabel = string.Empty;

    [ObservableProperty]
    private string _deviceLabel = string.Empty;

    [ObservableProperty]
    private string _routeLabel = "Main Out";

    public int ChannelCount => Channels.Count;

    public string ChannelCountLabel => _i18n.Format("mixer.channels", ChannelCount);

    partial void OnSelectedOutputDeviceChanged(DeviceInfo? value)
    {
        if (_suppressDeviceChange || value is not { } device)
        {
            return;
        }

        var current = _engine.CurrentDevice;
        if (_engine.IsRunning
            && current is { } active
            && string.Equals(active.Name, device.Name, StringComparison.OrdinalIgnoreCase))
        {
            DeviceLabel = device.Name;
            RouteLabel = device.Name;
            MasterChannel.Channel.RouteName = device.Name;
            MasterChannel.SyncFromChannel();
            return;
        }

        _engine.SetOutputDevice(device);
        DeviceLabel = device.Name;
        RouteLabel = device.Name;
        MasterChannel.Channel.RouteName = device.Name;
        MasterChannel.SyncFromChannel();
    }

    [RelayCommand]
    private void RefreshDevices()
    {
        _engine.RefreshDevices();
        _audioBackend.RefreshDevices();

        _suppressDeviceChange = true;
        OutputDevices.Clear();
        foreach (var device in _engine.PlaybackDevices)
        {
            OutputDevices.Add(device);
        }

        SelectedOutputDevice = _engine.CurrentDevice ?? OutputDevices.FirstOrDefault();
        _suppressDeviceChange = false;

        BackendLabel = _audioBackend.PlatformBackendId;
        DeviceLabel = _audioBackend.CurrentDeviceName ?? SelectedOutputDevice?.Name ?? _i18n["mixer.noDevice"];
        RouteLabel = DeviceLabel;
        MasterChannel.Channel.RouteName = RouteLabel;
        MasterChannel.SyncFromChannel();
    }

    [RelayCommand]
    private void SelectChannel(MixerChannelStripViewModel? strip)
    {
        if (strip is null)
        {
            return;
        }

        foreach (var channel in AllStrips())
        {
            channel.IsSelected = ReferenceEquals(channel, strip);
        }

        SelectedChannel = strip;
        BindFxPanel(strip);
    }

    private void BindFxPanel(MixerChannelStripViewModel strip)
    {
        SelectedChannelFx.ChannelName = strip.Name;
        ChannelDspSettings settings;
        switch (strip.Kind)
        {
            case MixerChannelKind.Master:
                SelectedChannelFx.Bind(s =>
                {
                    ApplyDspSettings(strip, s);
                    strip.SetFxSlots(s);
                });
                settings = _kitPlayer.GetMasterDsp();
                SelectedChannelFx.Load(settings);
                break;
            case MixerChannelKind.Group when Enum.TryParse<PadBus>(strip.Id, out var bus):
                SelectedChannelFx.Bind(s =>
                {
                    ApplyDspSettings(strip, s);
                    strip.SetFxSlots(s);
                });
                settings = _kitPlayer.GetBusDsp(bus);
                SelectedChannelFx.Load(settings);
                break;
            case MixerChannelKind.DrumVoice when int.TryParse(strip.Id["pad_".Length..], out var note):
                SelectedChannelFx.Bind(s =>
                {
                    ApplyDspSettings(strip, s);
                    strip.SetFxSlots(s);
                });
                settings = _kitPlayer.GetPadDsp(note);
                SelectedChannelFx.Load(settings);
                break;
            default:
                SelectedChannelFx.Bind(_ => { });
                settings = new ChannelDspSettings();
                SelectedChannelFx.Load(settings);
                break;
        }

        strip.SetFxSlots(settings);
    }

    [RelayCommand]
    private void OpenAudioSettings()
    {
        // TODO: navigate to Settings audio section when dedicated route exists.
    }

    private void OnGainChanged(MixerChannelStripViewModel strip, double gain)
    {
        switch (strip.Kind)
        {
            case MixerChannelKind.Master:
                _engine.MasterMixer.Volume = (float)gain;
                break;
            case MixerChannelKind.Group when _busStripByBus.Values.FirstOrDefault(s => s.Id == strip.Id) is { } groupStrip
                && Enum.TryParse<PadBus>(strip.Id, out var bus):
                _kitPlayer.SetBusVolume(bus, (float)gain);
                break;
            case MixerChannelKind.DrumVoice when _padStripByNote.Values.FirstOrDefault(s => s.Id == strip.Id) is not null:
                if (int.TryParse(strip.Id["pad_".Length..], out var note))
                {
                    _kitPlayer.SetPadVolume(note, (float)Math.Clamp(gain, 0, 1));
                }
                break;
        }
    }

    private void OnMuteChanged(MixerChannelStripViewModel strip, bool muted)
    {
        switch (strip.Kind)
        {
            case MixerChannelKind.Master:
                // TODO: master mute when engine supports it.
                break;
            case MixerChannelKind.Group when Enum.TryParse<PadBus>(strip.Id, out var bus):
                _kitPlayer.SetBusMute(bus, muted);
                break;
            case MixerChannelKind.DrumVoice:
                if (int.TryParse(strip.Id["pad_".Length..], out var note))
                {
                    _kitPlayer.SetPadMute(note, muted);
                }
                break;
        }
    }

    private void OnFxSlotChanged(MixerChannelStripViewModel strip, MixerFxSlot slot, bool enabled)
    {
        ChannelDspSettings settings = strip.Kind switch
        {
            MixerChannelKind.Master => _kitPlayer.GetMasterDsp(),
            MixerChannelKind.Group when Enum.TryParse<PadBus>(strip.Id, out var bus) => _kitPlayer.GetBusDsp(bus),
            MixerChannelKind.DrumVoice when int.TryParse(strip.Id["pad_".Length..], out var note) => _kitPlayer.GetPadDsp(note),
            _ => new ChannelDspSettings(),
        };

        switch (slot)
        {
            case MixerFxSlot.Eq:
                settings.EqEnabled = enabled;
                break;
            case MixerFxSlot.Compressor:
                settings.CompressorEnabled = enabled;
                break;
            case MixerFxSlot.Delay:
                settings.DelayEnabled = enabled;
                break;
            case MixerFxSlot.Reverb:
                settings.ReverbEnabled = enabled;
                break;
        }

        ApplyDspSettings(strip, settings);
        if (ReferenceEquals(SelectedChannel, strip))
        {
            SelectedChannelFx.Load(settings);
        }
    }

    private void ApplyDspSettings(MixerChannelStripViewModel strip, ChannelDspSettings settings)
    {
        switch (strip.Kind)
        {
            case MixerChannelKind.Master:
                _kitPlayer.SetMasterDsp(settings);
                break;
            case MixerChannelKind.Group when Enum.TryParse<PadBus>(strip.Id, out var bus):
                _kitPlayer.SetBusDsp(bus, settings);
                break;
            case MixerChannelKind.DrumVoice when int.TryParse(strip.Id["pad_".Length..], out var note):
                _kitPlayer.SetPadDsp(note, settings);
                break;
        }
    }

    private void OnSoloChanged(MixerChannelStripViewModel strip, bool solo)
    {
        if (strip.Kind != MixerChannelKind.DrumVoice)
        {
            return;
        }

        if (int.TryParse(strip.Id["pad_".Length..], out var note))
        {
            _kitPlayer.SetPadSolo(note, solo);
        }
    }

    private void BuildChannels()
    {
        Channels.Clear();
        _stripById.Clear();
        _padStripByNote.Clear();
        _busStripByBus.Clear();

        foreach (var bus in new[] { (PadBus.Drum, "DRUM"), (PadBus.Perc, "PERC"), (PadBus.Cym, "CYM") })
        {
            var strip = CreateGroupStrip(bus.Item1, bus.Item2);
            Channels.Add(strip);
            _stripById[strip.Id] = strip;
            _busStripByBus[bus.Item1] = strip;
        }

        foreach (var pad in GmPercussionMap.Pads)
        {
            var strip = CreateDrumStrip(pad);
            Channels.Add(strip);
            _stripById[strip.Id] = strip;
            _padStripByNote[pad.Note] = strip;
        }
    }

    private MixerChannelStripViewModel CreateMasterStrip()
    {
        var channel = new MixerChannel
        {
            Id = "master",
            Name = "Master",
            ShortName = "MST",
            Kind = MixerChannelKind.Master,
            Group = DrumGroup.Other,
            Gain = _engine.MasterMixer.Volume,
            IsPanEnabled = false,
            IsSoloEnabled = false,
            RouteName = RouteLabel,
            Meter = MixerMeterState.Disabled,
        };

        return new MixerChannelStripViewModel(
            channel,
            _i18n,
            onSelect: SelectChannel,
            onGainChanged: OnGainChanged,
            onMuteChanged: OnMuteChanged,
            onFxSlotChanged: OnFxSlotChanged);
    }

    private MixerChannelStripViewModel CreateGroupStrip(PadBus bus, string label)
    {
        var channel = new MixerChannel
        {
            Id = bus.ToString(),
            Name = label,
            ShortName = label,
            Kind = MixerChannelKind.Group,
            Group = bus switch
            {
                PadBus.Drum => DrumGroup.Drum,
                PadBus.Perc => DrumGroup.Percussion,
                PadBus.Cym => DrumGroup.Cymbal,
                _ => DrumGroup.Other,
            },
            Gain = 1.0,
            IsPanEnabled = false,
            IsSoloEnabled = false,
            RouteName = "Sub",
        };

        return new MixerChannelStripViewModel(
            channel,
            _i18n,
            onSelect: SelectChannel,
            onGainChanged: OnGainChanged,
            onMuteChanged: OnMuteChanged,
            onFxSlotChanged: OnFxSlotChanged);
    }

    private MixerChannelStripViewModel CreateDrumStrip(PercussionPad pad)
    {
        var channel = new MixerChannel
        {
            Id = $"pad_{pad.Note}",
            Name = pad.Label,
            ShortName = pad.Label,
            Kind = MixerChannelKind.DrumVoice,
            Group = pad.Bus switch
            {
                PadBus.Drum => DrumGroup.Drum,
                PadBus.Perc => DrumGroup.Percussion,
                PadBus.Cym => DrumGroup.Cymbal,
                _ => DrumGroup.Other,
            },
            Gain = 1.0,
            IsPanEnabled = false,
            IsSoloEnabled = true,
            RouteName = pad.Bus.ToString().ToUpperInvariant(),
        };

        return new MixerChannelStripViewModel(
            channel,
            _i18n,
            onSelect: SelectChannel,
            onGainChanged: OnGainChanged,
            onMuteChanged: OnMuteChanged,
            onSoloChanged: OnSoloChanged,
            onFxSlotChanged: OnFxSlotChanged);
    }

    private void PollMeters()
    {
        if (!_engine.IsRunning)
        {
            return;
        }

        var rms = Math.Clamp(_engine.MasterLevelMeter.Rms, 0f, 1f);
        var peak = Math.Clamp(_engine.MasterLevelMeter.Peak, 0f, 1f);
        MasterChannel.UpdateMeter(MixerMeterState.FromMono(rms, peak));

        foreach (var strip in Channels)
        {
            var meter = strip.Kind switch
            {
                MixerChannelKind.Group when Enum.TryParse<PadBus>(strip.Id, out var bus)
                    => _kitPlayer.PollBusMeter(bus),
                MixerChannelKind.DrumVoice when int.TryParse(strip.Id["pad_".Length..], out var note)
                    => _kitPlayer.PollPadMeter(note),
                _ => MixerMeterState.Disabled,
            };
            strip.UpdateMeter(meter);
        }
    }

    private IEnumerable<MixerChannelStripViewModel> AllStrips()
    {
        yield return MasterChannel;
        foreach (var channel in Channels)
        {
            yield return channel;
        }
    }

    public void RefreshAudioStatus()
    {
        BackendLabel = _audioBackend.PlatformBackendId;
        DeviceLabel = _engine.CurrentDevice?.Name
            ?? _audioBackend.CurrentDeviceName
            ?? SelectedOutputDevice?.Name
            ?? _i18n["mixer.noDevice"];
        RouteLabel = DeviceLabel;
        MasterChannel.Channel.RouteName = RouteLabel;
        MasterChannel.SyncFromChannel();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(ChannelCountLabel));
        RefreshAudioStatus();
        foreach (var strip in AllStrips())
        {
            strip.RefreshLocalizedLabels();
        }
    }

    public void Dispose()
    {
        _meterTimer.Stop();
        _i18n.LanguageChanged -= OnLanguageChanged;
    }
}
