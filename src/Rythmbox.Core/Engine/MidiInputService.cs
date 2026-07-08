using SoundFlow.Midi.Interfaces;
using SoundFlow.Midi.Routing;
using SoundFlow.Midi.Structs;

namespace Rythmbox.Core.Engine;

/// <summary>
/// Enumerates hardware MIDI input devices (cross-platform via PortMidi) and routes a selected
/// device into a target <see cref="IMidiControllable"/>.
/// </summary>
public sealed class MidiInputService : IDisposable
{
    private readonly PlaybackEngine _engine;
    private MidiRoute? _activeRoute;
    private int _portIndex = -1;

    public MidiInputService(PlaybackEngine engine)
    {
        _engine = engine;
    }

    public IReadOnlyList<MidiDeviceInfo> AvailableInputs => _engine.MidiManager.AvailableInputs;

    public MidiDeviceInfo? ConnectedDevice { get; private set; }

    public int ConnectedPortIndex => _portIndex;

    public bool IsConnected => _activeRoute is not null;

    public void RefreshDevices() => _engine.RefreshDevices();

    public void Connect(MidiDeviceInfo device, IMidiControllable target)
    {
        Disconnect();
        _activeRoute = _engine.MidiManager.CreateRoute(device, target);
        ConnectedDevice = device;
        _portIndex = AvailableInputs.ToList().FindIndex(d => d.Id == device.Id);
    }

    public bool ConnectByIndex(int index, IMidiControllable target)
    {
        RefreshDevices();
        var devices = AvailableInputs;
        if (devices.Count == 0)
        {
            Disconnect();
            return false;
        }

        var wrapped = ((index % devices.Count) + devices.Count) % devices.Count;
        Connect(devices[wrapped], target);
        _portIndex = wrapped;
        return true;
    }

    public bool ConnectNext(IMidiControllable target) =>
        ConnectByIndex(_portIndex < 0 ? 0 : _portIndex + 1, target);

    public bool ConnectPrevious(IMidiControllable target) =>
        ConnectByIndex(_portIndex < 0 ? 0 : _portIndex - 1, target);

    public void Disconnect()
    {
        if (_activeRoute is not null)
        {
            _engine.MidiManager.RemoveRoute(_activeRoute);
            _activeRoute = null;
        }

        ConnectedDevice = null;
        _portIndex = -1;
    }

    public void Dispose() => Disconnect();
}
