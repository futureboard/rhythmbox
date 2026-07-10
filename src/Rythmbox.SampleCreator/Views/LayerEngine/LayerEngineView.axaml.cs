using Avalonia.Controls;

namespace Rythmbox.SampleCreator.Views.LayerEngine;

/// <summary>Orchestrates the Layer Engine for the selected pad: velocity map, layer stack,
/// and the no-layers empty state. DataContext is the selected <c>PadSampleViewModel</c>.</summary>
public partial class LayerEngineView : UserControl
{
    public LayerEngineView()
    {
        InitializeComponent();
    }
}
