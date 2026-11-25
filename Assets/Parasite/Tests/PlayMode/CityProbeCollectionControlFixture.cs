#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Parasite.Tests
{
    /// <summary>Optional collection-wide diagnostic. No hooks or field changes without an explicit CLI mode.</summary>
    [SetUpFixture]
    public sealed class CityProbeCollectionControlFixture
    {
        [OneTimeSetUp]
        public void InstallOptionalControl() => CityProbeCollectionControl.Install("NUnit namespace setup fallback");

        [OneTimeTearDown]
        public void RestoreOptionalControl() => CityProbeCollectionControl.Finish("NUnit namespace teardown", true);
    }

    static class CityProbeCollectionControl
    {
        enum Mode { Observe, Disable, IndividualFaces }
        static readonly string[] CityNames =
        {
            "Probe_Plaza", "Probe_South", "Probe_Campus", "Probe_Lab", "Probe_Market", "Probe_Avenue", "Probe_Alley"
        };
        static readonly string[] MenuNames = { "Probe_Street" };
        static readonly List<Borrowed> borrowed = new();
        static readonly HashSet<int> seenScenes = new();
        static Report report;
        static Mode mode;
        static string output;
        static bool installed, finished;
        static long contextSerial, cameraSerial;
        static int contextDepth;

        [Serializable]
        sealed class Report
        {
            public int schema = 1, processId;
            public string status, mode, installedUtc, installationPhase, outputPath, api, gpu, finishedUtc, finishReason;
            public bool includeBootstrap;
            public string expectedSerializedTimeSlicing = "IndividualFaces";
            public bool helperCallsPlayerPrefs = false, changesNativeFlagDuringTests = false;
            public bool cleanupTemporarilySuspendedNativeScheduling, nativeFlagBeforeCleanup, nativeFlagAfterCleanup;
            public bool cleanupNativeFlagRestored, allControlledMutationsBeforeObservedSrp = true;
            public int cityScenes, bootstrapScenes, controlledCityScenes, controlledBootstrapScenes, restoredLiveProbes, alreadyDestroyedProbes;
            public string automaticNativeRequestStatus = "UNOBSERVED: automatic OnAwake render IDs are not exposed to this helper; no zero-pending or native-retirement claim.";
            public string lifecycleLimit = "sceneLoaded follows object Awake/OnEnable. Current generated probes already use serialized IndividualFaces. The individualFaces arm re-enables that same authored slicing mode; automatic cancellation remains unacknowledged and disable is diagnostic only.";
            public List<SceneRecord> scenes = new();
            public List<string> errors = new();
        }

        [Serializable]
        sealed class SceneRecord
        {
            public string name, path, entryPoint, eligibility, firstCameraName, firstCameraType;
            public int handle, loadedFrame, unloadedFrame = -1, firstContextFrame = -1, firstCameraFrame = -1;
            public int expectedAutomaticCount, actualAutomaticCount, unrelatedViaScriptingCount, contextDepthAtMutation;
            public long contextSerialBefore, contextSerialAfter, cameraSerialBefore, cameraSerialAfter;
            public long firstContextSerial = -1, firstCameraSerial = -1, firstNativeEnabledContextSerial = -1;
            public int contextsWithNativeSchedulingEnabled, contextsWithNativeSchedulingDisabled, firstNativeEnabledContextFrame = -1;
            public bool controlled, fieldsChanged, beforeObservedSrp;
            public List<ProbeRecord> probes = new();
        }

        [Serializable]
        sealed class Fields
        {
            public string name;
            public Vector3 position, size, center, scale;
            public Quaternion rotation;
            public bool hdr, boxProjection, renderDynamicObjects, useOcclusionCulling;
            public int resolution, cullingMask, importance, mode, refreshMode, clearFlags;
            public float nearClip, farClip, blendDistance, intensity, shadowDistance;
            public Color backgroundColor;
        }

        [Serializable]
        sealed class ProbeRecord
        {
            public int instanceId, originalTimeSlicing, appliedTimeSlicing;
            public bool originalEnabled, appliedEnabled, disabledAndReenabledBeforeRender, authoredFieldsPreserved;
            public string restoreDisposition, textureAtRestore;
            public Fields authored;
        }

        sealed class Borrowed
        {
            public ReflectionProbe probe;
            public ProbeRecord record;
            public string authoredJson;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void BeforeFirstScene() => Install("Runtime BeforeSceneLoad");

        internal static void Install(string phase)
        {
            if (installed || finished) return;
            var args = Environment.GetCommandLineArgs();
            string selected = Argument(args, "-cityProbeCollectionControl");
            if (selected == null) return; // Default path registers nothing and creates no output.
            switch (selected)
            {
                case "observe": mode = Mode.Observe; break;
                case "disable": mode = Mode.Disable; break;
                case "individualFaces": mode = Mode.IndividualFaces; break;
                default: throw new AssertionException("Unknown -cityProbeCollectionControl mode: " + selected);
            }
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string logs = Path.Combine(project, "Logs") + Path.DirectorySeparatorChar;
            string requested = Argument(args, "-cityProbeCollectionOut");
            output = requested == null
                ? Path.Combine(logs, "city-probe-collection-control", DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ") + "-" + Process.GetCurrentProcess().Id + ".json")
                : Path.GetFullPath(Path.IsPathRooted(requested) ? requested : Path.Combine(project, requested));
            if (!output.StartsWith(logs, StringComparison.Ordinal) || File.Exists(output) || Directory.Exists(output))
                throw new AssertionException("Diagnostic output must be a fresh file below project Logs: " + output);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            report = new Report
            {
                status = "installed", mode = selected, installedUtc = DateTime.UtcNow.ToString("O"), installationPhase = phase,
                outputPath = output, processId = Process.GetCurrentProcess().Id,
                api = SystemInfo.graphicsDeviceType.ToString(), gpu = SystemInfo.graphicsDeviceName,
                includeBootstrap = args.Contains("-cityProbeCollectionIncludeBootstrap")
            };
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                report.status = "invalid"; report.errors.Add("Collection probe controls require a real graphics device."); Write();
                throw new AssertionException(report.errors[0]);
            }
            installed = true;
            SceneManager.sceneLoaded += Loaded;
            SceneManager.sceneUnloaded += Unloaded;
            RenderPipelineManager.beginContextRendering += BeginContext;
            RenderPipelineManager.endContextRendering += EndContext;
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            Application.quitting += Quitting;
            Write();
            // A fallback is useful for later City loads, but cannot retrospectively certify the current scene's first render.
            if (phase != "Runtime BeforeSceneLoad")
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (scene.isLoaded && IsNamedScene(scene)) Handle(scene, "late existing scene at namespace setup", false);
                }
        }

        static string Argument(string[] args, string key)
        {
            string result = null;
            for (int i = 0; i < args.Length; i++)
                if (args[i] == key)
                {
                    if (result != null || i + 1 >= args.Length || args[i + 1].StartsWith("-", StringComparison.Ordinal))
                        throw new AssertionException(key + " requires one unique value.");
                    result = args[++i];
                }
            return result;
        }

        static bool IsNamedScene(Scene scene) => scene.name == GameManager.CityScene || scene.name == GameManager.MenuScene;
        static void Loaded(Scene scene, LoadSceneMode _) { if (IsNamedScene(scene)) Handle(scene, "sceneLoaded", true); }

        static void Handle(Scene scene, string entry, bool sceneLoadedCallback)
        {
            if (!installed || finished || !seenScenes.Add(scene.handle)) return;
            bool city = scene.name == GameManager.CityScene;
            if (city) report.cityScenes++; else report.bootstrapScenes++;
            var lighting = scene.GetRootGameObjects().Where(value => value.name == "Lighting").ToArray();
            var all = scene.GetRootGameObjects().SelectMany(value => value.GetComponentsInChildren<ReflectionProbe>(true)).ToArray();
            var automatic = all.Where(value => value.mode == ReflectionProbeMode.Realtime && value.refreshMode == ReflectionProbeRefreshMode.OnAwake).ToArray();
            string[] expected = city ? CityNames : MenuNames;
            var record = new SceneRecord
            {
                name = scene.name, path = scene.path, handle = scene.handle, entryPoint = entry, loadedFrame = Time.frameCount,
                expectedAutomaticCount = expected.Length, actualAutomaticCount = automatic.Length,
                unrelatedViaScriptingCount = all.Count(value => value.refreshMode == ReflectionProbeRefreshMode.ViaScripting),
                controlled = mode != Mode.Observe && (city || report.includeBootstrap),
                contextSerialBefore = contextSerial, cameraSerialBefore = cameraSerial, contextDepthAtMutation = contextDepth
            };
            report.scenes.Add(record);
            if (lighting.Length != 1 || automatic.Length != expected.Length || !expected.OrderBy(value => value).SequenceEqual(automatic.Select(value => value.name).OrderBy(value => value)))
                Fail("Expected exact generated Lighting probe inventory in " + scene.name + "; Lighting roots=" + lighting.Length + ", automatic count=" + automatic.Length);
            foreach (var probe in automatic.OrderBy(value => value.name))
            {
                if (probe.transform.parent != lighting[0].transform || probe.timeSlicingMode != ReflectionProbeTimeSlicingMode.IndividualFaces ||
                    probe.resolution != (probe.name == "Probe_Alley" ? 128 : 256) || !probe.hdr || probe.cullingMask != -1 || probe.farClipPlane != 250f)
                    Fail("Generated probe specification differs before diagnostic control: " + scene.name + "/" + probe.name);
                var fields = Read(probe);
                var item = new ProbeRecord
                {
                    instanceId = probe.GetInstanceID(), authored = fields,
                    originalEnabled = probe.enabled, originalTimeSlicing = (int)probe.timeSlicingMode,
                    appliedEnabled = probe.enabled, appliedTimeSlicing = (int)probe.timeSlicingMode
                };
                record.probes.Add(item);
                if (record.controlled)
                {
                    if (!sceneLoadedCallback || contextDepth != 0)
                        Fail("Late or in-render diagnostic control is ineligible: " + scene.name + " at " + entry + ", SRP depth=" + contextDepth);
                    if (!probe.enabled || !probe.gameObject.activeInHierarchy)
                        Fail("The generated automatic probe must be active before control: " + probe.name);
                    var state = new Borrowed { probe = probe, record = item, authoredJson = JsonUtility.ToJson(fields) };
                    borrowed.Add(state);
                    if (mode == Mode.Disable) probe.enabled = false;
                    else
                    {
                        // This hook is after Awake. Cancel/requeue once before observed rendering with the slicing field already set.
                        probe.enabled = false;
                        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
                        probe.enabled = item.originalEnabled;
                        item.disabledAndReenabledBeforeRender = true;
                    }
                    item.appliedEnabled = probe.enabled; item.appliedTimeSlicing = (int)probe.timeSlicingMode;
                    item.authoredFieldsPreserved = state.authoredJson == JsonUtility.ToJson(Read(probe));
                    if (!item.authoredFieldsPreserved) Fail("An unborrowed authored field changed in " + probe.name);
                    record.fieldsChanged = true;
                }
                else item.authoredFieldsPreserved = true;
            }
            record.contextSerialAfter = contextSerial; record.cameraSerialAfter = cameraSerial;
            record.beforeObservedSrp = sceneLoadedCallback && contextDepth == 0 && record.contextSerialBefore == contextSerial && record.cameraSerialBefore == cameraSerial;
            record.eligibility = record.beforeObservedSrp
                ? "sceneLoaded field handling completed before any additional SRP callback observed by this installed helper; Awake/OnEnable already occurred"
                : "late observation only; initial rendering before helper installation cannot be excluded";
            if (record.controlled)
            {
                if (!record.beforeObservedSrp) report.allControlledMutationsBeforeObservedSrp = false;
                if (city) report.controlledCityScenes++; else report.controlledBootstrapScenes++;
            }
            report.status = report.errors.Count == 0 ? "observing collection" : "invalid"; Write();
        }

        static Fields Read(ReflectionProbe value) => new()
        {
            name = value.name, position = value.transform.position, rotation = value.transform.rotation, scale = value.transform.lossyScale, size = value.size, center = value.center,
            hdr = value.hdr, boxProjection = value.boxProjection, renderDynamicObjects = value.renderDynamicObjects,
            useOcclusionCulling = ReadSerializedOcclusion(value), resolution = value.resolution, cullingMask = value.cullingMask,
            importance = value.importance, mode = (int)value.mode, refreshMode = (int)value.refreshMode, clearFlags = (int)value.clearFlags,
            nearClip = value.nearClipPlane, farClip = value.farClipPlane, blendDistance = value.blendDistance,
            intensity = value.intensity, shadowDistance = value.shadowDistance, backgroundColor = value.backgroundColor
        };

        static bool ReadSerializedOcclusion(ReflectionProbe probe)
        {
            // This authoring field is serialized but has no public ReflectionProbe property in installed Unity6000.3.
            using var serialized = new UnityEditor.SerializedObject(probe);
            var property = serialized.FindProperty("m_UseOcclusionCulling");
            if (property == null) throw new AssertionException("Installed ReflectionProbe serialized occlusion field is unavailable.");
            return property.boolValue; // Read only; never ApplyModifiedProperties.
        }

        static void BeginContext(ScriptableRenderContext _, List<Camera> __)
        {
            contextSerial++; contextDepth++;
            bool native = QualitySettings.realtimeReflectionProbes;
            foreach (var scene in report.scenes)
            {
                if (scene.unloadedFrame >= 0) continue;
                if (scene.firstContextSerial < 0) { scene.firstContextSerial = contextSerial; scene.firstContextFrame = Time.frameCount; }
                if (native)
                {
                    scene.contextsWithNativeSchedulingEnabled++;
                    if (scene.firstNativeEnabledContextSerial < 0)
                    { scene.firstNativeEnabledContextSerial = contextSerial; scene.firstNativeEnabledContextFrame = Time.frameCount; }
                }
                else scene.contextsWithNativeSchedulingDisabled++;
            }
        }
        static void EndContext(ScriptableRenderContext _, List<Camera> __) { contextDepth = Math.Max(0, contextDepth - 1); }
        static void BeginCamera(ScriptableRenderContext _, Camera camera)
        {
            cameraSerial++;
            foreach (var scene in report.scenes)
            {
                if (scene.unloadedFrame >= 0 || scene.firstCameraSerial >= 0) continue;
                scene.firstCameraSerial = cameraSerial; scene.firstCameraFrame = Time.frameCount;
                scene.firstCameraName = camera ? camera.name : "missing"; scene.firstCameraType = camera ? camera.cameraType.ToString() : "missing";
            }
        }
        static void Unloaded(Scene unloaded)
        {
            seenScenes.Remove(unloaded.handle); // Scene handles may be reused by later loads.
            foreach (var scene in report.scenes.Where(value => value.handle == unloaded.handle && value.unloadedFrame < 0)) scene.unloadedFrame = Time.frameCount;
            Write();
        }

        static void Fail(string message)
        {
            report.errors.Add(message); report.status = "invalid"; Write();
            throw new AssertionException(message);
        }
        static void Quitting() => Finish("Application quitting fallback", false);

        internal static void Finish(string reason, bool throwForInvalid)
        {
            if (!installed || finished) return;
            finished = true;
            SceneManager.sceneLoaded -= Loaded; SceneManager.sceneUnloaded -= Unloaded;
            RenderPipelineManager.beginContextRendering -= BeginContext; RenderPipelineManager.endContextRendering -= EndContext;
            RenderPipelineManager.beginCameraRendering -= BeginCamera; Application.quitting -= Quitting;
            if (report.cityScenes == 0) report.errors.Add("No City scene was observed; this does not test the requested collection.");
            if (!report.scenes.Any(value => value.name == GameManager.CityScene && value.contextsWithNativeSchedulingEnabled > 0)) report.errors.Add("No City SRP context with native scheduling enabled was observed; no active capture-stress inference is possible.");
            if (mode != Mode.Observe && report.controlledCityScenes != report.cityScenes) report.errors.Add("Not every City load received an eligible control.");
            bool oldNative = QualitySettings.realtimeReflectionProbes;
            report.nativeFlagBeforeCleanup = oldNative;
            var live = borrowed.Where(value => value.probe).ToArray();
            report.alreadyDestroyedProbes = borrowed.Count - live.Length;
            try
            {
                // Cleanup only: do not re-enable an OnAwake component while native scheduling is active.
                // During collection execution the helper never changes this flag or any preference.
                if (live.Length > 0 && oldNative)
                {
                    QualitySettings.realtimeReflectionProbes = false;
                    report.cleanupTemporarilySuspendedNativeScheduling = true;
                }
                foreach (var state in borrowed)
                {
                    if (!state.probe) { state.record.restoreDisposition = "probe already destroyed by its original scene lifecycle"; continue; }
                    try
                    {
                        var texture = state.probe.texture;
                        state.record.textureAtRestore = texture ? texture.GetType().Name + "/" + texture.width + "x" + texture.height : "none";
                        if (state.authoredJson != JsonUtility.ToJson(Read(state.probe))) report.errors.Add("Unborrowed fields differ at cleanup: " + state.probe.name);
                        state.probe.timeSlicingMode = (ReflectionProbeTimeSlicingMode)state.record.originalTimeSlicing;
                        state.probe.enabled = state.record.originalEnabled;
                        bool restored = state.probe.enabled == state.record.originalEnabled && (int)state.probe.timeSlicingMode == state.record.originalTimeSlicing;
                        state.record.restoreDisposition = restored ? "borrowed fields restored; automatic native request status remains unobserved" : "restore mismatch";
                        if (!restored) report.errors.Add("Borrowed state restore mismatch: " + state.probe.name);
                        else report.restoredLiveProbes++;
                    }
                    catch (Exception error) { state.record.restoreDisposition = "cleanup exception: " + error.Message; report.errors.Add(state.record.restoreDisposition); }
                }
            }
            finally { QualitySettings.realtimeReflectionProbes = oldNative; }
            report.nativeFlagAfterCleanup = QualitySettings.realtimeReflectionProbes;
            report.cleanupNativeFlagRestored = report.nativeFlagAfterCleanup == oldNative;
            if (!report.cleanupNativeFlagRestored) report.errors.Add("Global native scheduling flag did not restore exactly.");
            report.finishedUtc = DateTime.UtcNow.ToString("O"); report.finishReason = reason;
            report.status = report.errors.Count == 0 ? "diagnostic cleanup complete; automatic native jobs unobserved" : "invalid";
            Write();
            borrowed.Clear(); seenScenes.Clear(); installed = false;
            if (throwForInvalid && report.errors.Count > 0) throw new AssertionException(string.Join("; ", report.errors));
        }

        static void Write()
        {
            if (report == null || output == null) return;
            string temporary = output + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(report, true));
            if (File.Exists(output)) File.Replace(temporary, output, null);
            else File.Move(temporary, output);
        }
    }
}

#endif
