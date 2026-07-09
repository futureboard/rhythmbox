using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;

namespace Rythmbox.SampleCreator.Views;

/// <summary>Rotary knob — vertical drag (like mixer fader), click-to-set, scroll wheel.</summary>
public sealed class RotaryKnobView : Control
{
    public const double DefaultSize = 72;
    private const double StartAngleDeg = 135;
    private const double SweepDeg = 270;
    private const double DragPixelsForFullRange = 72;
    private const int TickCount = 11;

    private bool _isDragging;
    private double _dragStartValue;
    private Point _dragStartPoint;
    private IBrush? _accentBrush;
    private IBrush? _accentGlowBrush;
    private IBrush? _borderBrush;
    private IBrush? _trackBrush;
    private IBrush? _tickBrush;

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<RotaryKnobView, double>(nameof(Minimum), 0d);

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<RotaryKnobView, double>(nameof(Maximum), 1d);

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<RotaryKnobView, double>(
            nameof(Value),
            0d,
            defaultBindingMode: BindingMode.TwoWay,
            coerce: CoerceValue);

    public static readonly StyledProperty<double> DefaultValueProperty =
        AvaloniaProperty.Register<RotaryKnobView, double>(nameof(DefaultValue), 0d);

    public static readonly StyledProperty<bool> IsEnabledKnobProperty =
        AvaloniaProperty.Register<RotaryKnobView, bool>(nameof(IsEnabledKnob), true);

    static RotaryKnobView()
    {
        AffectsRender<RotaryKnobView>(MinimumProperty, MaximumProperty, ValueProperty, IsEnabledKnobProperty);
    }

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double DefaultValue
    {
        get => GetValue(DefaultValueProperty);
        set => SetValue(DefaultValueProperty, value);
    }

    public bool IsEnabledKnob
    {
        get => GetValue(IsEnabledKnobProperty);
        set => SetValue(IsEnabledKnobProperty, value);
    }

    public RotaryKnobView()
    {
        Width = DefaultSize;
        Height = DefaultSize;
        MinWidth = DefaultSize;
        MinHeight = DefaultSize;
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.SizeNorthSouth);

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += OnPointerCaptureLost;
        PointerWheelChanged += OnPointerWheelChanged;

        Loaded += (_, _) => ResolveThemeBrushes();
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(DefaultSize, DefaultSize);

    public override void Render(DrawingContext context)
    {
        ResolveThemeBrushes();
        var accent = _accentBrush ?? Brushes.Orange;
        var accentGlow = _accentGlowBrush ?? accent;
        var border = _borderBrush ?? Brushes.Gray;
        var track = _trackBrush ?? Brushes.Black;
        var tick = _tickBrush ?? Brushes.Gray;

        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var size = Math.Min(bounds.Width, bounds.Height);
        var center = new Point(bounds.Width / 2, bounds.Height / 2);
        var radius = size / 2 - 6;
        var norm = ValueToNorm(Value);
        var opacity = IsEnabledKnob ? 1.0 : 0.35;

        using (context.PushOpacity(opacity))
        {
            context.DrawEllipse(new SolidColorBrush(Color.FromArgb(48, 0, 0, 0)), null,
                new Rect(center.X - radius - 2, center.Y - radius, radius * 2 + 4, radius * 2 + 4));

            DrawTickMarks(context, center, radius + 2, track, tick);

            var faceRect = new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2);
            context.DrawEllipse(
                new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Color.Parse("#3A3C48"), 0),
                        new GradientStop(Color.Parse("#22232B"), 0.55),
                        new GradientStop(Color.Parse("#18191F"), 1),
                    },
                },
                new Pen(border, 1.5),
                faceRect);

            DrawArcTrack(context, center, radius - 4, 0, 1, track, 4);
            DrawArcTrack(context, center, radius - 4, 0, norm, accent, 4);

            var angleRad = (StartAngleDeg + norm * SweepDeg) * Math.PI / 180;
            var pointerLen = radius - 12;
            var tip = new Point(
                center.X + Math.Cos(angleRad) * pointerLen,
                center.Y + Math.Sin(angleRad) * pointerLen);

            context.DrawLine(new Pen(accentGlow, 5, lineCap: PenLineCap.Round), center, tip);
            context.DrawLine(new Pen(Brushes.White, 2, lineCap: PenLineCap.Round), center, tip);
            context.DrawEllipse(accent, new Pen(border, 1), new Rect(center.X - 4, center.Y - 4, 8, 8));
        }
    }

    private static void DrawTickMarks(DrawingContext context, Point center, double radius, IBrush track, IBrush tick)
    {
        for (var i = 0; i < TickCount; i++)
        {
            var t = i / (double)(TickCount - 1);
            var angle = StartAngleDeg + t * SweepDeg;
            var inner = radius - (i % 5 == 0 ? 6 : 3);
            var outer = radius;
            var brush = i % 5 == 0 ? tick : track;
            var p0 = PointOnCircle(center, inner, angle);
            var p1 = PointOnCircle(center, outer, angle);
            context.DrawLine(new Pen(brush, i % 5 == 0 ? 1.5 : 1, lineCap: PenLineCap.Round), p0, p1);
        }
    }

    private static void DrawArcTrack(DrawingContext context, Point center, double radius, double startNorm, double endNorm, IBrush brush, double thickness)
    {
        if (endNorm <= startNorm)
        {
            return;
        }

        var startAngle = StartAngleDeg + startNorm * SweepDeg;
        var endAngle = StartAngleDeg + endNorm * SweepDeg;
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(PointOnCircle(center, radius, startAngle), false);
            ctx.ArcTo(
                PointOnCircle(center, radius, endAngle),
                new Size(radius, radius),
                0,
                endAngle - startAngle > 180,
                SweepDirection.Clockwise);
        }

        context.DrawGeometry(null, new Pen(brush, thickness, lineCap: PenLineCap.Round), geometry);
    }

    private static Point PointOnCircle(Point center, double radius, double angleDeg)
    {
        var rad = angleDeg * Math.PI / 180;
        return new Point(center.X + Math.Cos(rad) * radius, center.Y + Math.Sin(rad) * radius);
    }

    private static double CoerceValue(AvaloniaObject sender, double value)
    {
        var knob = (RotaryKnobView)sender;
        return Math.Clamp(value, knob.Minimum, knob.Maximum);
    }

    private void ResolveThemeBrushes()
    {
        var accentColor = Color.Parse("#FF8C1A");
        if (Application.Current?.FindResource("RythmboxAccentBrush") is SolidColorBrush accentResource)
        {
            accentColor = accentResource.Color;
        }

        _accentBrush ??= new SolidColorBrush(accentColor);
        _accentGlowBrush ??= new SolidColorBrush(Color.FromArgb(96, accentColor.R, accentColor.G, accentColor.B));
        _borderBrush ??= new SolidColorBrush(Color.Parse("#5A5C68"));
        _trackBrush ??= new SolidColorBrush(Color.Parse("#14151A"));
        _tickBrush ??= new SolidColorBrush(Color.Parse("#6A6C78"));
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEnabledKnob || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        e.Handled = true;
        Focus();

        if (e.ClickCount >= 2)
        {
            SetCurrentValue(ValueProperty, DefaultValue);
            return;
        }

        var point = e.GetPosition(this);
        _isDragging = true;
        _dragStartValue = Value;
        _dragStartPoint = point;
        e.Pointer.Capture(this);

        if (!IsNearPointer(point))
        {
            SetCurrentValue(ValueProperty, PointerYToValue(point.Y));
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging || !IsEnabledKnob)
        {
            return;
        }

        e.Handled = true;
        var point = e.GetPosition(this);
        var deltaY = _dragStartPoint.Y - point.Y;
        var fine = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var range = Math.Max(1e-6, Maximum - Minimum);
        var dragRange = fine ? DragPixelsForFullRange * 3 : DragPixelsForFullRange;
        var deltaValue = deltaY / dragRange * range;
        SetCurrentValue(ValueProperty, Math.Clamp(_dragStartValue + deltaValue, Minimum, Maximum));
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;
        e.Pointer.Capture(null);
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => _isDragging = false;

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (!IsEnabledKnob)
        {
            return;
        }

        e.Handled = true;
        var range = Math.Max(1e-6, Maximum - Minimum);
        var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? range * 0.02 : range * 0.08;
        SetCurrentValue(ValueProperty, Math.Clamp(Value + (e.Delta.Y > 0 ? step : -step), Minimum, Maximum));
    }

    private bool IsNearPointer(Point point)
    {
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var norm = ValueToNorm(Value);
        var angleRad = (StartAngleDeg + norm * SweepDeg) * Math.PI / 180;
        var radius = Math.Min(Bounds.Width, Bounds.Height) / 2 - 18;
        var tip = new Point(
            center.X + Math.Cos(angleRad) * radius,
            center.Y + Math.Sin(angleRad) * radius);
        return Math.Abs(point.X - tip.X) <= 14 && Math.Abs(point.Y - tip.Y) <= 14;
    }

    private double PointerYToValue(double pointerY)
    {
        var pad = 10.0;
        var height = Math.Max(1, Bounds.Height - pad * 2);
        var norm = Math.Clamp((pointerY - pad) / height, 0, 1);
        return NormToValue(1 - norm);
    }

    private double ValueToNorm(double value)
    {
        var range = Math.Max(1e-6, Maximum - Minimum);
        return Math.Clamp((value - Minimum) / range, 0, 1);
    }

    private double NormToValue(double norm)
    {
        var range = Math.Max(1e-6, Maximum - Minimum);
        return Minimum + norm * range;
    }
}
