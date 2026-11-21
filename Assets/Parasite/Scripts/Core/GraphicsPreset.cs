using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Parasite
{
    /// <summary>Player-facing graphics presets (render scale, shadows, LOD, AA). Saved in PlayerPrefs.</summary>
    public static class GraphicsPreset
    {
        public static readonly string[] Names = { "Low", "Medium", "High", "Ultra" };
        const string Key = "parasite.graphics";
        public static int Current { get; private set; } = 3;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            int def = SystemInfo.graphicsMemorySize >= 4000 ? 3 : 2;
            Apply(PlayerPrefs.GetInt(Key, def), save: false);
            // New gameplay cameras are created on scene load (including checkpoint reloads).
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply(Current, save: false);

        public static void Cycle() => Apply((Current + 1) % Names.Length);

        public static void Apply(int level, bool save = true)
        {
            Current = Mathf.Clamp(level, 0, Names.Length - 1);
            if (save) { PlayerPrefs.SetInt(Key, Current); PlayerPrefs.Save(); }
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            float[] scale = { 0.7f, 0.85f, 1f, 1f };
            float[] shadow = { 40f, 70f, 110f, 160f };
            float[] lod = { 0.6f, 0.9f, 1.3f, 2.2f };
            int[] msaa = { 1, 1, 1, 4 };
            int[] shadowRes = { 1024, 2048, 2048, 4096 };
            if (urp)
            {
                urp.renderScale = scale[Current];
                urp.shadowDistance = shadow[Current];
                urp.msaaSampleCount = msaa[Current];
                urp.mainLightShadowmapResolution = shadowRes[Current];
                if (Current >= 2) urp.supportsHDR = true;
                EnableSSAO(urp, Current >= 2);
            }
            QualitySettings.lodBias = lod[Current];
            QualitySettings.anisotropicFiltering = Current >= 3 ? AnisotropicFiltering.ForceEnable : AnisotropicFiltering.Enable;
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = -1;
            var cam = Camera.main;
            if (cam)
            {
                if (Current >= 2) cam.allowHDR = true;
                if (cam.TryGetComponent<UniversalAdditionalCameraData>(out var d))
                {
                    d.antialiasing = Current >= 2 ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.FastApproximateAntialiasing;
                    if (Current >= 3) d.antialiasingQuality = AntialiasingQuality.High;
                    d.renderPostProcessing = true;
                }
            }
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
