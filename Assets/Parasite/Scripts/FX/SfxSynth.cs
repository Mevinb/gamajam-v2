using System.Collections.Generic;
using UnityEngine;

namespace Parasite.FX
{
    /// <summary>
    /// Procedural sound synthesis: every SfxId has a generated fallback clip (44.1 kHz mono, cached), plus
    /// seamless ambience loops (22.05 kHz). Pure C#, deterministic (seeded), a few ms per one-shot.
    /// </summary>
    internal static class SfxSynth
    {
        public const int SR = 44100;
        public const int AmbSR = 22050;
        const float TAU = Mathf.PI * 2f;
        static readonly Dictionary<SfxId, AudioClip> cache = new();

        // ---------------- tiny DSP toolkit ----------------
        struct Rng
        {
            uint s;
            public Rng(uint seed) { s = seed == 0 ? 0x9E3779B9u : seed; }
            public float Next() { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return (s & 0xFFFFFF) / 8388607.5f - 1f; } // [-1,1]
            public float Range(float a, float b) => a + (Next() * 0.5f + 0.5f) * (b - a);
        }

        /// <summary>Chamberlin state-variable filter (band/low/high outputs).</summary>
        struct Svf
        {
            float low, band;
            public static float Coef(float fc, int sr) => Mathf.Min(1.2f, 2f * Mathf.Sin(Mathf.PI * Mathf.Min(fc, sr * 0.2f) / sr));
            public float Band(float x, float fc, float q, int sr) => BandC(x, Coef(fc, sr), q);
            /// <summary>Band-pass with a precomputed coefficient (see <see cref="Coef"/>).</summary>
            public float BandC(float x, float f, float q)
            {
                low += f * band;
                float high = x - low - q * band;
                band += f * high;
                return band;
            }
            public float Low => low;
        }

        static int N(float sec, int sr) => Mathf.Max(1, Mathf.RoundToInt(sec * sr));

        /// <summary>Pitch-swept sine "blob" with wobble — the core of every squelch.</summary>
        static void Blob(float[] b, int sr, float start, float dur, float f0, float f1, float amp, float wobble = 0.08f, float harm = 0.3f)
        {
            int s0 = N(start, sr), n = N(dur, sr);
            float ph = 0f;
            for (int i = 0; i < n && s0 + i < b.Length; i++)
            {
                float x = (float)i / n, t = (float)i / sr;
                float f = f0 * Mathf.Pow(f1 / f0, x) * (1f + wobble * Mathf.Sin(TAU * 31f * t));
                ph += TAU * f / sr;
                float env = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Min(1f, x * 1.15f))), 0.6f) * (1f - x * 0.5f);
                b[s0 + i] += (Mathf.Sin(ph) + harm * Mathf.Sin(ph * 2f + 0.5f)) * env * amp;
            }
        }

        /// <summary>Band-passed noise with a swept centre frequency and attack/decay envelope.</summary>
        static void NoiseBand(float[] b, int sr, float start, float dur, float fc0, float fc1, float q, float amp, float attack, ref Rng r, float decayPow = 1.5f)
        {
            int s0 = N(start, sr), n = N(dur, sr);
            var f = new Svf();
            for (int i = 0; i < n && s0 + i < b.Length; i++)
            {
                float x = (float)i / n, t = (float)i / sr;
                float fc = fc0 * Mathf.Pow(fc1 / fc0, x);
                float env = (t < attack ? t / attack : 1f) * Mathf.Pow(1f - x, decayPow);
                b[s0 + i] += f.Band(r.Next(), fc, q, sr) * env * amp;
            }
        }

        /// <summary>Little rising "blip" bubbles scattered over a time range.</summary>
        static void Bubbles(float[] b, int sr, float start, float range, int count, float fmin, float fmax, float amp, ref Rng r)
        {
            for (int k = 0; k < count; k++)
            {
                float st = start + r.Range(0f, range), d = r.Range(0.012f, 0.035f), f = r.Range(fmin, fmax);
                int s0 = N(st, sr), n = N(d, sr);
                float ph = 0f, a = amp * r.Range(0.5f, 1f);
                for (int i = 0; i < n && s0 + i < b.Length; i++)
                {
                    float x = (float)i / n;
                    ph += TAU * f * (1f + x * 0.9f) / sr;
                    b[s0 + i] += Mathf.Sin(ph) * Mathf.Exp(-x * 4f) * Mathf.Min(1f, x * 12f) * a;
                }
            }
        }

        /// <summary>Exponentially decaying pitch-dropping sine (kick / thud / heartbeat).</summary>
        static void Thump(float[] b, int sr, float start, float f0, float f1, float decay, float amp)
        {
            int s0 = N(start, sr), n = N(decay * 5f, sr);
            float ph = 0f;
            for (int i = 0; i < n && s0 + i < b.Length; i++)
            {
                float t = (float)i / sr;
                float f = f1 + (f0 - f1) * Mathf.Exp(-t / (decay * 0.5f));
                ph += TAU * f / sr;
                b[s0 + i] += Mathf.Sin(ph) * Mathf.Exp(-t / decay) * Mathf.Min(1f, t * 600f) * amp;
            }
        }

        /// <summary>Tone with shape (0 sine, 1 triangle-ish, 2 soft square, 3 buzzy saw) and AR envelope.</summary>
        static void Tone(float[] b, int sr, float start, float dur, float f0, float f1, float amp, int shape, float attack = 0.005f, float release = 0.05f, float vib = 0f)
        {
            int s0 = N(start, sr), n = N(dur, sr);
            float ph = 0f;
            for (int i = 0; i < n && s0 + i < b.Length; i++)
            {
                float t = (float)i / sr, x = (float)i / n;
                float f = Mathf.Lerp(f0, f1, x) * (1f + vib * Mathf.Sin(TAU * 5.5f * t));
                ph += TAU * f / sr;
                float s = Mathf.Sin(ph);
                if (shape == 1) s = s + Mathf.Sin(ph * 3f) * -0.11f;
                else if (shape == 2) s = (float)System.Math.Tanh(s * 3f) * 0.8f;
                else if (shape == 3) s = (Mathf.Repeat(ph / TAU, 1f) * 2f - 1f) * 0.6f + Mathf.Sin(ph) * 0.4f;
                float env = Mathf.Min(1f, t / attack) * Mathf.Min(1f, (dur - t) / release);
                b[s0 + i] += s * Mathf.Max(0f, env) * amp;
            }
        }

        /// <summary>Inharmonic metallic partials (clank).</summary>
        static void Metal(float[] b, int sr, float start, float[] freqs, float decay, float amp)
        {
            int s0 = N(start, sr), n = N(decay * 4f, sr);
            for (int k = 0; k < freqs.Length; k++)
            {
                float ph = k * 1.3f, a = amp / (1f + k * 0.6f), dk = decay / (1f + k * 0.35f);
                for (int i = 0; i < n && s0 + i < b.Length; i++)
                {
                    float t = (float)i / sr;
                    ph += TAU * freqs[k] / sr;
                    b[s0 + i] += Mathf.Sin(ph) * Mathf.Exp(-t / dk) * Mathf.Min(1f, t * 2000f) * a;
                }
            }
        }

        static float[] Finish(float[] b, float peak = 0.9f, float fadeOut = 0.02f)
        {
            float m = 1e-6f;
            for (int i = 0; i < b.Length; i++)
            {
                if (float.IsNaN(b[i]) || float.IsInfinity(b[i])) b[i] = 0f;
                float a = Mathf.Abs(b[i]); if (a > m) m = a;
            }
            float g = peak / m;
            int fo = Mathf.Min(b.Length, N(fadeOut, SR));
            for (int i = 0; i < b.Length; i++)
            {
                float v = b[i] * g;
                int fromEnd = b.Length - 1 - i;
                if (fromEnd < fo) v *= (float)fromEnd / fo;
                b[i] = v;
            }
            return b;
        }

        static AudioClip Clip(string name, float[] data, int sr)
        {
            var c = AudioClip.Create(name, data.Length, 1, sr, false);
            c.SetData(data, 0);
            c.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return c;
        }

        // ---------------- one-shots ----------------
        public static AudioClip Get(SfxId id)
        {
            if (cache.TryGetValue(id, out var c) && c) return c;
            var data = Generate(id);
            c = Clip("synth_" + id, data, SR);
            cache[id] = c;
            return c;
        }

        static float[] Generate(SfxId id)
        {
            var r = new Rng(0xC0FFEEu + (uint)id * 7919u);
            float[] b;
            switch (id)
            {
                case SfxId.PossessLaunch: // stretchy wet "schlorp" rising into flight
                    b = new float[N(0.5f, SR)];
                    Blob(b, SR, 0f, 0.42f, 160f, 620f, 0.6f, 0.12f);
                    NoiseBand(b, SR, 0f, 0.45f, 400f, 2600f, 0.35f, 0.9f, 0.03f, ref r);
                    Bubbles(b, SR, 0.02f, 0.25f, 7, 500f, 1400f, 0.35f, ref r);
                    return Finish(b);
                case SfxId.PossessHit: // splat into a body
                    b = new float[N(0.6f, SR)];
                    NoiseBand(b, SR, 0f, 0.35f, 1800f, 260f, 0.5f, 1.2f, 0.004f, ref r, 2.2f);
                    Blob(b, SR, 0f, 0.35f, 320f, 85f, 0.8f, 0.1f);
                    Thump(b, SR, 0f, 140f, 55f, 0.07f, 0.5f);
                    Bubbles(b, SR, 0.08f, 0.4f, 10, 350f, 1100f, 0.28f, ref r);
                    return Finish(b);
                case SfxId.Release: // sucking out + pop
                    b = new float[N(0.55f, SR)];
                    Blob(b, SR, 0f, 0.38f, 110f, 380f, 0.7f, 0.15f);
                    NoiseBand(b, SR, 0f, 0.4f, 300f, 1500f, 0.4f, 0.7f, 0.05f, ref r);
                    Tone(b, SR, 0.38f, 0.05f, 900f, 260f, 0.5f, 0, 0.002f, 0.03f);
                    Bubbles(b, SR, 0.3f, 0.2f, 6, 600f, 1500f, 0.3f, ref r);
                    return Finish(b);
                case SfxId.Split: // mitosis: two blobs pulling apart, then a pop
                    b = new float[N(0.8f, SR)];
                    NoiseBand(b, SR, 0f, 0.5f, 250f, 1200f, 0.3f, 0.8f, 0.08f, ref r);
                    Blob(b, SR, 0f, 0.5f, 210f, 520f, 0.55f, 0.14f);
                    Blob(b, SR, 0.03f, 0.5f, 200f, 135f, 0.55f, 0.14f);
                    Tone(b, SR, 0.5f, 0.06f, 1100f, 220f, 0.7f, 0, 0.002f, 0.04f);
                    Bubbles(b, SR, 0.5f, 0.28f, 12, 500f, 1600f, 0.3f, ref r);
                    return Finish(b);
                case SfxId.Switch: // quick double bubble
                    b = new float[N(0.28f, SR)];
                    Blob(b, SR, 0f, 0.1f, 380f, 760f, 0.6f, 0.05f);
                    Blob(b, SR, 0.1f, 0.12f, 520f, 1040f, 0.5f, 0.05f);
                    return Finish(b, 0.75f);
                case SfxId.Eject: // violent burst out of a captured body
                    b = new float[N(0.7f, SR)];
                    NoiseBand(b, SR, 0f, 0.5f, 2500f, 300f, 0.6f, 1.3f, 0.003f, ref r, 2f);
                    Blob(b, SR, 0f, 0.45f, 520f, 110f, 0.7f, 0.2f);
                    Thump(b, SR, 0f, 180f, 60f, 0.06f, 0.5f);
                    Bubbles(b, SR, 0.05f, 0.5f, 14, 400f, 1800f, 0.3f, ref r);
                    return Finish(b);
                case SfxId.BlobHop: // small boing-squelch
                    b = new float[N(0.22f, SR)];
                    Blob(b, SR, 0f, 0.18f, 210f, 430f, 0.7f, 0.06f);
                    NoiseBand(b, SR, 0f, 0.12f, 500f, 900f, 0.5f, 0.4f, 0.005f, ref r);
                    return Finish(b, 0.7f);
                case SfxId.BlobContained: // squish + electric zap
                    b = new float[N(1.0f, SR)];
                    Blob(b, SR, 0f, 0.25f, 300f, 80f, 0.7f, 0.15f);
                    NoiseBand(b, SR, 0f, 0.25f, 1200f, 300f, 0.5f, 0.8f, 0.004f, ref r);
                    {
                        int s0 = N(0.22f, SR), n = N(0.7f, SR); float ph = 0f; var f = new Svf();
                        for (int i = 0; i < n && s0 + i < b.Length; i++)
                        {
                            float t = (float)i / SR, x = (float)i / n;
                            ph += TAU * (110f + 40f * r.Next()) / SR;
                            float buzz = Mathf.Sign(Mathf.Sin(ph)) * 0.4f + f.Band(r.Next(), 4200f, 0.3f, SR) * (r.Next() > 0.6f ? 1.2f : 0.2f);
                            b[s0 + i] += buzz * Mathf.Pow(1f - x, 2f) * Mathf.Min(1f, t * 300f) * 0.6f;
                        }
                    }
                    return Finish(b);
                case SfxId.DoorOpen: // pneumatic hiss
                    b = new float[N(1.0f, SR)];
                    Thump(b, SR, 0f, 110f, 60f, 0.05f, 0.6f);
                    NoiseBand(b, SR, 0.02f, 0.95f, 6000f, 3200f, 0.9f, 0.7f, 0.03f, ref r, 1.2f);
                    NoiseBand(b, SR, 0.02f, 0.7f, 1800f, 1200f, 0.7f, 0.25f, 0.05f, ref r, 1.5f);
                    return Finish(b, 0.8f);
                case SfxId.Denied: // two-tone low buzz
                    b = new float[N(0.48f, SR)];
                    Tone(b, SR, 0f, 0.18f, 185f, 185f, 0.6f, 3, 0.004f, 0.02f);
                    Tone(b, SR, 0.22f, 0.24f, 138f, 138f, 0.6f, 3, 0.004f, 0.05f);
                    return Finish(b, 0.75f);
                case SfxId.TerminalBeep: // bright chirp
                    b = new float[N(0.26f, SR)];
                    Tone(b, SR, 0f, 0.07f, 1300f, 2500f, 0.5f, 1, 0.002f, 0.01f);
                    Tone(b, SR, 0.09f, 0.14f, 2500f, 2500f, 0.45f, 1, 0.002f, 0.08f);
                    return Finish(b, 0.65f);
                case SfxId.AlertCurious: // rising two notes "?"
                    b = new float[N(0.55f, SR)];
                    Tone(b, SR, 0f, 0.18f, 660f, 660f, 0.5f, 1, 0.01f, 0.05f);
                    Tone(b, SR, 0.2f, 0.32f, 880f, 905f, 0.5f, 1, 0.01f, 0.15f, 0.006f);
                    return Finish(b, 0.7f);
                case SfxId.AlertPursuit: // alarm stab
                    b = new float[N(0.75f, SR)];
                    foreach (var f in new[] { 440f, 523.3f, 622.3f, 880f })
                        Tone(b, SR, 0f, 0.7f, f, f * 0.97f, 0.35f, 3, 0.003f, 0.4f);
                    NoiseBand(b, SR, 0f, 0.15f, 3000f, 900f, 0.6f, 0.6f, 0.002f, ref r, 2f);
                    Thump(b, SR, 0f, 120f, 50f, 0.08f, 0.6f);
                    for (int i = 0; i < b.Length; i++) b[i] = (float)System.Math.Tanh(b[i] * 1.6f);
                    return Finish(b, 0.85f);
                case SfxId.Capture: // thud + metallic clank
                    b = new float[N(0.8f, SR)];
                    Thump(b, SR, 0f, 95f, 42f, 0.12f, 0.9f);
                    NoiseBand(b, SR, 0f, 0.2f, 600f, 150f, 0.8f, 0.6f, 0.002f, ref r, 2f);
                    Metal(b, SR, 0.04f, new[] { 523f, 1307f, 2213f, 3521f, 4410f }, 0.18f, 0.45f);
                    return Finish(b);
                case SfxId.Footstep: // soft click
                    b = new float[N(0.1f, SR)];
                    NoiseBand(b, SR, 0f, 0.06f, 1600f, 700f, 0.8f, 0.8f, 0.001f, ref r, 3f);
                    Thump(b, SR, 0f, 140f, 80f, 0.015f, 0.5f);
                    return Finish(b, 0.5f);
                case SfxId.Jump: // cloth whoosh
                    b = new float[N(0.28f, SR)];
                    NoiseBand(b, SR, 0f, 0.26f, 500f, 1600f, 0.6f, 0.9f, 0.06f, ref r, 1.5f);
                    Thump(b, SR, 0f, 120f, 70f, 0.02f, 0.3f);
                    return Finish(b, 0.5f);
                case SfxId.Land:
                    b = new float[N(0.25f, SR)];
                    Thump(b, SR, 0f, 110f, 48f, 0.05f, 0.8f);
                    NoiseBand(b, SR, 0f, 0.12f, 900f, 300f, 0.8f, 0.6f, 0.001f, ref r, 2.5f);
                    return Finish(b, 0.6f);
                case SfxId.UiClick:
                    b = new float[N(0.06f, SR)];
                    Tone(b, SR, 0f, 0.02f, 2100f, 1800f, 0.6f, 0, 0.0005f, 0.015f);
                    NoiseBand(b, SR, 0f, 0.015f, 4000f, 3000f, 0.7f, 0.3f, 0.0005f, ref r, 2f);
                    return Finish(b, 0.5f);
                case SfxId.EndingSting: // eerie low swell
                    b = new float[N(5f, SR)];
                    {
                        float[] fr = { 55f, 82.4f, 110.6f, 155.6f, 220.8f };
                        float[] ph = new float[fr.Length + 2];
                        var f = new Svf();
                        for (int i = 0; i < b.Length; i++)
                        {
                            float t = (float)i / SR;
                            float env = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 2.4f)) * Mathf.Clamp01((5f - t) / 1.8f);
                            float s = 0f;
                            for (int k = 0; k < fr.Length; k++)
                            {
                                ph[k] += TAU * fr[k] * (1f + 0.004f * Mathf.Sin(t * (0.7f + k * 0.3f))) / SR;
                                s += Mathf.Sin(ph[k]) / (1f + k * 0.5f);
                            }
                            ph[fr.Length] += TAU * 1318.5f * (1f + 0.01f * Mathf.Sin(t * 6f)) / SR;
                            ph[fr.Length + 1] += TAU * 880f / SR;
                            s += (Mathf.Sin(ph[fr.Length]) * 0.07f + Mathf.Sin(ph[fr.Length + 1]) * 0.05f) * (0.5f + 0.5f * Mathf.Sin(t * 3.1f)) * Mathf.Clamp01(t - 1f);
                            s += f.Band(r.Next(), 300f + 900f * env, 0.6f, SR) * 0.35f * env;
                            b[i] = s * env;
                        }
                    }
                    return Finish(b, 0.8f, 0.3f);
                case SfxId.Heartbeat: // one "lub-dub" per second (looped; pitch speeds it up)
                    b = new float[N(1f, SR)];
                    Thump(b, SR, 0.01f, 70f, 38f, 0.07f, 1f);
                    Thump(b, SR, 0.29f, 62f, 34f, 0.06f, 0.75f);
                    NoiseBand(b, SR, 0.01f, 0.06f, 200f, 90f, 0.7f, 0.4f, 0.002f, ref r, 2f);
                    return Finish(b, 0.9f, 0.05f);
                default:
                    b = new float[N(0.1f, SR)];
                    Tone(b, SR, 0f, 0.08f, 800f, 600f, 0.5f, 0);
                    return Finish(b, 0.5f);
            }
        }

        // ---------------- ambience loops (seamless via crossfade) ----------------
        const float LoopSec = 24f, FadeSec = 1.5f;

        static AudioClip Loopify(string name, float[] raw, float gain)
        {
            int loop = N(LoopSec, AmbSR), fade = N(FadeSec, AmbSR);
            var o = new float[loop];
            float m = 1e-6f;
            for (int i = 0; i < raw.Length; i++) m = Mathf.Max(m, Mathf.Abs(raw[i]));
            float g = gain / m;
            for (int i = 0; i < loop; i++)
            {
                float v = raw[i];
                if (i < fade)
                {
                    float x = (float)i / fade;
                    v = raw[i] * Mathf.Sin(x * Mathf.PI * 0.5f) + raw[loop + i] * Mathf.Cos(x * Mathf.PI * 0.5f);
                }
                o[i] = v * g;
            }
            return Clip(name, o, AmbSR);
        }

        static float[] AmbBuffer() => new float[N(LoopSec + FadeSec, AmbSR)];

        /// <summary>City bed: low rumble, traffic swells, distant siren, birds, a far horn.</summary>
        public static AudioClip CityBed()
        {
            const int sr = AmbSR;
            var b = AmbBuffer();
            var r = new Rng(424242u);
            float brown = 0f, lp = 0f, lp2 = 0f;
            float a1 = 1f - Mathf.Exp(-TAU * 160f / sr), a2 = 1f - Mathf.Exp(-TAU * 60f / sr);
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / sr;
                brown = Mathf.Clamp(brown * 0.997f + r.Next() * 0.05f, -1f, 1f);
                lp += a1 * (brown - lp);
                lp2 += a2 * (r.Next() - lp2);
                b[i] = lp * (0.75f + 0.25f * Mathf.Sin(TAU * t / 12f)) * 0.9f + lp2 * 0.6f;
            }
            // traffic: cars passing (band noise swells with a doppler-ish downward sweep)
            for (int k = 0; k < 9; k++)
            {
                float st = r.Range(0f, LoopSec), d = r.Range(3f, 6.5f), fc = r.Range(450f, 900f);
                int s0 = N(st, sr), n = N(d, sr);
                var f = new Svf(); float amp = r.Range(0.12f, 0.3f), coef = 0f, env = 0f;
                for (int i = 0; i < n && s0 + i < b.Length; i++)
                {
                    if ((i & 31) == 0)
                    {
                        float x = (float)i / n;
                        float sx = Mathf.Sin(Mathf.PI * x);
                        env = sx * sx * amp;
                        coef = Svf.Coef(fc * Mathf.Lerp(1.15f, 0.8f, x), sr);
                    }
                    b[s0 + i] += f.BandC(r.Next(), coef, 0.5f) * env;
                }
            }
            // distant siren (wail), quiet
            {
                float st = r.Range(4f, 12f), d = 7f; int s0 = N(st, sr), n = N(d, sr); float ph = 0f;
                for (int i = 0; i < n && s0 + i < b.Length; i++)
                {
                    float t = (float)i / sr, x = (float)i / n;
                    ph += TAU * (640f + 210f * Mathf.Sin(TAU * 0.42f * t)) / sr;
                    b[s0 + i] += (Mathf.Sin(ph) + 0.25f * Mathf.Sin(ph * 2f)) * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * x)), 1.5f) * 0.035f;
                }
            }
            // far car horn
            Tone(b, sr, r.Range(14f, 20f), 0.35f, 392f, 392f, 0.025f, 2, 0.02f, 0.08f);
            Tone(b, sr, r.Range(14f, 20f), 0.3f, 466f, 466f, 0.02f, 2, 0.02f, 0.08f);
            // birds: little chirp phrases
            for (int k = 0; k < 7; k++)
            {
                float st = r.Range(0f, LoopSec - 1f); int notes = (int)r.Range(2f, 5.99f);
                float f0 = r.Range(2600f, 3800f);
                for (int j = 0; j < notes; j++)
                    Tone(b, sr, st + j * r.Range(0.07f, 0.12f), r.Range(0.04f, 0.07f), f0, f0 * r.Range(1.1f, 1.4f), 0.03f, 0, 0.005f, 0.02f);
            }
            return Loopify("amb_city", b, 0.8f);
        }

        /// <summary>Synth drone bed. tense=false: calm A-minor pad; tense=true: beating minor-2nd + tritone, tremolo, shimmer.</summary>
        public static AudioClip Drone(bool tense)
        {
            const int sr = AmbSR;
            var b = AmbBuffer();
            var r = new Rng(tense ? 777u : 555u);
            float[] fr = tense ? new[] { 55f, 58.27f, 77.78f, 116.54f, 155.56f } : new[] { 55f, 82.41f, 110.3f, 164.8f, 220f };
            int nk = fr.Length;
            var ph = new float[nk];
            var inc = new float[nk];   // control-rate (every 64 samples) phase increments + amplitudes
            var amp = new float[nk];
            float shim = 0f, shimInc = 0f, nlp = 0f, an = 1f - Mathf.Exp(-TAU * (tense ? 700f : 350f) / sr);
            float noiseAmp = 0f, trem = 1f, shimAmp = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / sr, s = 0f;
                if ((i & 63) == 0)
                {
                    for (int k = 0; k < nk; k++)
                    {
                        inc[k] = TAU * fr[k] * (1f + 0.003f * Mathf.Sin(t * (0.21f + k * 0.13f))) / sr;
                        amp[k] = (0.6f + 0.4f * Mathf.Sin(TAU * t / (6f + k * 2f) + k)) / (1f + k * 0.45f);
                    }
                    noiseAmp = (tense ? 1.2f : 0.6f) * (0.5f + 0.5f * Mathf.Sin(TAU * t / 8f));
                    if (tense)
                    {
                        float a = Mathf.Abs(Mathf.Sin(Mathf.PI * 1.1f * t)); a *= a;
                        trem = 0.65f + 0.35f * a * a;
                        shimInc = TAU * 1244.5f * (1f + 0.012f * Mathf.Sin(t * 7f)) / sr;
                        shimAmp = 0.05f * (0.5f + 0.5f * Mathf.Sin(TAU * t / 3f));
                    }
                }
                for (int k = 0; k < nk; k++)
                {
                    ph[k] += inc[k];
                    if (ph[k] > TAU * 64f) ph[k] -= TAU * 64f;
                    s += (Mathf.Sin(ph[k]) + 0.18f * Mathf.Sin(ph[k] * 2f)) * amp[k];
                }
                nlp += an * (r.Next() - nlp);
                s += nlp * noiseAmp;
                if (tense)
                {
                    shim += shimInc;
                    if (shim > TAU * 64f) shim -= TAU * 64f;
                    s = s * trem + Mathf.Sin(shim) * shimAmp;
                }
                b[i] = s;
            }
            return Loopify(tense ? "amb_drone_tense" : "amb_drone_calm", b, 0.7f);
        }
    }
}
