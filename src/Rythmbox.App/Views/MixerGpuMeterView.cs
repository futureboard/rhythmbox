using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Rythmbox.App.Views;

/// <summary>GPU-friendly level meter — draws directly via <see cref="Control.Render"/> instead of rebuilding visual tree.</summary>
public sealed class MixerGpuMeterView : Control
{
    public static readonly StyledProperty<double> RmsLeftProperty =
        AvaloniaProperty.Register<MixerGpuMeterView, double>(nameof(RmsLeft));

    public static readonly StyledProperty<double> RmsRightProperty =
        AvaloniaProperty.Register<MixerGpuMeterView, double>(nameof(RmsRight));

    public static readonly StyledProperty<double> PeakLeftProperty =
        AvaloniaProperty.Register<MixerGpuMeterView, double>(nameof(PeakLeft));

    public static readonly StyledProperty<double> PeakRightProperty =
        AvaloniaProperty.Register<MixerGpuMeterView, double>(nameof(PeakRight));

    public static readonly StyledProperty<bool> IsClippingProperty =
        AvaloniaProperty.Register<MixerGpuMeterView, bool>(nameof(IsClipping));

    public static readonly StyledProperty<bool> HasSignalDataProperty =
        AvaloniaProperty.Register<MixerGpuMeterView, bool>(nameof(HasSignalData));

    static MixerGpuMeterView()
    {
        AffectsRender<MixerGpuMeterView>(
            RmsLeftProperty,
            RmsRightProperty,
            PeakLeftProperty,
            PeakRightProperty,
            IsClippingProperty,
            HasSignalDataProperty);
    }

    public double RmsLeft { get => GetValue(RmsLeftProperty); set => SetValue(RmsLeftProperty, value); }
    public double RmsRight { get => GetValue(RmsRightProperty); set => SetValue(RmsRightProperty, value); }
    public double PeakLeft { get => GetValue(PeakLeftProperty); set => SetValue(PeakLeftProperty, value); }
    public double PeakRight { get => GetValue(PeakRightProperty); set => SetValue(PeakRightProperty, value); }
    public bool IsClipping { get => GetValue(IsClippingProperty); set => SetValue(IsClippingProperty, value); }
    public bool HasSignalData { get => GetValue(HasSignalDataProperty); set => SetValue(HasSignalDataProperty, value); }

    public MixerGpuMeterView()
    {
        Width = 24;
        Height = 120;
        MinWidth = 24;
        MinHeight = 108;
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var trackBrush = new SolidColorBrush(Color.Parse("#14151A"));
        var borderBrush = new SolidColorBrush(Color.Parse("#2E3038"));
        var fillBrush = new SolidColorBrush(IsClipping ? Color.Parse("#D64545") : Color.Parse("#3D8B5A"));
        var opacity = HasSignalData ? 1.0 : 0.35;

        var gap = 4.0;
        var laneWidth = Math.Max(6, (bounds.Width - gap) / 2);
        DrawLane(context, new Rect(0, 0, laneWidth, bounds.Height), trackBrush, borderBrush, fillBrush, RmsLeft, PeakLeft, opacity);
        DrawLane(context, new Rect(laneWidth + gap, 0, laneWidth, bounds.Height), trackBrush, borderBrush, fillBrush, RmsRight, PeakRight, opacity);
    }

    private static void DrawLane(
        DrawingContext context,
        Rect lane,
        IBrush trackBrush,
        IBrush borderBrush,
        IBrush fillBrush,
        double rms,
        double peak,
        double opacity)
    {
        var track = new Rect(lane.X, lane.Y, lane.Width, lane.Height);
        context.DrawRectangle(trackBrush, new Pen(borderBrush, 1), track);

        var level = Math.Clamp(Math.Max(rms, peak * 0.65), 0, 1);
        var fillHeight = Math.Max(2, level * lane.Height);
        var fillRect = new Rect(lane.X + 1, lane.Bottom - fillHeight, Math.Max(1, lane.Width - 2), fillHeight);
        using (context.PushOpacity(opacity))
        {
            context.DrawRectangle(fillBrush, null, fillRect);
        }
    }
}
