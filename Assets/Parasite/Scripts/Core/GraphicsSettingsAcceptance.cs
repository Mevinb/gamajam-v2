using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Parasite.Possession;
using Parasite.UI;
using Parasite.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Parasite.Testing
{
    /// <summary>Opt-in rendered Linux acceptance: -graphicsSettingsTestOut /absolute/report.json. Inert without the flag.</summary>
    public sealed class GraphicsSettingsAcceptance : MonoBehaviour
    {
        [Serializable] sealed class Check { public string name, detail; public bool passed; }
        [Serializable] sealed class InputReceipt { public string device, key, selected, module; public int frame, moveEvents, submitEvents; }
        [Serializable] sealed class ImageReceipt { public string file, sha256, view; public int width, height; public bool focused; public float minimum, maximum; }
        [Serializable] sealed class RenderReceipt
        {
            public string phase, pipeline, camera, postAA, renderedPostSource; public int cameraId, effectiveMsaa, pipelineMsaa, vSync, frameLimit, renderedCameraId, renderedFrame, renderedStackId;
            public bool hdr, cameraMsaa, aoFound, aoActive, realtimeProbes, bloomZero, grainZero, chromaticZero, postEnabled, renderedBloomZero, renderedGrainZero, renderedChromaticZero;
            public float renderScale, shadowDistance, ssrSteps;
        }
        [Serializable] sealed class PausedInputReceipt
        {
            public int movePerformed, splitPerformed, heldMoveFrames, heldSplitFrames; public float maximumMoveY;
            public double started, ended;
        }
        sealed class RenderedPost
        {
            public int cameraId, frame, stackId; public string source; public bool enabled, bloomZero, grainZero, chromaticZero;
        }
        [Serializable] sealed class Pacing
        {
            public string scope = "Raw frame intervals in one three-second idle City window at requested VSync Off / 30 FPS. No achieved 30-FPS bound is enforced and this is not a broad benchmark.";
            public double started, ended; public float[] intervalsMs; public bool[] focused;
            public float meanMs, medianMs, p95Ms, minimumMs, maximumMs; public int requestedCap;
        }
        [Serializable] sealed class Report
        {
            public int schema = 2, expectedNativeExit = 1; public string result = "RUNNING", startedUtc, endedUtc, device, api, platform, error;
            public string inputScope = "Most widget focus is assigned programmatically. Values and submission use queued virtual keyboard/gamepad through the normal InputSystemUIInputModule; five Down-arrow events separately prove actual focus navigation and scrolling. No physical-device claim.";
            public string sceneScope = "Real title/pause widgets and saved City reloads. Direct City scene load deliberately omits the unrelated opening film.";
            public string restorationError, originalPreferences, restoredPreferences, interruptionReason;
            public List<string> cleanupErrors = new();
            public bool restored, quitRequested, quitObserved, interrupted; public int width, height;
            public GraphicsSettingsData initialSettings, expectedCustom, appliedCustom, reloadedSettings;
            public List<Check> checks = new(); public List<InputReceipt> inputs = new(); public List<ImageReceipt> images = new(); public List<RenderReceipt> renders = new();
            public Pacing pacing = new(); public PausedInputReceipt pausedInput = new();
        }
        static readonly FieldInfo DraftField = typeof(SettingsOverlay).GetField("draft", BindingFlags.Instance | BindingFlags.NonPublic);
        string output; Report report = new(); Snapshot snapshot; Keyboard keyboard; Gamepad gamepad;
        InputSystemUIInputModule module; int moveEvents, submitEvents; bool failed; SettingsOverlay overlay;
        readonly List<InputSystemUIInputModule> modules = new();
        bool restorationStarted, restorationFinished; RenderedPost lastRenderedPost;
        Action<InputAction.CallbackContext> pausedMoveCallback, pausedSplitCallback;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-graphicsSettingsTestOut");
            if (index < 0) return;
            if (FindFirstObjectByType<GraphicsSettingsAcceptance>()) return;
            var go = new GameObject("Graphics settings native acceptance"); DontDestroyOnLoad(go);
            var owner = go.AddComponent<GraphicsSettingsAcceptance>(); owner.output = index + 1 < args.Length ? args[index + 1] : "";
        }
        void OnEnable()
        {
            Application.wantsToQuit += BeforeQuit; Application.quitting += OnQuit;
            RenderPipelineManager.endCameraRendering += OnEndCamera;
        }
        void OnDisable()
        {
            Application.wantsToQuit -= BeforeQuit; Application.quitting -= OnQuit;
            RenderPipelineManager.endCameraRendering -= OnEndCamera;
        }
        bool BeforeQuit()
        {
            if (!report.quitRequested) Interrupt("Window/OS/other external quit requested before acceptance completed");
            return true;
        }
        void OnQuit()
        {
            if (!report.quitRequested) Interrupt("application.quitting fallback before acceptance completed");
            report.quitObserved = true;
            if (!restorationFinished) Restore();
            WriteReport();
        }
        void Update()
        {
            // An earlier native-media quit handler can defer wantsToQuit delivery until its readers drain.
            if (!report.quitRequested && !report.interrupted && CinematicFilmLifetime.IsQuitting)
                Interrupt("External native shutdown observed while acceptance was still running");
        }
        void Interrupt(string reason)
        {
            if (report.interrupted) return;
            report.interrupted = true; failed = true; report.result = "INTERRUPTED"; report.expectedNativeExit = -1;
            report.interruptionReason = reason + ". External native exit code is not controlled by this harness and cannot establish PASS.";
            report.endedUtc = DateTime.UtcNow.ToString("O");
            // Restore synchronously before accepting quit; coroutine disposal is not a cleanup receipt.
            Restore(); StopAllCoroutines(); WriteReport();
        }
        void OnEndCamera(ScriptableRenderContext context, Camera camera)
        {
            if (!camera || camera.cameraType != CameraType.Game || camera != Camera.main || restorationStarted) return;
            var extra = camera.GetComponent<UniversalAdditionalCameraData>();
            // Sample inside THIS camera's callback. The EOF global stack can belong to a later camera.
            var stack = VolumeManager.instance.stack;
            var bloom = stack?.GetComponent<Bloom>(); var grain = stack?.GetComponent<FilmGrain>(); var chromatic = stack?.GetComponent<ChromaticAberration>();
            lastRenderedPost = new RenderedPost { cameraId = camera.GetInstanceID(), frame = Time.frameCount,
                stackId = stack == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(stack),
                source = "Camera.main endCameraRendering / active URP VolumeManager stack", enabled = extra && extra.renderPostProcessing,
                bloomZero = bloom && bloom.intensity.value == 0, grainZero = grain && grain.intensity.value == 0, chromaticZero = chromatic && chromatic.intensity.value == 0 };
        }
        IEnumerator Start()
        {
            report.startedUtc = DateTime.UtcNow.ToString("O"); report.device = SystemInfo.graphicsDeviceName;
            report.api = SystemInfo.graphicsDeviceType.ToString(); report.platform = Application.platform.ToString();
            report.width = Screen.width; report.height = Screen.height;
            try { yield return Guard(Exercise()); }
            finally { Restore(); }
            if (report.interrupted) yield break;
            bool clean = false; yield return CinematicFilmLifetime.Drain(CinematicFilmLifetime.DrainTimeout, value => clean = value);
            if (report.interrupted) yield break;
            Record(clean && CinematicFilmLifetime.ResourceCount == 0 && CinematicFilmLifetime.PendingCount == 0 && CinematicFilmLifetime.QuarantinedCount == 0,
                "Native media drained", $"owned={CinematicFilmLifetime.ResourceCount}, pending={CinematicFilmLifetime.PendingCount}, quarantine={CinematicFilmLifetime.QuarantinedCount}");
            report.endedUtc = DateTime.UtcNow.ToString("O"); report.result = failed ? "FAIL" : "PASS";
            report.expectedNativeExit = failed ? 1 : 0; report.quitRequested = true; WriteReport();
            if (failed) { report.result = "FAIL"; report.expectedNativeExit = 1; }
            Debug.Log($"[GraphicsSettingsAcceptance] {report.result}: {output}");
            CinematicFilmLifetime.Quit(report.expectedNativeExit);
        }
        IEnumerator Guard(IEnumerator body)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(body);
            try
            {
                while (stack.Count > 0)
                {
                    object yielded = null; bool more = false; Exception failure = null;
                    try { more = stack.Peek().MoveNext(); if (more) yielded = stack.Peek().Current; }
                    catch (Exception error) { failure = error; }
                    if (failure != null) { failed = true; report.error = failure.ToString(); Debug.LogError("[GraphicsSettingsAcceptance] " + failure); yield break; }
                    if (!more) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                    if (yielded is IEnumerator nested) stack.Push(nested); else yield return yielded;
                }
            }
            finally { while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose(); }
        }
        IEnumerator Exercise()
        {
            Require(!string.IsNullOrWhiteSpace(output) && Path.IsPathRooted(output), "Absolute report path", output);
            output = Path.GetFullPath(output); Directory.CreateDirectory(Path.GetDirectoryName(output));
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null && Application.platform == RuntimePlatform.LinuxPlayer,
                "Rendered Linux player", report.platform + " / " + report.api);
            Require(report.device.IndexOf("RTX", StringComparison.OrdinalIgnoreCase) >= 0, "RTX graphics device", report.device);
            snapshot = new Snapshot(); report.initialSettings = snapshot.settings.Clone(); report.originalPreferences = snapshot.preferences.Signature();
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            // Isolate queued-device evidence from accidental physical input and restore each original enabled state in finally.
            foreach (var state in snapshot.devices) if (state.enabled && (state.device is Keyboard || state.device is Mouse || state.device is Gamepad)) InputSystem.DisableDevice(state.device);
            keyboard = InputSystem.AddDevice<Keyboard>("GraphicsAcceptanceKeyboard"); gamepad = InputSystem.AddDevice<Gamepad>("GraphicsAcceptanceGamepad");
            keyboard.MakeCurrent(); GameInput.Simulated = false; GameInput.Ensure();
            RunState.NewGame(); GraphicsPreset.ApplySettings(GraphicsSettingsData.ForPreset(2));
            yield return LoadScene(GameManager.MenuScene);
            var menu = FindFirstObjectByType<MainMenuController>(); Require(menu, "Actual title menu exists");
            yield return Activate(menu.transform, "Btn_Settings"); overlay = ActiveOverlay();
            Require(!overlay.HasUnappliedGraphics && GraphicsPreset.Current == 2 && !GraphicsPreset.IsCustom, "Known High baseline");
            string baselinePrefs = PreferenceState.Read().Signature(); string baselineRender = RenderSignature();
            yield return StageCustom();
            report.expectedCustom = Expected();
            Require(ReadDraft().Equals(report.expectedCustom), "All independent widgets stage expected Custom", JsonUtility.ToJson(ReadDraft()));
            RequireUnapplied(baselinePrefs, baselineRender, "Initial staging");
            yield return Page("Display"); Select(overlay.transform, "RenderScale"); yield return Capture("01-title-display-draft", "Actual title Settings: unapplied display draft");
            // The preset cycler also travels through the real UI move action. Pacing survives that draft profile change.
            Select(overlay.transform, "Btn_Graphics"); yield return Pad(GamepadButton.DpadRight);
            var profileDraft = ReadDraft();
            Require(profileDraft.preset == 3 && !profileDraft.vSync && profileDraft.frameLimit == 30, "Preset changes retain independent frame pacing");
            RequireUnapplied(baselinePrefs, baselineRender, "Preset draft staging");
            yield return Activate(overlay.transform, "Btn_Cancel");
            Require(!overlay.HasUnappliedGraphics && ReadDraft().Equals(GraphicsSettingsData.ForPreset(2)), "Cancel restores applied High draft");
            RequireUnapplied(baselinePrefs, baselineRender, "Cancel preserves renderer and preferences");
            yield return StageCustom(); Require(ReadDraft().Equals(report.expectedCustom), "Restaged exact Custom");
            yield return Activate(overlay.transform, "Btn_Apply");
            report.appliedCustom = GraphicsPreset.CaptureSettings();
            Require(report.appliedCustom.Equals(report.expectedCustom) && GraphicsPreset.IsCustom && GraphicsPreset.DisplayName == "Custom (High)" && !overlay.HasUnappliedGraphics, "Apply commits Custom readout and complete independent state");
            CheckPersisted("Apply writes checksummed preferences"); yield return RenderFrames(); CheckRenderer("title-applied");
            // Prove real focus navigation reaches the clipped bottom Quality row and scrolls it into view.
            yield return Page("Quality"); Select(overlay.transform, "Btn_PostAA");
            for (int i = 0; i < 5; i++) yield return KeyPress(Key.DownArrow);
            Require(EventSystem.current.currentSelectedGameObject == Find(overlay.transform, "Btn_Reflections").gameObject, "Keyboard navigation reaches final Quality row");
            yield return RenderFrames(); RequireVisible(Find(overlay.transform, "Btn_Reflections"));
            yield return Capture("02-title-quality-applied", "Actual title Settings: applied Custom and scrolled Quality");
            yield return Page("Effects"); Select(overlay.transform, "Btn_Bloom"); yield return Pad(GamepadButton.DpadRight);
            Require(overlay.HasUnappliedGraphics && ReadDraft().bloom, "Real gamepad stages one further effect");
            yield return KeyPress(Key.Escape);
            Require(!overlay.gameObject.activeInHierarchy && GraphicsPreset.CaptureSettings().Equals(report.expectedCustom), "Title Back discards draft");
            CheckPersisted("Back leaves applied serialized preferences intact");
            yield return Activate(menu.transform, "Btn_Settings"); overlay = ActiveOverlay();
            Require(!overlay.HasUnappliedGraphics && ReadDraft().Equals(report.expectedCustom), "Title reopening reads applied Custom");
            yield return KeyPress(Key.Escape);

            ReloadSavedPolicy();
            yield return LoadScene(GameManager.CityScene); yield return RenderFrames();
            Require(CityDirector.Instance && CityDirector.Instance.Ready && PossessionManager.Instance && PossessionManager.Instance.ActiveBody != null,
                "Actual City managers and player ready");
            int firstCityCamera = Camera.main ? Camera.main.GetInstanceID() : 0; CheckPersisted("City load retains saved Custom"); CheckRenderer("city-late-camera");
            yield return ObservePacing();
            yield return KeyPress(Key.Escape);
            Require(GameManager.Instance.State == GameState.Paused && Time.timeScale == 0f, "Queued Esc pauses actual gameplay");
            yield return Activate(HUDController.Instance.transform, "Btn_Settings"); overlay = ActiveOverlay();
            Require(ReadDraft().Equals(report.expectedCustom), "Pause settings shares saved Custom");
            yield return RenderFrames(); var pm = PossessionManager.Instance; Vector3 position = pm.ActiveBody.Root.position;
            int agents = pm.Agents.Count, mission = RunState.MissionIndex; float playTime = RunState.PlayTime; string flags = Flags();
            yield return ObservePausedActions();
            Require(report.pausedInput.movePerformed > 0 && report.pausedInput.splitPerformed > 0 && report.pausedInput.heldMoveFrames > 0
                && report.pausedInput.heldSplitFrames > 0 && report.pausedInput.maximumMoveY > .9f,
                "Paused W/V bindings actually actuate gameplay actions and held reads", JsonUtility.ToJson(report.pausedInput));
            Require(pm.ActiveBody.Root.position == position && pm.Agents.Count == agents && RunState.MissionIndex == mission && RunState.PlayTime == playTime && Flags() == flags,
                "Paused Settings blocks queued movement, split and progression", $"agents={agents}, mission={mission}, playTime={playTime:R}, position={position}");
            yield return Page("Effects"); Select(overlay.transform, "Btn_Bloom"); yield return Pad(GamepadButton.DpadRight);
            yield return Capture("03-city-paused-effects-draft", "Actual City HUD pause Settings: effect draft; no fabricated HUD");
            yield return KeyPress(Key.Escape);
            Require(!overlay.gameObject.activeInHierarchy && GameManager.Instance.State == GameState.Paused && Time.timeScale == 0f,
                "First Esc closes Settings before unpausing");
            Require(GraphicsPreset.CaptureSettings().Equals(report.expectedCustom), "Pause Back discards effect draft");
            yield return KeyPress(Key.Escape); Require(GameManager.Instance.IsPlaying && Time.timeScale == 1f, "Second Esc resumes gameplay");
            yield return LoadScene(GameManager.CityScene); yield return RenderFrames();
            Require(Camera.main && Camera.main.GetInstanceID() != firstCityCamera, "Reload creates a genuinely late City camera");
            CheckPersisted("Second City reload deserializes saved Custom"); CheckRenderer("city-reloaded-camera");
            yield return KeyPress(Key.Escape); yield return Activate(HUDController.Instance.transform, "Btn_Quit to Menu");
            yield return WaitFor(() => SceneManager.GetActiveScene().name == GameManager.MenuScene && FindFirstObjectByType<MainMenuController>(), 20, "Quit to Menu completes");
            yield return RenderFrames(); menu = FindFirstObjectByType<MainMenuController>();
            yield return Activate(menu.transform, "Btn_Settings"); overlay = ActiveOverlay();
            Require(ReadDraft().Equals(report.expectedCustom) && !overlay.HasUnappliedGraphics, "Title scene reload preserves independent Custom");
            CheckPersisted("Title reload checksummed preferences unchanged"); CheckRenderer("title-reloaded");
            Require(report.images.Count == 3 && report.inputs.Any(row => row.device == "Keyboard" && row.moveEvents > 0) && report.inputs.Any(row => row.device == "Gamepad" && row.moveEvents > 0)
                && report.inputs.Any(row => row.submitEvents > 0), "Three actual UI captures and both queued UI device paths recorded");
        }
        IEnumerator ObservePausedActions()
        {
            void MovePerformed(InputAction.CallbackContext value) { if (value.control.device == keyboard) report.pausedInput.movePerformed++; }
            void SplitPerformed(InputAction.CallbackContext value) { if (value.control.device == keyboard) report.pausedInput.splitPerformed++; }
            pausedMoveCallback = MovePerformed; pausedSplitCallback = SplitPerformed;
            GameInput.Move.performed += pausedMoveCallback; GameInput.Split.performed += pausedSplitCallback;
            try
            {
                report.pausedInput.started = Time.realtimeSinceStartupAsDouble;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.V));
                double until = report.pausedInput.started + .4;
                while (Time.realtimeSinceStartupAsDouble < until)
                {
                    yield return null;
                    float y = GameInput.Move.ReadValue<Vector2>().y;
                    report.pausedInput.maximumMoveY = Mathf.Max(report.pausedInput.maximumMoveY, y);
                    if (y > .9f) report.pausedInput.heldMoveFrames++;
                    if (GameInput.Split.IsPressed()) report.pausedInput.heldSplitFrames++;
                }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return RenderFrames();
                report.pausedInput.ended = Time.realtimeSinceStartupAsDouble;
            }
            finally { GameInput.Move.performed -= MovePerformed; GameInput.Split.performed -= SplitPerformed; }
        }
        static GraphicsSettingsData Expected()
        {
            var value = GraphicsSettingsData.ForPreset(2); value.vSync = false; value.frameLimit = 30; value.renderScale = .85f;
            value.shadows = false; value.shadowDistance = 57; value.ambientOcclusion = false; value.reflectionLevel = 3;
            value.antiAliasing = 1; value.msaaSamples = 2; value.bloom = value.filmGrain = value.chromaticAberration = false; return value;
        }
        IEnumerator StageCustom()
        {
            yield return Page("Display"); yield return SliderTo("RenderScale", 85);
            Select(overlay.transform, "Btn_VSync"); yield return Pad(GamepadButton.DpadRight);
            Select(overlay.transform, "Btn_FrameLimit"); yield return KeyPress(Key.RightArrow);
            yield return Page("Quality"); Select(overlay.transform, "Btn_PostAA"); yield return KeyPress(Key.LeftArrow);
            Select(overlay.transform, "Btn_MSAA"); yield return Pad(GamepadButton.DpadRight);
            Select(overlay.transform, "Btn_Shadows"); yield return KeyPress(Key.RightArrow);
            yield return SliderTo("ShadowDistance", 57);
            Select(overlay.transform, "Btn_AmbientOcclusion"); yield return KeyPress(Key.RightArrow);
            Select(overlay.transform, "Btn_Reflections"); yield return Pad(GamepadButton.DpadRight);
            yield return Page("Effects");
            foreach (string row in new[] { "Btn_Bloom", "Btn_FilmGrain", "Btn_ChromaticAberration" }) { Select(overlay.transform, row); yield return KeyPress(Key.RightArrow); }
        }
        IEnumerator Page(string name) => Activate(overlay.transform, "Btn_" + name);
        IEnumerator Activate(Transform scope, string name) { Select(scope, name); yield return KeyPress(Key.Enter); yield return null; }
        void Select(Transform scope, string name)
        {
            BindModule(); var target = Find(scope, name); Require(target.gameObject.activeInHierarchy && target.interactable, "Widget selectable", name);
            EventSystem.current.SetSelectedGameObject(target.gameObject);
        }
        static Selectable Find(Transform scope, string name)
            => scope.GetComponentsInChildren<Selectable>(true).FirstOrDefault(item => item.name == name && item.gameObject.activeInHierarchy)
                ?? throw new InvalidOperationException("Active actual widget missing: " + name);
        SettingsOverlay ActiveOverlay() => FindObjectsByType<SettingsOverlay>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Single();
        GraphicsSettingsData ReadDraft() => ((GraphicsSettingsData)DraftField.GetValue(overlay)).Clone();
        void BindModule()
        {
            Require(EventSystem.current && EventSystem.current.currentInputModule is InputSystemUIInputModule, "Normal InputSystem UI module");
            module = (InputSystemUIInputModule)EventSystem.current.currentInputModule;
            if (modules.Contains(module)) return; modules.Add(module);
            Require(module.move && module.move.action.enabled && module.submit && module.submit.action.enabled, "Normal default UI move and submit actions enabled");
            module.move.action.performed += OnMove; module.submit.action.performed += OnSubmit;
        }
        void OnMove(InputAction.CallbackContext context) { if (context.control.device == keyboard || context.control.device == gamepad) moveEvents++; }
        void OnSubmit(InputAction.CallbackContext context) { if (context.control.device == keyboard || context.control.device == gamepad) submitEvents++; }
        IEnumerator KeyPress(Key key)
        {
            BindModule(); int moves = moveEvents, submits = submitEvents; string selected = EventSystem.current.currentSelectedGameObject?.name;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
            report.inputs.Add(new InputReceipt { device = "Keyboard", key = key.ToString(), selected = selected, module = module ? module.GetType().Name : "scene changed", frame = Time.frameCount, moveEvents = moveEvents - moves, submitEvents = submitEvents - submits });
        }
        IEnumerator Pad(GamepadButton button)
        {
            BindModule(); int moves = moveEvents, submits = submitEvents; string selected = EventSystem.current.currentSelectedGameObject?.name;
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(button)); yield return null; yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null; yield return null;
            report.inputs.Add(new InputReceipt { device = "Gamepad", key = button.ToString(), selected = selected, module = module ? module.GetType().Name : "scene changed", frame = Time.frameCount, moveEvents = moveEvents - moves, submitEvents = submitEvents - submits });
        }
        IEnumerator SliderTo(string name, int requested)
        {
            Select(overlay.transform, name); var slider = Find(overlay.transform, name) as Slider; Require(slider && slider.wholeNumbers, "Actual integer graphics slider", name);
            int limit = Mathf.Abs(Mathf.RoundToInt(slider.value) - requested) + 1;
            while (Mathf.RoundToInt(slider.value) != requested && limit-- > 0)
            {
                float old = slider.value; yield return KeyPress(slider.value > requested ? Key.LeftArrow : Key.RightArrow);
                Require(slider.value != old, "Queued keyboard changes actual slider", name + "=" + slider.value);
            }
            Require(slider.value == requested, "Slider reaches requested value", name + "=" + slider.value);
        }
        static IEnumerator RenderFrames() { yield return null; yield return null; yield return new WaitForEndOfFrame(); }
        IEnumerator LoadScene(string name)
        {
            Time.timeScale = 1; var loading = SceneManager.LoadSceneAsync(name, LoadSceneMode.Single);
            yield return WaitFor(() => loading.isDone && (name != GameManager.CityScene || CityDirector.Instance && CityDirector.Instance.Ready), 25, "Scene load " + name);
            GameInput.Simulated = false; yield return RenderFrames();
        }
        IEnumerator WaitFor(Func<bool> condition, float seconds, string label)
        {
            double end = Time.realtimeSinceStartupAsDouble + seconds;
            while (!condition() && Time.realtimeSinceStartupAsDouble < end) yield return null;
            Require(condition(), label);
        }
        void RequireUnapplied(string preferences, string renderer, string phase)
        {
            Require(GraphicsPreset.CaptureSettings().Equals(GraphicsSettingsData.ForPreset(2)) && PreferenceState.Read().Signature() == preferences
                && RenderSignature() == renderer, phase + " does not apply or write");
        }
        string RenderSignature()
        {
            var state = RenderState("baseline"); state.renderedFrame = state.renderedStackId = 0;
            return JsonUtility.ToJson(state);
        }
        void CheckPersisted(string name)
        {
            string raw = PlayerPrefs.GetString(GraphicsSettingsStore.PreferenceKey, "");
            Require(GraphicsSettingsStore.TryDeserialize(raw, out var saved) && saved.Equals(report.expectedCustom) && GraphicsSettingsStore.Load().Equals(report.expectedCustom)
                && GraphicsPreset.CaptureSettings().Equals(report.expectedCustom) && PlayerPrefs.GetInt(GraphicsSettingsStore.LegacyPresetKey, -1) == 2, name);
            report.reloadedSettings = saved;
        }
        void ReloadSavedPolicy()
        {
            string stored = PreferenceState.Read().Signature();
            // A deliberately distinct unsaved in-memory policy proves the following load uses persisted data.
            GraphicsPreset.ApplySettings(GraphicsSettingsData.ForPreset(0), false);
            Require(!GraphicsPreset.CaptureSettings().Equals(report.expectedCustom) && PreferenceState.Read().Signature() == stored,
                "Preference reload fixture replaces only live policy before loading saved data");
            var loaded = GraphicsSettingsStore.Load(); GraphicsPreset.ApplySettings(loaded, false);
            Require(loaded.Equals(report.expectedCustom) && GraphicsPreset.CaptureSettings().Equals(report.expectedCustom) && PreferenceState.Read().Signature() == stored,
                "Actual saved preference deserialize reload restores Custom from distinct live policy");
        }
        RenderReceipt RenderState(string phase)
        {
            var owner = GraphicsPreset.RuntimeOwner; var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset; var camera = Camera.main;
            var extra = camera ? camera.GetComponent<UniversalAdditionalCameraData>() : null;
            var features = pipeline ? pipeline.rendererDataList.ToArray().Where(data => data).SelectMany(data => data.rendererFeatures).Where(feature => feature && feature.GetType().Name == "ScreenSpaceAmbientOcclusion").ToArray() : Array.Empty<ScriptableRendererFeature>();
            var volume = owner ? owner.GetComponent<Volume>() : null; var profile = volume ? volume.sharedProfile : null;
            bool bloom = profile && profile.TryGet<Bloom>(out var b) && b.active && b.intensity.overrideState && b.intensity.value == 0;
            bool grain = profile && profile.TryGet<FilmGrain>(out var g) && g.active && g.intensity.overrideState && g.intensity.value == 0;
            bool chromatic = profile && profile.TryGet<ChromaticAberration>(out var c) && c.active && c.intensity.overrideState && c.intensity.value == 0;
            var rendered = lastRenderedPost;
            return new RenderReceipt { phase = phase, pipeline = pipeline ? pipeline.name : "missing", camera = camera ? camera.name : "missing", cameraId = camera ? camera.GetInstanceID() : 0,
                renderScale = pipeline ? pipeline.renderScale : 0, shadowDistance = pipeline ? pipeline.shadowDistance : -1, pipelineMsaa = pipeline ? pipeline.msaaSampleCount : 0,
                effectiveMsaa = GraphicsPreset.EffectiveMsaaSamples, vSync = QualitySettings.vSyncCount, frameLimit = Application.targetFrameRate,
                hdr = camera && camera.allowHDR, cameraMsaa = camera && camera.allowMSAA, postAA = extra ? extra.antialiasing.ToString() : "missing",
                aoFound = features.Length > 0, aoActive = features.Any(feature => feature.isActive), realtimeProbes = QualitySettings.realtimeReflectionProbes,
                ssrSteps = Shader.GetGlobalFloat("_ParasiteSSRSteps"), bloomZero = bloom, grainZero = grain, chromaticZero = chromatic,
                postEnabled = rendered != null && rendered.enabled, renderedBloomZero = rendered != null && rendered.bloomZero,
                renderedGrainZero = rendered != null && rendered.grainZero, renderedChromaticZero = rendered != null && rendered.chromaticZero,
                renderedCameraId = rendered?.cameraId ?? 0, renderedFrame = rendered?.frame ?? -1, renderedStackId = rendered?.stackId ?? 0, renderedPostSource = rendered?.source };
        }
        void CheckRenderer(string phase)
        {
            var state = RenderState(phase); report.renders.Add(state);
            Require(GraphicsPreset.RuntimeOwner && GraphicsPreset.RuntimeOwner.RenderLoopObserved && state.cameraId != 0 && state.renderScale == .85f && state.shadowDistance == 0
                && state.vSync == 0 && state.frameLimit == 30 && state.hdr && state.cameraMsaa == (state.effectiveMsaa > 1) && state.pipelineMsaa == state.effectiveMsaa
                && (state.effectiveMsaa == 1 || state.effectiveMsaa == 2) && state.postAA == AntialiasingMode.FastApproximateAntialiasing.ToString()
                && state.aoFound && !state.aoActive && state.realtimeProbes && state.ssrSteps == 36 && state.bloomZero && state.grainZero && state.chromaticZero
                && state.postEnabled && state.renderedBloomZero && state.renderedGrainZero && state.renderedChromaticZero
                && state.renderedCameraId == state.cameraId && state.renderedStackId != 0 && Time.frameCount - state.renderedFrame <= 2,
                "Actual native camera/pipeline/post policy " + phase, JsonUtility.ToJson(state));
        }
        void RequireVisible(Selectable item)
        {
            var scroll = item.GetComponentInParent<ScrollRect>(); var corners = new Vector3[4]; ((RectTransform)item.transform).GetWorldCorners(corners);
            var viewport = scroll.viewport; bool visible = corners.All(corner => RectTransformUtility.RectangleContainsScreenPoint(viewport, RectTransformUtility.WorldToScreenPoint(null, corner), null));
            Require(visible && scroll.verticalNormalizedPosition < .99f, "Actual keyboard focus scrolls last Quality control inside viewport", scroll.verticalNormalizedPosition.ToString("R"));
        }
        IEnumerator Capture(string file, string view)
        {
            yield return RenderFrames(); Texture2D image = null;
            try
            {
                image = ScreenCapture.CaptureScreenshotAsTexture(); Require(image && image.width == Screen.width && image.height == Screen.height, "Actual backbuffer screenshot", view);
                var pixels = image.GetPixels32(); byte minimum = 255, maximum = 0;
                foreach (var pixel in pixels) { minimum = Math.Min(minimum, Math.Min(pixel.r, Math.Min(pixel.g, pixel.b))); maximum = Math.Max(maximum, Math.Max(pixel.r, Math.Max(pixel.g, pixel.b))); }
                Require(maximum - minimum > 10, "Screenshot nonempty", view);
                string path = Path.Combine(Path.GetDirectoryName(output), file + ".png"); byte[] bytes = image.EncodeToPNG(); File.WriteAllBytes(path, bytes);
                using var sha = SHA256.Create();
                report.images.Add(new ImageReceipt { file = path, sha256 = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(), view = view,
                    width = image.width, height = image.height, focused = Application.isFocused, minimum = minimum / 255f, maximum = maximum / 255f });
            }
            finally { if (image) Destroy(image); }
        }
        IEnumerator ObservePacing()
        {
            var intervals = new List<float>(); var focus = new List<bool>(); report.pacing.requestedCap = Application.targetFrameRate;
            yield return new WaitForSecondsRealtime(.5f); report.pacing.started = Time.realtimeSinceStartupAsDouble; double previous = report.pacing.started;
            while (Time.realtimeSinceStartupAsDouble - report.pacing.started < 3)
            {
                yield return null; double now = Time.realtimeSinceStartupAsDouble; intervals.Add((float)((now - previous) * 1000)); focus.Add(Application.isFocused); previous = now;
            }
            report.pacing.ended = Time.realtimeSinceStartupAsDouble; report.pacing.intervalsMs = intervals.ToArray(); report.pacing.focused = focus.ToArray();
            var ordered = intervals.OrderBy(value => value).ToArray(); report.pacing.meanMs = intervals.Average(); report.pacing.medianMs = ordered[ordered.Length / 2];
            report.pacing.p95Ms = ordered[(int)((ordered.Length - 1) * .95f)]; report.pacing.minimumMs = ordered[0]; report.pacing.maximumMs = ordered[ordered.Length - 1];
            Require(intervals.Count >= 20 && intervals.All(value => !float.IsNaN(value) && value > 0) && report.pacing.ended - report.pacing.started >= 3
                && QualitySettings.vSyncCount == 0 && Application.targetFrameRate == 30, "Declared three-second 30-cap interval window recorded", JsonUtility.ToJson(report.pacing));
            Require(focus.All(value => value), "Cap observation window remains focused", "Focus was sampled for every interval; no broad performance conclusion is inferred.");
        }
        static string Flags() => string.Join("|", RunState.Flags.OrderBy(value => value, StringComparer.Ordinal));
        void Record(bool condition, string name, string detail = "") { report.checks.Add(new Check { name = name, detail = detail, passed = condition }); if (!condition) failed = true; }
        void Require(bool condition, string name, string detail = "") { Record(condition, name, detail); if (!condition) throw new InvalidOperationException(name + ": " + detail); }
        void RestoreStep(string name, Action action)
        {
            try { action(); }
            catch (Exception error) { failed = true; report.cleanupErrors.Add(name + ": " + error); }
        }
        void Restore()
        {
            if (restorationStarted) return;
            restorationStarted = true;
            try
            {
                RestoreStep("End-camera callback", () => RenderPipelineManager.endCameraRendering -= OnEndCamera);
                RestoreStep("Paused movement callback", () => { if (GameInput.Move != null && pausedMoveCallback != null) GameInput.Move.performed -= pausedMoveCallback; });
                RestoreStep("Paused split callback", () => { if (GameInput.Split != null && pausedSplitCallback != null) GameInput.Split.performed -= pausedSplitCallback; });
                foreach (var binding in modules)
                    if (binding) RestoreStep("UI callbacks " + binding.name, () => { if (binding.move) binding.move.action.performed -= OnMove; if (binding.submit) binding.submit.action.performed -= OnSubmit; });
                if (keyboard != null && keyboard.added)
                {
                    RestoreStep("Virtual keyboard neutral", () => InputSystem.ResetDevice(keyboard));
                    RestoreStep("Virtual keyboard removal", () => InputSystem.RemoveDevice(keyboard));
                }
                if (gamepad != null && gamepad.added)
                {
                    RestoreStep("Virtual gamepad neutral", () => InputSystem.ResetDevice(gamepad));
                    RestoreStep("Virtual gamepad removal", () => InputSystem.RemoveDevice(gamepad));
                }
                if (snapshot != null) snapshot.Restore(RestoreStep);
            }
            finally
            {
                // Even a graphics/device cleanup exception must not leave temporary settings in persistent storage.
                if (snapshot != null) RestoreStep("Critical raw preference restore", snapshot.preferences.Restore);
                RestoreStep("Exact restoration verification", () =>
                {
                    report.restoredPreferences = PreferenceState.Read().Signature();
                    report.restored = report.cleanupErrors.Count == 0 && (snapshot == null || snapshot.Matches() && report.restoredPreferences == report.originalPreferences)
                        && (keyboard == null || !keyboard.added) && (gamepad == null || !gamepad.added);
                    Record(report.restored, "Original preferences, devices, input and graphics globals restored once before quit/finally");
                });
                report.restorationError = string.Join("\n", report.cleanupErrors); restorationFinished = true;
            }
        }
        void WriteReport()
        {
            if (string.IsNullOrWhiteSpace(output) || !Path.IsPathRooted(output)) { Debug.LogError("[GraphicsSettingsAcceptance] Cannot write report: absolute -graphicsSettingsTestOut required."); return; }
            try { Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllText(output, JsonUtility.ToJson(report, true)); }
            catch (Exception error) { failed = true; Debug.LogError("[GraphicsSettingsAcceptance] Report write failed: " + error); }
        }
        sealed class DeviceState { public InputDevice device; public bool enabled; }
        sealed class PreferenceState
        {
            static readonly string[] FloatKeys = { AudioPreferences.MasterKey, AudioPreferences.AmbienceKey, AudioPreferences.MusicKey, AudioPreferences.EffectsKey, AudioPreferences.VoiceKey, "parasite.mouseSensitivity" };
            readonly bool jsonExists = PlayerPrefs.HasKey(GraphicsSettingsStore.PreferenceKey), legacyExists = PlayerPrefs.HasKey(GraphicsSettingsStore.LegacyPresetKey);
            readonly string json = PlayerPrefs.GetString(GraphicsSettingsStore.PreferenceKey, ""); readonly int legacy = PlayerPrefs.GetInt(GraphicsSettingsStore.LegacyPresetKey, -1);
            readonly bool[] exists = FloatKeys.Select(PlayerPrefs.HasKey).ToArray(); readonly float[] values = FloatKeys.Select(key => PlayerPrefs.GetFloat(key, 0)).ToArray();
            public static PreferenceState Read() => new();
            public string Signature() => jsonExists + ":" + json + "|" + legacyExists + ":" + legacy + "|" + string.Join("|", FloatKeys.Select((key, i) => key + ":" + exists[i] + ":" + values[i].ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
            public void Restore()
            {
                if (jsonExists) PlayerPrefs.SetString(GraphicsSettingsStore.PreferenceKey, json); else PlayerPrefs.DeleteKey(GraphicsSettingsStore.PreferenceKey);
                if (legacyExists) PlayerPrefs.SetInt(GraphicsSettingsStore.LegacyPresetKey, legacy); else PlayerPrefs.DeleteKey(GraphicsSettingsStore.LegacyPresetKey);
                for (int i = 0; i < FloatKeys.Length; i++) if (exists[i]) PlayerPrefs.SetFloat(FloatKeys[i], values[i]); else PlayerPrefs.DeleteKey(FloatKeys[i]); PlayerPrefs.Save();
            }
        }
        sealed class Snapshot
        {
            public readonly GraphicsSettingsData settings = GraphicsPreset.CaptureSettings(); public readonly PreferenceState preferences = PreferenceState.Read();
            public readonly DeviceState[] devices = InputSystem.devices.Select(device => new DeviceState { device = device, enabled = device.enabled }).ToArray();
            readonly Keyboard previousKeyboard = Keyboard.current; readonly Gamepad previousGamepad = Gamepad.current;
            readonly InputSettings.BackgroundBehavior background = InputSystem.settings.backgroundBehavior; readonly InputSettings.UpdateMode updateMode = InputSystem.settings.updateMode;
            readonly bool simulated = GameInput.Simulated, sprint = GameInput.SimSprint, jump = GameInput.SimJump, interact = GameInput.SimInteract, leave = GameInput.SimLeave, split = GameInput.SimSplit, change = GameInput.SimSwitch, ability = GameInput.SimAbility;
            readonly Vector2 move = GameInput.SimMove; readonly float timeScale = Time.timeScale, sensitivity = GameInput.MouseSensitivity;
            readonly int vsync = QualitySettings.vSyncCount, frameLimit = Application.targetFrameRate, lamps = FX.LampLights.Budget;
            readonly float lod = QualitySettings.lodBias, lampDistance = FX.LampLights.MaxDistance, steps = Shader.GetGlobalFloat("_ParasiteSSRSteps"), cone = Shader.GetGlobalFloat("_ParasiteConeScale"), shaft = Shader.GetGlobalFloat("_ParasiteShaftScale");
            readonly bool probes = QualitySettings.realtimeReflectionProbes, ssrOff = Shader.IsKeywordEnabled("_PARASITE_SSR_OFF");
            readonly AnisotropicFiltering anisotropic = QualitySettings.anisotropicFiltering;
            readonly CursorLockMode cursor = Cursor.lockState; readonly bool cursorVisible = Cursor.visible;
            readonly float[] audio = { AudioPreferences.MasterVolume, AudioPreferences.AmbienceVolume, AudioPreferences.MusicVolume, AudioPreferences.EffectsVolume, AudioPreferences.VoiceVolume };
            readonly Dictionary<FieldInfo, object> run = typeof(RunState).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Where(field => !field.IsInitOnly).ToDictionary(field => field, field => field.GetValue(null));
            readonly string[] flags = RunState.Flags.ToArray();
            public void Restore(Action<string, Action> step)
            {
                step("Graphics policy", () => GraphicsPreset.ApplySettings(settings, false));
                step("Quality globals", () => { QualitySettings.vSyncCount = vsync; Application.targetFrameRate = frameLimit;
                    QualitySettings.lodBias = lod; QualitySettings.anisotropicFiltering = anisotropic; QualitySettings.realtimeReflectionProbes = probes; });
                step("Lamp policy", () => { FX.LampLights.Budget = lamps; FX.LampLights.MaxDistance = lampDistance; });
                step("Shader globals", () => { if (ssrOff) Shader.EnableKeyword("_PARASITE_SSR_OFF"); else Shader.DisableKeyword("_PARASITE_SSR_OFF");
                    Shader.SetGlobalFloat("_ParasiteSSRSteps", steps); Shader.SetGlobalFloat("_ParasiteConeScale", cone); Shader.SetGlobalFloat("_ParasiteShaftScale", shaft); });
                step("Master audio", () => AudioPreferences.SetMaster(audio[0])); step("Ambience audio", () => AudioPreferences.SetAmbience(audio[1]));
                step("Music audio", () => AudioPreferences.SetMusic(audio[2])); step("Effects audio", () => AudioPreferences.SetEffects(audio[3])); step("Voice audio", () => AudioPreferences.SetVoice(audio[4]));
                step("Simulation and sensitivity", () => { GameInput.Simulated = simulated; GameInput.SimMove = move; GameInput.SimSprint = sprint; GameInput.SimJump = jump; GameInput.SimInteract = interact;
                    GameInput.SimLeave = leave; GameInput.SimSplit = split; GameInput.SimSwitch = change; GameInput.SimAbility = ability; GameInput.MouseSensitivity = sensitivity; });
                foreach (var state in devices) step("Original device " + state.device.name, () =>
                {
                    if (!state.device.added) throw new InvalidOperationException("Original device disappeared");
                    if (state.enabled && !state.device.enabled) InputSystem.EnableDevice(state.device); else if (!state.enabled && state.device.enabled) InputSystem.DisableDevice(state.device);
                });
                step("Current keyboard", () => { if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent(); });
                step("Current gamepad", () => { if (previousGamepad != null && previousGamepad.added) previousGamepad.MakeCurrent(); });
                step("Input settings", () => { InputSystem.settings.backgroundBehavior = background; InputSystem.settings.updateMode = updateMode; });
                foreach (var field in run) step("Run state " + field.Key.Name, () => field.Key.SetValue(null, field.Value));
                step("Run flags", () => { RunState.Flags.Clear(); foreach (var flag in flags) RunState.Flags.Add(flag); });
                step("Time and cursor", () => { Time.timeScale = timeScale; Cursor.lockState = cursor; Cursor.visible = cursorVisible; });
            }
            public bool Matches() => GraphicsPreset.CaptureSettings().Equals(settings) && QualitySettings.vSyncCount == vsync && Application.targetFrameRate == frameLimit
                && QualitySettings.lodBias == lod && QualitySettings.anisotropicFiltering == anisotropic && QualitySettings.realtimeReflectionProbes == probes
                && FX.LampLights.Budget == lamps && FX.LampLights.MaxDistance == lampDistance && Shader.GetGlobalFloat("_ParasiteSSRSteps") == steps
                && Shader.GetGlobalFloat("_ParasiteConeScale") == cone && Shader.GetGlobalFloat("_ParasiteShaftScale") == shaft && Shader.IsKeywordEnabled("_PARASITE_SSR_OFF") == ssrOff
                && InputSystem.settings.backgroundBehavior == background && InputSystem.settings.updateMode == updateMode && GameInput.Simulated == simulated && GameInput.MouseSensitivity == sensitivity
                && devices.All(state => state.device.added && state.device.enabled == state.enabled) && Time.timeScale == timeScale
                && Keyboard.current == previousKeyboard && Gamepad.current == previousGamepad && Cursor.lockState == cursor && Cursor.visible == cursorVisible
                && GameInput.SimMove == move && GameInput.SimSprint == sprint && GameInput.SimJump == jump && GameInput.SimInteract == interact
                && GameInput.SimLeave == leave && GameInput.SimSplit == split && GameInput.SimSwitch == change && GameInput.SimAbility == ability
                && AudioPreferences.MasterVolume == audio[0] && AudioPreferences.AmbienceVolume == audio[1] && AudioPreferences.MusicVolume == audio[2]
                && AudioPreferences.EffectsVolume == audio[3] && AudioPreferences.VoiceVolume == audio[4]
                && run.All(field => Equals(field.Key.GetValue(null), field.Value)) && RunState.Flags.SetEquals(flags);
        }
    }
}
