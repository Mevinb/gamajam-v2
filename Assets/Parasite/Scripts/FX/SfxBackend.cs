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

    /// <summary>Per-scene pool of AudioSources (created lazily; destroyed with the scene).</summary>
    internal class SfxPool : MonoBehaviour
    {
        const int Size = 20;
        static SfxPool inst;
        static bool quitting;
        AudioSource[] sources;
        float[] startTimes;
        int next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { inst = null; quitting = false; Application.quitting -= OnQuit; Application.quitting += OnQuit; }
        static void OnQuit() => quitting = true;

        public static SfxPool Get()
        {
            if (inst != null) return inst;
            if (quitting || !Application.isPlaying) return null;
            inst = new GameObject("[SFX Pool]").AddComponent<SfxPool>();
            return inst;
        }

        void Awake()
        {
            sources = new AudioSource[Size];
            startTimes = new float[Size];
            for (int i = 0; i < Size; i++)
            {
                var go = new GameObject("Sfx" + i);
                go.transform.SetParent(transform, false);
                var a = go.AddComponent<AudioSource>();
                a.playOnAwake = false;
                a.rolloffMode = AudioRolloffMode.Linear;
                a.minDistance = 2f;
                a.maxDistance = 35f;
                a.dopplerLevel = 0f;
                a.spread = 30f;
                sources[i] = a;
            }
        }

        void OnDestroy() { if (inst == this) inst = null; }

        public void Play(AudioClip clip, Vector3 pos, float volume, float pitch, bool ui)
        {
            if (!clip) return;
            AudioSource a = null;
            for (int i = 0; i < Size; i++)
            {
                int j = (next + i) % Size;
                if (!sources[j].isPlaying) { a = sources[j]; next = (j + 1) % Size; break; }
            }
            if (a == null)
            {
                // steal the oldest voice
                int oldest = 0;
                for (int i = 1; i < Size; i++) if (startTimes[i] < startTimes[oldest]) oldest = i;
                a = sources[oldest];
            }
            int idx = System.Array.IndexOf(sources, a);
            startTimes[idx] = Time.unscaledTime;
            a.transform.position = ui ? Vector3.zero : pos;
            a.spatialBlend = ui ? 0f : 1f;
            a.priority = ui ? 64 : 128;
            a.clip = clip;
            a.volume = volume;
            a.pitch = pitch;
            a.Play();
        }
    }

    /// <summary>Looping 2D city ambience + synth drone that tenses with danger. Generated procedurally at start.</summary>
    internal class SfxAmbience : MonoBehaviour
    {
        public static SfxAmbience Instance { get; private set; }
        AudioSource city, calm, tense;
        AudioLowPassFilter tenseLp;
        float fadeIn, danger;

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        AudioSource MakeSource(string n, AudioClip clip)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var a = go.AddComponent<AudioSource>();
            a.clip = clip; a.loop = true; a.spatialBlend = 0f; a.playOnAwake = false; a.volume = 0f; a.priority = 32;
            // random start offset so restarts don't sound identical
            a.timeSamples = Random.Range(0, Mathf.Max(1, clip.samples - 1));
            a.Play();
            return a;
        }

        IEnumerator Start()
        {
            yield return null;
            city = MakeSource("City", SfxSynth.CityBed());
            yield return null;
            calm = MakeSource("DroneCalm", SfxSynth.Drone(false));
            yield return null;
            tense = MakeSource("DroneTense", SfxSynth.Drone(true));
            tenseLp = tense.gameObject.AddComponent<AudioLowPassFilter>();
            tenseLp.cutoffFrequency = 600f;
            yield return SfxBackend.Warmup();
        }

        void Update()
        {
            fadeIn = Mathf.MoveTowards(fadeIn, 1f, Time.unscaledDeltaTime / 4f);
            danger = Mathf.Lerp(danger, FxBackend.Danger, 1f - Mathf.Exp(-1.5f * Time.unscaledDeltaTime));
            float v = Sfx.AmbienceVolume * Sfx.Volume * fadeIn;
            if (city) city.volume = v * 0.42f * (1f - danger * 0.25f);
            if (calm) calm.volume = v * 0.16f * (1f - danger * 0.75f);
            if (tense) tense.volume = v * 0.3f * Mathf.Pow(danger, 1.3f);
            if (tenseLp) tenseLp.cutoffFrequency = Mathf.Lerp(500f, 5000f, danger);
        }
    }
}
