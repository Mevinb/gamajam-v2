using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Parasite
{
    /// <summary>Owns runtime copies of the render pipeline, renderer features and optional post overrides.</summary>
    public sealed class GraphicsSettingsRuntime : MonoBehaviour
    {
        static readonly FieldInfo RendererList = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic);
        readonly List<ScriptableRendererData> renderers = new();
        readonly List<ScriptableRendererFeature> features = new();
        readonly Dictionary<Camera, CameraState> cameras = new();
        RenderPipelineAsset originalQualityOverride;
        RenderPipeline nativePipeline;
        Volume volume;
        VolumeProfile post;
        bool restored, disposed, rendererWasCreated;
        int oldVSync, oldFrameLimit, oldLampBudget;
        float oldLod, oldLampDistance, oldSteps, oldCone, oldShaft;
        bool oldProbes, oldSsrOff;
        AnisotropicFiltering oldAnisotropic;

        public UniversalRenderPipelineAsset SourcePipeline { get; private set; }
        public RenderPipelineAsset OriginalQualityOverride => originalQualityOverride;
        public UniversalRenderPipelineAsset Pipeline { get; private set; }
        public int OwnedRendererCount => renderers.Count;
        public int OwnedFeatureCount => features.Count;
        public int OwnedObjectCount => (Pipeline ? 1 : 0) + renderers.Count + features.Count + (post ? 1 + post.components.Count : 0);
        public bool Retiring { get; private set; }
        public bool RetirementComplete => disposed;
        public bool RenderLoopObserved => nativePipeline != null;

        struct CameraState
        {
            public bool hdr, msaa;
            public UniversalAdditionalCameraData data;
            public AntialiasingMode aa;
            public AntialiasingQuality quality;
        }

        internal void Initialize()
        {
            DontDestroyOnLoad(gameObject);
            originalQualityOverride = QualitySettings.renderPipeline;
            oldVSync = QualitySettings.vSyncCount; oldFrameLimit = Application.targetFrameRate;
            oldLod = QualitySettings.lodBias; oldAnisotropic = QualitySettings.anisotropicFiltering;
            oldProbes = QualitySettings.realtimeReflectionProbes;
            oldLampBudget = FX.LampLights.Budget; oldLampDistance = FX.LampLights.MaxDistance;
            oldSsrOff = Shader.IsKeywordEnabled(GraphicsPreset.SsrOff);
            oldSteps = Shader.GetGlobalFloat(GraphicsPreset.SsrSteps);
            oldCone = Shader.GetGlobalFloat(GraphicsPreset.ConeScale); oldShaft = Shader.GetGlobalFloat(GraphicsPreset.ShaftScale);
            EnsurePipeline();
        }

        internal void EnsurePipeline()
        {
            if (Pipeline || restored || disposed) return;
            SourcePipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (!SourcePipeline) return;
            try
            {
                if (RendererList == null) throw new InvalidOperationException("Installed URP renderer-list binding is unavailable.");
                Pipeline = Instantiate(SourcePipeline);
                Pipeline.name = SourcePipeline.name + " (player settings)"; Pipeline.hideFlags = HideFlags.DontSave;
                var sourceData = SourcePipeline.rendererDataList;
                var ownedData = new ScriptableRendererData[sourceData.Length];
                for (int i = 0; i < sourceData.Length; i++)
                {
                    if (!sourceData[i]) continue;
                    var data = Instantiate(sourceData[i]); data.name = sourceData[i].name + " (player settings)"; data.hideFlags = HideFlags.DontSave;
                    renderers.Add(data); ownedData[i] = data;
                    data.rendererFeatures.Clear();
                    foreach (var feature in sourceData[i].rendererFeatures)
                    {
                        if (!feature) { data.rendererFeatures.Add(null); continue; }
                        var owned = Instantiate(feature); owned.name = feature.name + " (player settings)"; owned.hideFlags = HideFlags.DontSave;
                        features.Add(owned); data.rendererFeatures.Add(owned);
                    }
                    data.SetDirty();
                }
                RendererList.SetValue(Pipeline, ownedData);
                // A pipeline can arrive after Boot. Capture the quality override at the moment ownership begins.
                originalQualityOverride = QualitySettings.renderPipeline;
                QualitySettings.renderPipeline = Pipeline;
            }
            catch
            {
                // A failed copy must never fall through to modifying an authoring asset.
                RestoreState(); DisposeObjects();
                throw;
            }
        }

        internal void ObserveRenderLoop()
        {
            if (Retiring || !Pipeline || GraphicsSettings.currentRenderPipeline != Pipeline) return;
            nativePipeline = RenderPipelineManager.currentPipeline;
            foreach (var renderer in Pipeline.renderers) if (renderer != null) rendererWasCreated = true;
        }

        internal void CaptureCamera(Camera camera)
        {
            if (!camera || camera.cameraType != CameraType.Game || cameras.ContainsKey(camera)) return;
            camera.TryGetComponent<UniversalAdditionalCameraData>(out var data);
            cameras.Add(camera, new CameraState
            {
                hdr = camera.allowHDR, msaa = camera.allowMSAA, data = data,
                aa = data ? data.antialiasing : AntialiasingMode.None,
                quality = data ? data.antialiasingQuality : AntialiasingQuality.Medium,
            });
        }

        internal bool SetAmbientOcclusion(bool enabled)
        {
            bool found = false;
            foreach (var feature in features)
                if (feature && feature.GetType().Name == "ScreenSpaceAmbientOcclusion")
                { feature.SetActive(enabled); found = true; }
            return found;
        }

        internal void ApplyPost(GraphicsSettingsData settings)
        {
            if (Retiring) return;
            if (!volume)
            {
                volume = gameObject.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 30f;
                post = ScriptableObject.CreateInstance<VolumeProfile>(); post.name = "Player graphics overrides"; post.hideFlags = HideFlags.DontSave;
                post.Add<Bloom>(false); post.Add<FilmGrain>(false); post.Add<ChromaticAberration>(false);
                foreach (var component in post.components) component.hideFlags = HideFlags.DontSave;
                volume.sharedProfile = post;
            }
            post.TryGet<Bloom>(out var bloom); post.TryGet<FilmGrain>(out var grain); post.TryGet<ChromaticAberration>(out var chromatic);
            bloom.active = grain.active = chromatic.active = true;
            bloom.intensity.value = 0f; bloom.intensity.overrideState = !settings.bloom;
            bloom.highQualityFiltering.value = false; bloom.highQualityFiltering.overrideState = settings.preset <= 1;
            grain.intensity.value = 0f; grain.intensity.overrideState = !settings.filmGrain;
            chromatic.intensity.value = 0f; chromatic.intensity.overrideState = !settings.chromaticAberration;
            volume.weight = 1f;
        }

        void RestoreState()
        {
            if (restored) return;
            restored = true;
            if (Pipeline && QualitySettings.renderPipeline == Pipeline) QualitySettings.renderPipeline = originalQualityOverride;
            if (volume) { volume.enabled = false; volume.sharedProfile = null; }
            foreach (var pair in cameras)
            {
                if (!pair.Key) continue;
                pair.Key.allowHDR = pair.Value.hdr; pair.Key.allowMSAA = pair.Value.msaa;
                if (pair.Value.data) { pair.Value.data.antialiasing = pair.Value.aa; pair.Value.data.antialiasingQuality = pair.Value.quality; }
            }
            cameras.Clear();
            QualitySettings.vSyncCount = oldVSync; Application.targetFrameRate = oldFrameLimit;
            QualitySettings.lodBias = oldLod; QualitySettings.anisotropicFiltering = oldAnisotropic;
            QualitySettings.realtimeReflectionProbes = oldProbes;
            FX.LampLights.Budget = oldLampBudget; FX.LampLights.MaxDistance = oldLampDistance;
            if (oldSsrOff) Shader.EnableKeyword(GraphicsPreset.SsrOff); else Shader.DisableKeyword(GraphicsPreset.SsrOff);
            Shader.SetGlobalFloat(GraphicsPreset.SsrSteps, oldSteps); Shader.SetGlobalFloat(GraphicsPreset.ConeScale, oldCone); Shader.SetGlobalFloat(GraphicsPreset.ShaftScale, oldShaft);
        }

        internal void BeginRetirement()
        {
            if (Retiring) return;
            Retiring = true;
            ObserveBeforeRestore(); RestoreState();
            StartCoroutine(RetireAfterPipelineSwitch());
        }

        void ObserveBeforeRestore()
        {
            if (!Pipeline) return;
            foreach (var renderer in Pipeline.renderers) if (renderer != null) rendererWasCreated = true;
        }

        IEnumerator RetireAfterPipelineSwitch()
        {
            // A pointer swap is not a disposal receipt. Keep the feature objects until the render loop releases its old instance.
            while (nativePipeline != null && !nativePipeline.disposed)
                yield return null;
            DisposeObjects();
            Destroy(gameObject);
        }

        void DisposeObjects()
        {
            if (disposed) return;
            disposed = true;
            bool hadRenderers = rendererWasCreated;
            if (Pipeline)
            {
                foreach (var renderer in Pipeline.renderers) if (renderer != null) hadRenderers = true;
                // URP's asset OnDisable disposes its renderers/passes before the feature ScriptableObjects below are destroyed.
                DestroyImmediate(Pipeline); Pipeline = null;
            }
            foreach (var feature in features)
            {
                if (!feature) continue;
                // Feature.OnEnable can allocate before a renderer is constructed; those unattached copies need explicit disposal.
                if (!hadRenderers) feature.Dispose();
                DestroyImmediate(feature);
            }
            features.Clear();
            foreach (var data in renderers) if (data) DestroyImmediate(data);
            renderers.Clear();
            if (post)
            {
                foreach (var component in post.components) if (component) DestroyImmediate(component);
                DestroyImmediate(post); post = null;
            }
        }

        void OnDestroy()
        {
            ObserveBeforeRestore(); RestoreState();
            // Normal ReleaseRuntime keeps this object alive until the old pipeline is disposed. Engine shutdown and explicit
            // object destruction run outside camera submission; the asset owns synchronous renderer/pass disposal in that path.
            DisposeObjects();
            GraphicsPreset.RuntimeOwnerDestroyed(this);
        }
    }
}
