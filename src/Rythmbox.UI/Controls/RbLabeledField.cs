using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;

namespace Rythmbox.UI.Controls;

/// <summary>Labeled text field — Ableton-style caption above inset input.</summary>
public class RbLabeledField : UserControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<RbLabeledField, string>(nameof(Label), string.Empty);

    public static readonly StyledProperty<string> HintProperty =
        AvaloniaProperty.Register<RbLabeledField, string>(nameof(Hint), string.Empty);

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<RbLabeledField, string>(
            nameof(Text),
            string.Empty,
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string> PlaceholderTextProperty =
        AvaloniaProperty.Register<RbLabeledField, string>(nameof(PlaceholderText), string.Empty);

    public static readonly StyledProperty<bool> UseMonoFontProperty =
        AvaloniaProperty.Register<RbLabeledField, bool>(nameof(UseMonoFont), false);

    private readonly TextBlock _labelBlock;
    private readonly TextBlock _hintBlock;
    private readonly TextBox _textBox;
    private bool _syncing;

    public RbLabeledField()
    {
        _labelBlock = new TextBlock { Classes = { "rbLabel" } };
        _hintBlock = new TextBlock { Classes = { "dim" }, FontSize = 9 };
        _textBox = new TextBox();
        _textBox.TextChanged += (_, _) =>
        {
            if (_syncing)
            {
                return;
            }

            Text = _textBox.Text ?? string.Empty;
        };

        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(_labelBlock);
        stack.Children.Add(_textBox);
        stack.Children.Add(_hintBlock);
        Content = stack;

        UpdateLabel();
        UpdateHint();
        UpdateMono();
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Hint
    {
        get => GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string PlaceholderText
    {
        get => GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public bool UseMonoFont
    {
        get => GetValue(UseMonoFontProperty);
        set => SetValue(UseMonoFontProperty, value);
    }

    public TextBox Editor => _textBox;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == LabelProperty)
        {
            UpdateLabel();
        }
        else if (change.Property == HintProperty)
        {
            UpdateHint();
        }
        else if (change.Property == PlaceholderTextProperty)
        {
            _textBox.PlaceholderText = PlaceholderText;
        }
        else if (change.Property == UseMonoFontProperty)
        {
            UpdateMono();
        }
        else if (change.Property == TextProperty)
        {
            _syncing = true;
            _textBox.Text = Text;
            _syncing = false;
        }
    }

    private void UpdateLabel()
    {
        _labelBlock.Text = Label;
        _labelBlock.IsVisible = !string.IsNullOrWhiteSpace(Label);
    }

    private void UpdateHint()
    {
        _hintBlock.Text = Hint;
        _hintBlock.IsVisible = !string.IsNullOrWhiteSpace(Hint);
    }

    private void UpdateMono() => _textBox.Classes.Set("mono", UseMonoFont);
}
