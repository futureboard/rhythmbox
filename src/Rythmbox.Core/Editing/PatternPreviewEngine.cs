using Rythmbox.Core.Engine;
using Rythmbox.Core.Models;

namespace Rythmbox.Core.Editing;

/// <summary>Step-by-step preview playback of a <see cref="DrumPattern"/> through a loaded drum kit.</summary>
public sealed class PatternPreviewEngine : IDisposable
{
    private readonly KitSamplePlayer _kitPlayer;
    private CancellationTokenSource? _playCts;

    public PatternPreviewEngine(KitSamplePlayer kitPlayer)
    {
        _kitPlayer = kitPlayer;
    }

    public bool IsPlaying => _playCts is not null;

    public int CurrentStep { get; private set; } = -1;

    public event Action<int>? StepAdvanced;

    public event Action? PlaybackFinished;

    public async Task PlayAsync(DrumPattern pattern, bool loop = true)
    {
        Stop();

        if (!_kitPlayer.IsLoaded)
        {
            return;
        }

        _playCts = new CancellationTokenSource();
        var token = _playCts.Token;
        var stepMs = StepDurationMs(pattern);

        try
        {
            do
            {
                for (var step = 0; step < pattern.TotalSteps; step++)
                {
                    token.ThrowIfCancellationRequested();
                    CurrentStep = step;
                    StepAdvanced?.Invoke(step);

                    foreach (var ((pad, hitStep), velocity) in pattern.Hits)
                    {
                        if (hitStep == step)
                        {
                            _kitPlayer.TriggerPad(pad, velocity / 127f);
                        }
                    }

                    await Task.Delay(stepMs, token);
                }
            }
            while (loop && !token.IsCancellationRequested);
        }
        catch (OperationCanceledException)
        {
            // Expected on stop.
        }
        finally
        {
            CurrentStep = -1;
            StepAdvanced?.Invoke(-1);
            _playCts?.Dispose();
            _playCts = null;
            PlaybackFinished?.Invoke();
        }
    }

    public void Stop()
    {
        _playCts?.Cancel();
        _kitPlayer.AllNotesOff();
    }

    public static int StepDurationMs(DrumPattern pattern) =>
        (int)Math.Max(20, 60000.0 / (pattern.Bpm * pattern.StepsPerBar / 4.0));

    public void Dispose() => Stop();
}
