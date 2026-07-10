using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace Rythmbox.UI.Controls;

/// <summary>Horizontal segmented toggle group (Ableton-style tab strip).</summary>
public class RbSegmentedBar : ItemsControl
{
    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<RbSegmentedBar, int>(nameof(SelectedIndex), -1, coerce: CoerceSelectedIndex);

    public RbSegmentedBar()
    {
        ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 0,
        });
    }

    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
    {
        var toggle = new ToggleButton
        {
            Classes = { "segment" },
            Content = item,
            Tag = index,
        };

        toggle.Click += OnSegmentClick;
        return toggle;
    }

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
    {
        recycleKey = null;
        return item is not ToggleButton;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SelectedIndexProperty)
        {
            SyncSegmentStates();
        }
    }

    private void OnSegmentClick(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { Tag: int index })
        {
            SelectedIndex = index;
        }
    }

    private void SyncSegmentStates()
    {
        for (var i = 0; i < ItemCount; i++)
        {
            if (ContainerFromIndex(i) is ToggleButton toggle)
            {
                toggle.IsChecked = i == SelectedIndex;
            }
        }
    }

    private static int CoerceSelectedIndex(AvaloniaObject sender, int value)
    {
        if (sender is not RbSegmentedBar bar || bar.ItemCount == 0)
        {
            return value < 0 ? -1 : value;
        }

        return Math.Clamp(value, 0, bar.ItemCount - 1);
    }
}
