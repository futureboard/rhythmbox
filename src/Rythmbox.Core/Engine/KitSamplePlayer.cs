using Rythmbox.Core.Audio.Dsp;
using Rythmbox.Core.Formats;
using Rythmbox.Core.Models;
using Rythmbox.Core.Models.Mixer;
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
        public float[] Buffer = [];
        public double Position;
        public float Gain = 1f;
        public bool Active;
        public int ChokeGroup;
        public int FadeOut;
    }

    private sealed class LevelMeter
    {
        public float Peak;
        public float Rms;
    }

    private readonly PlaybackEngine _engine;
    private readonly object _voiceLock = new();
    private readonly Voice[] _voices = Enumerable.Range(0, MaxVoices).Select(_ => new Voice()).ToArray();
    private readonly Dictionary<int, PadMixState> _padMix;
    private readonly Dictionary<PadBus, BusMixState> _busMix;
    private readonly int[] _drumMap = Enumerable.Repeat(-1, 128).ToArray();
    private readonly int[] _chokeGroups = Enumerable.Repeat(0, GmPercussionMap.Pads.Count).ToArray();
    private readonly float[] _pitchSemitones = new float[GmPercussionMap.Pads.Count];
    private readonly float[] _padGains = Enumerable.Repeat(1f, GmPercussionMap.Pads.Count).ToArray();
    private readonly PadPlaybackState[] _padPlayback = Enumerable.Range(0, GmPercussionMap.Pads.Count)
        .Select(_ => new PadPlaybackState())
        .ToArray();
    private readonly ChannelDspChain[] _padDsp = Enumerable.Range(0, GmPercussionMap.Pads.Count).Select(_ => new ChannelDspChain()).ToArray();
    private readonly ChannelDspChain[] _busDsp = Enum.GetValues<PadBus>().Select(_ => new ChannelDspChain()).ToArray();
    private readonly ChannelDspChain _masterDsp = new();
    private readonly ReverbEffect _reverb = new();
    private readonly LevelMeter[] _padMeters = Enumerable.Range(0, GmPercussionMap.Pads.Count).Select(_ => new LevelMeter()).ToArray();
    private readonly Dictionary<PadBus, LevelMeter> _busMeters = Enum.GetValues<PadBus>().ToDictionary(bus => bus, _ => new LevelMeter());
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

        LoadEmptyGmKit();
    }

    public string? LoadedKitPath { get; private set; }

    public string KitName { get; private set; } = "GM Kit";

    public bool IsLoaded => LoadedKitPath is not null || KitName.Length > 0;

    public IReadOnlyList<bool> PadHasSample =>
        _padPlayback.Select(static pad => pad.HasAudio).ToArray();

    public void LoadKit(string presetPath, string? samplesRoot = null)
    {
        var kit = string.Equals(Path.GetExtension(presetPath), ApakCodec.Extension, StringComparison.OrdinalIgnoreCase)
            ? ApakCodec.LoadFactory(presetPath)
            : KitPresetCodec.Load(presetPath, samplesRoot);
        ApplyKit(kit, presetPath);
    }

    public void LoadEmptyGmKit()
    {
        ApplyKit(KitPresetCodec.CreateDefaultGmKit(), null);
    }

    public void LoadKitPreset(KitPreset kit, string? presetPath = null) => ApplyKit(kit, presetPath);

    private void ApplyKit(KitPreset kit, string? jsonPath)
    {
        lock (_voiceLock)
        {
            StopAllVoices();

            KitName = kit.Name;
            LoadedKitPath = jsonPath;

            var kitByNote = kit.Pads
                .Where(static pad => pad.MidiNote >= 0)
                .GroupBy(static pad => pad.MidiNote)
                .ToDictionary(static group => group.Key, static group => group.First());

            for (var i = 0; i < GmPercussionMap.Pads.Count; i++)
            {
                var gmPad = GmPercussionMap.Pads[i];
                kitByNote.TryGetValue(gmPad.Note, out var sample);

                _padPlayback[i] = PadPlaybackState.FromSample(sample);
                _chokeGroups[i] = sample?.ChokeGroup ?? 0;
                _pitchSemitones[i] = sample?.PitchSemitones ?? 0f;
                _padGains[i] = sample?.Gain ?? 1f;
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

    public void ReattachToMixer()
    {
        if (_addedToMixer)
        {
            _engine.MasterMixer.RemoveComponent(this);
            _addedToMixer = false;
        }

        EnsureInMixer();
    }

    private void EnsureInMixer()
    {
        if (!_addedToMixer)
        {
            _engine.MasterMixer.AddComponent(this);
            _addedToMixer = true;
        }
    }

    public ChannelDspSettings GetPadDsp(int note)
    {
        var padIndex = NoteToPadIndex(note);
        return padIndex >= 0 ? _padDsp[padIndex].Settings.Clone() : new ChannelDspSettings();
    }

    public void SetPadDsp(int note, ChannelDspSettings settings)
    {
        var padIndex = NoteToPadIndex(note);
        if (padIndex >= 0)
        {
            _padDsp[padIndex].Settings = settings.Clone();
        }
    }

    public ChannelDspSettings GetBusDsp(PadBus bus) => _busDsp[(int)bus].Settings.Clone();

    public void SetBusDsp(PadBus bus, ChannelDspSettings settings) =>
        _busDsp[(int)bus].Settings = settings.Clone();

    public ChannelDspSettings GetMasterDsp() => _masterDsp.Settings.Clone();

    public void SetMasterDsp(ChannelDspSettings settings) => _masterDsp.Settings = settings.Clone();

    public MixerMeterState PollPadMeter(int note)
    {
        var padIndex = NoteToPadIndex(note);
        if (padIndex < 0)
        {
            return MixerMeterState.Disabled;
        }

        return TakeMeter(_padMeters[padIndex]);
    }

    public MixerMeterState PollBusMeter(PadBus bus) => TakeMeter(_busMeters[bus]);

    private static MixerMeterState TakeMeter(LevelMeter meter)
    {
        var peak = meter.Peak;
        var rms = meter.Rms;
        meter.Peak = 0f;
        meter.Rms *= 0.55f;

        if (peak <= 0.0001f && rms <= 0.0001f)
        {
            return MixerMeterState.Disabled;
        }

        return MixerMeterState.FromMono(rms, peak, peak >= 0.98f);
    }

    private static void PushMeter(LevelMeter meter, float sample)
    {
        var abs = MathF.Abs(sample);
        meter.Peak = MathF.Max(meter.Peak, abs);
        meter.Rms = MathF.Max(meter.Rms * 0.9f, abs);
    }

    private static int NoteToPadIndex(int note)
    {
        for (var i = 0; i < GmPercussionMap.Pads.Count; i++)
        {
            if (GmPercussionMap.Pads[i].Note == note)
            {
                return i;
            }
        }

        return -1;
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
        var midiVelocity = Math.Clamp((int)Math.Round(velocity * 127f), 1, 127);
        if (!TryResolveMix(note, velocity, out var gain))
        {
            return;
        }

        lock (_voiceLock)
        {
            StartVoice(padIndex, midiVelocity, gain * _padGains[padIndex]);
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
        var midiVelocity = Math.Clamp((int)Math.Round(velocity * 127f), 1, 127);
        if (!TryResolveMix(gmNote, velocity, out var gain))
        {
            return;
        }

        lock (_voiceLock)
        {
            StartVoice(padIndex, midiVelocity, gain * _padGains[padIndex]);
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

    private void StartVoice(int padIndex, int midiVelocity, float gain)
    {
        var buffer = _padPlayback[padIndex].SelectBuffer(midiVelocity);
        if (buffer is null || buffer.Length == 0)
        {
            return;
        }

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
        slot.Buffer = buffer;
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
            var sampleRate = (float)WavCodec.TargetSampleRate;
            _reverb.Size = _masterDsp.Settings.ReverbSize;
            _reverb.Mix = Math.Clamp(_masterDsp.Settings.ReverbMix, 0f, 1f);
            Span<float> busScratch = stackalloc float[Enum.GetValues<PadBus>().Length];

            for (var frame = 0; frame < frameCount; frame++)
            {
                var sampleL = 0f;
                var sampleR = 0f;
                var reverbSend = 0f;
                busScratch.Clear();

                foreach (var voice in _voices)
                {
                    if (!voice.Active)
                    {
                        continue;
                    }

                    var buf = voice.Buffer;
                    if (buf.Length == 0)
                    {
                        voice.Active = false;
                        continue;
                    }

                    var pitchRatio = WavCodec.PitchToPlaybackRatio(_pitchSemitones[voice.PadIndex]);
                    var index = (int)voice.Position;
                    if (index >= buf.Length)
                    {
                        voice.Active = false;
                        continue;
                    }

                    var src = buf[index] * voice.Gain;
                    voice.Position += pitchRatio;

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

                    var pad = GmPercussionMap.Pads[voice.PadIndex];
                    _padDsp[voice.PadIndex].Process(ref src, sampleRate);
                    reverbSend += _padDsp[voice.PadIndex].ReverbSend;

                    var busIndex = (int)pad.Bus;
                    busScratch[busIndex] += src;
                    PushMeter(_padMeters[voice.PadIndex], src);
                }

                for (var busIndex = 0; busIndex < busScratch.Length; busIndex++)
                {
                    var busSample = busScratch[busIndex];
                    if (MathF.Abs(busSample) <= 0.000001f)
                    {
                        continue;
                    }

                    var bus = (PadBus)busIndex;
                    _busDsp[busIndex].Process(ref busSample, sampleRate);
                    reverbSend += _busDsp[busIndex].ReverbSend;
                    PushMeter(_busMeters[bus], busSample);

                    sampleL += busSample;
                    sampleR += busSample;
                }

                var wet = _reverb.Process(reverbSend);
                sampleL += wet;
                sampleR += wet;

                var masterSample = (sampleL + sampleR) * 0.5f;
                _masterDsp.Process(ref masterSample, sampleRate);
                sampleL = masterSample;
                sampleR = masterSample;

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
