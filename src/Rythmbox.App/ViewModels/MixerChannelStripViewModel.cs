using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.App.Localization;
using Rythmbox.Core.Audio;
using Rythmbox.Core.Models.Mixer;

namespace Rythmbox.App.ViewModels;

/// <summary>One mixer channel strip — binds UI to <see cref="MixerChannel"/> state.</summary>
public sealed partial class MixerChannelStripViewModel : ViewModelBase
{
    private readonly Action<MixerChannelStripViewModel>? _onSelect;
    private readonly Action<MixerChannelStripViewModel, double>? _onGainChanged;
    private readonly Action<MixerChannelStripViewModel, bool>? _onMuteChanged;
    private readonly Action<MixerChannelStripViewModel, bool>? _onSoloChanged;
    private readonly Action<MixerChannelStripViewModel, MixerFxSlot, bool>? _onFxSlotChanged;
    private readonly LocalizationService _i18n;

    public MixerChannelStripViewModel(
        MixerChannel channel,
        LocalizationService i18n,
        Action<MixerChannelStripViewModel>? onSelect = null,
        Action<MixerChannelStripViewModel, double>? onGainChanged = null,
        Action<MixerChannelStripViewModel, bool>? onMuteChanged = null,
        Action<MixerChannelStripViewModel, bool>? onSoloChanged = null,
        Action<MixerChannelStripViewModel, MixerFxSlot, bool>? onFxSlotChanged = null)
    {
        Channel = channel;
        _i18n = i18n;
        _onSelect = onSelect;
        _onGainChanged = onGainChanged;
        _onMuteChanged = onMuteChanged;
        _onSoloChanged = onSoloChanged;
        _onFxSlotChanged = onFxSlotChanged;
        Meter = MixerMeterViewModel.FromState(channel.Meter, _i18n);
        SyncFromChannel();
    }

    public MixerChannel Channel { get; }

    public string Id => Channel.Id;

    public string Name => Channel.Name;

    public string ShortName => string.IsNullOrWhiteSpace(Channel.ShortName) ? Channel.Name : Channel.ShortName;

    public MixerChannelKind Kind => Channel.Kind;

    public DrumGroup Group => Channel.Group;

    public string TypeLabel => Kind switch
    {
        MixerChannelKind.Master => "MST",
        MixerChannelKind.Group => "GRP",
        MixerChannelKind.DrumVoice => "DRM",
        _ => "CH",
    };

    public string GroupLabel => Group switch
    {
        DrumGroup.Drum => "DRUM",
        DrumGroup.Percussion => "PERC",
        DrumGroup.Cymbal => "CYM",
        _ => "OTHER",
    };

    public int StripMinWidth => Kind switch
    {
        MixerChannelKind.Master => 180,
        MixerChannelKind.Group => 96,
        _ => 84,
    };

    public bool ShowPan => Channel.IsPanEnabled;

    public bool ShowSolo => Channel.IsSoloEnabled;

    public string RouteName => Channel.RouteName;

    public string SoloToolTip => ShowSolo ? "Solo" : "Solo is not available for this channel";

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isEnabled = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GainLabel))]
    [NotifyPropertyChangedFor(nameof(GainDbLabel))]
    [NotifyPropertyChangedFor(nameof(GainDbValue))]
    private double _faderValue = MixerVolume.UnityNorm;

    [ObservableProperty]
    private double _pan;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private bool _isSoloed;

    [ObservableProperty]
    private MixerMeterViewModel _meter;

    [ObservableProperty]
    private bool _fxEqEnabled = true;

    [ObservableProperty]
    private bool _fxCompEnabled;

    [ObservableProperty]
    private bool _fxDelayEnabled = true;

    [ObservableProperty]
    private bool _fxReverbEnabled = true;

    // Every channel, including the master bus, exposes its inserts on the surface.
    public bool ShowFxSlots => true;

    public string GainLabel => MixerVolume.FormatDb(FaderValue);

    public string GainDbLabel => MixerVolume.FormatDbWithUnit(FaderValue);

    public string GainDbValue => MixerVolume.FormatDb(FaderValue);

    public string AccentClass => Group switch
    {
        DrumGroup.Drum => "drum",
        DrumGroup.Percussion => "perc",
        DrumGroup.Cymbal => "cym",
        _ => Kind == MixerChannelKind.Master ? "master" : "other",
    };

    partial void OnFaderValueChanged(double value)
    {
        Channel.Gain = MixerVolume.NormToLinear(value);
        _onGainChanged?.Invoke(this, Channel.Gain);
        OnPropertyChanged(nameof(GainLabel));
        OnPropertyChanged(nameof(GainDbLabel));
        OnPropertyChanged(nameof(GainDbValue));
    }

    partial void OnPanChanged(double value) => Channel.Pan = value;

    partial void OnIsMutedChanged(bool value)
    {
        Channel.IsMuted = value;
        _onMuteChanged?.Invoke(this, value);
    }

    partial void OnIsSoloedChanged(bool value)
    {
        Channel.IsSoloed = value;
        _onSoloChanged?.Invoke(this, value);
    }

    partial void OnIsSelectedChanged(bool value) => Channel.IsSelected = value;

    partial void OnFxEqEnabledChanged(bool value)
    {
        if (!_suppressFx)
        {
            _onFxSlotChanged?.Invoke(this, MixerFxSlot.Eq, value);
        }
    }

    partial void OnFxCompEnabledChanged(bool value)
    {
        if (!_suppressFx)
        {
            _onFxSlotChanged?.Invoke(this, MixerFxSlot.Compressor, value);
        }
    }

    partial void OnFxDelayEnabledChanged(bool value)
    {
        if (!_suppressFx)
        {
            _onFxSlotChanged?.Invoke(this, MixerFxSlot.Delay, value);
        }
    }

    partial void OnFxReverbEnabledChanged(bool value)
    {
        if (!_suppressFx)
        {
            _onFxSlotChanged?.Invoke(this, MixerFxSlot.Reverb, value);
        }
    }

    public void SetFxSlots(ChannelDspSettings settings)
    {
        _suppressFx = true;
        FxEqEnabled = settings.EqEnabled;
        FxCompEnabled = settings.CompressorEnabled;
        FxDelayEnabled = settings.DelayEnabled;
        FxReverbEnabled = settings.ReverbEnabled;
        _suppressFx = false;
    }

    private bool _suppressFx;

    [RelayCommand]
    private void Select() => _onSelect?.Invoke(this);

    [RelayCommand]
    private void ResetGain()
    {
        FaderValue = MixerVolume.UnityNorm;
    }

    public void SyncFromChannel()
    {
        IsEnabled = Channel.IsEnabled;
        FaderValue = MixerVolume.LinearToNorm(Channel.Gain);
        Pan = Channel.Pan;
        IsMuted = Channel.IsMuted;
        IsSoloed = Channel.IsSoloed;
        IsSelected = Channel.IsSelected;
        Meter = MixerMeterViewModel.FromState(Channel.Meter, _i18n);
        OnPropertyChanged(nameof(ShowPan));
        OnPropertyChanged(nameof(ShowSolo));
        OnPropertyChanged(nameof(SoloToolTip));
    }

    public void UpdateMeter(MixerMeterState state)
    {
        Channel.Meter = state;
        Meter = MixerMeterViewModel.FromState(state, _i18n);
    }

    public void RefreshLocalizedLabels() => Meter.RefreshLocalizedLabels();
}

public sealed partial class MixerMeterViewModel : ViewModelBase
{
    private readonly LocalizationService? _i18n;

    private MixerMeterViewModel(LocalizationService? i18n)
    {
        _i18n = i18n;
    }

    [ObservableProperty]
    private double _rmsLeft;

    [ObservableProperty]
    private double _rmsRight;

    [ObservableProperty]
    private double _peakLeft;

    [ObservableProperty]
    private double _peakRight;

    [ObservableProperty]
    private bool _isClipping;

    [ObservableProperty]
    private bool _hasSignalData;

    public string StatusLabel => HasSignalData ? string.Empty : (_i18n?["common.noSignal"] ?? "No signal");

    public void RefreshLocalizedLabels() => OnPropertyChanged(nameof(StatusLabel));

    public static MixerMeterViewModel FromState(MixerMeterState state, LocalizationService? i18n = null) =>
        new(i18n)
        {
            RmsLeft = state.RmsLeft,
            RmsRight = state.RmsRight,
            PeakLeft = state.PeakLeft,
            PeakRight = state.PeakRight,
            IsClipping = state.IsClipping,
            HasSignalData = state.HasSignalData,
        };
}
