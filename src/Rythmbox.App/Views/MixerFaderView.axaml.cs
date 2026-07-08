using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Rythmbox.Core.Audio;

namespace Rythmbox.App.Views;

/// <summary>
/// Vertical DAW fader ported from Futureboard GPUI <c>fader.rs</c>.
/// Drag-only value changes; double-click resets to 0 dB unity.
/// </summary>
public partial class MixerFaderView : UserControl
{
    public const double ThumbRadius = 11;
    public const double TrackWidth = 3;
    public const double HitWidth = 28;

    private readonly Canvas _canvas;
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
        InitializeComponent();
        _canvas = new Canvas
        {
            Width = HitWidth,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
        };

        Content = _canvas;
        MinWidth = HitWidth;
        MinHeight = 130;
        Cursor = new Cursor(StandardCursorType.SizeNorthSouth);

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += OnPointerCaptureLost;

        ValueProperty.Changed.AddClassHandler<MixerFaderView>((_, _) => RequestRedraw());
        IsFaderEnabledProperty.Changed.AddClassHandler<MixerFaderView>((_, _) => RequestRedraw());

        Loaded += (_, _) =>
        {
            ResolveThemeBrushes();
            _canvas.SizeChanged += (_, _) => Redraw();
            RequestRedraw();
        };
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty)
        {
            RequestRedraw();
        }
    }

    private void ResolveThemeBrushes()
    {
        _accentBrush = Application.Current?.FindResource("RythmboxAccentBrush") as IBrush
                     ?? new SolidColorBrush(Color.Parse("#7B6BB8"));
        _railBrush = new SolidColorBrush(Color.Parse("#2A2B33"));
        _grooveBrush = new SolidColorBrush(Color.Parse("#14151A"));
        _tickBrush = new SolidColorBrush(Color.Parse("#5A5C68"));
        _thumbFillBrush = new SolidColorBrush(Color.Parse("#2E2F38"));
        _thumbBorderBrush = new SolidColorBrush(Color.Parse("#4A4C58"));
    }

    private void RequestRedraw() => Dispatcher.UIThread.Post(Redraw);

    private void Redraw()
    {
        if (_canvas.Bounds.Height <= 0)
        {
            return;
        }

        ResolveThemeBrushes();
        _canvas.Children.Clear();

        var width = _canvas.Bounds.Width;
        var height = _canvas.Bounds.Height;
        var centerX = width / 2;
        var trackTop = ThumbRadius + 4;
        var trackBottom = height - ThumbRadius - 4;
        var trackHeight = Math.Max(1, trackBottom - trackTop);
        var norm = Math.Clamp(Value, 0, 1);
        var thumbY = trackBottom - norm * trackHeight;
        var enabled = IsFaderEnabled;

        // Rail background
        _canvas.Children.Add(new Border
        {
            Width = TrackWidth,
            Height = trackHeight,
            CornerRadius = new CornerRadius(TrackWidth),
            Background = _railBrush,
            BorderBrush = _grooveBrush,
            BorderThickness = new Thickness(1),
            [Canvas.LeftProperty] = centerX - TrackWidth / 2,
            [Canvas.TopProperty] = trackTop,
            Opacity = enabled ? 1 : 0.4,
        });

        // dB tick marks on rail
        foreach (var (db, _) in MixerVolume.ScaleMarks)
        {
            var fraction = MixerVolume.DbToTopFraction(db);
            var tickY = trackTop + fraction * trackHeight;
            var tickWidth = db is 0 or MixerVolume.MaxDb ? 14 : 9;
            _canvas.Children.Add(new Border
            {
                Width = tickWidth,
                Height = 1,
                Background = _tickBrush,
                Opacity = db is 0 or MixerVolume.MaxDb ? 0.8 : 0.3,
                [Canvas.LeftProperty] = centerX - tickWidth / 2,
                [Canvas.TopProperty] = tickY,
            });
        }

        // Active fill below thumb (toward bottom = quieter side in inverted mapping)
        var fillTop = thumbY;
        var fillHeight = Math.Max(0, trackBottom - thumbY);
        if (fillHeight > 0 && enabled)
        {
            _canvas.Children.Add(new Border
            {
                Width = TrackWidth,
                Height = fillHeight,
                Background = _accentBrush,
                Opacity = 0.55,
                [Canvas.LeftProperty] = centerX - TrackWidth / 2,
                [Canvas.TopProperty] = fillTop,
            });
        }

        // Thumb shadow
        _canvas.Children.Add(new Ellipse
        {
            Width = ThumbRadius * 2 + 2,
            Height = ThumbRadius * 2 + 2,
            Fill = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)),
            [Canvas.LeftProperty] = centerX - ThumbRadius - 1,
            [Canvas.TopProperty] = thumbY - ThumbRadius,
            Opacity = enabled ? 1 : 0.35,
        });

        // Thumb body
        _canvas.Children.Add(new Ellipse
        {
            Width = ThumbRadius * 2,
            Height = ThumbRadius * 2,
            Fill = _thumbFillBrush,
            Stroke = _thumbBorderBrush,
            StrokeThickness = 1,
            [Canvas.LeftProperty] = centerX - ThumbRadius,
            [Canvas.TopProperty] = thumbY - ThumbRadius,
            Opacity = enabled ? 1 : 0.35,
        });

        // Accent ring on thumb
        _canvas.Children.Add(new Ellipse
        {
            Width = ThumbRadius * 2 - 6,
            Height = ThumbRadius * 2 - 6,
            Stroke = _accentBrush,
            StrokeThickness = 2,
            [Canvas.LeftProperty] = centerX - ThumbRadius + 3,
            [Canvas.TopProperty] = thumbY - ThumbRadius + 3,
            Opacity = enabled ? 0.95 : 0.35,
        });

        // Center grip dot
        _canvas.Children.Add(new Ellipse
        {
            Width = 5,
            Height = 5,
            Fill = Brushes.White,
            Opacity = enabled ? 0.85 : 0.25,
            [Canvas.LeftProperty] = centerX - 2.5,
            [Canvas.TopProperty] = thumbY - 2.5,
        });
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsFaderEnabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        e.Handled = true;
        var point = e.GetPosition(_canvas);

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
        var point = e.GetPosition(_canvas);
        var fine = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var deltaY = point.Y - _dragStartPoint.Y;
        var trackHeight = Math.Max(1, _canvas.Bounds.Height - (ThumbRadius + 4) * 2);
        var deltaNorm = -(deltaY / trackHeight);

        if (fine)
        {
            deltaNorm *= 0.25;
        }

        Value = Math.Clamp(_dragStartNorm + deltaNorm, 0, 1);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        EndDrag();
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        EndDrag();
    }

    private void EndDrag()
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
    }

    private bool IsNearThumb(Point point)
    {
        var height = _canvas.Bounds.Height;
        var trackTop = ThumbRadius + 4;
        var trackBottom = height - ThumbRadius - 4;
        var trackHeight = Math.Max(1, trackBottom - trackTop);
        var thumbY = trackBottom - Math.Clamp(Value, 0, 1) * trackHeight;
        return Math.Abs(point.Y - thumbY) <= ThumbRadius + 8;
    }

    private double PointerToNorm(double pointerY) =>
        MixerVolume.PointerYToNorm(pointerY, 0, _canvas.Bounds.Height, ThumbRadius + 4);
}
