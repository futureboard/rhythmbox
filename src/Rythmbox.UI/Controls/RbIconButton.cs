using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

namespace Rythmbox.UI.Controls;

/// <summary>Ableton-style icon button — flat control with optional label.</summary>
public class RbIconButton : Button
{
    public static readonly StyledProperty<Geometry?> IconDataProperty =
        AvaloniaProperty.Register<RbIconButton, Geometry?>(nameof(IconData));

    public static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<RbIconButton, double>(nameof(IconSize), 14d);

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<RbIconButton, string?>(nameof(Label));

    public RbIconButton()
    {
        Classes.Add("icon");
    }

    public Geometry? IconData
    {
        get => GetValue(IconDataProperty);
        set => SetValue(IconDataProperty, value);
    }

    public double IconSize
    {
        get => GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IconDataProperty
            || change.Property == IconSizeProperty
            || change.Property == LabelProperty)
        {
            RebuildContent();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RebuildContent();
    }

    private void RebuildContent()
    {
        if (IconData is null)
        {
            return;
        }

        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        stack.Children.Add(new Avalonia.Controls.Shapes.Path
        {
            Data = IconData,
            Width = IconSize,
            Height = IconSize,
            Stretch = Stretch.Uniform,
            Classes = { "icon", "small" },
        });

        if (!string.IsNullOrWhiteSpace(Label))
        {
            stack.Children.Add(new TextBlock
            {
                Text = Label,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        Content = stack;
    }
}
