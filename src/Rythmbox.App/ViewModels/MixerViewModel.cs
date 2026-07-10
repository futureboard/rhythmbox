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
    private readonly Dictionary<DrumMixGroup, MixerChannelStripViewModel> _mixGroupStripByGroup = new();
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
    private bool _isFxEditorOpen;

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

        // The initial assignment happens while notifications are suppressed so
        // the ComboBox does not reopen the device repeatedly. Explicitly apply
        // that first available output afterwards; otherwise the UI shows a
        // speaker but the engine remains "Audio: Off".
        if (_engine.CurrentDevice is null && SelectedOutputDevice is { } initialDevice)
        {
            _engine.SetOutputDevice(initialDevice);
        }

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
        IsFxEditorOpen = true;
    }

    [RelayCommand]
    private void CloseFxEditor() => IsFxEditorOpen = false;

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
            case MixerChannelKind.Group when TryGetMixGroup(strip, out var mixGroup):
                SelectedChannelFx.Bind(s =>
                {
                    ApplyDspSettings(strip, s);
                    strip.SetFxSlots(s);
                });
                settings = _kitPlayer.GetMixGroupDsp(mixGroup);
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

    private void OnGainChanged(MixerChannelStripViewModel strip, double gain)
    {
        switch (strip.Kind)
        {
            case MixerChannelKind.Master:
                _engine.MasterMixer.Volume = (float)gain;
                break;
            case MixerChannelKind.Group when TryGetMixGroup(strip, out var mixGroup):
                _kitPlayer.SetMixGroupVolume(mixGroup, (float)gain);
                break;
        }
    }

    private void OnMuteChanged(MixerChannelStripViewModel strip, bool muted)
    {
        switch (strip.Kind)
        {
            case MixerChannelKind.Master:
                _engine.MasterMixer.Mute = muted;
                break;
            case MixerChannelKind.Group when TryGetMixGroup(strip, out var mixGroup):
                _kitPlayer.SetMixGroupMute(mixGroup, muted);
                break;
        }
    }

    private void OnFxSlotChanged(MixerChannelStripViewModel strip, MixerFxSlot slot, bool enabled)
    {
        ChannelDspSettings settings = strip.Kind switch
        {
            MixerChannelKind.Master => _kitPlayer.GetMasterDsp(),
            MixerChannelKind.Group when TryGetMixGroup(strip, out var mixGroup) => _kitPlayer.GetMixGroupDsp(mixGroup),
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
            case MixerChannelKind.Group when TryGetMixGroup(strip, out var mixGroup):
                _kitPlayer.SetMixGroupDsp(mixGroup, settings);
                break;
        }
    }

    private void OnSoloChanged(MixerChannelStripViewModel strip, bool solo)
    {
        if (!TryGetMixGroup(strip, out var mixGroup))
        {
            return;
        }

        _kitPlayer.SetMixGroupSolo(mixGroup, solo);
    }

    private void BuildChannels()
    {
        Channels.Clear();
        _stripById.Clear();
        _mixGroupStripByGroup.Clear();

        foreach (var group in GmPercussionMap.MixGroups)
        {
            var strip = CreateGroupStrip(group);
            Channels.Add(strip);
            _stripById[strip.Id] = strip;
            _mixGroupStripByGroup[group] = strip;
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

    private MixerChannelStripViewModel CreateGroupStrip(DrumMixGroup mixGroup)
    {
        var label = GmPercussionMap.GetMixGroupLabel(mixGroup);
        var channel = new MixerChannel
        {
            Id = $"group_{mixGroup}",
            Name = label,
            ShortName = label,
            Kind = MixerChannelKind.Group,
            Group = mixGroup switch
            {
                DrumMixGroup.Kick or DrumMixGroup.Snare or DrumMixGroup.Toms => DrumGroup.Drum,
                DrumMixGroup.HiHat or DrumMixGroup.Cymbals => DrumGroup.Cymbal,
                _ => DrumGroup.Percussion,
            },
            Gain = 1.0,
            IsPanEnabled = false,
            IsSoloEnabled = true,
            RouteName = mixGroup switch
            {
                DrumMixGroup.Kick or DrumMixGroup.Snare => "DRUM",
                DrumMixGroup.Cymbals => "CYM",
                _ => "PERC",
            },
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
        // Read the kit's own post-master meter rather than the backend analyser.
        // This keeps the surface alive across WASAPI device reconfiguration and
        // makes it reflect exactly the audio path sent by KitSamplePlayer.
        MasterChannel.UpdateMeter(_kitPlayer.PollMasterMeter());

        foreach (var strip in Channels)
        {
            var meter = strip.Kind switch
            {
                MixerChannelKind.Group when TryGetMixGroup(strip, out var mixGroup)
                    => _kitPlayer.PollMixGroupMeter(mixGroup),
                _ => MixerMeterState.Disabled,
            };
            strip.UpdateMeter(meter);
        }
    }

    private static bool TryGetMixGroup(MixerChannelStripViewModel strip, out DrumMixGroup group)
    {
        group = default;
        return strip.Kind == MixerChannelKind.Group
            && strip.Id.StartsWith("group_", StringComparison.Ordinal)
            && Enum.TryParse(strip.Id["group_".Length..], out group);
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
