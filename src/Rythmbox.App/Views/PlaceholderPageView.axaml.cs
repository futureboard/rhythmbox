using Avalonia;
using Avalonia.Controls;

namespace Rythmbox.App.Views;

public partial class PlaceholderPageView : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<PlaceholderPageView, string>(nameof(Title), "PAGE");

    public PlaceholderPageView()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
}
