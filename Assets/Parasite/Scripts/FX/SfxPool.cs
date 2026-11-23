using System.Collections.Generic;
using UnityEngine;

namespace Parasite.FX
{
    /// <summary>
    /// Per-scene pool of one-shot AudioSources with voice limits: each tag (an SfxId or a custom tag) has a maximum
    /// number of simultaneous instances, and when the pool is full the oldest, least important voice is stolen
    /// (Unity priority: 0 = most important). 3D voices use a smooth custom roll-off that reaches silence at maxDistance.
    /// </summary>
    internal class SfxPool : MonoBehaviour
    {
        public const int Size = 32;
        public const int TagFootstep = 1000, TagVocal = 1001, TagAmbientEvent = 1002, TagHorn = 1003, TagSquish = 1004;
        static SfxPool inst;
        static bool quitting;
        AudioSource[] sources;
        float[] startTimes;
        int[] tags, priorities;
        int next;
        static readonly Dictionary<int, AnimationCurve> curves = new();

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
            tags = new int[Size];
            priorities = new int[Size];
            for (int i = 0; i < Size; i++)
            {
                var go = new GameObject("Sfx" + i);
                go.transform.SetParent(transform, false);
                var a = go.AddComponent<AudioSource>();
                a.playOnAwake = false;
                a.dopplerLevel = 0f;
                a.spread = 25f;
                sources[i] = a;
                tags[i] = -1;
            }
        }

        void OnDestroy() { if (inst == this) inst = null; }

        /// <summary>Normalised-distance roll-off: flat to min, then ~inverse falloff, silent at max.</summary>
        internal static AnimationCurve CurveFor(float minDist, float maxDist)
        {
            float r = Mathf.Clamp01(minDist / Mathf.Max(0.01f, maxDist));
            int key = Mathf.RoundToInt(r * 100f);
            if (curves.TryGetValue(key, out var c)) return c;
            r = key / 100f;
            var keys = new List<Keyframe> { new(0f, 1f) };
            if (r > 0.001f) keys.Add(new Keyframe(r, 1f));
            for (int i = 1; i <= 6; i++)
            {
                float x = Mathf.Lerp(r, 1f, i / 6f);
                float inv = r / Mathf.Max(r, x) ;                       // 1/d law relative to min distance
                float edge = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, x));
                keys.Add(new Keyframe(x, Mathf.Clamp01(Mathf.Lerp(inv, 1f - x, 0.35f)) * edge));
            }
            c = new AnimationCurve(keys.ToArray());
            for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
            curves[key] = c;
            return c;
        }

        int ActiveWithTag(int tag, out int oldest)
        {
            int count = 0; oldest = -1;
            for (int i = 0; i < Size; i++)
            {
                if (tags[i] != tag || !sources[i].isPlaying) continue;
                count++;
                if (oldest < 0 || startTimes[i] < startTimes[oldest]) oldest = i;
            }
            return count;
        }

        /// <summary>Legacy entry point (default distances, untagged).</summary>
        public void Play(AudioClip clip, Vector3 pos, float volume, float pitch, bool ui) =>
            Play(clip, pos, volume, pitch, ui, -1, ui ? 64 : 128, 2f, 35f, 8);

        public AudioSource Play(AudioClip clip, Vector3 pos, float volume, float pitch, bool ui, int tag, int priority,
            float minDist, float maxDist, int maxVoices, float reverbMix = 1f)
        {
            if (!clip || sources == null) return null;
            int slot = -1;
            if (tag >= 0 && maxVoices > 0 && ActiveWithTag(tag, out int oldestSame) >= maxVoices) slot = oldestSame;
            if (slot < 0)
                for (int i = 0; i < Size; i++)
                {
                    int j = (next + i) % Size;
                    if (!sources[j].isPlaying) { slot = j; next = (j + 1) % Size; break; }
                }
            if (slot < 0)
            {
                // steal the oldest voice among the least important ones that are not more important than this request
                int worstPriority = -1;
                for (int i = 0; i < Size; i++) worstPriority = Mathf.Max(worstPriority, priorities[i]);
                if (worstPriority < priority) return null;
                for (int i = 0; i < Size; i++)
                    if (priorities[i] == worstPriority && (slot < 0 || startTimes[i] < startTimes[slot])) slot = i;
            }
            if (slot < 0) return null;
            var a = sources[slot];
            a.Stop();
            startTimes[slot] = Time.unscaledTime;
            tags[slot] = tag;
            priorities[slot] = priority;
            a.transform.position = ui ? Vector3.zero : pos;
            a.spatialBlend = ui ? 0f : 1f;
            a.bypassListenerEffects = ui;      // UI stays crisp under the pause-menu low-pass
            a.bypassReverbZones = ui;
            a.reverbZoneMix = ui ? 0f : reverbMix;
            a.ignoreListenerPause = ui;
            a.priority = priority;
            if (!ui)
            {
                a.rolloffMode = AudioRolloffMode.Custom;
                a.minDistance = minDist;
                a.maxDistance = Mathf.Max(minDist + 0.1f, maxDist);
                a.SetCustomCurve(AudioSourceCurveType.CustomRolloff, CurveFor(minDist, maxDist));
            }
            a.clip = clip;
            a.volume = Mathf.Clamp01(volume);
            a.pitch = pitch;
            a.loop = false;
            a.mute = false;
            a.Play();
            return a;
        }
    }
}
