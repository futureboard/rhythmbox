using SoundFlow.Midi.Interfaces;
using SoundFlow.Midi.Routing;
using SoundFlow.Midi.Structs;

namespace Rythmbox.Core.Engine;

/// <summary>
/// Enumerates hardware MIDI input devices (cross-platform via PortMidi) and routes a selected
/// device straight into a target <see cref="IMidiControllable"/> (typically the loaded SoundFont synth).
/// </summary>
public sealed class MidiInputService : IDisposable
{
    private readonly PlaybackEngine _engine;
    private MidiRoute? _activeRoute;

    public MidiInputService(PlaybackEngine engine)
    {
        _engine = engine;
    }

    public IReadOnlyList<MidiDeviceInfo> AvailableInputs => _engine.MidiManager.AvailableInputs;

    public MidiDeviceInfo? ConnectedDevice { get; private set; }

    public bool IsConnected => _activeRoute is not null;

    public void RefreshDevices() => _engine.RefreshDevices();

    public void Connect(MidiDeviceInfo device, IMidiControllable target)
    {
        Disconnect();
        _activeRoute = _engine.MidiManager.CreateRoute(device, target);
        ConnectedDevice = device;
    }

    public void Disconnect()
    {
        if (_activeRoute is not null)
        {
            _engine.MidiManager.RemoveRoute(_activeRoute);
            _activeRoute = null;
        }

        ConnectedDevice = null;
    }

    public void Dispose() => Disconnect();
}
