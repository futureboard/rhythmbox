using Avalonia.Controls;
using Avalonia.Input;
using Rythmbox.App.ViewModels;

namespace Rythmbox.App.Views;

public partial class PercussionPadGridView : UserControl
{
    public PercussionPadGridView()
    {
        InitializeComponent();
    }

    private static PadViewModel? GetPad(object? sender) => (sender as Control)?.DataContext as PadViewModel;

    private static void OnPadPointerEntered(object? sender, PointerEventArgs e) => GetPad(sender)?.SetHovered(true);

    private static void OnPadPointerExited(object? sender, PointerEventArgs e) => GetPad(sender)?.SetHovered(false);

    private static void OnPadPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control)
        {
            e.Pointer.Capture(control);
        }

        GetPad(sender)?.PointerDown();
    }

    private static void OnPadPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Control control && e.Pointer.Captured == control)
        {
            e.Pointer.Capture(null);
        }

        GetPad(sender)?.Release();
    }

    private static void OnPadPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => GetPad(sender)?.Release();
}
