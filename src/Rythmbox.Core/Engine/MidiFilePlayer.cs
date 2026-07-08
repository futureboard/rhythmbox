using Rythmbox.Core.Models;
using SoundFlow.Enums;
using SoundFlow.Metadata.Midi;
using SoundFlow.Midi.Enums;
using SoundFlow.Midi.Interfaces;
using SoundFlow.Providers;
using SoundFlow.Synthesis;

namespace Rythmbox.Core.Engine;

/// <summary>
/// Loads a standard MIDI file and plays it back through a target synthesizer via SoundFlow's
/// <see cref="Sequencer"/>, exposing transport control and per-channel track info.
/// </summary>
public sealed class MidiFilePlayer : IDisposable
{
    private readonly PlaybackEngine _engine;
    private readonly TrackChannelRouter _router;
    private Sequencer? _sequencer;

    public MidiFilePlayer(PlaybackEngine engine, IMidiControllable target)
    {
        _engine = engine;
        _router = new TrackChannelRouter(target);
    }

    public string? LoadedFilePath { get; private set; }

    public bool IsLoaded => _sequencer is not null;

    public IReadOnlyList<MidiTrackInfo> Tracks { get; private set; } = Array.Empty<MidiTrackInfo>();

    /// <summary>The distinct note numbers hit anywhere in the loaded file, used to highlight pads that this loop uses.</summary>
    public IReadOnlySet<int> UsedNoteNumbers { get; private set; } = new HashSet<int>();

    /// <summary>The file's detected tempo (first tempo meta-event), or 120 BPM if none was found.</summary>
    public double Bpm { get; private set; } = 120.0;

    public PlaybackState State => _sequencer?.State ?? PlaybackState.Stopped;

    public TimeSpan Position => _sequencer?.CurrentTime ?? TimeSpan.Zero;

    public TimeSpan Duration => _sequencer?.Duration ?? TimeSpan.Zero;

    public bool IsLooping
    {
        get => _sequencer?.IsLooping ?? false;
        set
        {
            if (_sequencer is not null)
            {
                _sequencer.IsLooping = value;
            }
        }
    }

    public void Load(string path)
    {
        Unload();

        using var stream = File.OpenRead(path);
        var midiFile = MidiFileParser.Parse(stream);
        var dataProvider = new MidiDataProvider(midiFile);

        _router.Reset();
        _sequencer = new Sequencer(_engine.RawEngine, _engine.Format, dataProvider, _router)
        {
            Name = "MIDI File Sequencer",
        };
        _engine.MasterMixer.AddComponent(_sequencer);

        Tracks = MidiTrackInfo.FromMidiFile(midiFile);
        UsedNoteNumbers = ExtractUsedNoteNumbers(midiFile);
        Bpm = midiFile.InitialBeatsPerMinute ?? 120.0;
        LoadedFilePath = path;
    }

    private static HashSet<int> ExtractUsedNoteNumbers(MidiFile midiFile)
    {
        var notes = new HashSet<int>();

        foreach (var track in midiFile.Tracks)
        {
            foreach (var midiEvent in track.Events)
            {
                if (midiEvent is ChannelEvent { Message: var message } &&
                    message.Command == MidiCommand.NoteOn &&
                    message.Velocity > 0)
                {
                    notes.Add(message.NoteNumber);
                }
            }
        }

        return notes;
    }

    /// <summary>Re-adds the sequencer to the current MasterMixer, used after an output device switch.</summary>
    public void ReattachToMixer()
    {
        if (_sequencer is not null)
        {
            _engine.MasterMixer.AddComponent(_sequencer);
        }
    }

    public void Play() => _sequencer?.Play();

    public void Pause() => _sequencer?.Pause();

    public void Stop() => _sequencer?.Stop();

    public void Seek(TimeSpan time) => _sequencer?.Seek(time);

    public void SetTrackMute(int channel, bool mute) => _router.SetMute(channel, mute);

    public void SetTrackSolo(int channel, bool solo) => _router.SetSolo(channel, solo);

    public void SetTrackVolume(int channel, float volume) => _router.SetVolume(channel, volume);

    private void Unload()
    {
        if (_sequencer is not null)
        {
            _sequencer.Stop();
            _engine.MasterMixer.RemoveComponent(_sequencer);
            _sequencer.Dispose();
            _sequencer = null;
        }

        Tracks = Array.Empty<MidiTrackInfo>();
        UsedNoteNumbers = new HashSet<int>();
        Bpm = 120.0;
        LoadedFilePath = null;
    }

    public void Dispose() => Unload();
}
