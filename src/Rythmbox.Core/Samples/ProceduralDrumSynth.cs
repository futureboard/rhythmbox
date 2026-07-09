namespace Rythmbox.Core.Samples;

/// <summary>Procedural one-shot drum samples used when a kit preset has no WAV for a pad (ported from old DrumStage).</summary>
public static class ProceduralDrumSynth
{
    public static float[] ForLabel(string label, int sampleRate = WavCodec.TargetSampleRate) =>
        label.ToUpperInvariant() switch
        {
            var u when u.Contains("KICK") => SynthKick(sampleRate),
            var u when u.Contains("SNARE") => SynthSnare(sampleRate),
            var u when u.Contains("O.HAT") || u.Contains("OPEN") => SynthHat(sampleRate, 0.35f, 0.22f),
            var u when u.Contains("HAT") => SynthHat(sampleRate, 0.08f, 0.04f),
            var u when u.Contains("CLAP") => SynthClap(sampleRate),
            var u when u.Contains("F.TOM") => SynthTom(sampleRate, 70f),
            var u when u.Contains("L.TOM") => SynthTom(sampleRate, 95f),
            var u when u.Contains("M.TOM") => SynthTom(sampleRate, 135f),
            var u when u.Contains("H.TOM") => SynthTom(sampleRate, 185f),
            var u when u.Contains("CHINA") => SynthCrash(sampleRate),
            var u when u.Contains("CYM") || u.Contains("CRASH") => SynthCrash(sampleRate),
            var u when u.Contains("RIDE") => SynthRide(sampleRate),
            var u when u.Contains("BONGO") => SynthTom(sampleRate, u.StartsWith('L') ? 110f : 170f),
            var u when u.Contains("TIMB") => SynthTom(sampleRate, u.StartsWith('L') ? 125f : 200f),
            var u when u.Contains("AGOGO") => SynthHat(sampleRate, 0.12f, 0.08f),
            var u when u.Contains("CABASA") || u.Contains("MARACA") => SynthHat(sampleRate, 0.14f, 0.1f),
            var u when u.Contains("WH") => SynthHat(sampleRate, 0.2f, 0.16f),
            var u when u.Contains("GUI") => SynthHat(sampleRate, 0.16f, 0.12f),
            var u when u.Contains("CLAVE") => SynthTom(sampleRate, 240f),
            var u when u.Contains("WOOD") => SynthTom(sampleRate, u.StartsWith('L') ? 210f : 260f),
            var u when u.Contains("CUICA") => SynthTom(sampleRate, 150f),
            var u when u.Contains("TRI") => SynthHat(sampleRate, 0.45f, 0.35f),
            var u when u.Contains("SPLASH") => SynthCrash(sampleRate),
            var u when u.Contains("VIBRA") => SynthClap(sampleRate),
            var u when u.Contains("CONGA") || u.Contains("CNGA") => SynthConga(label, sampleRate),
            var u when u.Contains("COWBELL") || u.Contains("TAMB") => SynthHat(sampleRate, 0.18f, 0.12f),
            _ => SynthHat(sampleRate, 0.08f, 0.04f),
        };

    private static float[] SynthConga(string label, int sr)
    {
        var u = label.ToUpperInvariant();
        if (u.StartsWith('L'))
        {
            return SynthTom(sr, 115f);
        }

        if (u.StartsWith("M."))
        {
            return SynthTom(sr, 150f);
        }

        if (u.StartsWith('O'))
        {
            return SynthTom(sr, 175f);
        }

        if (u.StartsWith('H'))
        {
            return SynthTom(sr, 195f);
        }

        return SynthTom(sr, 160f);
    }

    private static float[] EnvAd(int n, float attack, float decay, int sr)
    {
        var e = new float[n];
        attack = Math.Max(attack, 1e-4f);
        decay = Math.Max(decay, 1e-4f);

        for (var i = 0; i < n; i++)
        {
            var t = (float)i / sr;
            var a = Math.Clamp(t / attack, 0f, 1f);
            var d = MathF.Exp(-Math.Max(t - attack, 0f) / decay);
            e[i] = a * d;
        }

        return e;
    }

    private static float[] SynthKick(int sr)
    {
        var n = (int)(sr * 0.40f);
        var buf = new float[n];
        var e = EnvAd(n, 0.001f, 0.28f, sr);
        var rng = new Random(1);
        double phase = 0;

        for (var i = 0; i < n; i++)
        {
            var t = (float)i / sr;
            var f = 45f + (120f - 45f) * MathF.Exp(-t * 35f);
            phase += 2 * Math.PI * f / sr;
            var body = MathF.Sin((float)phase);
            var click = Frand(rng) * MathF.Exp(-t * 400f) * 0.3f;
            buf[i] = (body * e[i] + click) * 0.95f;
        }

        return buf;
    }

    private static float[] SynthSnare(int sr)
    {
        var n = (int)(sr * 0.25f);
        var buf = new float[n];
        var e1 = EnvAd(n, 0.001f, 0.10f, sr);
        var e2 = EnvAd(n, 0.001f, 0.18f, sr);
        var rng = new Random(2);

        for (var i = 0; i < n; i++)
        {
            var t = (float)i / sr;
            var tone = 0.5f * (MathF.Sin(2 * MathF.PI * 200f * t) + MathF.Sin(2 * MathF.PI * 330f * t));
            var noise = Frand(rng);
            buf[i] = (tone * e1[i] + noise * e2[i] * 0.9f) * 0.6f;
        }

        return buf;
    }

    private static float[] SynthHat(int sr, float dur, float decay)
    {
        var n = (int)(sr * dur);
        var buf = new float[n];
        var e = EnvAd(n, 0.001f, decay, sr);
        var rng = new Random(3);
        var prev = 0f;
        var prev2 = 0f;

        for (var i = 0; i < n; i++)
        {
            var x = Frand(rng);
            var d1 = x - prev;
            prev = x;
            var d2 = d1 - prev2;
            prev2 = d1;
            buf[i] = d2 * e[i] * 0.5f;
        }

        return buf;
    }

    private static float[] SynthClap(int sr)
    {
        var n = (int)(sr * 0.18f);
        var buf = new float[n];
        var e = EnvAd(n, 0.001f, 0.12f, sr);
        var rng = new Random(4);

        for (var i = 0; i < n; i++)
        {
            var burst = (i % (sr / 200)) < (sr / 2000) ? 1f : 0.3f;
            buf[i] = Frand(rng) * e[i] * burst * 0.7f;
        }

        return buf;
    }

    private static float[] SynthTom(int sr, float freq)
    {
        var n = (int)(sr * 0.35f);
        var buf = new float[n];
        var e = EnvAd(n, 0.001f, 0.22f, sr);
        double phase = 0;

        for (var i = 0; i < n; i++)
        {
            var t = (float)i / sr;
            var f = freq * MathF.Exp(-t * 8f);
            phase += 2 * Math.PI * f / sr;
            buf[i] = MathF.Sin((float)phase) * e[i] * 0.85f;
        }

        return buf;
    }

    private static float[] SynthCrash(int sr)
    {
        var n = (int)(sr * 1.2f);
        var buf = new float[n];
        var e = EnvAd(n, 0.002f, 0.55f, sr);
        var rng = new Random(5);
        var prev = 0f;

        for (var i = 0; i < n; i++)
        {
            var x = Frand(rng);
            var d = x - prev;
            prev = x;
            buf[i] = d * e[i] * 0.35f;
        }

        return buf;
    }

    private static float[] SynthRide(int sr)
    {
        var n = (int)(sr * 0.9f);
        var buf = new float[n];
        var e = EnvAd(n, 0.002f, 0.45f, sr);
        var rng = new Random(6);
        var prev = 0f;

        for (var i = 0; i < n; i++)
        {
            var t = (float)i / sr;
            var tone = MathF.Sin(2 * MathF.PI * 3200f * t) * MathF.Exp(-t * 6f) * 0.15f;
            var x = Frand(rng);
            var d = x - prev;
            prev = x;
            buf[i] = (d * 0.25f + tone) * e[i] * 0.4f;
        }

        return buf;
    }

    private static float Frand(Random rng) =>
        (float)(rng.NextDouble() * 2.0 - 1.0);
}
