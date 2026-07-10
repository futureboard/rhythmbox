namespace Rythmbox.SampleCreator.ViewModels;

/// <summary>
/// Single source of truth for what the Layer Engine inspector is editing.
/// Replaces the scattered boolean selection flags: the inspector derives its
/// mode (pad / layer / sample) purely from this value on the selected pad.
/// <c>None</c> is represented implicitly by <see cref="SampleCreatorViewModel.SelectedPad"/>
/// being <c>null</c>.
/// </summary>
public enum LayerEngineFocus
{
    Pad,
    Layer,
    Sample,
}
