using Avalonia.Controls;
using Rythmbox.Core.Audio;

namespace Rythmbox.App.Views;

/// <summary>dB scale column aligned to <see cref="MixerFaderView"/> rail ticks.</summary>
public partial class MixerDbScaleView : UserControl
{
    public MixerDbScaleView()
    {
        InitializeComponent();
        Loaded += (_, _) => BuildScale();
        SizeChanged += (_, _) => BuildScale();
    }

    private void BuildScale()
    {
        if (Content is not Canvas canvas)
        {
            return;
        }

        canvas.Children.Clear();
        var height = Bounds.Height;
        if (height <= 0)
        {
            return;
        }

        const double pad = MixerFaderView.ThumbRadius + 4;

        foreach (var (db, label) in MixerVolume.ScaleMarks)
        {
            var fraction = MixerVolume.DbToTopFraction(db);
            var top = pad + fraction * Math.Max(1, height - pad * 2) - 6;
            var isMajor = db is 0 or MixerVolume.MaxDb;

            canvas.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 7.5,
                Foreground = isMajor
                    ? Avalonia.Media.Brushes.White
                    : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#7A7C88")),
                [Canvas.TopProperty] = top,
                [Canvas.RightProperty] = 0,
            });
        }
    }
}
