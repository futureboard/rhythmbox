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
        Stop();

        if (!sample.HasAudio)
        {
            return;
        }

        var buffer = (float[])sample.Samples.Clone();
        if (MathF.Abs(sample.PitchSemitones) > 0.001f)
        {
            buffer = WavCodec.PitchShift(buffer, sample.PitchSemitones);
        }

        if (sample.Gain != 1f)
        {
            WavCodec.ApplyGain(buffer, sample.Gain);
        }

        // Engine format is stereo (DvdHq); mono buffers must be interleaved L/R or playback runs ~1 octave high.
        buffer = ToStereoInterleaved(buffer);
        var provider = new RawDataProvider(buffer, WavCodec.TargetSampleRate);
        _player = new SoundPlayer(_engine.RawEngine, _engine.Format, provider)
        {
            Name = sample.Label,
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
