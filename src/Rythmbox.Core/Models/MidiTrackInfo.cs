using System.Text;
using SoundFlow.Metadata.Midi;
using SoundFlow.Metadata.Midi.Enums;
using SoundFlow.Midi.Enums;

namespace Rythmbox.Core.Models;

/// <summary>
/// A per-MIDI-channel summary of a loaded file, used to drive the track panel
/// (mute/solo/volume operate on the synthesizer per-channel, not per raw track chunk).
/// </summary>
public sealed record MidiTrackInfo(int Channel, string Name, int ProgramNumber, int NoteCount)
{
    public static IReadOnlyList<MidiTrackInfo> FromMidiFile(MidiFile midiFile)
    {
        var names = new Dictionary<int, string>();
        var programs = new Dictionary<int, int>();
        var noteCounts = new Dictionary<int, int>();
        var seenChannels = new HashSet<int>();

        foreach (var track in midiFile.Tracks)
        {
            string? pendingName = null;

            foreach (var midiEvent in track.Events)
            {
                switch (midiEvent)
                {
                    case MetaEvent { Type: MetaEventType.TrackName or MetaEventType.InstrumentName } meta:
                        var text = Encoding.ASCII.GetString(meta.Data).Trim('\0', ' ');
                        if (text.Length > 0)
                        {
                            pendingName = text;
                        }

                        break;

                    case ChannelEvent channelEvent:
                        var message = channelEvent.Message;
                        var channel = message.Channel;
                        seenChannels.Add(channel);

                        if (pendingName is not null && !names.ContainsKey(channel))
                        {
                            names[channel] = pendingName;
                        }

                        if (message.Command == MidiCommand.ProgramChange)
                        {
                            programs[channel] = message.Data1;
                        }
                        else if (message.Command == MidiCommand.NoteOn && message.Velocity > 0)
                        {
                            noteCounts[channel] = noteCounts.GetValueOrDefault(channel) + 1;
                        }

                        break;
                }
            }
        }

        return seenChannels
            .OrderBy(channel => channel)
            .Select(channel => new MidiTrackInfo(
                channel,
                names.TryGetValue(channel, out var name) ? name : channel == 9 ? "Percussion" : $"Channel {channel + 1}",
                programs.GetValueOrDefault(channel),
                noteCounts.GetValueOrDefault(channel)))
            .ToList();
    }
}
