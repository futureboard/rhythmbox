using Avalonia;
using Avalonia.Controls;

namespace Rythmbox.UI.Controls;

/// <summary>Read-only value pill — Ableton-style parameter readout.</summary>
public class RbValueDisplay : Border
{
    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<RbValueDisplay, string>(nameof(Value), string.Empty);

    public static readonly StyledProperty<string> UnitProperty =
        AvaloniaProperty.Register<RbValueDisplay, string>(nameof(Unit), string.Empty);

    public RbValueDisplay()
    {
        Classes.Add("rbValue");
    }

    public string Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ValueProperty || change.Property == UnitProperty)
        {
            Child = new TextBlock
            {
                Classes = { "rbValue" },
                Text = string.IsNullOrWhiteSpace(Unit) ? Value : $"{Value} {Unit}",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            };
        }
    }
}
