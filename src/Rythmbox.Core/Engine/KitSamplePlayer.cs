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
        public DrumMixGroup MixGroup;
    }

    private sealed class MixGroupState
    {
        public bool IsMuted;
        public bool IsSoloed;
        public float Volume = 1f;
    }

    private sealed class BusMixState
    {
        public bool IsMuted;
        public float Volume = 1f;
    }

    private sealed class Voice
    {
        public int PadIndex = -1;
        public IPlaybackSample? Sample;
        public double Position;
        public float Gain = 1f;
        public bool Active;
        public int ChokeGroup;
        public int FadeOut;
        public int EnvelopeFrame;
        public int AttackFrames;
        public int DecayFrames;
        public int ReleaseFrames;
        public int ReleaseFramesRemaining;
        public float SustainLevel = 1f;
        public float EnvelopeLevel = 1f;
        public bool IsReleasing;
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
    private readonly Dictionary<DrumMixGroup, MixGroupState> _mixGroups;
    private readonly Dictionary<PadBus, BusMixState> _busMix;
    private readonly int[] _drumMap = Enumerable.Repeat(-1, 128).ToArray();
    private readonly int[] _padMidiNotes = new int[GmPercussionMap.Pads.Count];
    private readonly int[] _chokeGroups = Enumerable.Repeat(0, GmPercussionMap.Pads.Count).ToArray();
    private readonly float[] _pitchSemitones = new float[GmPercussionMap.Pads.Count];
    private readonly float[] _padGains = Enumerable.Repeat(1f, GmPercussionMap.Pads.Count).ToArray();
    private readonly PadEnvelopeSettings[] _padEnvelopes = Enumerable.Range(0, GmPercussionMap.Pads.Count)
        .Select(_ => new PadEnvelopeSettings())
        .ToArray();
    private readonly PadPlaybackState[] _padPlayback = Enumerable.Range(0, GmPercussionMap.Pads.Count)
        .Select(_ => new PadPlaybackState())
        .ToArray();
    private readonly ChannelDspChain[] _padDsp = Enumerable.Range(0, GmPercussionMap.Pads.Count).Select(_ => new ChannelDspChain()).ToArray();
    private readonly ChannelDspChain[] _busDsp = Enum.GetValues<PadBus>().Select(_ => new ChannelDspChain()).ToArray();
    private readonly Dictionary<DrumMixGroup, ChannelDspChain> _mixGroupDsp;
    private readonly ChannelDspChain _masterDsp = new();
    private readonly ReverbEffect _reverb = new();
    private readonly LevelMeter[] _padMeters = Enumerable.Range(0, GmPercussionMap.Pads.Count).Select(_ => new LevelMeter()).ToArray();
    private readonly Dictionary<PadBus, LevelMeter> _busMeters = Enum.GetValues<PadBus>().ToDictionary(bus => bus, _ => new LevelMeter());
    private readonly Dictionary<DrumMixGroup, LevelMeter> _mixGroupMeters;
    private readonly LevelMeter _masterMeter = new();
    private bool _anyPadSoloed;
    private bool _addedToMixer;

    public KitSamplePlayer(PlaybackEngine engine)
        : base(engine.RawEngine, engine.Format)
    {
        _engine = engine;
        Name = "Kit Sample Player";

        _padMix = GmPercussionMap.Pads.ToDictionary(
            pad => pad.Index,
            pad => new PadMixState { Bus = pad.Bus, MixGroup = GmPercussionMap.GetMixGroup(pad.Note) });

        _mixGroups = GmPercussionMap.MixGroups.ToDictionary(group => group, _ => new MixGroupState());
        _mixGroupDsp = GmPercussionMap.MixGroups.ToDictionary(group => group, _ => new ChannelDspChain());
        _mixGroupMeters = GmPercussionMap.MixGroups.ToDictionary(group => group, _ => new LevelMeter());

        _busMix = Enum.GetValues<PadBus>().ToDictionary(bus => bus, _ => new BusMixState());

        LoadEmptyGmKit();
    }

    public string? LoadedKitPath { get; private set; }

    public string KitName { get; private set; } = "GM Kit";

    public bool IsLoaded => LoadedKitPath is not null || KitName.Length > 0;

    public IReadOnlyList<bool> PadHasSample =>
        _padPlayback.Select(static pad => pad.HasAudio).ToArray();

    /// <summary>Current user-assigned MIDI note for the physical pad slot.</summary>
    public int GetPadMidiNote(int padIndex) =>
        (uint)padIndex < (uint)_padMidiNotes.Length ? _padMidiNotes[padIndex] : -1;

    /// <summary>Current user-assigned mixer output for the physical pad slot.</summary>
    public DrumMixGroup GetPadOutputGroup(int padIndex) =>
        _padMix.TryGetValue(padIndex, out var mix) ? mix.MixGroup : DrumMixGroup.Percussion;

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
        // A pad slot stays stable even when its MIDI note is reassigned. This
        // normalization also upgrades older note-addressed presets in memory.
        KitPresetCodec.EnsurePadCount(kit);

        lock (_voiceLock)
        {
            StopAllVoices();
            foreach (var state in _padPlayback)
            {
                state.Dispose();
            }

            KitName = kit.Name;
            LoadedKitPath = jsonPath;

            for (var i = 0; i < GmPercussionMap.Pads.Count; i++)
            {
                var gmPad = GmPercussionMap.Pads[i];
                var sample = kit.Pads[i];

                _padPlayback[i] = PadPlaybackState.FromSample(sample);
                _chokeGroups[i] = sample.ChokeGroup;
                _pitchSemitones[i] = sample.PitchSemitones;
                _padGains[i] = sample.Gain;
                _padEnvelopes[i] = sample.Envelope.Clone();
                _padMidiNotes[i] = sample.MidiNote is >= 0 and < 128 ? sample.MidiNote : gmPad.Note;

                var mix = _padMix[i];
                mix.MixGroup = sample.OutputGroup;
                mix.Bus = GetBusForMixGroup(sample.OutputGroup);
            }

            RebuildDrumMap();
        }

        EnsureInMixer();
    }

    private void RebuildDrumMap()
    {
        Array.Fill(_drumMap, -1);

        for (var i = 0; i < GmPercussionMap.Pads.Count; i++)
        {
            var note = _padMidiNotes[i];
            if (note is >= 0 and < 128)
            {
                _drumMap[note] = i;
            }
        }
    }

    private static PadBus GetBusForMixGroup(DrumMixGroup group) => group switch
    {
        DrumMixGroup.Kick or DrumMixGroup.Snare => PadBus.Drum,
        DrumMixGroup.HiHat or DrumMixGroup.Cymbals => PadBus.Cym,
        _ => PadBus.Perc,
    };

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

    public ChannelDspSettings GetMixGroupDsp(DrumMixGroup group) => _mixGroupDsp[group].Settings.Clone();

    public void SetMixGroupDsp(DrumMixGroup group, ChannelDspSettings settings) =>
        _mixGroupDsp[group].Settings = settings.Clone();

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

    public MixerMeterState PollMixGroupMeter(DrumMixGroup group) => TakeMeter(_mixGroupMeters[group]);

    /// <summary>Returns a held post-master reading for the UI mixer VU.</summary>
    public MixerMeterState PollMasterMeter() => TakeMeter(_masterMeter);

    private static MixerMeterState TakeMeter(LevelMeter meter)
    {
        var peak = meter.Peak;
        var rms = meter.Rms;
        // Preserve a short hold between UI polls. Resetting peak to zero caused
        // short drum transients to disappear before the 50 ms UI timer rendered.
        meter.Peak *= 0.82f;
        meter.Rms *= 0.72f;

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

    private int NoteToPadIndex(int note) => note is >= 0 and < 128 ? _drumMap[note] : -1;

    public void NoteOn(int channel, int note, int velocity) =>
        TriggerByNote(note, velocity / 127f);

    public void NoteOff(int channel, int note)
    {
        if (note is < 0 or >= 128 || _drumMap[note] < 0)
        {
            return;
        }

        lock (_voiceLock)
        {
            BeginReleaseForPad(_drumMap[note]);
        }
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
        SetPadMuteByIndex(NoteToPadIndex(note), mute);
    }

    public void SetPadSolo(int note, bool solo)
    {
        SetPadSoloByIndex(NoteToPadIndex(note), solo);
    }

    public void SetPadVolume(int note, float volume) =>
        SetPadVolumeByIndex(NoteToPadIndex(note), volume);

    public void SetPadMuteByIndex(int padIndex, bool mute)
    {
        if (_padMix.TryGetValue(padIndex, out var state))
        {
            state.IsMuted = mute;
        }
    }

    public void SetPadSoloByIndex(int padIndex, bool solo)
    {
        if (!_padMix.TryGetValue(padIndex, out var state))
        {
            return;
        }

        state.IsSoloed = solo;
        _anyPadSoloed = _padMix.Values.Any(s => s.IsSoloed);
    }

    public void SetPadVolumeByIndex(int padIndex, float volume)
    {
        if (_padMix.TryGetValue(padIndex, out var state))
        {
            state.Volume = Math.Clamp(volume, 0f, 1f);
        }
    }

    public void SetMixGroupMute(DrumMixGroup group, bool mute)
    {
        if (_mixGroups.TryGetValue(group, out var state))
        {
            state.IsMuted = mute;
        }
    }

    public void SetMixGroupSolo(DrumMixGroup group, bool solo)
    {
        if (_mixGroups.TryGetValue(group, out var state))
        {
            state.IsSoloed = solo;
            _anyPadSoloed = _padMix.Values.Any(s => s.IsSoloed) || _mixGroups.Values.Any(s => s.IsSoloed);
        }
    }

    public void SetMixGroupVolume(DrumMixGroup group, float volume)
    {
        if (_mixGroups.TryGetValue(group, out var state))
        {
            state.Volume = Math.Clamp(volume, 0f, 1.5f);
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

        var midiVelocity = Math.Clamp((int)Math.Round(velocity * 127f), 1, 127);
        if (!TryResolveMix(padIndex, velocity, out var gain))
        {
            return;
        }

        lock (_voiceLock)
        {
            if (StartVoice(padIndex, midiVelocity, gain * _padGains[padIndex]))
            {
                RegisterTriggerMeter(padIndex, gain);
            }
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

        var midiVelocity = Math.Clamp((int)Math.Round(velocity * 127f), 1, 127);
        if (!TryResolveMix(padIndex, velocity, out var gain))
        {
            return;
        }

        lock (_voiceLock)
        {
            if (StartVoice(padIndex, midiVelocity, gain * _padGains[padIndex]))
            {
                RegisterTriggerMeter(padIndex, gain);
            }
        }
    }

    private bool TryResolveMix(int padIndex, float velocity, out float gain)
    {
        gain = velocity;

        if (!_padMix.TryGetValue(padIndex, out var padState))
        {
            return false;
        }

        var busState = _busMix[padState.Bus];
        var groupState = _mixGroups[padState.MixGroup];
        var anyGroupSoloed = _mixGroups.Values.Any(static state => state.IsSoloed);
        var anyPadSoloed = _padMix.Values.Any(static state => state.IsSoloed);
        if (padState.IsMuted || groupState.IsMuted || busState.IsMuted
            || (anyGroupSoloed && !groupState.IsSoloed)
            || (anyPadSoloed && !padState.IsSoloed))
        {
            return false;
        }

        gain = velocity * padState.Volume * groupState.Volume * busState.Volume;
        return gain > 0.0001f;
    }

    private void RegisterTriggerMeter(int padIndex, float gain)
    {
        if (!_padMix.TryGetValue(padIndex, out var mix))
        {
            return;
        }

        // MIDI input reaches this point before the next audio callback. A small
        // trigger tap makes every accepted note visible in its output strip;
        // the audio-path tap below immediately replaces it with real levels.
        var level = Math.Clamp(gain * 0.7f, 0.02f, 1f);
        PushMeter(_padMeters[padIndex], level);
        PushMeter(_mixGroupMeters[mix.MixGroup], level);
        PushMeter(_masterMeter, level);
    }

    private bool StartVoice(int padIndex, int midiVelocity, float gain)
    {
        var sample = _padPlayback[padIndex].SelectSample(midiVelocity);
        if (sample is null || sample.FrameCount == 0)
        {
            return false;
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
        slot.Sample = sample;
        slot.Position = 0;
        slot.Gain = gain;
        slot.Active = true;
        slot.ChokeGroup = choke;
        slot.FadeOut = 0;
        ConfigureEnvelope(slot, _padEnvelopes[padIndex]);
        return true;
    }

    private void StopAllVoices()
    {
        foreach (var voice in _voices)
        {
            voice.Active = false;
            voice.FadeOut = 0;
            voice.Sample = null;
            voice.IsReleasing = false;
        }
    }

    private static void ConfigureEnvelope(Voice voice, PadEnvelopeSettings settings)
    {
        voice.EnvelopeFrame = 0;
        voice.AttackFrames = MillisecondsToFrames(settings.AttackMs);
        voice.DecayFrames = MillisecondsToFrames(settings.DecayMs);
        voice.ReleaseFrames = MillisecondsToFrames(settings.ReleaseMs);
        voice.ReleaseFramesRemaining = 0;
        voice.SustainLevel = Math.Clamp(settings.SustainLevel, 0f, 1f);
        voice.EnvelopeLevel = voice.AttackFrames > 0 ? 0f : 1f;
        voice.IsReleasing = false;
    }

    private static int MillisecondsToFrames(float milliseconds) =>
        Math.Clamp((int)Math.Round(Math.Max(0f, milliseconds) * WavCodec.TargetSampleRate / 1_000f), 0, WavCodec.TargetSampleRate * 10);

    private void BeginReleaseForPad(int padIndex)
    {
        foreach (var voice in _voices.Where(voice => voice.Active && voice.PadIndex == padIndex))
        {
            if (voice.ReleaseFrames <= 0)
            {
                voice.Active = false;
                continue;
            }

            voice.IsReleasing = true;
            voice.ReleaseFramesRemaining = voice.ReleaseFrames;
        }
    }

    private static float AdvanceEnvelope(Voice voice)
    {
        if (voice.IsReleasing)
        {
            if (voice.ReleaseFramesRemaining <= 0)
            {
                voice.Active = false;
                return 0f;
            }

            var level = voice.EnvelopeLevel * voice.ReleaseFramesRemaining / Math.Max(1, voice.ReleaseFrames);
            voice.ReleaseFramesRemaining--;
            if (voice.ReleaseFramesRemaining <= 0)
            {
                voice.Active = false;
            }

            return level;
        }

        var frame = voice.EnvelopeFrame++;
        if (voice.AttackFrames > 0 && frame < voice.AttackFrames)
        {
            voice.EnvelopeLevel = (frame + 1) / (float)voice.AttackFrames;
            return voice.EnvelopeLevel;
        }

        var decayFrame = frame - voice.AttackFrames;
        if (voice.DecayFrames > 0 && decayFrame < voice.DecayFrames)
        {
            voice.EnvelopeLevel = 1f + ((voice.SustainLevel - 1f) * (decayFrame + 1) / voice.DecayFrames);
            return voice.EnvelopeLevel;
        }

        voice.EnvelopeLevel = voice.SustainLevel;
        return voice.EnvelopeLevel;
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

                    var sample = voice.Sample;
                    if (sample is null || sample.FrameCount == 0)
                    {
                        voice.Active = false;
                        continue;
                    }

                    var pitchRatio = WavCodec.PitchToPlaybackRatio(_pitchSemitones[voice.PadIndex]);
                    var index = (int)voice.Position;
                    if (index >= sample.FrameCount)
                    {
                        voice.Active = false;
                        continue;
                    }

                    var fraction = (float)(voice.Position - index);
                    var first = sample.ReadFrame(index);
                    var second = index + 1 < sample.FrameCount ? sample.ReadFrame(index + 1) : first;
                    var src = (first + ((second - first) * fraction)) * voice.Gain;
                    voice.Position += pitchRatio * sample.SampleRate / WavCodec.TargetSampleRate;

                    var envelope = AdvanceEnvelope(voice);
                    if (!voice.Active && envelope <= 0f)
                    {
                        continue;
                    }

                    var framesRemaining = sample.FrameCount - index;
                    var tailRelease = voice.ReleaseFrames > 0
                        ? Math.Min(1f, framesRemaining / (float)voice.ReleaseFrames)
                        : 1f;
                    src *= envelope * tailRelease;

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

                    var padMix = _padMix[voice.PadIndex];
                    _padDsp[voice.PadIndex].Process(ref src, sampleRate);
                    reverbSend += _padDsp[voice.PadIndex].ReverbSend;

                    var mixGroup = padMix.MixGroup;
                    _mixGroupDsp[mixGroup].Process(ref src, sampleRate);
                    reverbSend += _mixGroupDsp[mixGroup].ReverbSend;
                    PushMeter(_mixGroupMeters[mixGroup], src);

                    var busIndex = (int)padMix.Bus;
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
                PushMeter(_masterMeter, (sampleL + sampleR) * 0.5f);

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
        lock (_voiceLock)
        {
            StopAllVoices();
            foreach (var state in _padPlayback)
            {
                state.Dispose();
            }
        }

        if (_addedToMixer)
        {
            _engine.MasterMixer.RemoveComponent(this);
            _addedToMixer = false;
        }
    }
}
