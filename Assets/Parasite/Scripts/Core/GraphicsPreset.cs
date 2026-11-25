using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Parasite
{
    /// <summary>Compatibility facade for named profiles and independently saved graphics choices.</summary>
    public static class GraphicsPreset
    {
        public static readonly string[] Names = { "Low", "Medium", "High", "Ultra" };
        static GraphicsSettingsData settings = GraphicsSettingsData.ForPreset(3);
        static GraphicsSettingsRuntime runtime;
        static bool runtimeSuppressed;
        public static int Current => settings.preset;
        public static bool IsCustom => !settings.Equals(GraphicsSettingsData.ForPreset(Current));
        public static string DisplayName => IsCustom ? "Custom (" + Names[Current] + ")" : Names[Current];
        public static int ReflectionLevel => settings.reflectionLevel;
        public static bool RealtimeReflectionsEnabled => settings.reflectionLevel >= 2 && QualitySettings.realtimeReflectionProbes && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
        public static int EffectiveMsaaSamples { get; private set; } = 1;
        public static bool AmbientOcclusionAvailable { get; private set; }
        public static GraphicsSettingsRuntime RuntimeOwner => runtime;
        public static event Action<int> Applied;
        static readonly float[] LodBias = { .6f, .9f, 1.3f, 2.5f };
        static readonly int[] ShadowResolutions = { 1024, 2048, 2048, 4096 };
        static readonly int[] LampBudgets = { 6, 14, 32, 64 };
        static readonly float[] LampDistances = { 32f, 48f, 70f, 110f };
        static readonly int[] ReflectionSteps = { 0, 16, 28, 36 };
        internal const string SsrOff = "_PARASITE_SSR_OFF";
        internal static readonly int SsrSteps = Shader.PropertyToID("_ParasiteSSRSteps");
        internal static readonly int ConeScale = Shader.PropertyToID("_ParasiteConeScale");
        internal static readonly int ShaftScale = Shader.PropertyToID("_ParasiteShaftScale");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            runtime = null; runtimeSuppressed = false; Applied = null;
            settings = GraphicsSettingsData.ForPreset(3); EffectiveMsaaSamples = 1; AmbientOcclusionAvailable = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            int fallback = SystemInfo.graphicsMemorySize >= 4000 ? 3 : 2;
            ApplySettings(GraphicsSettingsStore.Load(fallback), save: false);
            SceneManager.sceneLoaded -= OnSceneLoaded; SceneManager.sceneLoaded += OnSceneLoaded;
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera; RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyActiveSettings();
        static void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (!runtime || runtime.Retiring) return;
            runtime.ObserveRenderLoop();
            if (camera && camera.cameraType == CameraType.Game) ApplyCamera(camera);
        }

        public static GraphicsSettingsData CaptureSettings() => settings.Clone();
        public static void Cycle() => Apply((Current + 1) % Names.Length);
        public static void Apply(int level, bool save = true)
        {
            var next = GraphicsSettingsData.ForPreset(level);
            // Quality changes do not reset the separately chosen display pacing policy.
            next.vSync = settings.vSync; next.frameLimit = settings.frameLimit;
            ApplySettings(next, save);
        }

        public static void ApplySettings(GraphicsSettingsData requested, bool save = true)
        {
            if (requested == null) throw new ArgumentNullException(nameof(requested));
            settings = requested.Normalized(); runtimeSuppressed = false;
            if (save) GraphicsSettingsStore.Save(settings);
            ApplyActiveSettings();
        }

        static void EnsureRuntime()
        {
            if (runtimeSuppressed || !Application.isPlaying) return;
            try
            {
                if (runtime) runtime.EnsurePipeline();
                else
                {
                    var go = new GameObject("[Graphics Settings Runtime]") { hideFlags = HideFlags.DontSave };
                    runtime = go.AddComponent<GraphicsSettingsRuntime>();
                    runtime.Initialize();
                }
            }
            catch (Exception error)
            {
                if (runtime) UnityEngine.Object.DestroyImmediate(runtime.gameObject);
                runtime = null;
                Debug.LogWarning("[GraphicsSettings] Runtime render copy unavailable; authoring assets were left unchanged: " + error.Message);
            }
        }

        static int ResolveMsaa(int requested)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return 1;
            var descriptor = new RenderTextureDescriptor(4, 4, Current >= 2 ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32, 24)
            { msaaSamples = requested };
            int supported = SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor);
            return supported == 2 || supported == 4 ? Math.Min(requested, supported) : 1;
        }

        static void ApplyActiveSettings()
        {
            if (runtimeSuppressed) return;
            EnsureRuntime();
            EffectiveMsaaSamples = ResolveMsaa(settings.msaaSamples);
            var urp = runtime ? runtime.Pipeline : null;
            AmbientOcclusionAvailable = false;
            if (urp)
            {
                urp.renderScale = settings.renderScale;
                urp.shadowDistance = settings.shadows ? settings.shadowDistance : 0f;
                urp.msaaSampleCount = EffectiveMsaaSamples;
                urp.mainLightShadowmapResolution = ShadowResolutions[Current];
                urp.supportsHDR = Current >= 2;
                AmbientOcclusionAvailable = runtime.SetAmbientOcclusion(settings.ambientOcclusion);
            }
            FX.LampLights.Budget = LampBudgets[Current]; FX.LampLights.MaxDistance = LampDistances[Current];
            if (settings.reflectionLevel == 0) Shader.EnableKeyword(SsrOff); else Shader.DisableKeyword(SsrOff);
            Shader.SetGlobalFloat(SsrSteps, ReflectionSteps[settings.reflectionLevel]);
            Shader.SetGlobalFloat(ConeScale, Current == 0 ? .7f : 1f);
            Shader.SetGlobalFloat(ShaftScale, Current == 0 ? -1f : 1f);
            if (runtime) runtime.ApplyPost(settings);
            // Generated probes can also schedule native rendering; preserve the Null-device safety guard.
            QualitySettings.realtimeReflectionProbes = settings.reflectionLevel >= 2 && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
            QualitySettings.lodBias = LodBias[Current];
            QualitySettings.anisotropicFiltering = Current >= 3 ? AnisotropicFiltering.ForceEnable : AnisotropicFiltering.Enable;
            QualitySettings.vSyncCount = settings.vSync ? 1 : 0;
            Application.targetFrameRate = settings.frameLimit;
            foreach (var camera in Camera.allCameras) ApplyCamera(camera);
            Applied?.Invoke(Current);
        }

        /// <summary>Apply image sampling to game/cinematic cameras, preserving their authored post-processing choice.</summary>
        public static void ApplyCamera(Camera camera)
        {
            if (!camera || camera.cameraType != CameraType.Game || runtimeSuppressed) return;
            if (runtime) runtime.CaptureCamera(camera);
            camera.allowHDR = Current >= 2; camera.allowMSAA = EffectiveMsaaSamples > 1;
            if (camera.TryGetComponent<UniversalAdditionalCameraData>(out var data))
            {
                data.antialiasing = settings.antiAliasing == 0 ? AntialiasingMode.None : settings.antiAliasing == 1
                    ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = Current >= 3 ? AntialiasingQuality.High : AntialiasingQuality.Medium;
            }
        }

        /// <summary>Restore original pipeline, globals and live cameras; the owner retires after the render loop releases its pipeline.</summary>
        public static GraphicsSettingsRuntime ReleaseRuntime()
        {
            var owner = runtime; runtime = null; runtimeSuppressed = true;
            if (owner) owner.BeginRetirement();
            return owner;
        }

        internal static void RuntimeOwnerDestroyed(GraphicsSettingsRuntime owner)
        {
            if (ReferenceEquals(runtime, owner)) { runtime = null; runtimeSuppressed = true; }
        }
    }
}
