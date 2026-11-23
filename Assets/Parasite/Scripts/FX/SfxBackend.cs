using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Parasite.FX
{
    /// <summary>
    /// Sound playback. Lookup order per id: generated/processed variants in Resources/Audio/Sfx (name_1..name_N),
    /// then the original Kenney CC0 clips in Resources/Audio (optionally layered over the synth), then the procedural
    /// <see cref="SfxSynth"/> fallback. Variants rotate without immediate repeats, with random pitch/volume.
    /// 3D one-shots use <see cref="SfxPool"/> (voice limits + priorities); Ui() plays 2D and bypasses the pause low-pass.
    /// </summary>
    internal static class SfxBackend
    {
        enum Mode { SynthOnly, PreferKenney, Layer }

        readonly struct Spec
        {
            public readonly string asset, kenney;
            public readonly Mode mode;
            public readonly float vol, kenneyVol, pitchVar, volVar, minDist, maxDist;
            public readonly int priority, maxVoices;
            public readonly bool forceUi;
            public Spec(string asset, string kenney, Mode mode, float vol = 1f, float pitchVar = 0.06f, float volVar = 0.12f,
                int priority = 128, int maxVoices = 3, float minDist = 2f, float maxDist = 35f, bool forceUi = false, float kenneyVol = 1f)
            {
                this.asset = asset; this.kenney = kenney; this.mode = mode; this.vol = vol; this.pitchVar = pitchVar; this.volVar = volVar;
                this.priority = priority; this.maxVoices = maxVoices; this.minDist = minDist; this.maxDist = maxDist;
                this.forceUi = forceUi; this.kenneyVol = kenneyVol;
            }
        }

        static Spec SpecFor(SfxId id) => id switch
        {
            SfxId.PossessLaunch => new Spec("possess_launch", null, Mode.SynthOnly, 0.95f, 0.05f, 0.08f, 40, 2, 2f, 40f),
            SfxId.PossessHit => new Spec("possess_hit", "possess_hit", Mode.Layer, 1f, 0.05f, 0.08f, 40, 2, 2f, 40f, kenneyVol: 0.6f),
            SfxId.Release => new Spec("release", null, Mode.SynthOnly, 0.9f, 0.06f, 0.1f, 60, 2, 2f, 30f),
            SfxId.Split => new Spec("split", null, Mode.SynthOnly, 1f, 0.04f, 0.05f, 40, 2, 2f, 35f),
            SfxId.Switch => new Spec("switch", "switch", Mode.Layer, 0.8f, 0.03f, 0.05f, 50, 2, kenneyVol: 0.5f),
            SfxId.DoorOpen => new Spec("door_open", "door_clunk", Mode.Layer, 0.9f, 0.03f, 0.05f, 70, 2, 2f, 30f, kenneyVol: 0.55f),
            SfxId.DoorClose => new Spec("door_close", "door_clunk", Mode.PreferKenney, 0.85f, 0.03f, 0.05f, 80, 2, 2f, 28f, kenneyVol: 0.45f),
            SfxId.Denied => new Spec("denied", "denied", Mode.PreferKenney, 0.85f, 0.01f, 0.03f, 50, 2, kenneyVol: 0.9f),
            SfxId.TerminalBeep => new Spec("terminal_beep", "terminal_beep", Mode.PreferKenney, 0.75f, 0.01f, 0.05f, 70, 3, kenneyVol: 0.8f),
            SfxId.AlertCurious => new Spec("alert_curious", "alert_curious", Mode.PreferKenney, 0.85f, 0.01f, 0.03f, 30, 2, 3f, 45f, kenneyVol: 0.9f),
            SfxId.AlertPursuit => new Spec("alert_pursuit", null, Mode.SynthOnly, 0.9f, 0.01f, 0.03f, 20, 2, 3f, 50f),
            SfxId.Capture => new Spec("capture", "capture_clank", Mode.Layer, 1f, 0.04f, 0.05f, 20, 2, 2f, 40f, kenneyVol: 0.75f),
            SfxId.Eject => new Spec("eject", null, Mode.SynthOnly, 1f, 0.05f, 0.06f, 30, 2, 2f, 40f),
            SfxId.BlobContained => new Spec("contained", "contained_zap", Mode.Layer, 1f, 0.03f, 0.05f, 20, 2, 2f, 40f, kenneyVol: 0.6f),
            SfxId.Footstep => new Spec("fs_concrete", "footstep", Mode.PreferKenney, 0.8f, 0.08f, 0.2f, 170, 8, 1f, 22f, kenneyVol: 0.8f),
            SfxId.Jump => new Spec("jump", null, Mode.SynthOnly, 0.8f, 0.08f, 0.15f, 140, 2, 1f, 22f),
            SfxId.Land => new Spec("land", "land", Mode.PreferKenney, 0.9f, 0.06f, 0.15f, 140, 3, 1f, 22f, kenneyVol: 0.9f),
            SfxId.UiClick => new Spec("ui_click", "ui_click", Mode.PreferKenney, 0.7f, 0.03f, 0.05f, 10, 2, kenneyVol: 0.8f, forceUi: true),
            SfxId.EndingSting => new Spec("ending_sting", null, Mode.SynthOnly, 0.9f, 0f, 0f, 5, 1, forceUi: true),
            SfxId.Heartbeat => new Spec("heartbeat", null, Mode.SynthOnly, 0.9f, 0f, 0f, 30, 2, forceUi: true),
            SfxId.BlobHop => new Spec("blob_hop", null, Mode.SynthOnly, 0.85f, 0.07f, 0.12f, 110, 3, 1f, 25f),
            SfxId.AlarmSiren => new Spec("alarm_siren", null, Mode.SynthOnly, 0.9f, 0.01f, 0.03f, 25, 2, 6f, 80f),
            SfxId.RainAmbience => new Spec(null, null, Mode.SynthOnly, 0.8f, 0.02f, 0.05f, 200, 2),
            SfxId.BioSquelch => new Spec("squelch", null, Mode.SynthOnly, 1f, 0.06f, 0.1f, 60, 3, 1.5f, 30f),
            SfxId.LaserHum => new Spec(null, null, Mode.SynthOnly, 0.9f, 0.02f, 0.05f, 90, 2),
            SfxId.UiHover => new Spec("ui_hover", null, Mode.SynthOnly, 0.45f, 0.04f, 0.08f, 12, 2, forceUi: true),
            SfxId.UiOpen => new Spec("ui_open", null, Mode.SynthOnly, 0.7f, 0.02f, 0.03f, 10, 1, forceUi: true),
            SfxId.UiClose => new Spec("ui_close", null, Mode.SynthOnly, 0.7f, 0.02f, 0.03f, 10, 1, forceUi: true),
            SfxId.UiBack => new Spec("ui_back", null, Mode.SynthOnly, 0.6f, 0.02f, 0.03f, 10, 1, forceUi: true),
            SfxId.ObjectiveUpdate => new Spec("objective", null, Mode.SynthOnly, 0.7f, 0f, 0f, 8, 1, forceUi: true),
            SfxId.Checkpoint => new Spec("checkpoint", null, Mode.SynthOnly, 0.7f, 0f, 0f, 8, 1, forceUi: true),
            SfxId.MissionComplete => new Spec("../Music/stinger_mission", null, Mode.SynthOnly, 0.85f, 0f, 0f, 5, 1, forceUi: true),
            SfxId.BlobLand => new Spec("blob_land", "land", Mode.SynthOnly, 0.85f, 0.08f, 0.12f, 110, 3, 1f, 25f),
            SfxId.BlobSquish => new Spec("blob_squish", null, Mode.SynthOnly, 0.6f, 0.1f, 0.25f, 160, 4, 1f, 18f),
            SfxId.CarHorn => new Spec("car_horn", null, Mode.SynthOnly, 0.9f, 0.03f, 0.1f, 90, 2, 5f, 80f),
            SfxId.RadioSquelch => new Spec("radio_squelch", null, Mode.SynthOnly, 0.6f, 0.05f, 0.1f, 80, 2, 1f, 18f),
            SfxId.Drip => new Spec("drip", null, Mode.SynthOnly, 0.6f, 0.12f, 0.35f, 190, 4, 1f, 16f),
            SfxId.LabBeep => new Spec("lab_beep", null, Mode.SynthOnly, 0.5f, 0.0f, 0.2f, 180, 3, 1f, 16f),
            SfxId.ThunderDistant => new Spec("thunder", null, Mode.SynthOnly, 0.8f, 0.05f, 0.25f, 150, 1, forceUi: true),
            SfxId.DogBark => new Spec("dog_bark", null, Mode.SynthOnly, 0.8f, 0.06f, 0.2f, 190, 1, 10f, 110f),
            SfxId.SirenDistant => new Spec("siren_distant", null, Mode.SynthOnly, 0.8f, 0.03f, 0.2f, 190, 1, 25f, 220f),
            SfxId.Whoosh => new Spec("whoosh", null, Mode.SynthOnly, 0.8f, 0.08f, 0.1f, 90, 3, 1f, 25f),
            _ => new Spec(null, null, Mode.SynthOnly),
        };

        static readonly Dictionary<string, AudioClip[]> variantCache = new();
        static readonly Dictionary<string, int> lastVariant = new();

        /// <summary>All clips "Audio/{path}" plus consecutive "Audio/{path}_1", "_2", … (cached; null when none exist).</summary>
        public static AudioClip[] Variants(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (variantCache.TryGetValue(path, out var arr)) return arr;
            var list = new List<AudioClip>();
            var single = Resources.Load<AudioClip>("Audio/" + path);
            if (single) list.Add(single);
            for (int i = 1; i <= 64; i++) { var c = Resources.Load<AudioClip>($"Audio/{path}_{i}"); if (c) list.Add(c); else if (i > 1) break; }
            arr = list.Count > 0 ? list.ToArray() : null;
            variantCache[path] = arr;
            return arr;
        }

        /// <summary>Random variant without immediately repeating the previous pick for the same set.</summary>
        public static AudioClip Pick(AudioClip[] set, string key)
        {
            if (set == null || set.Length == 0) return null;
            if (set.Length == 1) return set[0];
            lastVariant.TryGetValue(key, out int last);
            int i = Random.Range(0, set.Length - 1);
            if (i >= last) i++;
            lastVariant[key] = i;
            return set[i];
        }

        static string AssetPath(Spec s) => s.asset == null ? null : s.asset.StartsWith("../") ? s.asset.Substring(3) : "Sfx/" + s.asset;
        static AudioClip[] Asset(Spec s) => Variants(AssetPath(s));
        static AudioClip[] Kenney(Spec s) => Variants(s.kenney);

        /// <summary>The clip used for an id (generated asset, else Kenney if preferred, else synth). Used for loops.</summary>
        public static AudioClip ClipFor(SfxId id)
        {
            var s = SpecFor(id);
            var a = Asset(s);
            if (a != null) return a[0];
            var k = s.mode == Mode.PreferKenney ? Kenney(s) : null;
            return k != null ? Pick(k, s.kenney) : SfxSynth.Get(id);
        }

        public static void Play(SfxId id, Vector3 position, float volume, bool ui)
        {
            if (!Application.isPlaying || volume <= 0f || Sfx.Volume <= 0f) return;
            // Surface-aware footsteps for every visible host come from Audio.Footsteps; ignore the legacy call.
            if (id == SfxId.Footstep && Audio.Footsteps.Active) return;
            var pool = SfxPool.Get();
            if (pool == null) return;
            var s = SpecFor(id);
            if (s.forceUi) ui = true;
            float pitch = 1f + Random.Range(-s.pitchVar, s.pitchVar);
            float vol = Mathf.Clamp01(volume) * Sfx.Volume * (1f - Random.Range(0f, s.volVar));
            var asset = Asset(s);
            if (asset != null)
            {
                pool.Play(Pick(asset, s.asset), position, vol * s.vol, pitch, ui, (int)id, s.priority, s.minDist, s.maxDist, s.maxVoices);
                return;
            }
            var k = s.mode != Mode.SynthOnly ? Kenney(s) : null;
            if (k != null)
            {
                pool.Play(Pick(k, s.kenney), position, vol * s.kenneyVol, pitch, ui, (int)id, s.priority, s.minDist, s.maxDist, s.maxVoices);
                if (s.mode == Mode.PreferKenney) return;
                vol *= 0.85f;
            }
            pool.Play(SfxSynth.Get(id), position, vol, pitch, ui, (int)id + 500, s.priority, s.minDist, s.maxDist, s.maxVoices);
        }

        /// <summary>Play an arbitrary clip through the pool on the effects bus (used by footsteps, vocals, ambience events).</summary>
        public static AudioSource PlayClip(AudioClip clip, Vector3 position, float volume, float pitch, int tag, int priority,
            float minDist, float maxDist, int maxVoices, bool ui = false, float reverbMix = 1f)
        {
            if (!Application.isPlaying || !clip || volume <= 0f) return null;
            var pool = SfxPool.Get();
            return pool == null ? null : pool.Play(clip, position, volume, pitch, ui, tag, priority, minDist, maxDist, maxVoices, reverbMix);
        }

        public static void StartAmbience()
        {
            if (!Application.isPlaying) return;
            if (SfxAmbience.Instance != null) return;
            var go = new GameObject("[Ambience]");
            go.AddComponent<SfxAmbience>();
        }

        /// <summary>Load clips/generate the remaining synth one-shots in the background (one id per frame).</summary>
        public static IEnumerator Warmup()
        {
            foreach (SfxId id in System.Enum.GetValues(typeof(SfxId)))
            {
                var s = SpecFor(id);
                if (Asset(s) == null) { SfxSynth.Get(id); Kenney(s); }
                yield return null;
            }
        }
    }
}
