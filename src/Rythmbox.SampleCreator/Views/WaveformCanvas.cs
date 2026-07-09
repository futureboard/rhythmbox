using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Rythmbox.Core.Samples;

namespace Rythmbox.SampleCreator.Views;

/// <summary>GPU-friendly waveform — symmetric min/max envelope (BBC audiowaveform / NAudio MaxPeakProvider).</summary>
public sealed class WaveformCanvas : Control
{
    private static readonly IBrush PeakBrush = new SolidColorBrush(Color.Parse("#FF8C1A"));
    private static readonly IBrush CenterLineBrush = new SolidColorBrush(Color.Parse("#3A3B44"));
    private static readonly IBrush TrimOverlayBrush = new SolidColorBrush(Color.FromArgb(96, 0, 0, 0));
    private static readonly IPen TrimMarkerPen = new Pen(new SolidColorBrush(Color.Parse("#FF8C1A")), 2);

    public static readonly StyledProperty<IReadOnlyList<WaveformPeak>?> PeaksProperty =
        AvaloniaProperty.Register<WaveformCanvas, IReadOnlyList<WaveformPeak>?>(nameof(Peaks));

    public static readonly StyledProperty<double> TrimStartProperty =
        AvaloniaProperty.Register<WaveformCanvas, double>(nameof(TrimStart), 0d);

    public static readonly StyledProperty<double> TrimEndProperty =
        AvaloniaProperty.Register<WaveformCanvas, double>(nameof(TrimEnd), 1d);

    static WaveformCanvas()
    {
        AffectsRender<WaveformCanvas>(PeaksProperty, TrimStartProperty, TrimEndProperty);
    }

    public IReadOnlyList<WaveformPeak>? Peaks
    {
        get => GetValue(PeaksProperty);
        set => SetValue(PeaksProperty, value);
    }

    public double TrimStart
    {
        get => GetValue(TrimStartProperty);
        set => SetValue(TrimStartProperty, value);
    }

    public double TrimEnd
    {
        get => GetValue(TrimEndProperty);
        set => SetValue(TrimEndProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var peaks = Peaks;
        if (peaks is null || peaks.Count == 0)
        {
            return;
        }

        var width = bounds.Width;
        var height = bounds.Height;
        var midY = height / 2;
        var barWidth = Math.Max(2, width / peaks.Count - 1);
        var step = barWidth + 1;

        var maxAbs = 0f;
        foreach (var peak in peaks)
        {
            maxAbs = Math.Max(maxAbs, Math.Max(Math.Abs(peak.Min), Math.Abs(peak.Max)));
        }

        var scale = maxAbs > 1e-6f ? (height - 8) / 2 / maxAbs : 1f;

        context.DrawLine(new Pen(CenterLineBrush, 1), new Point(0, midY), new Point(width, midY));

        for (var i = 0; i < peaks.Count; i++)
        {
            var peak = peaks[i];
            var x = i * step;
            var yTop = midY - peak.Max * scale;
            var yBottom = midY - peak.Min * scale;
            var barHeight = Math.Max(1, yBottom - yTop);
            context.FillRectangle(PeakBrush, new Rect(x, yTop, barWidth, barHeight), 1);
        }

        var trimStart = Math.Clamp(TrimStart, 0, 1);
        var trimEnd = Math.Clamp(TrimEnd, trimStart, 1);
        if (trimStart <= 0 && trimEnd >= 1)
        {
            return;
        }

        var startX = width * trimStart;
        var endX = width * trimEnd;

        if (startX > 0)
        {
            context.FillRectangle(TrimOverlayBrush, new Rect(0, 0, startX, height));
        }

        if (endX < width)
        {
            context.FillRectangle(TrimOverlayBrush, new Rect(endX, 0, width - endX, height));
        }

        context.DrawLine(TrimMarkerPen, new Point(startX, 0), new Point(startX, height));
        context.DrawLine(TrimMarkerPen, new Point(endX, 0), new Point(endX, height));
    }
}
