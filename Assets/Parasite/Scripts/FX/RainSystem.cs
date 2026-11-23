using Parasite.Possession;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Parasite.FX
{
    /// <summary>
    /// Procedural camera-following rain and ambient mist particle system.
    /// Spawns 1,000-1,500 stretched velocity billboard raindrops falling at a slight angle,
    /// with ground collision kill and an ambient low-lying mist layer.
    /// Automatically tracks PlayerCamera / MainCamera across scene loads and camera switches.
    /// </summary>
    public class RainSystem : MonoBehaviour
    {
        public static RainSystem Instance { get; private set; }

        Camera targetCamera;
        ParticleSystem rainPs;
        ParticleSystem mistPs;
        Transform mistTransform;
        Vector3 lastCamPos;

        const int MaxRainParticles = 1450;
        /// <summary>Rain strength fed to the wet-street shader (ripple rings in puddles), 0..1.</summary>
        public const float Intensity = 0.65f;
        static readonly int RainId = Shader.PropertyToID("_ParasiteRain");
        const float RainRate = 720f; // ~720/s * ~1.8s lifetime ≈ 1,300 active particles

        public static RainSystem Ensure()
        {
            if (Instance) return Instance;
            var existing = FindFirstObjectByType<RainSystem>();
            if (existing) return existing;
            var go = new GameObject("RainSystem");
            return go.AddComponent<RainSystem>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoInit() => Ensure();

        void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            BuildRain();
            BuildMist();
            Shader.SetGlobalFloat(RainId, Intensity);
            FindTargetCamera();

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) { Instance = null; Shader.SetGlobalFloat(RainId, 0f); }
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            FindTargetCamera();
            if (rainPs && !rainPs.isPlaying) rainPs.Play();
            if (mistPs && !mistPs.isPlaying) mistPs.Play();
        }

        void Start()
        {
            FindTargetCamera();
            if (rainPs && !rainPs.isPlaying) rainPs.Play();
            if (mistPs && !mistPs.isPlaying) mistPs.Play();
        }

        void FindTargetCamera()
        {
            if (PlayerCamera.Instance && PlayerCamera.Instance.Cam && PlayerCamera.Instance.Cam.isActiveAndEnabled)
                targetCamera = PlayerCamera.Instance.Cam;
            else if (Camera.main && Camera.main.isActiveAndEnabled)
                targetCamera = Camera.main;
            else
            {
                var cam = FindFirstObjectByType<Camera>();
                if (cam && cam.isActiveAndEnabled) targetCamera = cam;
            }

            if (targetCamera) lastCamPos = targetCamera.transform.position;
        }

        void LateUpdate()
        {
            if (!targetCamera || !targetCamera.isActiveAndEnabled)
            {
                FindTargetCamera();
                if (!targetCamera) return;
            }

            Vector3 camPos = targetCamera.transform.position;
            // Detect sudden camera teleport (scene load / cutscene / reset)
            if ((camPos - lastCamPos).sqrMagnitude > 400f)
            {
                if (rainPs) rainPs.Clear();
                if (mistPs) mistPs.Clear();
            }
            lastCamPos = camPos;

            // Position rain emitter box above camera, slightly tilted and biased forward
            Vector3 camFwd = targetCamera.transform.forward;
            camFwd.y = 0f;
            if (camFwd.sqrMagnitude > 0.01f) camFwd.Normalize();
            else camFwd = Vector3.forward;

            transform.position = camPos + camFwd * 5f + Vector3.up * 13f;

            // Mist stays at low ground level near camera
            if (mistTransform)
            {
                mistTransform.position = new Vector3(camPos.x, Mathf.Max(0.5f, camPos.y - 1.5f), camPos.z);
            }
        }

        void BuildRain()
        {
            var rainGo = new GameObject("RainEmitter");
            rainGo.transform.SetParent(transform, false);

            rainPs = rainGo.AddComponent<ParticleSystem>();
            rainPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = rainPs.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 5f;
            main.maxParticles = MaxRainParticles;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(22f, 28f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
            // Soft blue-grey tint
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.68f, 0.78f, 0.90f, 0.38f),
                new Color(0.55f, 0.68f, 0.82f, 0.28f)
            );
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.gravityModifier = 0.5f;

            var em = rainPs.emission;
            em.rateOverTime = RainRate;

            var shape = rainPs.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(34f, 1f, 34f);
            // Slight angle (falling tilted by ~7 degrees forward & slight side drift)
            shape.rotation = new Vector3(83f, 12f, 0f);

            var col = rainPs.collision;
            col.enabled = true;
            col.type = ParticleSystemCollisionType.World;
            col.mode = ParticleSystemCollisionMode.Collision3D;
            col.collidesWith = 1 << 0; // Default layer (environment/floors)
            col.lifetimeLoss = 1.0f; // Collision kill
            col.bounce = 0f;
            col.dampen = 1f;
            col.quality = ParticleSystemCollisionQuality.Low;
            col.maxCollisionShapes = 64;

            var rend = rainGo.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.cameraVelocityScale = 0f;
            rend.velocityScale = 0.042f;
            rend.lengthScale = 1.35f;
            rend.sharedMaterial = FxAssets.Alpha;
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;
            rend.lightProbeUsage = LightProbeUsage.Off;
            rend.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        void BuildMist()
        {
            var mistGo = new GameObject("AmbientMist");
            mistGo.transform.SetParent(transform, false);
            mistTransform = mistGo.transform;

            mistPs = mistGo.AddComponent<ParticleSystem>();
            mistPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = mistPs.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 6f;
            main.maxParticles = 80;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4.5f, 6.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            // Soft subtle mist in blue-grey
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.62f, 0.74f, 0.85f, 0.045f),
                new Color(0.52f, 0.64f, 0.78f, 0.035f)
            );
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var em = mistPs.emission;
            em.rateOverTime = 12f;

            var shape = mistPs.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(36f, 1.5f, 36f);

            var rot = mistPs.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);

            var vel = mistPs.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.1f, 0.35f);
            vel.z = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            vel.y = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);

            var col = mistPs.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.25f),
                    new GradientAlphaKey(1f, 0.75f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            col.color = new ParticleSystem.MinMaxGradient(g);

            var rend = mistGo.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.sharedMaterial = FxAssets.Alpha;
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;
            rend.lightProbeUsage = LightProbeUsage.Off;
            rend.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
    }
}
