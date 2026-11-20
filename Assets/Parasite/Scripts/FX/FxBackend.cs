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
}
