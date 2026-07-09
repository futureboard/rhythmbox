using Rythmbox.Core.Models.Mixer;
using Rythmbox.Core.Samples;

namespace Rythmbox.Core.Audio.Dsp;

/// <summary>Per-channel insert chain: trim gain, EQ, compressor, delay, and reverb send.</summary>
public sealed class ChannelDspChain
{
    private readonly BiquadFilter _lowShelf = new();
    private readonly BiquadFilter _midPeak = new();
    private readonly BiquadFilter _highShelf = new();
    private readonly DynamicsCompressor _compressor = new();
    private readonly DelayEffect _delay = new();
    private ChannelDspSettings _settings = new();
    private bool _coefficientsDirty = true;

    public ChannelDspSettings Settings
    {
        get => _settings;
        set
        {
            _settings = value ?? new ChannelDspSettings();
            _coefficientsDirty = true;
        }
    }

    public float ReverbSend { get; private set; }

    public void Process(ref float sample, float sampleRate)
    {
        if (_coefficientsDirty)
        {
            UpdateCoefficients(sampleRate);
        }

        sample *= Math.Clamp(_settings.TrimGain, 0f, 4f);

        if (_settings.EqEnabled)
        {
            sample = _lowShelf.Process(sample);
            sample = _midPeak.Process(sample);
            sample = _highShelf.Process(sample);
        }

        sample = _compressor.Process(sample, sampleRate);

        if (_settings.DelayEnabled)
        {
            sample = _delay.Process(sample, sampleRate);
        }

        ReverbSend = _settings.ReverbEnabled
            ? sample * Math.Clamp(_settings.ReverbMix, 0f, 1f)
            : 0f;
    }

    public void Reset()
    {
        _lowShelf.Reset();
        _midPeak.Reset();
        _highShelf.Reset();
        _delay.Reset();
        ReverbSend = 0f;
    }

    private void UpdateCoefficients(float sampleRate)
    {
        _lowShelf.SetLowShelf(sampleRate, 120f, _settings.LowGainDb);
        _midPeak.SetPeaking(sampleRate, _settings.MidFrequencyHz, _settings.MidGainDb, 1.1f);
        _highShelf.SetHighShelf(sampleRate, 6_500f, _settings.HighGainDb);

        _compressor.Enabled = _settings.CompressorEnabled;
        _compressor.ThresholdDb = _settings.CompressorThresholdDb;
        _compressor.Ratio = Math.Max(1f, _settings.CompressorRatio);
        _compressor.MakeupDb = _settings.CompressorMakeupDb;

        _delay.TimeMs = _settings.DelayTimeMs;
        _delay.Feedback = _settings.DelayFeedback;
        _delay.Mix = _settings.DelayMix;

        _coefficientsDirty = false;
    }
}
