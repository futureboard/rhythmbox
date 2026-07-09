using Rythmbox.Core.Formats;
using Rythmbox.Core.Models;
using Rythmbox.Core.Samples;
using SoundFlow.Abstracts;
using SoundFlow.Midi.Enums;
using SoundFlow.Midi.Interfaces;
using SoundFlow.Midi.Structs;
using SoundFlow.Structs;

namespace Rythmbox.Core.Engine;

/// <summary>
/// Polyphonic WAV sample kit player: loads <c>shared/PRESETS/*.json</c> kits and mixes one-shot
/// voices in real time. Implements <see cref="IMidiControllable"/> for MIDI file sequencing and
/// live hardware input.
/// </summary>
public sealed class KitSamplePlayer : SoundComponent, IMidiControllable, IDisposable
{
    private const int MaxVoices = 32;
    private const int ChokeFadeFrames = 256;

    private sealed class PadMixState
    {
        public bool IsMuted;
        public bool IsSoloed;
        public float Volume = 1f;
        public PadBus Bus;
    }

    private sealed class BusMixState
    {
        public bool IsMuted;
        public float Volume = 1f;
    }

    private sealed class Voice
    {
        public int PadIndex = -1;
        public int Position;
        public float Gain = 1f;
        public bool Active;
        public int ChokeGroup;
        public int FadeOut;
    }

    private readonly PlaybackEngine _engine;
    private readonly object _voiceLock = new();
    private readonly Voice[] _voices = Enumerable.Range(0, MaxVoices).Select(_ => new Voice()).ToArray();
    private readonly Dictionary<int, PadMixState> _padMix;
    private readonly Dictionary<PadBus, BusMixState> _busMix;
    private readonly int[] _drumMap = Enumerable.Repeat(-1, 128).ToArray();
    private readonly int[] _chokeGroups = Enumerable.Repeat(0, GmPercussionMap.Pads.Count).ToArray();
    private readonly float[][] _buffers = new float[GmPercussionMap.Pads.Count][];
    private bool _anyPadSoloed;
    private bool _addedToMixer;

    public KitSamplePlayer(PlaybackEngine engine)
        : base(engine.RawEngine, engine.Format)
    {
        _engine = engine;
        Name = "Kit Sample Player";

        _padMix = GmPercussionMap.Pads.ToDictionary(
            pad => pad.Note,
            pad => new PadMixState { Bus = pad.Bus });

        _busMix = Enum.GetValues<PadBus>().ToDictionary(bus => bus, _ => new BusMixState());

        LoadProceduralGmKit();
    }

    public string? LoadedKitPath { get; private set; }

    public string KitName { get; private set; } = "GM Kit";

    public bool IsLoaded => LoadedKitPath is not null || KitName.Length > 0;

    public IReadOnlyList<bool> PadHasSample =>
        _buffers.Select(buf => buf.Length > 0).ToArray();

    public void LoadKit(string presetPath, string? samplesRoot = null)
    {
        var kit = string.Equals(Path.GetExtension(presetPath), ApakCodec.Extension, StringComparison.OrdinalIgnoreCase)
            ? ApakCodec.LoadFactory(presetPath)
            : KitPresetCodec.Load(presetPath, samplesRoot);
        ApplyKit(kit, presetPath);
    }

    public void LoadProceduralGmKit()
    {
        ApplyKit(KitPresetCodec.CreateDefaultGmKit(), null);
    }

    private void ApplyKit(KitPreset kit, string? jsonPath)
    {
        lock (_voiceLock)
        {
            StopAllVoices();

            KitName = kit.Name;
            LoadedKitPath = jsonPath;

            for (var i = 0; i < GmPercussionMap.Pads.Count; i++)
            {
                var gmPad = GmPercussionMap.Pads[i];
                var sample = i < kit.Pads.Count ? kit.Pads[i] : null;

                if (sample?.HasAudio == true)
                {
                    _buffers[i] = sample.Samples;
                    _chokeGroups[i] = sample.ChokeGroup;
                }
                else
                {
                    var label = sample?.Label ?? gmPad.Label;
                    _buffers[i] = ProceduralDrumSynth.ForLabel(label, WavCodec.TargetSampleRate);
                    _chokeGroups[i] = sample?.ChokeGroup ?? 0;
                }
            }

            RebuildDrumMap(kit);
        }

        EnsureInMixer();
    }

    private void RebuildDrumMap(KitPreset kit)
    {
        Array.Fill(_drumMap, -1);

        for (var i = 0; i < GmPercussionMap.Pads.Count; i++)
        {
            _drumMap[GmPercussionMap.Pads[i].Note] = i;
        }

        for (var i = 0; i < kit.Pads.Count && i < GmPercussionMap.Pads.Count; i++)
        {
            var note = kit.Pads[i].MidiNote;
            if (note is >= 0 and < 128)
            {
                _drumMap[note] = i;
            }
        }
    }

    public void ReattachToMixer() => EnsureInMixer();

    private void EnsureInMixer()
    {
        if (!_addedToMixer)
        {
            _engine.MasterMixer.AddComponent(this);
            _addedToMixer = true;
        }
    }

    public void NoteOn(int channel, int note, int velocity) =>
        TriggerByNote(note, velocity / 127f);

    public void NoteOff(int channel, int note)
    {
        // One-shot samples; note-off is a no-op.
    }

    public void AllNotesOff()
    {
        lock (_voiceLock)
        {
            StopAllVoices();
        }
    }

    public void SetPadMute(int note, bool mute)
    {
        if (_padMix.TryGetValue(note, out var state))
        {
            state.IsMuted = mute;
        }
    }

    public void SetPadSolo(int note, bool solo)
    {
        if (!_padMix.TryGetValue(note, out var state))
        {
            return;
        }

        state.IsSoloed = solo;
        _anyPadSoloed = _padMix.Values.Any(s => s.IsSoloed);
    }

    public void SetPadVolume(int note, float volume)
    {
        if (_padMix.TryGetValue(note, out var state))
        {
            state.Volume = Math.Clamp(volume, 0f, 1f);
        }
    }

    public void SetBusMute(PadBus bus, bool mute)
    {
        if (_busMix.TryGetValue(bus, out var state))
        {
            state.IsMuted = mute;
        }
    }

    public void SetBusVolume(PadBus bus, float volume)
    {
        if (_busMix.TryGetValue(bus, out var state))
        {
            state.Volume = Math.Clamp(volume, 0f, 1.5f);
        }
    }

    public void TriggerPad(int padIndex, float velocity = 1f)
    {
        if (padIndex < 0 || padIndex >= GmPercussionMap.Pads.Count)
        {
            return;
        }

        var note = GmPercussionMap.Pads[padIndex].Note;
        if (!TryResolveMix(note, velocity, out var gain))
        {
            return;
        }

        lock (_voiceLock)
        {
            StartVoice(padIndex, gain);
        }
    }

    public void TriggerNote(int note, float velocity = 1f) => TriggerByNote(note, velocity);

    public void ProcessMidiMessage(MidiMessage message)
    {
        if (message.Command != MidiCommand.NoteOn || message.Velocity <= 0)
        {
            return;
        }

        TriggerByNote(message.NoteNumber, message.Velocity / 127f);
    }

    private void TriggerByNote(int note, float velocity)
    {
        if (note is < 0 or >= 128)
        {
            return;
        }

        var padIndex = _drumMap[note];
        if (padIndex < 0)
        {
            return;
        }

        var gmNote = GmPercussionMap.Pads[padIndex].Note;
        if (!TryResolveMix(gmNote, velocity, out var gain))
        {
            return;
        }

        lock (_voiceLock)
        {
            StartVoice(padIndex, gain);
        }
    }

    private bool TryResolveMix(int note, float velocity, out float gain)
    {
        gain = velocity;

        if (!_padMix.TryGetValue(note, out var padState))
        {
            return true;
        }

        var busState = _busMix[padState.Bus];
        if (padState.IsMuted || busState.IsMuted || (_anyPadSoloed && !padState.IsSoloed))
        {
            return false;
        }

        gain = velocity * padState.Volume * busState.Volume;
        return gain > 0.0001f;
    }

    private void StartVoice(int padIndex, float gain)
    {
        var choke = _chokeGroups[padIndex];
        if (choke > 0)
        {
            foreach (var voice in _voices)
            {
                if (voice.Active && voice.ChokeGroup == choke)
                {
                    voice.FadeOut = ChokeFadeFrames;
                }
            }
        }

        var slot = _voices.FirstOrDefault(v => !v.Active) ?? _voices[0];
        slot.PadIndex = padIndex;
        slot.Position = 0;
        slot.Gain = gain;
        slot.Active = true;
        slot.ChokeGroup = choke;
        slot.FadeOut = 0;
    }

    private void StopAllVoices()
    {
        foreach (var voice in _voices)
        {
            voice.Active = false;
            voice.FadeOut = 0;
        }
    }

    protected override void GenerateAudio(Span<float> outputBuffer, int channels)
    {
        outputBuffer.Clear();

        lock (_voiceLock)
        {
            var frameCount = outputBuffer.Length / channels;

            for (var frame = 0; frame < frameCount; frame++)
            {
                var sampleL = 0f;
                var sampleR = 0f;

                foreach (var voice in _voices)
                {
                    if (!voice.Active)
                    {
                        continue;
                    }

                    var buf = _buffers[voice.PadIndex];
                    if (voice.Position >= buf.Length)
                    {
                        voice.Active = false;
                        continue;
                    }

                    var src = buf[voice.Position++] * voice.Gain;

                    if (voice.FadeOut > 0)
                    {
                        var fade = voice.FadeOut / (float)ChokeFadeFrames;
                        src *= fade;
                        voice.FadeOut--;
                        if (voice.FadeOut <= 0)
                        {
                            voice.Active = false;
                            continue;
                        }
                    }

                    sampleL += src;
                    sampleR += src;
                }

                sampleL = MathF.Tanh(sampleL);
                sampleR = MathF.Tanh(sampleR);

                var baseIndex = frame * channels;
                outputBuffer[baseIndex] += sampleL;
                if (channels > 1)
                {
                    outputBuffer[baseIndex + 1] += sampleR;
                }
            }
        }
    }

    public new void Dispose()
    {
        if (_addedToMixer)
        {
            _engine.MasterMixer.RemoveComponent(this);
            _addedToMixer = false;
        }
    }
}
