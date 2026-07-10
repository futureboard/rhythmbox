using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Rythmbox.App.Views;

/// <summary>
/// Draws a software mouse pointer for the embedded DRM/KMS backend, which does not
/// render one itself. The pointer follows physical mouse movement and hides for touch,
/// so a touchscreen stays uncluttered while a plugged-in mouse gets a visible cursor.
/// </summary>
public partial class SoftwareCursorLayer : UserControl
{
    private TopLevel? _topLevel;

    public SoftwareCursorLayer()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // The desktop OS already draws a cursor; only take over on the framebuffer.
        if (!EmbeddedRuntime.IsDrm)
        {
            return;
        }

        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel is null)
        {
            return;
        }

        // Tunnel + bubble with handledEventsToo so the pointer tracks regardless of
        // which control handles the event; the layer itself is hit-test invisible.
        _topLevel.AddHandler(
            InputElement.PointerMovedEvent,
            OnPointerMoved,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            handledEventsToo: true);
        _topLevel.AddHandler(
            InputElement.PointerPressedEvent,
            OnPointerActivity,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            handledEventsToo: true);
        _topLevel.AddHandler(
            InputElement.PointerExitedEvent,
            OnPointerExited,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            handledEventsToo: true);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_topLevel is not null)
        {
            _topLevel.RemoveHandler(InputElement.PointerMovedEvent, OnPointerMoved);
            _topLevel.RemoveHandler(InputElement.PointerPressedEvent, OnPointerActivity);
            _topLevel.RemoveHandler(InputElement.PointerExitedEvent, OnPointerExited);
            _topLevel = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e) => Update(e);

    private void OnPointerActivity(object? sender, PointerEventArgs e) => Update(e);

    private void OnPointerExited(object? sender, PointerEventArgs e) => Pointer.IsVisible = false;

    private void Update(PointerEventArgs e)
    {
        // A real mouse gets a pointer; touch and pen do not, so the touchscreen UI
        // is not cluttered by a stray cursor.
        if (e.Pointer.Type != PointerType.Mouse)
        {
            Pointer.IsVisible = false;
            return;
        }

        var position = e.GetPosition(Root);
        Canvas.SetLeft(Pointer, position.X);
        Canvas.SetTop(Pointer, position.Y);
        Pointer.IsVisible = true;
    }
}
