using Avalonia.Controls;

namespace Rythmbox.App.Views;

public partial class HeaderView : UserControl
{
    public HeaderView()
    {
        InitializeComponent();
    }

    private void OnToggleFullscreenClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            window.WindowState = window.WindowState == WindowState.FullScreen
                ? WindowState.Normal
                : WindowState.FullScreen;
        }
    }
}
