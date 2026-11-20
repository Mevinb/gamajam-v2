using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Parasite.FX
{
    /// <summary>Visual effects implementation behind the <see cref="Fx"/> contract.</summary>
    internal static class FxBackend
    {
        /// <summary>Last danger value passed to SetDanger (0..1). Read by ambience + runner.</summary>
        public static float Danger { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Danger = 0f;

        // ---------------- possession projectile ----------------
        public static Transform CreateProjectile()
        {
            var root = new GameObject("AmoebaProjectile");
            const float size = 0.38f;

            var shell = FxAssets.NewShell(out bool isShader);
            if (isShader)
            {
                shell.SetFloat("_InfectPulse", 0.8f);
                shell.SetFloat("_Emission", 1.3f);
                shell.SetFloat("_WobbleAmp", 0.09f);
                shell.SetFloat("_WobbleSpeed", 5f);
                shell.SetFloat("_Alpha", 0.75f);
            }
            var body = FxPrim.Make(PrimitiveType.Sphere, root.transform, Vector3.zero, Vector3.one * size, shell, "Body");
            var nucMat = FxAssets.NewNucleus(out bool nucShader);
            if (nucShader) { nucMat.SetFloat("_Intensity", 2.6f); nucMat.SetFloat("_PulseRate", 9f); }
            FxPrim.Make(PrimitiveType.Sphere, body.transform, Vector3.zero, Vector3.one * 0.45f, nucMat, "Nucleus");
            root.AddComponent<FxOwnedMaterials>().mats = new[] { shell, nucMat };
            var wob = root.AddComponent<FxProjectileWobble>();
            wob.body = body.transform;

            var l = new GameObject("Glow").AddComponent<Light>();
            l.transform.SetParent(root.transform, false);
            l.type = LightType.Point; l.color = Palette.Amoeba; l.range = 4f; l.intensity = 2.2f; l.shadows = LightShadows.None;

            // trail + sparkles live on a child so they can be detached and fade out after impact
            var fx = new GameObject("ProjectileTrail");
            fx.transform.SetParent(root.transform, false);
            var tr = fx.AddComponent<TrailRenderer>();
            tr.time = 0.38f;
            tr.minVertexDistance = 0.04f;
            tr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.34f), new Keyframe(0.4f, 0.16f), new Keyframe(1f, 0f));
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(0.75f, 1f, 0.55f), 0f), new GradientColorKey(Palette.Amoeba, 0.35f), new GradientColorKey(new Color(0.1f, 0.6f, 0.2f), 1f) },
                      new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
            tr.colorGradient = g;
            tr.sharedMaterial = FxAssets.Trail;
            tr.numCapVertices = 3;
            tr.shadowCastingMode = ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.emitting = false; // enabled on the first LateUpdate, after the caller has placed the projectile
            wob.trail = tr;

            var sp = FxParticles.Make("Sparkles", fx.transform, Vector3.zero, FxAssets.Additive);
            var m = sp.main;
            m.loop = true; m.duration = 5f;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.7f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
            m.startColor = FxParticles.TwoTone(new Color(0.8f, 1f, 0.5f, 0.9f), new Color(0.3f, 1f, 0.4f, 0.8f));
            m.gravityModifier = 0.3f;
            var em = sp.emission; em.rateOverTime = 45f;
            var sh = sp.shape; sh.radius = 0.12f;
            FxParticles.SizeCurve(sp, 1f, 0f);

            // drips falling from the flying blob
            var dr = FxParticles.Make("Drips", fx.transform, Vector3.zero, FxAssets.Alpha);
            var dm = dr.main;
            dm.loop = true; dm.duration = 5f;
            dm.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            dm.startSpeed = 0.2f;
            dm.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            dm.startColor = new Color(0.3f, 0.95f, 0.35f, 0.85f);
            dm.gravityModifier = 1.2f;
            var de = dr.emission; de.rateOverTime = 10f;
            var ds = dr.shape; ds.radius = 0.1f;
            wob.systems = new[] { sp, dr };
            return root.transform;
        }

        public static void DestroyProjectile(Transform t)
        {
            if (!t) return;
            var fx = t.Find("ProjectileTrail");
            if (fx)
            {
                fx.SetParent(null, true);
                foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>()) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                var tr = fx.GetComponent<TrailRenderer>();
                if (tr) tr.emitting = false;
                Object.Destroy(fx.gameObject, 0.9f);
            }
            Object.Destroy(t.gameObject);
        }

        // ---------------- splashes ----------------
        public static void Splat(Vector3 position) => Splash(position, 1f, false);
        public static void SplitBurst(Vector3 position) => Splash(position, 1.45f, true);

        static void Splash(Vector3 pos, float scale, bool split)
        {
            if (!Application.isPlaying) return;
            var root = new GameObject(split ? "FX_SplitBurst" : "FX_Splat");
            root.transform.position = pos;
            var light = new Color(0.55f, 1f, 0.45f, 1f);
            var dark = new Color(0.08f, 0.55f, 0.15f, 1f);
            var alt = split ? new Color(0.85f, 1f, 0.35f, 1f) : light;

            // droplets with gravity, landing on the environment
            var d = FxParticles.Make("Droplets", root.transform, Vector3.zero, FxAssets.Alpha);
            var m = d.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 1.0f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(1.6f * scale, 4.6f * scale);
            m.startSize = new ParticleSystem.MinMaxCurve(0.05f * scale, 0.14f * scale);
            m.startColor = FxParticles.TwoTone(split ? alt : light, dark);
            m.gravityModifier = 1.5f;
            var sh = d.shape; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.08f * scale;
            sh.rotation = new Vector3(-90f, 0f, 0f);
            var col = d.collision;
            col.enabled = true;
            col.type = ParticleSystemCollisionType.World;
            col.mode = ParticleSystemCollisionMode.Collision3D;
            col.quality = ParticleSystemCollisionQuality.Low;
            col.collidesWith = Layers.EnvironmentMask;
            col.dampen = 0.7f; col.bounce = 0.1f; col.lifetimeLoss = 0.25f; col.radiusScale = 0.5f;
            FxParticles.SizeCurve(d, 1f, 0.35f);
            FxParticles.FadeOut(d, 1f, 0.7f);
            FxParticles.Burst(d, split ? 55 : 30);
            d.Play();

            // a quick expanding shock ring
            Ring(root.transform, 0f, 1.7f * scale, 0.32f, new Color(0.5f, 1f, 0.45f, 0.9f));
            if (split) Ring(root.transform, 0.07f, 2.6f, 0.4f, new Color(0.85f, 1f, 0.35f, 0.75f));

            // soft mist puffs
            var mist = FxParticles.Make("Mist", root.transform, Vector3.zero, FxAssets.Additive);
            var mm = mist.main;
            mm.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            mm.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.0f * scale);
            mm.startSize = new ParticleSystem.MinMaxCurve(0.25f * scale, 0.55f * scale);
            mm.startColor = new Color(0.35f, 1f, 0.4f, 0.35f);
            FxParticles.SizeCurve(mist, 0.6f, 1.3f);
            FxParticles.FadeOut(mist, 1f, 0.2f);
            FxParticles.Burst(mist, split ? 12 : 7);
            mist.Play();

            if (split)
            {
                var pop = FxParticles.Make("Pop", root.transform, Vector3.zero, FxAssets.Additive);
                var pm = pop.main;
                pm.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
                pm.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6.5f);
                pm.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
                pm.startColor = FxParticles.TwoTone(Color.white, alt);
                FxParticles.SizeCurve(pop, 1f, 0f);
                FxParticles.Burst(pop, 24);
                pop.Play();
            }

            FxLightFlash.Spawn(pos, Palette.Amoeba, split ? 4.5f : 3f, split ? 6f : 4.5f, split ? 0.4f : 0.28f);
            Object.Destroy(root, 1.8f);
        }

        static void Ring(Transform parent, float delay, float size, float life, Color c)
        {
            var r = FxParticles.Make("Ring", parent, Vector3.zero, FxAssets.Ring, false);
            var m = r.main;
            m.startDelay = delay;
            m.startLifetime = life;
            m.startSpeed = 0f;
            m.startSize = size;
            m.startColor = c;
            var sh = r.shape; sh.enabled = false;
            FxParticles.SizeCurve(r, 0.15f, 1f);
            FxParticles.FadeOut(r, 1f, 0.3f);
            FxParticles.Burst(r, 1);
            r.Play();
        }

        // ---------------- screen effects ----------------
        public static void ScreenPulse(Color color, float strength, float duration)
        {
            var run = FxRunner.Get();
            if (run != null) run.Pulse(color, strength, duration);
        }

        public static void SetDanger(float t01)
        {
            Danger = Mathf.Clamp01(t01);
            // only spin up the runner once there is something to show
            if (Danger > 0.001f || FxRunner.Exists) FxRunner.Get();
        }
    }

    /// <summary>Stretches the flying blob along its direction of travel.</summary>
    internal class FxProjectileWobble : MonoBehaviour
    {
        public Transform body;
        public TrailRenderer trail;
        public ParticleSystem[] systems;
        Vector3 last; float stretch; bool init;
        void LateUpdate()
        {
            if (!init)
            {
                // start emitting only once the projectile is at its real start position (no streak from the origin)
                if (trail) { trail.Clear(); trail.emitting = true; }
                if (systems != null) foreach (var ps in systems) if (ps) ps.Play();
            }
            if (!body) return;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            float speed = init ? (transform.position - last).magnitude / dt : 0f;
            last = transform.position; init = true;
            stretch = Mathf.Lerp(stretch, Mathf.Clamp(speed * 0.035f, 0f, 0.6f), 1f - Mathf.Exp(-12f * dt));
            float wob = Mathf.Sin(Time.time * 22f) * 0.05f;
            float z = 1f + stretch + wob, xy = 1f / Mathf.Sqrt(z);
            body.localScale = new Vector3(xy, xy, z) * 0.38f;
        }
    }

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
            volume.weight = Mathf.Clamp01(Mathf.Max(p, d) * 4f);
        }
    }
}
