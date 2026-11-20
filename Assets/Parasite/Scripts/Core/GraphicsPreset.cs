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
        public static int Current { get; private set; } = 2;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            int def = SystemInfo.graphicsMemorySize >= 5000 ? 2 : 1;
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
            float[] shadow = { 40f, 70f, 110f, 150f };
            float[] lod = { 0.6f, 0.9f, 1.3f, 2f };
            int[] msaa = { 1, 1, 1, 4 };
            if (urp)
            {
                urp.renderScale = scale[Current];
                urp.shadowDistance = shadow[Current];
                urp.msaaSampleCount = msaa[Current];
            }
            QualitySettings.lodBias = lod[Current];
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = -1;
            var cam = Camera.main;
            if (cam && cam.TryGetComponent<UniversalAdditionalCameraData>(out var d))
                d.antialiasing = Current >= 2 ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.FastApproximateAntialiasing;
        }
    }
}
