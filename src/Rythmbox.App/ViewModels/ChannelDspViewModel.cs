using CommunityToolkit.Mvvm.ComponentModel;
using Rythmbox.Core.Models.Mixer;

namespace Rythmbox.App.ViewModels;

/// <summary>FX panel for the selected mixer channel.</summary>
public sealed partial class ChannelDspViewModel : ViewModelBase
{
    private Action<ChannelDspSettings>? _apply;

    public void Bind(Action<ChannelDspSettings> apply) => _apply = apply;

    public void Load(ChannelDspSettings settings)
    {
        _suppressApply = true;
        TrimGainDb = LinearToDb(settings.TrimGain);
        LowGainDb = settings.LowGainDb;
        MidGainDb = settings.MidGainDb;
        HighGainDb = settings.HighGainDb;
        CompressorEnabled = settings.CompressorEnabled;
        CompressorThresholdDb = settings.CompressorThresholdDb;
        CompressorRatio = settings.CompressorRatio;
        DelayTimeMs = settings.DelayTimeMs;
        DelayFeedback = settings.DelayFeedback;
        DelayMix = settings.DelayMix * 100;
        ReverbMix = settings.ReverbMix * 100;
        ReverbSize = settings.ReverbSize * 100;
        EqEnabled = settings.EqEnabled;
        DelayEnabled = settings.DelayEnabled;
        ReverbEnabled = settings.ReverbEnabled;
        _suppressApply = false;
    }

    private bool _suppressApply;

    [ObservableProperty]
    private string _channelName = "Channel";

    [ObservableProperty]
    private double _trimGainDb;

    [ObservableProperty]
    private double _lowGainDb;

    [ObservableProperty]
    private double _midGainDb;

    [ObservableProperty]
    private double _highGainDb;

    [ObservableProperty]
    private bool _compressorEnabled;

    [ObservableProperty]
    private bool _eqEnabled = true;

    [ObservableProperty]
    private bool _delayEnabled = true;

    [ObservableProperty]
    private bool _reverbEnabled = true;

    [ObservableProperty]
    private double _compressorThresholdDb = -18;

    [ObservableProperty]
    private double _compressorRatio = 3;

    [ObservableProperty]
    private double _delayTimeMs;

    [ObservableProperty]
    private double _delayFeedback = 35;

    [ObservableProperty]
    private double _delayMix;

    [ObservableProperty]
    private double _reverbMix;

    [ObservableProperty]
    private double _reverbSize = 45;

    partial void OnTrimGainDbChanged(double value) => Apply();
    partial void OnLowGainDbChanged(double value) => Apply();
    partial void OnMidGainDbChanged(double value) => Apply();
    partial void OnHighGainDbChanged(double value) => Apply();
    partial void OnCompressorEnabledChanged(bool value) => Apply();
    partial void OnEqEnabledChanged(bool value) => Apply();
    partial void OnDelayEnabledChanged(bool value) => Apply();
    partial void OnReverbEnabledChanged(bool value) => Apply();
    partial void OnCompressorThresholdDbChanged(double value) => Apply();
    partial void OnCompressorRatioChanged(double value) => Apply();
    partial void OnDelayTimeMsChanged(double value) => Apply();
    partial void OnDelayFeedbackChanged(double value) => Apply();
    partial void OnDelayMixChanged(double value) => Apply();
    partial void OnReverbMixChanged(double value) => Apply();
    partial void OnReverbSizeChanged(double value) => Apply();

    private void Apply()
    {
        if (_suppressApply)
        {
            return;
        }

        _apply?.Invoke(new ChannelDspSettings
        {
            TrimGain = DbToLinear(TrimGainDb),
            LowGainDb = (float)LowGainDb,
            MidGainDb = (float)MidGainDb,
            HighGainDb = (float)HighGainDb,
            CompressorEnabled = CompressorEnabled,
            CompressorThresholdDb = (float)CompressorThresholdDb,
            CompressorRatio = (float)Math.Max(1, CompressorRatio),
            EqEnabled = EqEnabled,
            DelayEnabled = DelayEnabled,
            ReverbEnabled = ReverbEnabled,
            DelayTimeMs = (float)Math.Max(0, DelayTimeMs),
            DelayFeedback = (float)Math.Clamp(DelayFeedback / 100.0, 0, 0.95),
            DelayMix = (float)Math.Clamp(DelayMix / 100.0, 0, 1),
            ReverbMix = (float)Math.Clamp(ReverbMix / 100.0, 0, 1),
            ReverbSize = (float)Math.Clamp(ReverbSize / 100.0, 0, 1),
        });
    }

    private static double LinearToDb(double linear)
    {
        if (linear <= 0.000001)
        {
            return -60;
        }

        return 20 * Math.Log10(linear);
    }

    private static float DbToLinear(double db) => (float)Math.Pow(10, db / 20.0);
}
