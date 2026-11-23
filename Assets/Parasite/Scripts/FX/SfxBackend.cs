using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Parasite.FX
{
    /// <summary>
    /// Sound playback. Kenney CC0 clips from Resources/Audio are preferred (or layered over the synth for
    /// richer hits); every id falls back to a procedural clip from <see cref="SfxSynth"/>.
    /// 3D one-shots use a per-scene pool of AudioSources; Ui() plays 2D.
    /// </summary>
    internal static class SfxBackend
    {
        enum Mode { SynthOnly, PreferKenney, Layer }

        readonly struct Spec
        {
            public readonly string kenney; public readonly Mode mode; public readonly float kenneyVol, pitchVar;
            public Spec(string k, Mode m, float kv = 1f, float pv = 0.06f) { kenney = k; mode = m; kenneyVol = kv; pitchVar = pv; }
        }

        static Spec SpecFor(SfxId id) => id switch
        {
            SfxId.Footstep => new Spec("footstep", Mode.PreferKenney, 0.8f, 0.08f),
            SfxId.Land => new Spec("land", Mode.PreferKenney, 0.9f),
            SfxId.Capture => new Spec("capture_clank", Mode.Layer, 0.75f, 0.04f),
            SfxId.PossessHit => new Spec("possess_hit", Mode.Layer, 0.6f),
            SfxId.DoorOpen => new Spec("door_clunk", Mode.Layer, 0.55f, 0.03f),
            SfxId.UiClick => new Spec("ui_click", Mode.PreferKenney, 0.8f, 0.03f),
            SfxId.Switch => new Spec("switch", Mode.Layer, 0.5f, 0.03f),
            SfxId.Denied => new Spec("denied", Mode.PreferKenney, 0.9f, 0.01f),
            SfxId.TerminalBeep => new Spec("terminal_beep", Mode.PreferKenney, 0.8f, 0.01f),
            SfxId.AlertCurious => new Spec("alert_curious", Mode.PreferKenney, 0.9f, 0.01f),
            SfxId.BlobContained => new Spec("contained_zap", Mode.Layer, 0.6f, 0.03f),
            SfxId.EndingSting => new Spec(null, Mode.SynthOnly, 1f, 0f),
            SfxId.Heartbeat => new Spec(null, Mode.SynthOnly, 1f, 0f),
            SfxId.AlertPursuit => new Spec(null, Mode.SynthOnly, 1f, 0.01f),
            SfxId.AlarmSiren => new Spec(null, Mode.SynthOnly, 1f, 0.01f),
            SfxId.RainAmbience => new Spec(null, Mode.SynthOnly, 0.8f, 0.02f),
            SfxId.BioSquelch => new Spec(null, Mode.SynthOnly, 1f, 0.05f),
            SfxId.LaserHum => new Spec(null, Mode.SynthOnly, 0.9f, 0.02f),
            _ => new Spec(null, Mode.SynthOnly),
        };

        static readonly Dictionary<string, AudioClip[]> kenneyCache = new();

        static AudioClip[] Kenney(string baseName)
        {
            if (string.IsNullOrEmpty(baseName)) return null;
            if (kenneyCache.TryGetValue(baseName, out var arr)) return arr;
            var list = new List<AudioClip>();
            var single = Resources.Load<AudioClip>("Audio/" + baseName);
            if (single) list.Add(single);
            for (int i = 1; i <= 8; i++) { var c = Resources.Load<AudioClip>($"Audio/{baseName}_{i}"); if (c) list.Add(c); }
            arr = list.Count > 0 ? list.ToArray() : null;
            kenneyCache[baseName] = arr;
            return arr;
        }

        /// <summary>The clip used for an id (Kenney if preferred and present, else synth). Used for loops.</summary>
        public static AudioClip ClipFor(SfxId id)
        {
            var s = SpecFor(id);
            var k = s.mode == Mode.PreferKenney ? Kenney(s.kenney) : null;
            return k != null ? k[Random.Range(0, k.Length)] : SfxSynth.Get(id);
        }

        public static void Play(SfxId id, Vector3 position, float volume, bool ui)
        {
            if (!Application.isPlaying || volume <= 0f || Sfx.Volume <= 0f) return;
            var pool = SfxPool.Get();
            if (pool == null) return;
            if (id == SfxId.EndingSting) ui = true; // a musical sting must always be heard
            var s = SpecFor(id);
            var k = s.mode != Mode.SynthOnly ? Kenney(s.kenney) : null;
            float pitch = 1f + Random.Range(-s.pitchVar, s.pitchVar);
            float vol = Mathf.Clamp01(volume) * Sfx.Volume;
            if (k != null)
            {
                pool.Play(k[Random.Range(0, k.Length)], position, vol * s.kenneyVol, pitch, ui);
                if (s.mode == Mode.PreferKenney) return;
                vol *= 0.85f;
            }
            pool.Play(SfxSynth.Get(id), position, vol, pitch, ui);
        }

        public static void StartAmbience()
        {
            if (!Application.isPlaying) return;
            if (SfxAmbience.Instance != null) return;
            var go = new GameObject("[Ambience]");
            go.AddComponent<SfxAmbience>();
        }

        /// <summary>Generate the remaining synth one-shots in the background (one per frame).</summary>
        public static IEnumerator Warmup()
        {
            foreach (SfxId id in System.Enum.GetValues(typeof(SfxId)))
            {
                SfxSynth.Get(id);
                Kenney(SpecFor(id).kenney);
                yield return null;
            }
        }
    }
}
