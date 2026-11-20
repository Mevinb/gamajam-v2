using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Parasite.FX
{
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
}
