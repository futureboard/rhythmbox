using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Rythmbox.UI.Controls;

/// <summary>
/// Attached behaviors that extend the stock Avalonia <see cref="Slider"/> with
/// mouse-wheel adjustment, matching the professional fader/knob interaction the
/// custom mixer fader already provides. Enable globally from the shared theme so
/// every slider behaves consistently.
/// </summary>
public static class SliderBehaviors
{
    /// <summary>Fraction of the slider's range moved per wheel notch.</summary>
    private const double WheelStepFraction = 0.02;

    /// <summary>Multiplier applied to the step while Shift is held (fine adjust).</summary>
    private const double FineFactor = 0.25;

    public static readonly AttachedProperty<bool> WheelEnabledProperty =
        AvaloniaProperty.RegisterAttached<Slider, bool>("WheelEnabled", typeof(SliderBehaviors));

    static SliderBehaviors()
    {
        WheelEnabledProperty.Changed.AddClassHandler<Slider>(OnWheelEnabledChanged);
    }

    public static void SetWheelEnabled(Slider slider, bool value) => slider.SetValue(WheelEnabledProperty, value);

    public static bool GetWheelEnabled(Slider slider) => slider.GetValue(WheelEnabledProperty);

    private static void OnWheelEnabledChanged(Slider slider, AvaloniaPropertyChangedEventArgs e)
    {
        slider.PointerWheelChanged -= OnPointerWheelChanged;
        if (e.GetNewValue<bool>())
        {
            slider.PointerWheelChanged += OnPointerWheelChanged;
        }
    }

    private static void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (sender is not Slider { IsEffectivelyEnabled: true } slider || e.Delta.Y == 0)
        {
            return;
        }

        var range = slider.Maximum - slider.Minimum;
        if (range <= 0)
        {
            return;
        }

        var step = range * WheelStepFraction;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            step *= FineFactor;
        }

        var next = slider.Value + Math.Sign(e.Delta.Y) * step;
        slider.Value = Math.Clamp(next, slider.Minimum, slider.Maximum);
        e.Handled = true;
    }
}
