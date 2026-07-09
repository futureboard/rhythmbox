using Rythmbox.Core.Models.Styles;

namespace Rythmbox.Core.Styles;

/// <summary>Live arranger session state (style, pattern, transport, macros).</summary>
public sealed class ArrangerSession
{
    public string? SelectedStyleId { get; set; }

    public StyleDefinition? SelectedStyle { get; set; }

    public string? SelectedPatternId { get; set; }

    public string? PlayingPatternId { get; set; }

    public string? QueuedPatternId { get; set; }

    public string? SelectedKitId { get; set; }

    public string KitDisplayName { get; set; } = "Not Loaded";

    public ArrangerTransportState TransportState { get; set; } = ArrangerTransportState.Stopped;

    public int CurrentBar { get; set; } = 1;

    public int CurrentBeat { get; set; } = 1;

    /// <summary>The groove's base time signature (from the selected style).</summary>
    public TimeSignature BaseTimeSignature { get; set; } = TimeSignature.FourFour;

    /// <summary>The time signature of the bar currently playing (may differ during a momentary switch).</summary>
    public TimeSignature CurrentTimeSignature { get; set; } = TimeSignature.FourFour;

    /// <summary>True while a momentary time-signature switch is queued or counting down.</summary>
    public bool TimeSignatureOverridePending { get; set; }

    public double CurrentTempo { get; set; } = 120;

    public RhythmMacros Macros { get; set; } = RhythmMacros.Default;

    public IReadOnlyList<SongChainEntry> SongChain { get; set; } = [];

    public string? LastError { get; set; }

    public PatternPadVisualState GetPadVisualState(PatternPadSlot slot)
    {
        if (slot.Kind == PatternPadKind.Stop)
        {
            return TransportState == ArrangerTransportState.Playing
                ? PatternPadVisualState.Idle
                : PatternPadVisualState.Idle;
        }

        if (slot.Kind == PatternPadKind.TapTempo)
        {
            return PatternPadVisualState.Idle;
        }

        if (slot.PatternKey is null)
        {
            return PatternPadVisualState.Disabled;
        }

        if (SelectedStyle?.Patterns.TryGetValue(slot.PatternKey, out var pattern) != true || pattern is null)
        {
            return PatternPadVisualState.Missing;
        }

        if (!pattern.HasMidiFile)
        {
            return PatternPadVisualState.Missing;
        }

        if (PlayingPatternId == slot.PatternKey)
        {
            return PatternPadVisualState.Playing;
        }

        if (QueuedPatternId == slot.PatternKey)
        {
            return PatternPadVisualState.Queued;
        }

        if (SelectedPatternId == slot.PatternKey)
        {
            return PatternPadVisualState.Selected;
        }

        return PatternPadVisualState.Idle;
    }

    public StylePattern? GetPattern(string? patternKey)
    {
        if (patternKey is null || SelectedStyle is null)
        {
            return null;
        }

        return SelectedStyle.Patterns.TryGetValue(patternKey, out var pattern) ? pattern : null;
    }
}
