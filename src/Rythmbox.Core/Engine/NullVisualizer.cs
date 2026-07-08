using SoundFlow.Interfaces;

namespace Rythmbox.Core.Engine;

/// <summary>
/// A no-op <see cref="IVisualizer"/> used to satisfy analyzers that require a visualizer
/// even when the host only wants to poll numeric values (e.g. RMS/Peak) directly.
/// </summary>
internal sealed class NullVisualizer : IVisualizer
{
    public string Name => "None";

    public event EventHandler? VisualizationUpdated
    {
        add { }
        remove { }
    }

    public void ProcessOnAudioData(ReadOnlySpan<float> audioData)
    {
    }

    public void Render(IVisualizationContext context)
    {
    }

    public void Dispose()
    {
    }
}
