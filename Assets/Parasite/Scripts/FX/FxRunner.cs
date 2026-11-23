using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Parasite.FX
{
    /// <summary>
    /// Per-scene runner: global post-processing Volume (vignette, chromatic aberration, colour filter) for
    /// ScreenPulse + danger, and the heartbeat loop. Created lazily; a scene reload destroys it.
    /// </summary>
    internal class FxRunner : MonoBehaviour
    {
        static FxRunner inst;
        static bool quitting;

        Volume volume;
        VolumeProfile profile;
        Vignette vignette;
        ChromaticAberration chroma;
        ColorAdjustments color;
        LensDistortion lens;
        Bloom bloom;
        AudioSource heart;

        Color pulseColor = Color.white;
        float pulseStrength, pulseStart = -10f, pulseDuration = 0.6f, danger, camCheckT;

        static readonly Color DangerRed = new(0.75f, 0.02f, 0.02f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { inst = null; quitting = false; Application.quitting -= OnQuit; Application.quitting += OnQuit; }
        static void OnQuit() => quitting = true;

        public static bool Exists => inst != null;

        public static FxRunner Get()
        {
            if (inst != null) return inst;
            if (quitting || !Application.isPlaying) return null;
            inst = new GameObject("[FX Runner]").AddComponent<FxRunner>();
            return inst;
        }

        void OnDestroy()
        {
            if (inst == this) inst = null;
            if (profile) Destroy(profile);
        }

        public void Pulse(Color c, float strength, float duration)
        {
            float now = Time.unscaledTime;
            // keep a stronger pulse that is still near its peak
            if (PulseEnvelope(now) * pulseStrength > strength * 0.8f) return;
            pulseColor = c; pulseStrength = Mathf.Clamp(strength, 0f, 2f); pulseDuration = Mathf.Max(0.1f, duration); pulseStart = now;
            EnsureVolume();
        }

        float PulseEnvelope(float now)
        {
            float x = (now - pulseStart) / pulseDuration;
            if (x < 0f || x >= 1f) return 0f;
            const float rise = 0.12f;
            return x < rise ? Mathf.SmoothStep(0f, 1f, x / rise) : Mathf.Pow(1f - (x - rise) / (1f - rise), 2f);
        }

        void EnsureVolume()
        {
            if (volume) return;
            var go = new GameObject("[FX Volume]");
            go.transform.SetParent(transform, false);
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 50f;
            volume.weight = 0f;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "FX Runtime Profile";
            // only the parameters we drive are overridden so the scene's own grading is untouched
            vignette = profile.Add<Vignette>(false);
            vignette.intensity.Override(0f);
            vignette.color.Override(DangerRed);
            vignette.smoothness.Override(0.5f);
            chroma = profile.Add<ChromaticAberration>(false);
            chroma.intensity.Override(0f);
            color = profile.Add<ColorAdjustments>(false);
            color.colorFilter.Override(Color.white);
            // a short lens "gulp" + bloom swell sells possession/capture hits
            lens = profile.Add<LensDistortion>(false);
            lens.intensity.Override(0f);
            lens.scale.Override(1f);
            bloom = profile.Add<Bloom>(false);
            bloom.intensity.Override(0.95f);
            volume.sharedProfile = profile;
            EnsureCameraPost();
        }

        static void EnsureCameraPost()
        {
            var cam = Camera.main;
            if (!cam) return;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data && !data.renderPostProcessing) data.renderPostProcessing = true;
        }

        void EnsureHeart()
        {
            if (heart) return;
            heart = gameObject.AddComponent<AudioSource>();
            heart.clip = SfxBackend.ClipFor(SfxId.Heartbeat);
            heart.loop = true; heart.spatialBlend = 0f; heart.playOnAwake = false; heart.volume = 0f; heart.priority = 40;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime, now = Time.unscaledTime;
            danger = Mathf.Lerp(danger, FxBackend.Danger, 1f - Mathf.Exp(-4f * dt));
            float d = danger;

            // heartbeat loop above 0.6 danger
            float hb = Mathf.InverseLerp(0.6f, 1f, d);
            float beat = 0f;
            if (hb > 0.001f)
            {
                EnsureHeart();
                if (heart.clip && !heart.isPlaying) heart.Play();
                heart.volume = hb * 0.75f * Sfx.Volume;
                heart.pitch = 1f + 0.55f * hb;
                if (heart.clip)
                {
                    float ph = (float)heart.timeSamples / heart.clip.frequency;
                    beat = Mathf.Exp(-ph * 12f) + (ph > 0.29f ? 0.7f * Mathf.Exp(-(ph - 0.29f) * 12f) : 0f);
                }
            }
            else if (heart && heart.isPlaying) { heart.volume = Mathf.MoveTowards(heart.volume, 0f, dt); if (heart.volume <= 0f) heart.Stop(); }

            float p = PulseEnvelope(now) * pulseStrength;
            if (p <= 0.001f && d <= 0.001f) { if (volume) volume.weight = 0f; return; }
            EnsureVolume();
            if (!volume) return;

            if ((camCheckT -= dt) <= 0f) { camCheckT = 1f; EnsureCameraPost(); }

            float vigD = d < 0.5f ? Mathf.Lerp(0f, 0.14f, d / 0.5f) : Mathf.Lerp(0.14f, 0.46f, (d - 0.5f) / 0.5f);
            vigD += beat * 0.07f * hb;
            float vigP = 0.45f * p;
            float vi = Mathf.Max(vigD, vigP);
            float pw = vigP / Mathf.Max(1e-4f, vigD + vigP);
            vignette.intensity.value = Mathf.Clamp01(vi);
            vignette.color.value = Color.Lerp(DangerRed, pulseColor * 0.8f, pw);
            chroma.intensity.value = Mathf.Clamp01(Mathf.Max(p * 0.75f, Mathf.InverseLerp(0.5f, 1f, d) * 0.22f + beat * 0.12f * hb));
            var filter = Color.Lerp(Color.white, Color.Lerp(Color.white, pulseColor, 0.55f), Mathf.Clamp01(p * 0.6f));
            filter = Color.Lerp(filter, new Color(1f, 0.82f, 0.82f), Mathf.InverseLerp(0.7f, 1f, d) * 0.45f);
            color.colorFilter.value = filter;
            float env = p > 0f ? Mathf.Sin(Mathf.Clamp01((now - pulseStart) / pulseDuration) * Mathf.PI) : 0f;
            lens.intensity.value = -0.28f * env * Mathf.Min(1f, pulseStrength);
            lens.scale.value = 1f + 0.04f * env;
            bloom.intensity.value = 0.95f + 1.4f * p;
            volume.weight = Mathf.Clamp01(Mathf.Max(p, d) * 4f);
        }
    }
}
