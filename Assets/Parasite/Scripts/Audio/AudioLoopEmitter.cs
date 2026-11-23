using Parasite.FX;
using UnityEngine;

namespace Parasite.Audio
{
    /// <summary>
    /// Positional world loop (fountain, crowd, AC unit, steam, engine, servo). Plays only while the listener is within
    /// range (saves voices), starts at a random offset, follows the ambience or effects bus, and exposes
    /// <see cref="Modulation"/>/<see cref="Pitch"/> for owners that animate it (car speed, camera sweep).
    /// </summary>
    public sealed class AudioLoopEmitter : MonoBehaviour
    {
        public enum Bus { Ambience, Effects }
        public AudioClip clip;
        public float volume = 1f, minDistance = 2f, maxDistance = 25f;
        public Bus bus = Bus.Ambience;
        public float Modulation { get; set; } = 1f;
        public float Pitch { get; set; } = 1f;
        public AudioSource Source { get; private set; }
        float checkT;
        bool inRange;

        public static AudioLoopEmitter Create(Transform parent, string name, Vector3 position, AudioClip clip, float volume,
            float minDistance, float maxDistance, Bus bus = Bus.Ambience, int priority = 160)
        {
            if (!clip) return null;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var e = go.AddComponent<AudioLoopEmitter>();
            e.Setup(clip, volume, minDistance, maxDistance, bus, priority);
            return e;
        }

        public void Setup(AudioClip c, float vol, float minDist, float maxDist, Bus b, int priority = 160)
        {
            clip = c; volume = vol; minDistance = minDist; maxDistance = maxDist; bus = b;
            if (!Source) Source = gameObject.AddComponent<AudioSource>();
            var a = Source;
            a.playOnAwake = false; a.loop = true; a.clip = c;
            a.spatialBlend = 1f; a.dopplerLevel = 0f; a.spread = 40f; a.priority = priority;
            a.rolloffMode = AudioRolloffMode.Custom;
            a.minDistance = minDist; a.maxDistance = Mathf.Max(minDist + 0.1f, maxDist);
            a.SetCustomCurve(AudioSourceCurveType.CustomRolloff, SfxPool.CurveFor(minDist, maxDist));
            a.volume = 0f;
        }

        void Update()
        {
            if (!Source || !clip) return;
            checkT -= Time.unscaledDeltaTime;
            if (checkT <= 0f)
            {
                checkT = 0.25f + Random.value * 0.1f;
                float d = (transform.position - AudioBus.ListenerPosition).sqrMagnitude;
                inRange = d < maxDistance * maxDistance * 1.1f;
                if (inRange && !Source.isPlaying)
                {
                    Source.timeSamples = Random.Range(0, Mathf.Max(1, clip.samples - 1));
                    Source.Play();
                }
                else if (!inRange && Source.isPlaying) Source.Stop();
            }
            float gain = bus == Bus.Ambience ? AudioBus.AmbienceGain : Sfx.Volume;
            Source.volume = Mathf.Clamp01(volume * Modulation * gain);
            Source.pitch = Pitch;
        }
    }
}
