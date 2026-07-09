using Avalonia.Controls;
using Avalonia.Media;

namespace Rythmbox.App.Views;

public partial class MixerSurfaceView : UserControl
{
    public MixerSurfaceView()
    {
        InitializeComponent();
        RenderOptions.SetEdgeMode(this, EdgeMode.Antialias);
    }
}
