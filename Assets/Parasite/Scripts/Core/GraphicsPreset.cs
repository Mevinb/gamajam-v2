using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Parasite
{
    /// <summary>Player-facing graphics presets (render scale, shadows, LOD, AA, lamp-light budget, water reflections,
    /// SSAO, post quality). Saved in PlayerPrefs.</summary>
    public static class GraphicsPreset
    {
        public static readonly string[] Names = { "Low", "Medium", "High", "Ultra" };
        const string Key = "parasite.graphics";
        public static int Current { get; private set; } = 3;
        public static event System.Action<int> Applied;
        static readonly float[] RenderScales = { 0.7f, 0.85f, 1f, 1.25f };
        static readonly float[] ShadowDistances = { 40f, 70f, 110f, 180f };
        static readonly float[] LodBias = { 0.6f, 0.9f, 1.3f, 2.5f };
        static readonly int[] MsaaSamples = { 1, 1, 1, 4 };
        static readonly int[] ShadowResolutions = { 1024, 2048, 2048, 4096 };
        static readonly int[] LampBudgets = { 6, 14, 32, 64 };
        static readonly float[] LampDistances = { 32f, 48f, 70f, 110f };
        static readonly int[] ReflectionSteps = { 0, 16, 28, 36 };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            int def = SystemInfo.graphicsMemorySize >= 4000 ? 3 : 2;
            Apply(PlayerPrefs.GetInt(Key, def), save: false);
            // New gameplay cameras are created on scene load (including checkpoint reloads).
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            // Cinematic cameras can be created after sceneLoaded. Apply the saved policy before their first frame too.
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply(Current, save: false);
        static void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera && camera.cameraType == CameraType.Game) ApplyCamera(camera);
        }

        public static void Cycle() => Apply((Current + 1) % Names.Length);

        public static void Apply(int level, bool save = true)
        {
            Current = Mathf.Clamp(level, 0, Names.Length - 1);
            if (save) { PlayerPrefs.SetInt(Key, Current); PlayerPrefs.Save(); }
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp)
            {
                urp.renderScale = RenderScales[Current];
                urp.shadowDistance = ShadowDistances[Current];
                urp.msaaSampleCount = MsaaSamples[Current];
                urp.mainLightShadowmapResolution = ShadowResolutions[Current];
                urp.supportsHDR = Current >= 2;
                EnableSSAO(urp, Current >= 2);
            }
            // street-lamp light budget (FX.LampLights), screen-space water reflections, light-cone haze
            FX.LampLights.Budget = LampBudgets[Current];
            FX.LampLights.MaxDistance = LampDistances[Current];
            if (Current == 0) Shader.EnableKeyword(SsrOff); else Shader.DisableKeyword(SsrOff);
            Shader.SetGlobalFloat(SsrSteps, ReflectionSteps[Current]);
            Shader.SetGlobalFloat(ConeScale, Current == 0 ? 0.7f : 1f);
            Shader.SetGlobalFloat(ShaftScale, Current == 0 ? -1f : 1f);   // sun shafts off on Low
            ApplyPostQuality();
            // Generated city probes also schedule native rendering; a Null device must keep the global scheduler off.
            QualitySettings.realtimeReflectionProbes = Current >= 2 && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
            QualitySettings.lodBias = LodBias[Current];
            QualitySettings.anisotropicFiltering = Current >= 3 ? AnisotropicFiltering.ForceEnable : AnisotropicFiltering.Enable;
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = -1;
            foreach (var camera in Camera.allCameras) ApplyCamera(camera);
            Applied?.Invoke(Current);
        }

        /// <summary>Apply the current preset to a new gameplay/cinematic camera without saving preferences.</summary>
        public static void ApplyCamera(Camera camera)
        {
            if (!camera) return;
            camera.allowHDR = Current >= 2;
            camera.allowMSAA = Current >= 3;
            if (camera.TryGetComponent<UniversalAdditionalCameraData>(out var d))
            {
                d.antialiasing = Current >= 2 ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.FastApproximateAntialiasing;
                d.antialiasingQuality = Current >= 3 ? AntialiasingQuality.High : AntialiasingQuality.Medium;
                // Keep each camera's authored post-processing choice (the temporary lake set supplies its own grading).
            }
        }

        const string SsrOff = "_PARASITE_SSR_OFF";
        static readonly int SsrSteps = Shader.PropertyToID("_ParasiteSSRSteps");
        static readonly int ConeScale = Shader.PropertyToID("_ParasiteConeScale");
        static readonly int ShaftScale = Shader.PropertyToID("_ParasiteShaftScale");
        static Volume postOverride;
        static VolumeProfile postProfile;

        /// <summary>Low/Medium: cheaper bloom, no film grain / chromatic aberration (a runtime override volume, so the
        /// scene's grading asset is never modified).</summary>
        static void ApplyPostQuality()
        {
            if (!Application.isPlaying) return;
            if (!postOverride)
            {
                var go = new GameObject("[Graphics Preset Post]");
                Object.DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.DontSave;
                postOverride = go.AddComponent<Volume>();
                postOverride.isGlobal = true;
                postOverride.priority = 30f;
                postProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                postProfile.name = "Graphics Preset Overrides";
                var bloom = postProfile.Add<Bloom>(false); bloom.highQualityFiltering.Override(false);
                var grain = postProfile.Add<FilmGrain>(false); grain.intensity.Override(0f);
                var ca = postProfile.Add<ChromaticAberration>(false); ca.intensity.Override(0f);
                postOverride.sharedProfile = postProfile;
            }
            postOverride.weight = Current <= 1 ? 1f : 0f;
        }

        static void EnableSSAO(UniversalRenderPipelineAsset urp, bool enable)
        {
            try
            {
                foreach (var data in urp.rendererDataList)
                {
                    if (!data) continue;
                    foreach (var f in data.rendererFeatures)
                    {
                        if (f && f.GetType().Name == "ScreenSpaceAmbientOcclusion")
                            f.SetActive(enable);
                    }
                }
            }
            catch
            {
                // Fallback for custom or stripped renderers
            }
        }
    }
}
