using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Providers;
using Rythmbox.Core.Engine;
using Rythmbox.Core.Models;
using Rythmbox.Core.Samples;

namespace Rythmbox.Core.Samples;

/// <summary>One-shot preview playback for mono drum samples via SoundFlow <see cref="SoundPlayer"/>.</summary>
public sealed class SamplePreviewPlayer : IDisposable
{
    private readonly PlaybackEngine _engine;
    private SoundPlayer? _player;

    public SamplePreviewPlayer(PlaybackEngine engine)
    {
        _engine = engine;
    }

    public bool IsPlaying => _player?.State == PlaybackState.Playing;

    public void Play(DrumSample sample)
    {
        float[]? buffer = null;
        if (sample.Samples.Length > 0)
        {
            buffer = sample.Samples;
        }
        else if (sample.HasVelocityLayers)
        {
            buffer = sample.VelocityLayers
                .SelectMany(static layer => layer.RoundRobinSamples)
                .FirstOrDefault(static samples => samples.Length > 0);
        }

        if (buffer is null || buffer.Length == 0)
        {
            return;
        }

        Play(buffer, sample.Gain, sample.PitchSemitones, sample.Label);
    }

    public void Play(float[] monoSamples, float gain = 1f, float pitchSemitones = 0f, string name = "Preview")
    {
        Stop();

        if (monoSamples.Length == 0)
        {
            return;
        }

        var buffer = (float[])monoSamples.Clone();
        if (MathF.Abs(pitchSemitones) > 0.001f)
        {
            buffer = WavCodec.PitchShift(buffer, pitchSemitones);
        }

        if (gain != 1f)
        {
            WavCodec.ApplyGain(buffer, gain);
        }

        // Engine format is stereo (DvdHq); mono buffers must be interleaved L/R or playback runs ~1 octave high.
        buffer = ToStereoInterleaved(buffer);
        var provider = new RawDataProvider(buffer, WavCodec.TargetSampleRate);
        _player = new SoundPlayer(_engine.RawEngine, _engine.Format, provider)
        {
            Name = name,
        };
        _player.PlaybackEnded += OnPlaybackEnded;
        _engine.MasterMixer.AddComponent(_player);
        _player.Play();
    }

    public void Stop()
    {
        if (_player is null)
        {
            return;
        }

        _player.PlaybackEnded -= OnPlaybackEnded;
        _player.Stop();
        _engine.MasterMixer.RemoveComponent(_player);
        _player.Dispose();
        _player = null;
    }

    private void OnPlaybackEnded(object? sender, EventArgs e) => Stop();

    private static float[] ToStereoInterleaved(float[] mono)
    {
        var stereo = new float[mono.Length * 2];
        for (var i = 0; i < mono.Length; i++)
        {
            stereo[i * 2] = mono[i];
            stereo[i * 2 + 1] = mono[i];
        }

        return stereo;
    }

    public void Dispose() => Stop();
}
