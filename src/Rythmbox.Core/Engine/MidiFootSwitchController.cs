namespace Rythmbox.Core.Engine;

/// <summary>
/// Turns a stream of MIDI Control Change values from a sustain-style foot switch into discrete
/// "pressed" edges. A press fires when the value rises to/above <see cref="Threshold"/> (64 by
/// default, matching a standard on/off pedal); the pedal must be released below the threshold
/// before it can fire again, so a held pedal does not chatter.
/// </summary>
public sealed class MidiFootSwitchController
{
    private bool _pressed;

    public MidiFootSwitchController(int controllerNumber = 64, int threshold = 64)
    {
        ControllerNumber = controllerNumber;
        Threshold = threshold;
    }

    /// <summary>The CC number the pedal transmits on (default 64 = Damper/Sustain).</summary>
    public int ControllerNumber { get; set; }

    /// <summary>Values at/above this count as "pressed" (default 64).</summary>
    public int Threshold { get; set; }

    public bool IsEnabled { get; set; } = true;

    /// <summary>Raised on the rising edge of a matching CC (pedal pressed down).</summary>
    public event Action? Pressed;

    /// <summary>Feed a Control Change. Returns true when this call produced a press edge.</summary>
    public bool ProcessControlChange(int controllerNumber, int value)
    {
        if (!IsEnabled || controllerNumber != ControllerNumber)
        {
            return false;
        }

        var down = value >= Threshold;

        if (down && !_pressed)
        {
            _pressed = true;
            Pressed?.Invoke();
            return true;
        }

        if (!down)
        {
            _pressed = false;
        }

        return false;
    }

    public void Reset() => _pressed = false;
}
