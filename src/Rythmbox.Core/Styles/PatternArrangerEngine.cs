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

        TimeSig = new TimeSignatureController();
        TimeSig.Changed += OnTimeSignatureChanged;
        SyncTimeSignatureToSession();
    }

    public ArrangerSession Session { get; }

    /// <summary>Manages the base signature and momentary switches (e.g. a foot-switch 2/4 bar).</summary>
    public TimeSignatureController TimeSig { get; }

    /// <summary>The signature applied when a momentary switch is triggered. Defaults to a 2/4 bar.</summary>
    public TimeSignature MomentarySignature { get; set; } = new(2, 4);

    public event Action? SessionChanged;

    public void SelectStyle(StyleDefinition style)
    {
        Session.SelectedStyle = style;
        Session.SelectedStyleId = style.Id;
        Session.SelectedPatternId = null;
        Session.PlayingPatternId = null;
        Session.QueuedPatternId = null;
        Session.TransportState = ArrangerTransportState.Stopped;
        Session.Macros = style.Macros.Clone();
        Session.CurrentTempo = style.DefaultTempo;
        Session.LastError = style.IsValid ? null : string.Join("; ", style.ValidationErrors);
        TimeSig.SetBase(TimeSignature.Parse(style.TimeSignature));
        TimeSig.Reset();

        // Selecting a style should make the global Play button meaningful. We
        // preload Verse A when available (or the first valid pattern) but do
        // not start audio until the user explicitly presses Play.
        var defaultPattern = style.Patterns.TryGetValue("verse_a", out var verse)
            ? verse
            : style.Patterns.Values.FirstOrDefault(static pattern => pattern.HasMidiFile);
        if (defaultPattern is { HasMidiFile: true })
        {
            try
            {
                PreparePattern(defaultPattern);
            }
            catch (Exception ex)
            {
                Session.LastError = ex.Message;
                Session.SelectedPatternId = null;
            }
        }

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

        // One-shots fire immediately; looping patterns are queued and swapped in
        // at the next bar boundary (see OnBarAdvanced -> ApplyQueuedPatternIfReady).
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

        // Clear the queue before loading so LoadAndPlayPattern's notification
        // reflects the now-playing pattern and drops the "queued" highlight.
        Session.QueuedPatternId = null;
        LoadAndPlayPattern(pattern);
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

            PreparePattern(pattern);
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

    private void PreparePattern(StylePattern pattern)
    {
        if (pattern.ResolvedMidiPath is null)
        {
            return;
        }

        _midiPlayer.Load(pattern.ResolvedMidiPath);
        _midiPlayer.IsLooping = pattern.PlaybackMode == PatternPlaybackMode.Loop
                                && !pattern.OneShot
                                && pattern.Type is not PatternType.Ending;
        Session.SelectedPatternId = pattern.Id;
    }

    /// <summary>Drop a momentary <see cref="MomentarySignature"/> bar (default 2/4), then return to base.</summary>
    public void TriggerMomentarySwitch() => TimeSig.TriggerMomentary(MomentarySignature);

    /// <summary>Called by the transport clock on each bar boundary so momentary overrides expire.</summary>
    public void OnBarAdvanced()
    {
        // At each bar boundary, swap in any pattern queued while playing so the
        // switch is quantized to the bar rather than cutting in mid-pattern.
        ApplyQueuedPatternIfReady();
        TimeSig.AdvanceBar();
    }

    private void OnTimeSignatureChanged()
    {
        SyncTimeSignatureToSession();
        Notify();
    }

    private void SyncTimeSignatureToSession()
    {
        Session.BaseTimeSignature = TimeSig.Base;
        Session.CurrentTimeSignature = TimeSig.Current;
        Session.TimeSignatureOverridePending = TimeSig.HasPendingOverride;
    }

    private void Notify() => SessionChanged?.Invoke();
}
