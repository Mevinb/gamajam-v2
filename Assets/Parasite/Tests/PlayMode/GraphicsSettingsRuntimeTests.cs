using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using Parasite.Possession;
using Parasite.Testing;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Parasite.Tests
{
    public class GraphicsSettingsRuntimeTests
    {
        GraphicsSettingsData original;
        bool hadJson, hadLegacy;
        string oldJson;
        int oldLegacy;
        readonly List<Object> created = new();

        [SetUp]
        public void PreservePreferences()
        {
            original = GraphicsPreset.CaptureSettings();
            hadJson = PlayerPrefs.HasKey(GraphicsSettingsStore.PreferenceKey); oldJson = PlayerPrefs.GetString(GraphicsSettingsStore.PreferenceKey);
            hadLegacy = PlayerPrefs.HasKey(GraphicsSettingsStore.LegacyPresetKey); oldLegacy = PlayerPrefs.GetInt(GraphicsSettingsStore.LegacyPresetKey);
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            foreach (var value in created) if (value) Object.Destroy(value);
            created.Clear();
            if (hadJson) PlayerPrefs.SetString(GraphicsSettingsStore.PreferenceKey, oldJson); else PlayerPrefs.DeleteKey(GraphicsSettingsStore.PreferenceKey);
            if (hadLegacy) PlayerPrefs.SetInt(GraphicsSettingsStore.LegacyPresetKey, oldLegacy); else PlayerPrefs.DeleteKey(GraphicsSettingsStore.LegacyPresetKey);
            PlayerPrefs.Save();
            GraphicsPreset.ApplySettings(original, save: false);
            GameInput.Simulated = false; GameInput.SimMove = Vector2.zero;
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator CustomSettingsSurviveSceneReloadAndDoNotModifyAuthoringAssets()
        {
            RequireGpu(); yield return AutoTest.LoadCity();
            var owner = GraphicsPreset.RuntimeOwner;
            Assert.IsNotNull(owner); Assert.IsNotNull(owner.Pipeline); Assert.IsNotNull(owner.SourcePipeline);
            Assert.AreNotSame(owner.SourcePipeline, owner.Pipeline);
            var sources = SnapshotSources(owner.SourcePipeline);
            var runtimePipeline = owner.Pipeline;
            var chosen = GraphicsSettingsData.ForPreset(0);
            chosen.renderScale = .9f; chosen.reflectionLevel = 2; chosen.ambientOcclusion = true;
            chosen.shadows = false; chosen.vSync = false; chosen.frameLimit = 90;
            chosen.antiAliasing = 0; chosen.msaaSamples = 1;
            GraphicsPreset.ApplySettings(chosen);
            Assert.IsTrue(GraphicsPreset.IsCustom); Assert.AreEqual(0, GraphicsPreset.Current);
            Assert.IsTrue(GraphicsPreset.RealtimeReflectionsEnabled, "A Low baseline must not veto independently enabled scene probes.");
            Assert.AreEqual(28, Shader.GetGlobalFloat("_ParasiteSSRSteps"));
            Assert.AreEqual(.9f, runtimePipeline.renderScale); Assert.AreEqual(0, runtimePipeline.shadowDistance);
            Assert.IsTrue(GraphicsPreset.AmbientOcclusionAvailable);
            Assert.AreEqual(0, QualitySettings.vSyncCount); Assert.AreEqual(90, Application.targetFrameRate);
            Assert.IsTrue(chosen.Equals(GraphicsSettingsStore.Load(3)));
            yield return AutoTest.LoadCity();
            Assert.AreSame(runtimePipeline, GraphicsPreset.RuntimeOwner.Pipeline, "Scene loading reuses the owned pipeline instead of copying another graph.");
            Assert.IsTrue(chosen.Equals(GraphicsPreset.CaptureSettings()), "Scene callbacks must reapply the active Custom snapshot.");

            var cameraObject = Track(new GameObject("Late Custom game camera"));
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
            var data = cameraObject.AddComponent<UniversalAdditionalCameraData>(); data.renderPostProcessing = false;
            camera.allowHDR = true; camera.allowMSAA = true; data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            GraphicsPreset.ApplyCamera(camera);
            Assert.IsFalse(camera.allowHDR); Assert.IsFalse(camera.allowMSAA);
            Assert.AreEqual(AntialiasingMode.None, data.antialiasing); Assert.IsFalse(data.renderPostProcessing);

            var reflectionObject = Track(new GameObject("Internal reflection camera control"));
            var reflection = reflectionObject.AddComponent<Camera>(); reflection.enabled = false; reflection.cameraType = CameraType.Reflection;
            reflection.allowHDR = true; reflection.allowMSAA = true;
            GraphicsPreset.ApplyCamera(reflection);
            Assert.IsTrue(reflection.allowHDR && reflection.allowMSAA, "Game-camera policy must leave native reflection-camera sampling alone.");

            GraphicsPreset.Apply(3, save: false);
            Assert.AreEqual(90, Application.targetFrameRate); Assert.AreEqual(0, QualitySettings.vSyncCount,
                "Choosing image quality must preserve independently chosen pacing.");
            Assert.AreEqual(1.25f, runtimePipeline.renderScale); Assert.AreEqual(4, GraphicsPreset.CaptureSettings().msaaSamples);
            Assert.AreSame(owner, GraphicsPreset.RuntimeOwner);
            AssertSourcesUnchanged(sources);
            yield return SettingsUITests.Capture("12-graphics-custom-pipeline.png");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ReleasingRenderedOwnerRestoresOriginalsAndDestroysItsWholeGraph()
        {
            RequireGpu(); yield return AutoTest.LoadCity();
            var selected = GraphicsSettingsData.ForPreset(2); selected.bloom = selected.filmGrain = selected.chromaticAberration = false;
            GraphicsPreset.ApplySettings(selected, save: false);
            var owner = GraphicsPreset.RuntimeOwner;
            var sourceOverride = owner.OriginalQualityOverride;
            var sources = SnapshotSources(owner.SourcePipeline);
            var owned = SnapshotOwned(owner);
            float deadline = Time.realtimeSinceStartup + 5;
            while (!owner.RenderLoopObserved && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(owner.RenderLoopObserved, "The owned renderer must actually submit frames before it is retired.");
            yield return SettingsUITests.Capture("12-graphics-owner-before-retirement.png");
            var returned = GraphicsPreset.ReleaseRuntime(); Assert.AreSame(owner, returned);
            Assert.AreSame(sourceOverride, QualitySettings.renderPipeline, "Restore the exact override, including a null/default-pipeline override.");
            deadline = Time.realtimeSinceStartup + 10;
            while (AnyAlive(owned) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(AnyAlive(owned), "Pipeline, renderer data, features, post profile and components must all be destroyed.");
            Assert.IsFalse(owner, "The retirement host must also leave the scene after native pipeline release.");
            AssertSourcesUnchanged(sources);
            yield return null; yield return null;
            yield return SettingsUITests.Capture("12-graphics-owner-source-restored.png");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator BloomOffRemovesActualRenderedHaloAndOnUsesAuthoredIntensity()
        {
            RequireGpu(); yield return AutoTest.LoadCity();
            var selected = GraphicsSettingsData.ForPreset(2); selected.antiAliasing = 0; selected.msaaSamples = 1;
            selected.reflectionLevel = 0; selected.filmGrain = selected.chromaticAberration = false;
            GraphicsPreset.ApplySettings(selected, save: false);
            var fixture = Track(new GameObject("Bloom pixel proof")); fixture.layer = 31;
            var volume = fixture.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 20;
            var authored = Track(ScriptableObject.CreateInstance<VolumeProfile>()); volume.sharedProfile = authored;
            var bloom = authored.Add<Bloom>(true); Track(bloom);
            bloom.intensity.Override(8); bloom.threshold.Override(.3f); bloom.scatter.Override(.9f);
            var tone = authored.Add<Tonemapping>(true); Track(tone); tone.mode.Override(TonemappingMode.Neutral);
            var quad = Track(GameObject.CreatePrimitive(PrimitiveType.Quad)); quad.layer = 31;
            quad.transform.position = new Vector3(10000, 1000, 4); quad.transform.localScale = Vector3.one * .25f;
            Object.Destroy(quad.GetComponent<Collider>());
            var shader = Shader.Find("Universal Render Pipeline/Unlit"); Assert.IsNotNull(shader);
            var material = Track(new Material(shader)); material.SetColor("_BaseColor", new Color(8, 8, 8, 1)); material.SetFloat("_Cull", 0);
            quad.GetComponent<Renderer>().sharedMaterial = material;
            var cameraObject = Track(new GameObject("Bloom proof camera"));
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
            camera.transform.position = new Vector3(10000, 1000, 0); camera.transform.rotation = Quaternion.identity;
            camera.orthographic = true; camera.orthographicSize = 1; camera.aspect = 1; camera.nearClipPlane = .1f; camera.farClipPlane = 10;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.cullingMask = 1 << 31;
            var data = cameraObject.AddComponent<UniversalAdditionalCameraData>(); data.renderPostProcessing = true;
            data.volumeLayerMask = (1 << 31) | 1; GraphicsPreset.ApplyCamera(camera);
            var target = Track(new RenderTexture(256, 256, 24, RenderTextureFormat.ARGBHalf)); Assert.IsTrue(target.Create());
            string diagnostic = Path.Combine(Application.dataPath, "../Logs/graphics-bloom-diagnostic", System.DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ"));
            Directory.CreateDirectory(diagnostic);
            yield return null; yield return null;
            Texture2D on = null, off = null, restored = null;
            try
            {
                yield return ReadImage(camera, target, value => on = value, diagnostic, "on");
                selected.bloom = false; GraphicsPreset.ApplySettings(selected, save: false);
                yield return null; yield return ReadImage(camera, target, value => off = value, diagnostic, "off");
                selected.bloom = true; GraphicsPreset.ApplySettings(selected, save: false);
                yield return null; yield return ReadImage(camera, target, value => restored = value, diagnostic, "restored");
                float lit = Halo(on), dark = Halo(off), again = Halo(restored);
                Debug.Log($"[GraphicsSettingsPixels] DIAGNOSTIC on={lit:F8} off={dark:F8} restored={again:F8} actual authored intensity={bloom.intensity.value} output={diagnostic}");
                Assert.That(lit - dark, Is.GreaterThan(.01f), "Bloom Off must remove actual pixels outside the emissive quad.");
                Assert.That(again - dark, Is.GreaterThan(.01f));
                Assert.That(again, Is.EqualTo(lit).Within(.025f));
                Assert.AreEqual(8f, bloom.intensity.value, "The player's On/Off choices must not rewrite the authored intensity.");
                Debug.Log($"[GraphicsSettingsPixels] PASS bloom halo on={lit:F4} off={dark:F4} restored={again:F4}; authored intensity remains8");
            }
            finally
            {
                if (on) Object.Destroy(on); if (off) Object.Destroy(off); if (restored) Object.Destroy(restored);
                camera.targetTexture = null; target.Release();
            }
        }

        static IEnumerator ReadImage(Camera camera, RenderTexture target, System.Action<Texture2D> result, string directory, string label)
        {
            camera.targetTexture = target;
            string before = BloomState(camera, target, "before submission");
            var request = new RenderPipeline.StandardRequest { destination = target };
            Assert.IsTrue(RenderPipeline.SupportsRenderRequest(camera, request));
            RenderPipeline.SubmitRenderRequest(camera, request);
            string after = BloomState(camera, target, "after submission");
            yield return null;
            var prior = RenderTexture.active;
            var image = new Texture2D(256, 256, TextureFormat.RGBAFloat, false, true);
            try { RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); image.Apply(); }
            finally { RenderTexture.active = prior; }
            File.WriteAllText(Path.Combine(directory, label + "-render-stage.txt"), before + "\n" + after);
            SaveBloomImage(directory, label, image);
            result(image);
        }

        static string BloomState(Camera camera, RenderTexture target, string stage)
        {
            var builder = new StringBuilder();
            var data = camera.GetUniversalAdditionalCameraData();
            var stack = VolumeManager.instance.stack.GetComponent<Bloom>();
            builder.AppendLine($"stage={stage} request=StandardRequest camera={camera.name} type={camera.cameraType} enabled={camera.enabled} allowHDR={camera.allowHDR} post={data.renderPostProcessing} mask={data.volumeLayerMask.value} target={target.format}/{target.graphicsFormat} srgb={target.sRGB}");
            builder.AppendLine($"effective-stack bloom active={stack.active} isActive={stack.IsActive()} intensity={stack.intensity.value} threshold={stack.threshold.value} scatter={stack.scatter.value} override={stack.intensity.overrideState}");
            foreach (var volume in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                var profile = volume.sharedProfile;
                if (!profile || !profile.TryGet<Bloom>(out var bloom)) continue;
                builder.AppendLine($"volume={volume.name} layer={volume.gameObject.layer} enabled={volume.enabled}/{volume.gameObject.activeInHierarchy} global={volume.isGlobal} priority={volume.priority} weight={volume.weight} bloomactive={bloom.active} intensity={bloom.intensity.value} intensityOverride={bloom.intensity.overrideState} threshold={bloom.threshold.value} scatter={bloom.scatter.value}");
            }
            return builder.ToString();
        }

        [System.Serializable]
        sealed class BloomImageEvidence
        {
            public string name, format;
            public float mean, maximum, minimum, centerMean, centerMaximum, halo;
            public int hotPixels, hotMinX = 256, hotMaxX = -1, hotMinY = 256, hotMaxY = -1;
        }

        static void SaveBloomImage(string directory, string label, Texture2D image)
        {
            var evidence = new BloomImageEvidence { name = label, format = image.format.ToString(), minimum = float.PositiveInfinity, halo = Halo(image) };
            var display = new Texture2D(image.width, image.height, TextureFormat.RGB24, false);
            var pixels = image.GetPixels();
            float centerSum = 0; int centerCount = 0;
            for (int y = 0; y < image.height; y++)
                for (int x = 0; x < image.width; x++)
                {
                    int index = y * image.width + x; float value = pixels[index].grayscale;
                    evidence.mean += value; evidence.minimum = Mathf.Min(evidence.minimum, value); evidence.maximum = Mathf.Max(evidence.maximum, value);
                    if (x >= 120 && x < 136 && y >= 120 && y < 136) { centerSum += value; centerCount++; evidence.centerMaximum = Mathf.Max(evidence.centerMaximum, value); }
                    if (value > .2f)
                    {
                        evidence.hotPixels++; evidence.hotMinX = Mathf.Min(evidence.hotMinX, x); evidence.hotMaxX = Mathf.Max(evidence.hotMaxX, x);
                        evidence.hotMinY = Mathf.Min(evidence.hotMinY, y); evidence.hotMaxY = Mathf.Max(evidence.hotMaxY, y);
                    }
                    pixels[index] = pixels[index].gamma;
                }
            evidence.mean /= image.width * image.height; evidence.centerMean = centerSum / centerCount;
            try
            {
                File.WriteAllBytes(Path.Combine(directory, label + ".exr"), image.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat | Texture2D.EXRFlags.CompressZIP));
                display.SetPixels(pixels); display.Apply();
                File.WriteAllBytes(Path.Combine(directory, label + ".png"), display.EncodeToPNG());
                File.WriteAllText(Path.Combine(directory, label + "-hdr-stats.json"), JsonUtility.ToJson(evidence, true));
            }
            finally { Object.Destroy(display); }
            Debug.Log("[GraphicsSettingsPixels] " + JsonUtility.ToJson(evidence));
        }

        static float Halo(Texture2D image)
        {
            float sum = 0; int count = 0;
            for (int x = 149; x < 184; x++) for (int y = 119; y < 137; y++) { sum += image.GetPixel(x, y).grayscale; count++; }
            return sum / count;
        }

        T Track<T>(T value) where T : Object { created.Add(value); return value; }
        static void RequireGpu() { if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Actual rendering and pipeline retirement require a GPU."); }
        static bool AnyAlive(List<Object> objects) { foreach (var value in objects) if (value) return true; return false; }
        static List<(Object value, string json)> SnapshotSources(UniversalRenderPipelineAsset source)
        {
            var result = new List<(Object, string)> { (source, JsonUtility.ToJson(source)) };
            foreach (var data in source.rendererDataList)
            {
                if (!data) continue; result.Add((data, JsonUtility.ToJson(data)));
                foreach (var feature in data.rendererFeatures) if (feature) result.Add((feature, JsonUtility.ToJson(feature)));
            }
            return result;
        }
        static void AssertSourcesUnchanged(List<(Object value, string json)> sources)
        { foreach (var source in sources) Assert.AreEqual(source.json, JsonUtility.ToJson(source.value), source.value.name + " authoring state changed"); }
        static List<Object> SnapshotOwned(GraphicsSettingsRuntime owner)
        {
            var result = new List<Object> { owner, owner.Pipeline };
            foreach (var data in owner.Pipeline.rendererDataList)
            {
                if (!data) continue; result.Add(data);
                foreach (var feature in data.rendererFeatures) if (feature) result.Add(feature);
            }
            var volume = owner.GetComponent<Volume>();
            if (volume && volume.sharedProfile)
            {
                result.Add(volume.sharedProfile);
                foreach (var component in volume.sharedProfile.components) if (component) result.Add(component);
            }
            return result;
        }
    }
}
