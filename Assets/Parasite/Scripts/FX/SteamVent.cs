using UnityEngine;
using UnityEngine.Rendering;

namespace Parasite.FX
{
    /// <summary>
    /// Lightweight particle emitter placed on manholes and storm drains.
    /// Emits soft, rising volumetric steam puffs with random rotation,
    /// slow upward drift, and smooth fade-in/fade-out over 2-3 seconds.
    /// </summary>
    public class SteamVent : MonoBehaviour
    {
        ParticleSystem steamPs;
        float burstTimer;
        float burstInterval = 1.8f;

        static readonly Vector3[] DefaultCityVents = new[]
        {
            // South alley storm drain
            new Vector3(-33f, 0.05f, -84.5f),
            // Street manholes
            new Vector3(2.5f, 0.02f, -60f),
            new Vector3(-3f, 0.02f, 10f),
            new Vector3(40f, 0.02f, -27f),
            new Vector3(-50f, 0.02f, 47f),
            new Vector3(30f, 0.02f, 44f),
            new Vector3(-2.4f, 0.02f, -28f),
            new Vector3(-62f, 0.02f, -29f),
            new Vector3(64f, 0.02f, 47.5f),
            new Vector3(2.8f, 0.02f, 24f),
            // Curb storm grates
            new Vector3(-6.75f, 0.02f, -55f),
            new Vector3(6.75f, 0.02f, -8f),
            new Vector3(-6.75f, 0.02f, 20f),
            new Vector3(-40f, 0.02f, -33.75f),
            new Vector3(36f, 0.02f, -22.25f),
            new Vector3(-30f, 0.02f, 40.25f),
            new Vector3(44f, 0.02f, 51.75f)
        };

        public static SteamVent Create(Vector3 worldPos, Transform parent = null)
        {
            var go = new GameObject("SteamVent");
            if (parent) go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            return go.AddComponent<SteamVent>();
        }

        public static void EnsureCityVents()
        {
            // If already spawned in scene, don't duplicate
            if (FindFirstObjectByType<SteamVent>() != null) return;
            var root = new GameObject("SteamVents");
            foreach (var pos in DefaultCityVents)
            {
                Create(pos, root.transform);
            }
        }

        void Awake()
        {
            InitParticles();
        }

        void Start()
        {
            if (steamPs && !steamPs.isPlaying) steamPs.Play();
            burstInterval = Random.Range(1.5f, 2.8f);
            burstTimer = Random.Range(0f, burstInterval);
        }

        void Update()
        {
            // Organic pressure release puffs on top of constant gentle seepage
            burstTimer += Time.deltaTime;
            if (burstTimer >= burstInterval)
            {
                burstTimer = 0f;
                burstInterval = Random.Range(1.6f, 3.2f);
                if (steamPs && steamPs.isPlaying)
                {
                    steamPs.Emit(Random.Range(2, 5));
                }
            }
        }

        void InitParticles()
        {
            if (steamPs) return;
            steamPs = GetComponent<ParticleSystem>();
            if (!steamPs)
            {
                var child = new GameObject("SteamEmitter");
                child.transform.SetParent(transform, false);
                child.transform.localPosition = Vector3.zero;
                steamPs = child.AddComponent<ParticleSystem>();
            }

            steamPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = steamPs.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 4f;
            main.maxParticles = 35;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.2f); // 2-3 seconds
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.45f, 0.9f); // slow upward drift
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f); // random rotation
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.92f, 0.94f, 0.96f, 0.22f),
                new Color(0.85f, 0.88f, 0.92f, 0.16f)
            );
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.gravityModifier = -0.04f; // buoyancy lifts steam upward

            var em = steamPs.emission;
            em.rateOverTime = 7f; // steady gentle rising puffs

            var shape = steamPs.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.35f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // point upward

            // Volumetric puff expansion over lifetime
            var size = steamPs.sizeOverLifetime;
            size.enabled = true;
            var sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.6f);
            sizeCurve.AddKey(0.3f, 1.3f);
            sizeCurve.AddKey(1f, 2.6f);
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Smooth rotation drift
            var rot = steamPs.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

            // Smooth fade-in and fade-out over 2-3 seconds
            var col = steamPs.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.22f),
                    new GradientAlphaKey(0.8f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            col.color = new ParticleSystem.MinMaxGradient(g);

            // Slow upward drift & gentle turbulence
            var vel = steamPs.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.y = new ParticleSystem.MinMaxCurve(0.4f, 0.85f);
            vel.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);

            var rend = steamPs.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.sharedMaterial = FxAssets.Alpha;
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;
            rend.lightProbeUsage = LightProbeUsage.Off;
            rend.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
    }
}
