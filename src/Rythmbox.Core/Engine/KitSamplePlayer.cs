using Rythmbox.Core.Audio;
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

    private readonly PlaybackEngine _engine;
    private readonly object _voiceLock = new();
    private readonly Voice[] _voices = Enumerable.Range(0, MaxVoices).Select(_ => new Voice()).ToArray();
    private readonly Dictionary<int, PadMixState> _padMix;
    private readonly Dictionary<DrumMixGroup, MixGroupState> _mixGroups;
    private readonly Dictionary<PadBus, BusMixState> _busMix;
    private int[][] _padMidiNotes = GmPercussionMap.Pads.Select(pad => new[] { pad.Note }).ToArray();
    private int[][] _noteToPads = CreateEmptyNoteIndex();
    private readonly int[] _activeVoiceCounts = new int[GmPercussionMap.Pads.Count];
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
    private readonly RealtimeLevelMeter[] _padMeters = Enumerable.Range(0, GmPercussionMap.Pads.Count).Select(_ => new RealtimeLevelMeter()).ToArray();
    private readonly Dictionary<PadBus, RealtimeLevelMeter> _busMeters = Enum.GetValues<PadBus>().ToDictionary(bus => bus, _ => new RealtimeLevelMeter());
    private readonly Dictionary<DrumMixGroup, RealtimeLevelMeter> _mixGroupMeters;
    private bool _anyPadSoloed;
    private bool _anyMixGroupSoloed;
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
        _mixGroupMeters = GmPercussionMap.MixGroups.ToDictionary(group => group, _ => new RealtimeLevelMeter());

        _busMix = Enum.GetValues<PadBus>().ToDictionary(bus => bus, _ => new BusMixState());

        LoadEmptyGmKit();
    }

    public string? LoadedKitPath { get; private set; }

    public string KitName { get; private set; } = "GM Kit";

    public bool IsLoaded => LoadedKitPath is not null || KitName.Length > 0;

    /// <summary>
    /// True only when this component is attached to the current device's
    /// MasterMixer. This is graph membership, not a claim that the current
    /// block contains audible samples; the debug trace proves that separately.
    /// </summary>
    public bool HasMasterMixerRoute => IsAttachedToCurrentMasterMixer();

    public IReadOnlyList<bool> PadHasSample =>
        _padPlayback.Select(static pad => pad.HasAudio).ToArray();

    /// <summary>Current user-assigned MIDI note for the physical pad slot.</summary>
    public int GetPadMidiNote(int padIndex) =>
        GetPadMidiNotes(padIndex).FirstOrDefault(-1);

    /// <summary>All assigned MIDI notes for a physical pad slot.</summary>
    public IReadOnlyList<int> GetPadMidiNotes(int padIndex)
    {
        var notesByPad = Volatile.Read(ref _padMidiNotes);
        return (uint)padIndex < (uint)notesByPad.Length ? notesByPad[padIndex] : Array.Empty<int>();
    }

    /// <summary>Current active audio voice count, published lock-free for UI runtime state.</summary>
    public int GetActiveVoiceCount(int padIndex) =>
        (uint)padIndex < (uint)_activeVoiceCounts.Length ? Volatile.Read(ref _activeVoiceCounts[padIndex]) : 0;

    /// <summary>Raised when a real pad voice starts: pad index, source MIDI note (-1 for direct UI), velocity.</summary>
    public event Action<int, int, int>? PadTriggered;

    /// <summary>Raised when a note-off releases every pad mapped to that MIDI note.</summary>
    public event Action<int, int>? PadNoteReleased;

    /// <summary>Raised when all pad note holds have been cleared.</summary>
    public event Action? AllPadNotesReleased;

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
                var resolvedNotes = sample.ResolveMidiNotes();
                _padMidiNotes[i] = resolvedNotes.Length > 0
                    ? NormalizeMidiNotes(resolvedNotes)
                    : sample.MidiNote is >= 0 and < 128 ? [sample.MidiNote] : [gmPad.Note];

                var mix = _padMix[i];
                mix.MixGroup = sample.OutputGroup;
                mix.Bus = GetBusForMixGroup(sample.OutputGroup);
            }

            RebuildNoteToPadIndex();
        }

        EnsureInMixer();
        AllPadNotesReleased?.Invoke();
    }

    private void RebuildNoteToPadIndex(int[][]? sourceNotesByPad = null)
    {
        var buckets = Enumerable.Range(0, 128).Select(_ => new List<int>()).ToArray();
        var notesByPad = sourceNotesByPad ?? Volatile.Read(ref _padMidiNotes);
        for (var i = 0; i < notesByPad.Length; i++)
        {
            foreach (var note in notesByPad[i])
            {
                buckets[note].Add(i);
            }
        }

        Volatile.Write(ref _noteToPads, buckets.Select(static bucket => bucket.ToArray()).ToArray());
    }

    private static int[][] CreateEmptyNoteIndex() => Enumerable.Range(0, 128).Select(_ => Array.Empty<int>()).ToArray();

    private static int[] NormalizeMidiNotes(IEnumerable<int> notes) => notes
        .Where(static note => note is >= 0 and <= 127)
        .Distinct()
        .OrderBy(static note => note)
        .ToArray();

    private static PadBus GetBusForMixGroup(DrumMixGroup group) => group switch
    {
        DrumMixGroup.Kick or DrumMixGroup.Snare => PadBus.Drum,
        DrumMixGroup.HiHat or DrumMixGroup.Cymbals => PadBus.Cym,
        _ => PadBus.Perc,
    };

    public void ReattachToMixer()
    {
        // Device switches create a fresh MasterMixer. The previous mixer may
        // already be disposed, so just mark this component detached and add it
        // to the current graph.
        _addedToMixer = false;
        EnsureInMixer();
    }

    private void EnsureInMixer()
    {
        if (IsAttachedToCurrentMasterMixer())
        {
            _addedToMixer = true;
            return;
        }

        _engine.MasterMixer.AddComponent(this);
        _addedToMixer = true;

#if DEBUG
        if (!IsAttachedToCurrentMasterMixer())
        {
            throw new InvalidOperationException("Kit Sample Player was not attached to the current MasterMixer.");
        }
#endif
    }

    private bool IsAttachedToCurrentMasterMixer()
    {
        if (!_engine.IsRunning)
        {
            return false;
        }

        var masterMixer = _engine.MasterMixer;
        return ReferenceEquals(Parent, masterMixer)
            && masterMixer.Components.Any(component => ReferenceEquals(component, this));
    }

    public ChannelDspSettings GetPadDsp(int note)
    {
        var padIndex = GetPadIndicesForNote(note).FirstOrDefault(-1);
        return padIndex >= 0 ? _padDsp[padIndex].Settings.Clone() : new ChannelDspSettings();
    }

    public void SetPadDsp(int note, ChannelDspSettings settings)
    {
        var padIndex = GetPadIndicesForNote(note).FirstOrDefault(-1);
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
        var padIndex = GetPadIndicesForNote(note).FirstOrDefault(-1);
        if (padIndex < 0)
        {
            return MixerMeterState.Disabled;
        }

        return _padMeters[padIndex].Poll();
    }

    /// <summary>Non-consuming actual sample meter read used by the Debug pad inspector.</summary>
    public MixerMeterState PeekPadMeter(int padIndex) =>
        (uint)padIndex < (uint)_padMeters.Length ? _padMeters[padIndex].Peek() : MixerMeterState.Disabled;

    public AudioGraphTraceSnapshot GetAudioGraphTraceSnapshot() => _engine.GetAudioGraphTraceSnapshot();

    public MixerMeterState PollBusMeter(PadBus bus) => _busMeters[bus].Poll();

    /// <summary>
    /// Post-fader group meter. The tap is fed only from the rendered audio
    /// samples after pad/group gain, mute, solo, and group DSP.
    /// </summary>
    public MixerMeterState PollMixGroupMeter(DrumMixGroup group) => _mixGroupMeters[group].Poll();

    private IReadOnlyList<int> GetPadIndicesForNote(int note)
    {
        var index = Volatile.Read(ref _noteToPads);
        return note is >= 0 and < 128 ? index[note] : Array.Empty<int>();
    }

    public void NoteOn(int channel, int note, int velocity)
    {
        if (velocity <= 0)
        {
            NoteOff(channel, note);
            return;
        }

        TriggerByNote(note, velocity / 127f);
    }

    public void NoteOff(int channel, int note)
    {
        var padIndices = GetPadIndicesForNote(note);
        if (padIndices.Count == 0)
        {
            return;
        }

        foreach (var padIndex in padIndices)
        {
            ReleasePad(padIndex, note);
        }
    }

    /// <summary>Releases voices for one physical pad slot without changing the audio device.</summary>
    public void ReleasePad(int padIndex, int sourceNote = -1)
    {
        if ((uint)padIndex >= (uint)_padPlayback.Length)
        {
            return;
        }

        lock (_voiceLock)
        {
            BeginReleaseForPad(padIndex);
        }

        PadNoteReleased?.Invoke(padIndex, sourceNote);
    }

    public void AllNotesOff()
    {
        lock (_voiceLock)
        {
            StopAllVoices();
        }

        AllPadNotesReleased?.Invoke();
    }

    public void SetPadMute(int note, bool mute)
    {
        foreach (var padIndex in GetPadIndicesForNote(note))
        {
            SetPadMuteByIndex(padIndex, mute);
        }
    }

    public void SetPadSolo(int note, bool solo)
    {
        foreach (var padIndex in GetPadIndicesForNote(note))
        {
            SetPadSoloByIndex(padIndex, solo);
        }
    }

    public void SetPadVolume(int note, float volume)
    {
        foreach (var padIndex in GetPadIndicesForNote(note))
        {
            SetPadVolumeByIndex(padIndex, volume);
        }
    }

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

    public void SetPadMidiNoteByIndex(int padIndex, int midiNote)
        => SetPadMidiNotesByIndex(padIndex, [midiNote]);

    /// <summary>Updates the live note-to-pad index without rebuilding the audio device or kit.</summary>
    public void SetPadMidiNotesByIndex(int padIndex, IEnumerable<int> midiNotes)
    {
        var notesByPad = Volatile.Read(ref _padMidiNotes);
        if ((uint)padIndex >= (uint)notesByPad.Length)
        {
            return;
        }

        lock (_voiceLock)
        {
            var updatedNotesByPad = (int[][])notesByPad.Clone();
            updatedNotesByPad[padIndex] = NormalizeMidiNotes(midiNotes);
            Volatile.Write(ref _padMidiNotes, updatedNotesByPad);
            RebuildNoteToPadIndex(updatedNotesByPad);
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
            _anyMixGroupSoloed = _mixGroups.Values.Any(s => s.IsSoloed);
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

    public void TriggerPad(int padIndex, float velocity = 1f, int sourceNote = -1)
    {
        if (padIndex < 0 || padIndex >= GmPercussionMap.Pads.Count)
        {
            return;
        }

        TriggerPadByIndex(padIndex, velocity, sourceNote);
    }

    public void TriggerNote(int note, float velocity = 1f) => TriggerByNote(note, velocity);

    public void ProcessMidiMessage(MidiMessage message)
    {
        if (message.Command == MidiCommand.ControlChange)
        {
            if (message.ControllerNumber is 120 or 123) // All Sound Off / All Notes Off
            {
                AllNotesOff();
            }

            return;
        }

        if (message.Command == MidiCommand.NoteOn)
        {
            if (message.Velocity <= 0)
            {
                NoteOff(message.Channel, message.NoteNumber);
                return;
            }

            NoteOn(message.Channel, message.NoteNumber, message.Velocity);
            return;
        }

        if (message.Command == MidiCommand.NoteOff)
        {
            NoteOff(message.Channel, message.NoteNumber);
        }
    }

    private void TriggerByNote(int note, float velocity)
    {
        foreach (var padIndex in GetPadIndicesForNote(note))
        {
            TriggerPadByIndex(padIndex, velocity, note);
        }
    }

    private void TriggerPadByIndex(int padIndex, float velocity, int sourceNote)
    {
        var midiVelocity = Math.Clamp((int)Math.Round(velocity * 127f), 1, 127);
        if (!TryResolveMix(padIndex, velocity, out var gain))
        {
            return;
        }

        bool started;
        lock (_voiceLock)
        {
            started = StartVoice(padIndex, midiVelocity, gain * _padGains[padIndex]);
        }

        if (started)
        {
            PadTriggered?.Invoke(padIndex, sourceNote, midiVelocity);
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
        if (padState.IsMuted || groupState.IsMuted || busState.IsMuted
            || (_anyMixGroupSoloed && !groupState.IsSoloed)
            || (_anyPadSoloed && !padState.IsSoloed))
        {
            return false;
        }

        gain = velocity;
        return gain > 0.0001f;
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

        for (var i = 0; i < _activeVoiceCounts.Length; i++)
        {
            Volatile.Write(ref _activeVoiceCounts[i], 0);
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
            Span<float> busScratch = stackalloc float[3];
            Span<float> padPeaks = stackalloc float[GmPercussionMap.Pads.Count];
            Span<float> padSumSquares = stackalloc float[GmPercussionMap.Pads.Count];
            Span<int> padSampleCounts = stackalloc int[GmPercussionMap.Pads.Count];
            Span<float> groupPeaks = stackalloc float[GmPercussionMap.MixGroups.Count];
            Span<float> groupSumSquares = stackalloc float[GmPercussionMap.MixGroups.Count];
            Span<int> groupSampleCounts = stackalloc int[GmPercussionMap.MixGroups.Count];
            Span<float> busPeaks = stackalloc float[3];
            Span<float> busSumSquares = stackalloc float[3];
            Span<int> busSampleCounts = stackalloc int[3];
            var sourcePeak = 0f;
            var sourcePadIndex = -1;
            var sourceMidiNote = -1;
            var mixerInputPeak = 0f;
            var mixerOutputPeak = 0f;
            var masterInputPeak = 0f;
            var masterOutputPeak = 0f;

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

                    // Source tap: the voice has rendered a real sample but has
                    // not yet entered pad/group/bus mixing or mute/solo gates.
                    var sourceAbsolute = MathF.Abs(src);
                    if (sourceAbsolute > sourcePeak)
                    {
                        sourcePeak = sourceAbsolute;
                        sourcePadIndex = voice.PadIndex;
                        sourceMidiNote = GetPadMidiNote(voice.PadIndex);
                    }
                    var padMix = _padMix[voice.PadIndex];
                    var groupState = _mixGroups[padMix.MixGroup];
                    var busState = _busMix[padMix.Bus];
                    if (padMix.IsMuted || groupState.IsMuted || busState.IsMuted
                        || (_anyMixGroupSoloed && !groupState.IsSoloed)
                        || (_anyPadSoloed && !padMix.IsSoloed))
                    {
                        continue;
                    }

                    src *= padMix.Volume * groupState.Volume * busState.Volume;
                    if (MathF.Abs(src) <= 0.000001f)
                    {
                        continue;
                    }

                    mixerInputPeak = MathF.Max(mixerInputPeak, MathF.Abs(src));
                    _padDsp[voice.PadIndex].Process(ref src, sampleRate);
                    reverbSend += _padDsp[voice.PadIndex].ReverbSend;

                    var padIndex = voice.PadIndex;
                    var padAbsolute = MathF.Abs(src);
                    padPeaks[padIndex] = MathF.Max(padPeaks[padIndex], padAbsolute);
                    padSumSquares[padIndex] += src * src;
                    padSampleCounts[padIndex]++;

                    var mixGroup = padMix.MixGroup;
                    _mixGroupDsp[mixGroup].Process(ref src, sampleRate);
                    reverbSend += _mixGroupDsp[mixGroup].ReverbSend;

                    var groupIndex = (int)mixGroup;
                    var groupAbsolute = MathF.Abs(src);
                    mixerOutputPeak = MathF.Max(mixerOutputPeak, groupAbsolute);
                    groupPeaks[groupIndex] = MathF.Max(groupPeaks[groupIndex], groupAbsolute);
                    groupSumSquares[groupIndex] += src * src;
                    groupSampleCounts[groupIndex]++;

                    var busIndex = (int)padMix.Bus;
                    busScratch[busIndex] += src;
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
                    var busAbsolute = MathF.Abs(busSample);
                    busPeaks[busIndex] = MathF.Max(busPeaks[busIndex], busAbsolute);
                    busSumSquares[busIndex] += busSample * busSample;
                    busSampleCounts[busIndex]++;

                    sampleL += busSample;
                    sampleR += busSample;
                }

                var wet = _reverb.Process(reverbSend);
                sampleL += wet;
                sampleR += wet;

                masterInputPeak = MathF.Max(masterInputPeak, MathF.Max(MathF.Abs(sampleL), MathF.Abs(sampleR)));
                var masterSample = (sampleL + sampleR) * 0.5f;
                _masterDsp.Process(ref masterSample, sampleRate);
                sampleL = masterSample;
                sampleR = masterSample;

                sampleL = MathF.Tanh(sampleL);
                sampleR = MathF.Tanh(sampleR);
                masterOutputPeak = MathF.Max(masterOutputPeak, MathF.Max(MathF.Abs(sampleL), MathF.Abs(sampleR)));

                var baseIndex = frame * channels;
                outputBuffer[baseIndex] += sampleL;
                if (channels > 1)
                {
                    outputBuffer[baseIndex + 1] += sampleR;
                }
            }

            Span<int> activeVoices = stackalloc int[GmPercussionMap.Pads.Count];
            foreach (var voice in _voices)
            {
                if (voice.Active && (uint)voice.PadIndex < (uint)activeVoices.Length)
                {
                    activeVoices[voice.PadIndex]++;
                }
            }

            for (var i = 0; i < activeVoices.Length; i++)
            {
                Volatile.Write(ref _activeVoiceCounts[i], activeVoices[i]);
            }

            for (var i = 0; i < padSampleCounts.Length; i++)
            {
                if (padSampleCounts[i] == 0)
                {
                    continue;
                }

                _padMeters[i].RecordBlock(padPeaks[i], MathF.Sqrt(padSumSquares[i] / padSampleCounts[i]));
                _engine.AudioGraphTrace.RecordMeterTap(padPeaks[i]);
            }

            for (var i = 0; i < groupSampleCounts.Length; i++)
            {
                var peak = groupPeaks[i];
                _engine.AudioGraphTrace.RecordChannelPeak(i, peak);
                if (groupSampleCounts[i] == 0)
                {
                    continue;
                }

                _mixGroupMeters[(DrumMixGroup)i].RecordBlock(peak, MathF.Sqrt(groupSumSquares[i] / groupSampleCounts[i]));
                _engine.AudioGraphTrace.RecordMeterTap(peak);
            }

            for (var i = 0; i < busSampleCounts.Length; i++)
            {
                if (busSampleCounts[i] > 0)
                {
                    _busMeters[(PadBus)i].RecordBlock(busPeaks[i], MathF.Sqrt(busSumSquares[i] / busSampleCounts[i]));
                }
            }

            _engine.AudioGraphTrace.RecordSource(sourcePadIndex, sourceMidiNote, sourcePeak);
            _engine.AudioGraphTrace.RecordMixerInput(mixerInputPeak);
            _engine.AudioGraphTrace.RecordMixerOutput(mixerOutputPeak);
            _engine.AudioGraphTrace.RecordMasterInput(masterInputPeak);
            _engine.AudioGraphTrace.RecordMasterOutput(masterOutputPeak);
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

        if (_addedToMixer && IsAttachedToCurrentMasterMixer())
        {
            _engine.MasterMixer.RemoveComponent(this);
        }

        _addedToMixer = false;
    }
}
