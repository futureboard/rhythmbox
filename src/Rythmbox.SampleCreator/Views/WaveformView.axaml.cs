using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Rythmbox.SampleCreator.ViewModels;

namespace Rythmbox.SampleCreator.Views;

public partial class WaveformView : UserControl
{
    private static readonly IBrush BarBrush = new SolidColorBrush(Color.Parse("#FF8C1A"));
    private static readonly IBrush BarDimBrush = new SolidColorBrush(Color.Parse("#3A3B44"));
    private PadSampleViewModel? _pad;

    public WaveformView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => AttachPad();
        WaveCanvas.SizeChanged += (_, _) => Redraw();
    }

    private void AttachPad()
    {
        if (_pad is not null)
        {
            _pad.PropertyChanged -= OnPadPropertyChanged;
        }

        _pad = DataContext as PadSampleViewModel;
        if (_pad is not null)
        {
            _pad.PropertyChanged += OnPadPropertyChanged;
        }

        Redraw();
    }

    private void OnPadPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PadSampleViewModel.WaveformPeaks))
        {
            Redraw();
        }
    }

    private void Redraw()
    {
        WaveCanvas.Children.Clear();

        if (DataContext is not PadSampleViewModel pad || pad.WaveformPeaks.Count == 0)
        {
            return;
        }

        var width = WaveCanvas.Bounds.Width;
        var height = WaveCanvas.Bounds.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var peaks = pad.WaveformPeaks;
        var barWidth = Math.Max(2, width / peaks.Count - 1);

        for (var i = 0; i < peaks.Count; i++)
        {
            var peak = peaks[i];
            var barHeight = Math.Max(2, peak * (height - 4));
            var x = i * (barWidth + 1);
            var y = height - barHeight;

            WaveCanvas.Children.Add(new Border
            {
                Width = barWidth,
                Height = barHeight,
                Background = peak > 0.02 ? BarBrush : BarDimBrush,
                CornerRadius = new CornerRadius(1),
                [Canvas.LeftProperty] = x,
                [Canvas.TopProperty] = y,
            });
        }
    }
}
