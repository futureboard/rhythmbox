using Rythmbox.Core.Engine;
using Rythmbox.Core.Models.Styles;
using SoundFlow.Enums;

namespace Rythmbox.Core.Styles;

/// <summary>Pattern selection, queueing, and MIDI playback for the Machine arranger.</summary>
public sealed class PatternArrangerEngine
{
    private readonly MidiFilePlayer _midiPlayer;

    public PatternArrangerEngine(MidiFilePlayer midiPlayer)
    {
        _midiPlayer = midiPlayer;
        Session = new ArrangerSession();
    }

    public ArrangerSession Session { get; }

    public event Action? SessionChanged;

    public void SelectStyle(StyleDefinition style)
    {
        Session.SelectedStyle = style;
        Session.SelectedStyleId = style.Id;
        Session.SelectedPatternId = null;
        Session.PlayingPatternId = null;
        Session.QueuedPatternId = null;
        Session.Macros = style.Macros.Clone();
        Session.CurrentTempo = style.DefaultTempo;
        Session.LastError = style.IsValid ? null : string.Join("; ", style.ValidationErrors);
        Notify();
    }

    public void ClearStyle()
    {
        Session.SelectedStyle = null;
        Session.SelectedStyleId = null;
        Session.SelectedPatternId = null;
        Session.PlayingPatternId = null;
        Session.QueuedPatternId = null;
        Notify();
    }

    public void SetKitDisplay(string? kitId, string displayName)
    {
        Session.SelectedKitId = kitId;
        Session.KitDisplayName = displayName;
        Notify();
    }

    public void SyncTransport(MidiFilePlayer player, double userTempo)
    {
        Session.CurrentTempo = userTempo;

        if (!player.IsLoaded)
        {
            Session.TransportState = ArrangerTransportState.Stopped;
            Session.PlayingPatternId = null;
            Notify();
            return;
        }

        Session.TransportState = player.State switch
        {
            PlaybackState.Playing => ArrangerTransportState.Playing,
            PlaybackState.Paused => ArrangerTransportState.Paused,
            _ => ArrangerTransportState.Stopped,
        };

        if (Session.TransportState == ArrangerTransportState.Stopped)
        {
            Session.PlayingPatternId = null;
        }

        Notify();
    }

    public void TriggerPad(PatternPadSlot slot)
    {
        switch (slot.Kind)
        {
            case PatternPadKind.Stop:
                _midiPlayer.Stop();
                Session.QueuedPatternId = null;
                Session.PlayingPatternId = null;
                Session.TransportState = ArrangerTransportState.Stopped;
                Notify();
                return;

            case PatternPadKind.TapTempo:
                // Handled by ViewModel (updates tempo).
                return;
        }

        if (slot.PatternKey is null || Session.SelectedStyle is null)
        {
            return;
        }

        if (!Session.SelectedStyle.Patterns.TryGetValue(slot.PatternKey, out var pattern))
        {
            Session.LastError = $"Pattern '{slot.PatternKey}' not in style.";
            Notify();
            return;
        }

        if (!pattern.HasMidiFile)
        {
            Session.LastError = $"Missing MIDI for '{pattern.Name}'.";
            Notify();
            return;
        }

        Session.LastError = null;
        Session.SelectedPatternId = slot.PatternKey;

        var isPlaying = _midiPlayer.State == PlaybackState.Playing;

        if (!isPlaying)
        {
            LoadAndPlayPattern(pattern);
            return;
        }

        // While playing: queue for next bar boundary (TODO: bar-quantized switch).
        if (pattern.OneShot || pattern.PlaybackMode == PatternPlaybackMode.OneShot)
        {
            LoadAndPlayPattern(pattern);
            return;
        }

        Session.QueuedPatternId = slot.PatternKey;
        Notify();
    }

    public void ApplyQueuedPatternIfReady()
    {
        if (Session.QueuedPatternId is not { } queued)
        {
            return;
        }

        if (Session.GetPattern(queued) is not { HasMidiFile: true } pattern)
        {
            Session.QueuedPatternId = null;
            Notify();
            return;
        }

        // TODO: gate on bar boundary using CurrentBar from beat clock.
        LoadAndPlayPattern(pattern);
        Session.QueuedPatternId = null;
    }

    public void SelectPattern(string patternKey)
    {
        if (Session.SelectedStyle?.Patterns.ContainsKey(patternKey) != true)
        {
            return;
        }

        Session.SelectedPatternId = patternKey;
        Notify();
    }

    public void UpdateMacros(RhythmMacros macros)
    {
        Session.Macros = macros.Clone();
        Notify();
    }

    private void LoadAndPlayPattern(StylePattern pattern)
    {
        if (pattern.ResolvedMidiPath is null)
        {
            return;
        }

        try
        {
            Session.TransportState = ArrangerTransportState.Loading;
            Notify();

            _midiPlayer.Load(pattern.ResolvedMidiPath);
            _midiPlayer.IsLooping = pattern.PlaybackMode == PatternPlaybackMode.Loop
                                    && !pattern.OneShot
                                    && pattern.Type is not PatternType.Ending;
            _midiPlayer.Play();

            Session.PlayingPatternId = pattern.Id;
            Session.SelectedPatternId = pattern.Id;
            Session.TransportState = ArrangerTransportState.Playing;
        }
        catch (Exception ex)
        {
            Session.TransportState = ArrangerTransportState.Error;
            Session.LastError = ex.Message;
        }

        Notify();
    }

    private void Notify() => SessionChanged?.Invoke();
}
