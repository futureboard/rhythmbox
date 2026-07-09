using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Rythmbox.App.Views;

public partial class HeaderView : UserControl
{
    public HeaderView()
    {
        InitializeComponent();
    }

    private void OnToggleFullscreenMenuClick(object? sender, RoutedEventArgs e) => ToggleFullscreen();

    private void ToggleFullscreen()
    {
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            window.WindowState = window.WindowState == WindowState.FullScreen
                ? WindowState.Normal
                : WindowState.FullScreen;
        }
    }
}
