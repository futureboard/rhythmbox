using Avalonia.Controls;

namespace Rythmbox.SampleCreator.Views.LayerEngine;

/// <summary>Right-hand inspector that switches between pad, layer, and sample panels based on
/// the selected pad's <c>Focus</c>. DataContext is the <c>SampleCreatorViewModel</c>.</summary>
public partial class ContextInspectorView : UserControl
{
    public ContextInspectorView()
    {
        InitializeComponent();
    }
}
