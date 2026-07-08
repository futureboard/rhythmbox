using SoundFlow.Midi.Enums;
using SoundFlow.Midi.Interfaces;
using SoundFlow.Midi.Structs;

namespace Rythmbox.Core.Engine;

/// <summary>
/// Sits between a <see cref="SoundFlow.Synthesis.Sequencer"/> and the real synthesizer target,
/// applying per-MIDI-channel mute/solo/volume before forwarding messages. This keeps track-panel
/// behavior fully under our control instead of depending on ambiguous modifier-chain semantics.
/// </summary>
internal sealed class TrackChannelRouter : IMidiControllable
{
    private const int ChannelCount = 16;

    private readonly IMidiControllable _target;
    private readonly bool[] _mute = new bool[ChannelCount];
    private readonly bool[] _solo = new bool[ChannelCount];
    private readonly float[] _volume = new float[ChannelCount];
    private bool _anySolo;

    public TrackChannelRouter(IMidiControllable target)
    {
        _target = target;
        Array.Fill(_volume, 1f);
    }

    public void Reset()
    {
        Array.Clear(_mute);
        Array.Clear(_solo);
        Array.Fill(_volume, 1f);
        _anySolo = false;
    }

    public void SetMute(int channel, bool mute)
    {
        if (IsValidChannel(channel))
        {
            _mute[channel] = mute;
        }
    }

    public void SetSolo(int channel, bool solo)
    {
        if (!IsValidChannel(channel))
        {
            return;
        }

        _solo[channel] = solo;
        _anySolo = Array.Exists(_solo, isSoloed => isSoloed);
    }

    public void SetVolume(int channel, float volume)
    {
        if (IsValidChannel(channel))
        {
            _volume[channel] = Math.Clamp(volume, 0f, 1f);
        }
    }

    public void ProcessMidiMessage(MidiMessage message)
    {
        var channel = message.Channel;

        if (IsValidChannel(channel))
        {
            var isNoteMessage = message.Command is MidiCommand.NoteOn or MidiCommand.NoteOff or MidiCommand.PolyphonicKeyPressure;
            var blocked = _mute[channel] || (_anySolo && !_solo[channel]);

            if (blocked && isNoteMessage)
            {
                return;
            }

            if (message.Command == MidiCommand.NoteOn && _volume[channel] < 1f)
            {
                var scaledVelocity = (byte)Math.Clamp(message.Velocity * _volume[channel], 0, 127);
                message = new MidiMessage(message.StatusByte, (byte)message.NoteNumber, scaledVelocity, message.Timestamp);
            }
        }

        _target.ProcessMidiMessage(message);
    }

    private static bool IsValidChannel(int channel) => channel is >= 0 and < ChannelCount;
}
