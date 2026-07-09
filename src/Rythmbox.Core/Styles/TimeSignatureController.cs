using Rythmbox.Core.Models.Styles;

namespace Rythmbox.Core.Styles;

/// <summary>
/// Tracks the arranger's base time signature plus momentary overrides. A foot switch (or UI button)
/// can drop a single short bar — e.g. a 2/4 turnaround — after which playback automatically returns
/// to the base 4/4 groove. This is a pure state machine; bar advancement is driven by the transport
/// clock calling <see cref="AdvanceBar"/> on each bar boundary.
/// </summary>
public sealed class TimeSignatureController
{
    private TimeSignature? _override;
    private int _overrideBarsRemaining;

    /// <summary>The groove's default signature (from the selected style).</summary>
    public TimeSignature Base { get; private set; } = TimeSignature.FourFour;

    /// <summary>The signature of the bar currently playing.</summary>
    public TimeSignature Current { get; private set; } = TimeSignature.FourFour;

    /// <summary>True while a momentary override is queued or still counting down.</summary>
    public bool HasPendingOverride => _override is not null;

    public TimeSignature? PendingOverride => _override;

    public event Action? Changed;

    public void SetBase(TimeSignature signature)
    {
        if (!signature.IsValid || Base == signature)
        {
            return;
        }

        Base = signature;

        // Only reflect the new base immediately when no override is masking it.
        if (_override is null)
        {
            Current = signature;
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Queue a momentary switch. It takes effect on the next bar boundary and lasts
    /// <paramref name="bars"/> bars before reverting to <see cref="Base"/>.
    /// </summary>
    public void TriggerMomentary(TimeSignature signature, int bars = 1)
    {
        if (bars < 1 || !signature.IsValid)
        {
            return;
        }

        _override = signature;
        _overrideBarsRemaining = bars;
        Changed?.Invoke();
    }

    /// <summary>Cancel a queued/active override; the next bar returns to <see cref="Base"/>.</summary>
    public void ClearOverride()
    {
        if (_override is null)
        {
            return;
        }

        _override = null;
        _overrideBarsRemaining = 0;
        Changed?.Invoke();
    }

    /// <summary>Advance to the next bar, applying or expiring any override. Returns the new bar's signature.</summary>
    public TimeSignature AdvanceBar()
    {
        if (_override is { } activeOverride && _overrideBarsRemaining > 0)
        {
            Current = activeOverride;
            _overrideBarsRemaining--;
            if (_overrideBarsRemaining == 0)
            {
                _override = null;
            }
        }
        else
        {
            Current = Base;
        }

        Changed?.Invoke();
        return Current;
    }

    public void Reset()
    {
        _override = null;
        _overrideBarsRemaining = 0;
        Current = Base;
        Changed?.Invoke();
    }
}
