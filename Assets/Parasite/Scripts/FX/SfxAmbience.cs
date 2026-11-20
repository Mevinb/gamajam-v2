using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Parasite.FX
{
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
