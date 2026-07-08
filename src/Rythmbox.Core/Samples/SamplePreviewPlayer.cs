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
        if (sample.Gain != 1f)
        {
            WavCodec.ApplyGain(buffer, sample.Gain);
        }

        var provider = new RawDataProvider(buffer, sample.SampleRate);
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

    public void Dispose() => Stop();
}
