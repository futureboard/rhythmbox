using Rythmbox.Core.Models;
using SoundFlow.Midi.Interfaces;
using SoundFlow.Midi.Structs;
using SoundFlow.Synthesis;
using SoundFlow.Synthesis.Banks;

namespace Rythmbox.Core.Engine;

/// <summary>
/// Loads a SoundFont (.sf2) bank and exposes a polyphonic <see cref="Synthesizer"/> that can be
/// driven by note-on/off calls (on-screen keyboard), live MIDI hardware input, or a MIDI file sequencer.
/// </summary>
public sealed class SoundFontPlayer : IMidiControllable, IDisposable
{
    private readonly PlaybackEngine _engine;
    private SoundFontBank? _bank;
    private Synthesizer? _synth;

    public SoundFontPlayer(PlaybackEngine engine)
    {
        _engine = engine;
    }

    public string? LoadedSoundFontPath { get; private set; }

    public bool IsLoaded => _synth is not null;

    public IReadOnlyList<PresetInfo> Presets => _bank?.AvailablePresets ?? Array.Empty<PresetInfo>();

    public Synthesizer Synthesizer => _synth ?? throw new InvalidOperationException("Load a SoundFont first.");

    public void LoadSoundFont(string path)
    {
        Unload();

        _bank = new SoundFontBank(path, _engine.Format);
        _synth = new Synthesizer(_engine.RawEngine, _engine.Format, _bank)
        {
            Name = "SoundFont Synth",
        };
        _engine.MasterMixer.AddComponent(_synth);
        LoadedSoundFontPath = path;

        // General MIDI convention: channel 10 (index 9) is percussion. Arm the standard drum
        // kit (bank 128, program 0) so the pad grid sounds correct without extra user setup.
        SelectPreset(GmPercussionMap.PercussionChannel, new PresetInfo(128, 0, "Standard Kit"));
    }

    /// <summary>Re-adds the synthesizer to the current MasterMixer, used after an output device switch.</summary>
    public void ReattachToMixer()
    {
        if (_synth is not null)
        {
            _engine.MasterMixer.AddComponent(_synth);
        }
    }

    public void SelectPreset(int channel, PresetInfo preset)
    {
        var bankMsb = (byte)((preset.Bank >> 7) & 0x7F);
        var bankLsb = (byte)(preset.Bank & 0x7F);
        var now = DateTime.UtcNow.Ticks;

        ProcessMidiMessage(new MidiMessage((byte)(0xB0 | channel), 0, bankMsb, now));
        ProcessMidiMessage(new MidiMessage((byte)(0xB0 | channel), 32, bankLsb, now));
        ProcessMidiMessage(new MidiMessage((byte)(0xC0 | channel), (byte)preset.Program, 0, now));
    }

    public void NoteOn(int channel, int note, int velocity) =>
        ProcessMidiMessage(new MidiMessage((byte)(0x90 | channel), (byte)note, (byte)velocity, DateTime.UtcNow.Ticks));

    public void NoteOff(int channel, int note) =>
        ProcessMidiMessage(new MidiMessage((byte)(0x80 | channel), (byte)note, 0, DateTime.UtcNow.Ticks));

    /// <summary>Sends MIDI CC 123 (All Notes Off) on every channel.</summary>
    public void AllNotesOff()
    {
        for (var channel = 0; channel < 16; channel++)
        {
            ProcessMidiMessage(new MidiMessage((byte)(0xB0 | channel), 123, 0, DateTime.UtcNow.Ticks));
        }
    }

    public void ProcessMidiMessage(MidiMessage message) => _synth?.ProcessMidiMessage(message);

    private void Unload()
    {
        if (_synth is not null)
        {
            _engine.MasterMixer.RemoveComponent(_synth);
            _synth.Dispose();
            _synth = null;
        }

        _bank?.Dispose();
        _bank = null;
        LoadedSoundFontPath = null;
    }

    public void Dispose() => Unload();
}
