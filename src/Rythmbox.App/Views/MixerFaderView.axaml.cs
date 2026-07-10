using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Rythmbox.Core.Audio;

namespace Rythmbox.App.Views;

/// <summary>GPU-friendly vertical fader — single <see cref="Control.Render"/> pass per frame.</summary>
public sealed class MixerFaderView : Control
{
    public const double ThumbRadius = 11;
    public const double TrackWidth = 3;
    public const double HitWidth = 28;

    // Fader adjustment steps, in decibels, for wheel and keyboard input.
    private const double WheelDbStep = 1.0;
    private const double KeyDbStep = 1.0;
    private const double PageDbStep = 6.0;
    private const double FineFactor = 0.25;

    private bool _isDragging;
    private double _dragStartNorm;
    private Point _dragStartPoint;
    private IBrush? _accentBrush;
    private IBrush? _railBrush;
    private IBrush? _grooveBrush;
    private IBrush? _tickBrush;
    private IBrush? _thumbFillBrush;
    private IBrush? _thumbBorderBrush;

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<MixerFaderView, double>(nameof(Value), MixerVolume.UnityNorm);

    public static readonly StyledProperty<bool> IsFaderEnabledProperty =
        AvaloniaProperty.Register<MixerFaderView, bool>(nameof(IsFaderEnabled), true);

    static MixerFaderView()
    {
        AffectsRender<MixerFaderView>(ValueProperty, IsFaderEnabledProperty, IsFocusedProperty);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public bool IsFaderEnabled
    {
        get => GetValue(IsFaderEnabledProperty);
        set => SetValue(IsFaderEnabledProperty, value);
    }

    public MixerFaderView()
    {
        MinWidth = HitWidth;
        MinHeight = 130;
        Width = HitWidth;
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.SizeNorthSouth);

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += OnPointerCaptureLost;
        PointerWheelChanged += OnPointerWheelChanged;

        Loaded += (_, _) => ResolveThemeBrushes();
    }

    public override void Render(DrawingContext context)
    {
        ResolveThemeBrushes();
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var centerX = width / 2;
        var trackTop = ThumbRadius + 4;
        var trackBottom = height - ThumbRadius - 4;
        var trackHeight = Math.Max(1, trackBottom - trackTop);
        var norm = Math.Clamp(Value, 0, 1);
        var thumbY = trackBottom - norm * trackHeight;
        var enabled = IsFaderEnabled;
        var opacity = enabled ? 1.0 : 0.35;

        using (context.PushOpacity(opacity))
        {
            var railRect = new Rect(centerX - TrackWidth / 2, trackTop, TrackWidth, trackHeight);
            context.DrawRectangle(_railBrush, new Pen(_grooveBrush, 1), railRect);

            foreach (var (db, _) in MixerVolume.ScaleMarks)
            {
                var fraction = MixerVolume.DbToTopFraction(db);
                var tickY = trackTop + fraction * trackHeight;
                var tickWidth = db is 0 or MixerVolume.MaxDb ? 14 : 9;
                var tickOpacity = db is 0 or MixerVolume.MaxDb ? 0.8 : 0.3;
                using (context.PushOpacity(tickOpacity))
                {
                    context.DrawRectangle(_tickBrush, null, new Rect(centerX - tickWidth / 2, tickY, tickWidth, 1));
                }
            }

            var fillHeight = Math.Max(0, trackBottom - thumbY);
            if (fillHeight > 0 && enabled)
            {
                using (context.PushOpacity(0.55))
                {
                    context.DrawRectangle(_accentBrush, null, new Rect(centerX - TrackWidth / 2, thumbY, TrackWidth, fillHeight));
                }
            }

            context.DrawEllipse(new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)), null,
                new Rect(centerX - ThumbRadius - 1, thumbY - ThumbRadius, ThumbRadius * 2 + 2, ThumbRadius * 2 + 2));
            context.DrawEllipse(_thumbFillBrush, new Pen(_thumbBorderBrush, 1),
                new Rect(centerX - ThumbRadius, thumbY - ThumbRadius, ThumbRadius * 2, ThumbRadius * 2));
            context.DrawEllipse(null, new Pen(_accentBrush, 2),
                new Rect(centerX - ThumbRadius + 3, thumbY - ThumbRadius + 3, ThumbRadius * 2 - 6, ThumbRadius * 2 - 6));
            context.DrawEllipse(Brushes.White, null, new Rect(centerX - 2.5, thumbY - 2.5, 5, 5));
        }

        if (IsFocused && enabled)
        {
            using (context.PushOpacity(0.7))
            {
                context.DrawRectangle(null, new Pen(_accentBrush, 1),
                    new Rect(0.5, 0.5, width - 1, height - 1), 3, 3);
            }
        }
    }

    private void ResolveThemeBrushes()
    {
        _accentBrush ??= Application.Current?.FindResource("RythmboxAccentBrush") as IBrush
                         ?? new SolidColorBrush(Color.Parse("#7B6BB8"));
        _railBrush ??= new SolidColorBrush(Color.Parse("#2A2B33"));
        _grooveBrush ??= new SolidColorBrush(Color.Parse("#14151A"));
        _tickBrush ??= new SolidColorBrush(Color.Parse("#5A5C68"));
        _thumbFillBrush ??= new SolidColorBrush(Color.Parse("#2E2F38"));
        _thumbBorderBrush ??= new SolidColorBrush(Color.Parse("#4A4C58"));
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsFaderEnabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        e.Handled = true;
        Focus();
        var point = e.GetPosition(this);

        if (e.ClickCount >= 2)
        {
            Value = MixerVolume.UnityNorm;
            return;
        }

        _isDragging = true;
        _dragStartNorm = Value;
        _dragStartPoint = point;
        e.Pointer.Capture(this);

        if (!IsNearThumb(point))
        {
            Value = PointerToNorm(point.Y);
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging || !IsFaderEnabled)
        {
            return;
        }

        e.Handled = true;
        var point = e.GetPosition(this);
        var fine = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var deltaY = point.Y - _dragStartPoint.Y;
        var trackHeight = Math.Max(1, Bounds.Height - (ThumbRadius + 4) * 2);
        var deltaNorm = -(deltaY / trackHeight);

        if (fine)
        {
            deltaNorm *= 0.25;
        }

        Value = Math.Clamp(_dragStartNorm + deltaNorm, 0, 1);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e) => _isDragging = false;

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => _isDragging = false;

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (!IsFaderEnabled || e.Delta.Y == 0)
        {
            return;
        }

        e.Handled = true;
        var step = WheelDbStep * (e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? FineFactor : 1.0);
        Value = MixerVolume.NudgeDb(Value, Math.Sign(e.Delta.Y) * step);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!IsFaderEnabled)
        {
            return;
        }

        var fine = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? FineFactor : 1.0;
        switch (e.Key)
        {
            case Key.Up:
                Value = MixerVolume.NudgeDb(Value, KeyDbStep * fine);
                break;
            case Key.Down:
                Value = MixerVolume.NudgeDb(Value, -KeyDbStep * fine);
                break;
            case Key.PageUp:
                Value = MixerVolume.NudgeDb(Value, PageDbStep);
                break;
            case Key.PageDown:
                Value = MixerVolume.NudgeDb(Value, -PageDbStep);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private bool IsNearThumb(Point point)
    {
        var trackTop = ThumbRadius + 4;
        var trackBottom = Bounds.Height - ThumbRadius - 4;
        var trackHeight = Math.Max(1, trackBottom - trackTop);
        var thumbY = trackBottom - Math.Clamp(Value, 0, 1) * trackHeight;
        return Math.Abs(point.Y - thumbY) <= ThumbRadius + 8;
    }

    private double PointerToNorm(double pointerY) =>
        MixerVolume.PointerYToNorm(pointerY, 0, Bounds.Height, ThumbRadius + 4);
}
