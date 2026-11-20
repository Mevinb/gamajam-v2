using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Parasite.Characters;
using Parasite.FX;
using Parasite.Missions;
using Parasite.Possession;
using Parasite.Testing;
using Parasite.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Parasite.Tests
{
    /// <summary>Exercise the actual generated menus, scene reloads, paused controls and saved settings.</summary>
    public class SettingsUITests
    {
        static readonly string[] Keys = { "parasite.audio.master", "parasite.audio.ambience", "parasite.mouseSensitivity", "parasite.graphics" };
        readonly bool[] hadKey = new bool[Keys.Length];
        readonly float[] oldFloat = new float[Keys.Length];
        int oldGraphics, oldPreset;
        float oldSensitivity, oldListener, oldAmbience;
        Keyboard keyboard;
        InputSettings.BackgroundBehavior oldBackgroundBehavior;
        InputSettings.UpdateMode oldUpdateMode;
#if UNITY_EDITOR
        InputSettings.EditorInputBehaviorInPlayMode oldEditorInputBehavior;
#endif

        [SetUp]
        public void SavePreferences()
        {
            for (int i = 0; i < Keys.Length; i++)
            {
                hadKey[i] = PlayerPrefs.HasKey(Keys[i]);
                if (i < 3) oldFloat[i] = PlayerPrefs.GetFloat(Keys[i]);
            }
            oldGraphics = PlayerPrefs.GetInt(Keys[3]); oldPreset = GraphicsPreset.Current;
            oldSensitivity = GameInput.MouseSensitivity; oldListener = AudioListener.volume; oldAmbience = Sfx.AmbienceVolume;
            oldBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            oldUpdateMode = InputSystem.settings.updateMode;
            // Batch mode has no focused GameView. Route queued keys through the normal player
            // input update rather than the Editor UI, matching focused standalone-player behavior.
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
#if UNITY_EDITOR
            oldEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            keyboard = InputSystem.AddDevice<Keyboard>("Settings UI test keyboard");
        }

        [UnityTearDown]
        public IEnumerator RestorePreferences()
        {
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = oldBackgroundBehavior;
            InputSystem.settings.updateMode = oldUpdateMode;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInputBehavior;
#endif
            ClearInput();
            Time.timeScale = 1f;
            // Unload open overlays first: their OnDisable flushes the preferences they edited.
            SceneManager.LoadScene(GameManager.MenuScene);
            yield return null;
            GraphicsPreset.Apply(oldPreset, save: false);
            for (int i = 0; i < Keys.Length; i++)
            {
                if (!hadKey[i]) PlayerPrefs.DeleteKey(Keys[i]);
                else if (i < 3) PlayerPrefs.SetFloat(Keys[i], oldFloat[i]);
                else PlayerPrefs.SetInt(Keys[i], oldGraphics);
            }
            AudioPreferences.Load();
            GameInput.MouseSensitivity = oldSensitivity; AudioListener.volume = oldListener; Sfx.AmbienceVolume = oldAmbience;
            PlayerPrefs.Save();
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator TitleAndPauseSettingsApplyPersistAndKeepGameplayPaused()
        {
            ClearInput();
            Time.timeScale = 1f;
            SceneManager.LoadScene(GameManager.MenuScene);
            yield return null;
            yield return new WaitForSecondsRealtime(1f); // allow the title fade to finish
            var menu = Object.FindFirstObjectByType<MainMenuController>();
            Assert.IsNotNull(menu, "Generated Bootstrap scene has its title menu.");
            var titleSettings = menu.GetComponentInChildren<SettingsOverlay>(true);
            Assert.IsNotNull(titleSettings);
            Click(menu.transform, "Btn_Settings");
            Assert.IsTrue(titleSettings.gameObject.activeInHierarchy);
            Assert.AreEqual(titleSettings.FirstSelection.gameObject, EventSystem.current.currentSelectedGameObject);

            SliderFor(titleSettings, "MasterVolume").value = 0.37f;
            SliderFor(titleSettings, "AmbienceVolume").value = 0.23f;
            SliderFor(titleSettings, "Sensitivity").value = 0.19f;
            Assert.That(AudioListener.volume, Is.EqualTo(0.37f).Within(0.001f), "Master changes the active listener immediately.");
            Assert.That(Sfx.AmbienceVolume, Is.EqualTo(0.23f).Within(0.001f));
            Assert.That(GameInput.MouseSensitivity, Is.EqualTo(0.19f).Within(0.001f));
            int nextPreset = (GraphicsPreset.Current + 1) % GraphicsPreset.Names.Length;
            titleSettings.FirstSelection.onClick.Invoke();
            Assert.AreEqual(nextPreset, GraphicsPreset.Current);
            AssertInsideScreen((RectTransform)titleSettings.transform.Find("Panel"));
            yield return Capture("01-title-settings.png");
            Click(titleSettings.transform, "Btn_Back");
            Assert.IsFalse(titleSettings.gameObject.activeSelf);
            Assert.IsTrue(menu.transform.Find("MainMenu Canvas/Column").gameObject.activeInHierarchy);

            // Simulate a fresh settings load independently of Unity's once-per-session initialization.
            AudioListener.volume = 0.8f; Sfx.AmbienceVolume = 0.8f; GameInput.MouseSensitivity = 0.08f;
            UIKit.LoadSettings();
            Assert.That(AudioListener.volume, Is.EqualTo(0.37f).Within(0.001f), "Saved master volume reloads.");
            Assert.That(Sfx.AmbienceVolume, Is.EqualTo(0.23f).Within(0.001f), "Saved ambience volume reloads.");
            Assert.That(GameInput.MouseSensitivity, Is.EqualTo(0.19f).Within(0.001f), "Saved sensitivity reloads.");
            // Normal scene transitions must preserve the values chosen in the title menu.
            yield return AutoTest.LoadCity();
            Assert.That(AudioListener.volume, Is.EqualTo(0.37f).Within(0.001f));
            Assert.That(Sfx.AmbienceVolume, Is.EqualTo(0.23f).Within(0.001f));
            Assert.That(GameInput.MouseSensitivity, Is.EqualTo(0.19f).Within(0.001f));
            Assert.AreEqual(nextPreset, GraphicsPreset.Current);

            var gm = GameManager.Instance;
            var hud = HUDController.Instance;
            var possession = PossessionManager.Instance;
            Assert.IsNotNull(gm); Assert.IsNotNull(hud); Assert.IsNotNull(possession.ActiveBody);
            gm.SetPaused(true);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(GameState.Paused, gm.State);
            Assert.That(Time.timeScale, Is.Zero);
            var pause = hud.transform.Find("HUD Canvas/Pause");
            AssertInsideScreen((RectTransform)pause.Find("Content/Panel"));
            AssertInsideScreen((RectTransform)pause.Find("Content/Journal"));
            yield return Capture("02-pause-journal.png");

            var pauseSettings = hud.GetComponentInChildren<SettingsOverlay>(true);
            Click(pause, "Btn_Settings");
            Assert.IsTrue(pauseSettings.gameObject.activeInHierarchy);
            Assert.IsFalse(pause.Find("Content").gameObject.activeSelf, "Journal hides behind settings.");
            Assert.That(SliderFor(pauseSettings, "MasterVolume").value, Is.EqualTo(0.37f).Within(0.001f));
            Assert.That(SliderFor(pauseSettings, "AmbienceVolume").value, Is.EqualTo(0.23f).Within(0.001f));
            Assert.That(SliderFor(pauseSettings, "Sensitivity").value, Is.EqualTo(0.19f).Within(0.001f));

            // Menu input must never launch, split, switch, move or progress the game while paused.
            var activeBody = possession.ActiveBody;
            Vector3 position = activeBody.Root.position;
            float playTime = RunState.PlayTime;
            int agents = possession.Agents.Count, flags = RunState.Flags.Count, hosts = RunState.HostsPossessed;
            GameInput.Simulated = true; GameInput.SimMove = Vector2.one;
            GameInput.SimInteract = GameInput.SimLeave = GameInput.SimSplit = GameInput.SimSwitch = GameInput.SimAbility = GameInput.SimJump = true;
            for (int i = 0; i < 4; i++) yield return null;
            Assert.AreSame(activeBody, possession.ActiveBody);
            Assert.That(Vector3.Distance(position, activeBody.Root.position), Is.LessThan(0.001f));
            Assert.AreEqual(agents, possession.Agents.Count);
            Assert.AreEqual(flags, RunState.Flags.Count); Assert.AreEqual(hosts, RunState.HostsPossessed);
            Assert.AreEqual(playTime, RunState.PlayTime);
            Assert.IsFalse(possession.Active.InTransit);
            ClearInput();

            SliderFor(pauseSettings, "MasterVolume").value = 0f;
            SliderFor(pauseSettings, "AmbienceVolume").value = 0.61f;
            yield return Capture("03-pause-settings.png");
            yield return PressEscape();
            Assert.IsFalse(pauseSettings.gameObject.activeSelf, "Esc returns from settings.");
            Assert.IsTrue(pause.Find("Content").gameObject.activeInHierarchy);
            Assert.AreEqual(GameState.Paused, gm.State, "Returning from a submenu keeps the city paused.");
            Assert.That(Time.timeScale, Is.Zero);
            Click(pause, "Btn_Controls");
            yield return PressEscape();
            Assert.AreEqual(GameState.Paused, gm.State, "Controls has the same Back behavior.");
            Assert.IsFalse(pause.Find("ControlsOverlay").gameObject.activeSelf);
            Click(pause, "Btn_Resume");
            Assert.AreEqual(GameState.Playing, gm.State);
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            SceneManager.LoadScene(GameManager.MenuScene);
            yield return null;
            menu = Object.FindFirstObjectByType<MainMenuController>();
            Click(menu.transform, "Btn_Settings");
            titleSettings = menu.GetComponentInChildren<SettingsOverlay>(true);
            Assert.That(SliderFor(titleSettings, "MasterVolume").value, Is.Zero, "Mute survives returning to the title.");
            Assert.That(AudioListener.volume, Is.Zero);
            Assert.That(SliderFor(titleSettings, "AmbienceVolume").value, Is.EqualTo(0.61f).Within(0.001f));
            Assert.That(SliderFor(titleSettings, "Sensitivity").value, Is.EqualTo(0.19f).Within(0.001f));
            Debug.Log("[SettingsUI] PASS title/pause settings, live audio, persistence, keyboard Back and paused gameplay isolation");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator LateCampaignJournalScrollsAndShowsTheCurrentObjectives()
        {
            yield return AutoTest.LoadCity();
            var missionManager = MissionManager.Instance;
            Assert.AreEqual(8, missionManager.Missions.Count, "The extended campaign has eight missions.");
            // UI fixture: show the last mission without bypassing any gameplay in acceptance tests.
            RunState.MissionIndex = missionManager.Missions.Count - 1;
            var gm = GameManager.Instance;
            gm.SetPaused(true);
            yield return new WaitForSecondsRealtime(0.3f);
            var journal = HUDController.Instance.transform.Find("HUD Canvas/Pause/Content/Journal");
            var scroll = journal.Find("ScrollView").GetComponent<ScrollRect>();
            Assert.IsNotNull(scroll.viewport.GetComponent<RectMask2D>(), "Journal entries are clipped to their viewport.");
            var entries = scroll.content.GetComponentsInChildren<Text>();
            Assert.AreEqual(8, entries.Length);
            for (int i = 0; i < entries.Length; i++) StringAssert.Contains(missionManager.Missions[i].title, entries[i].text);
            var current = entries[entries.Length - 1];
            StringAssert.Contains(missionManager.Current.briefing, current.text);
            foreach (var objective in missionManager.Current.objectives) StringAssert.Contains(objective.text, current.text);
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height), "The full campaign can overflow without clipping its text permanently.");
            Assert.That(scroll.verticalNormalizedPosition, Is.LessThan(0.99f), "Opening the journal focuses the late current mission.");
            var entryCorners = new Vector3[4]; var viewportCorners = new Vector3[4];
            current.rectTransform.GetWorldCorners(entryCorners); scroll.viewport.GetWorldCorners(viewportCorners);
            Assert.That(entryCorners[0].y, Is.GreaterThanOrEqualTo(viewportCorners[0].y - 1f), "The current mission's final objective is visible.");
            Assert.That(entryCorners[2].y, Is.LessThanOrEqualTo(viewportCorners[2].y + 1f), "The current mission's title is visible.");
            scroll.verticalNormalizedPosition = 1f;
            scroll.OnScroll(new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0, -4f) });
            yield return null;
            Assert.That(scroll.verticalNormalizedPosition, Is.LessThan(0.99f), "Mouse-wheel scrolling reaches later entries.");
            scroll.verticalNormalizedPosition = 0f;
            yield return Capture("04-late-campaign-journal.png");
            Click(HUDController.Instance.transform.Find("HUD Canvas/Pause"), "Btn_Settings");
            yield return PressEscape();
            Assert.AreEqual(GameState.Paused, gm.State, "The expanded journal preserves Settings Back behavior.");

            // Exercise only the end-screen presentation; the campaign suite verifies real extraction.
            gm.BeginEnding();
            gm.FinishEnding(new EndingData { route = EntryRoute.ServiceTunnel, timeSeconds = 936f, hostsPossessed = 12, peakSuspicion = 70f, captures = 1 });
            var epilogue = LakesideEpilogue.Instance;
            Assert.IsNotNull(epilogue);
            epilogue.RevealChoices();
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsTrue(epilogue.ChoicesReady);
            yield return Capture("05-lakeside-choice.png");
            epilogue.Choose(EndingOutcome.Peace);
            yield return new WaitForSecondsRealtime(1f);
            yield return Capture("06-peace-resolution.png");
            epilogue.FinishResolution();
            yield return new WaitForSecondsRealtime(1.3f);
            var ending = HUDController.Instance.transform.Find("HUD Canvas/EndScreen");
            Assert.IsTrue(ending.gameObject.activeInHierarchy);
            var texts = ending.GetComponentsInChildren<Text>();
            StringAssert.Contains("ORDINARY", texts.Single(text => text.name == "Headline").text);
            StringAssert.Contains("lake", texts.Single(text => text.name == "Epilogue").text);
            Assert.AreEqual("15:36", ending.Find("Column/Stats/V1").GetComponent<Text>().text, "Escape statistics include the entire run.");
            yield return Capture("07-peace-summary.png");
            Debug.Log("[SettingsUI] PASS eight-mission journal, current-objective visibility, scroll input, submenu Back and extended ending presentation (UI fixture)");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator LakesideRequiresAChoiceAndRestoresTemporarySceneState()
        {
            yield return AutoTest.LoadCity();
            var gm = GameManager.Instance;
            var oldCamera = Camera.main;
            bool cameraEnabled = oldCamera.enabled, fog = RenderSettings.fog;
            var oldLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Select(light => (light, light.enabled)).ToArray();
            var oldAudio = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Select(source => (source, source.mute)).ToArray();
            int hostCount = HostCharacter.All.Count, possessions = RunState.HostsPossessed;
            bool published = false; EndingData result = default;
            void OnEnding(EndingData data) { published = true; result = data; }
            GameEvents.Ending += OnEnding;
            try
            {
                gm.BeginEnding(); gm.FinishEnding(new EndingData { timeSeconds = 1200f });
                var epilogue = LakesideEpilogue.Instance;
                Assert.IsNotNull(epilogue); Assert.IsNotNull(epilogue.SeatedGirl);
                Assert.IsNull(epilogue.SeatedGirl.GetComponent<HostCharacter>(), "The seated girl is visual staging, not a new gameplay possession.");
                var thigh = epilogue.SeatedGirl.Find("Model/Hips/LThigh");
                Assert.That(Quaternion.Angle(thigh.localRotation, Quaternion.Euler(-90, 0, 0)), Is.LessThan(1f), "The girl is visibly posed seated.");
                Assert.IsNotNull(epilogue.SeatedGirl.Find("Model/Hips/Spine/Neck/Head/Long hair"));
                var lake = GameObject.Find("Lakeside film set/Lake").GetComponent<Renderer>();
                Assert.AreEqual("Parasite/LakesideWater", lake.sharedMaterial.shader.name);
                Assert.IsTrue(lake.sharedMaterial.shader.isSupported, "The lake shader compiled for this GPU.");
                Assert.AreEqual(GameState.Ending, gm.State); Assert.That(Time.timeScale, Is.Zero);
                Assert.AreEqual(hostCount, HostCharacter.All.Count); Assert.AreEqual(possessions, RunState.HostsPossessed);
                Assert.IsFalse(published);
                Assert.IsFalse(HUDController.Instance.transform.Find("HUD Canvas/EndScreen").gameObject.activeSelf);

                // A held skip/confirm may reveal the choices but must not click one on arrival.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                yield return null; yield return null;
                epilogue.RevealChoices();
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.IsFalse(epilogue.ChoicesReady, "Held skip input must be released before choosing.");
                epilogue.Choose(EndingOutcome.Dominion);
                epilogue.FinishResolution();
                Assert.AreEqual(EndingOutcome.Unchosen, epilogue.Outcome); Assert.IsFalse(published);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null; yield return new WaitForSecondsRealtime(0.5f);
                Assert.IsTrue(epilogue.ChoicesReady);
                yield return new WaitForSecondsRealtime(0.8f);
                Assert.AreEqual(EndingOutcome.Unchosen, epilogue.Outcome, "Waiting at the choice never selects a future.");
                epilogue.FinishResolution(); Assert.IsFalse(published);
                Click(epilogue.transform, "Btn_Become the world");
                Assert.AreEqual(EndingOutcome.Dominion, epilogue.Outcome);
                epilogue.Choose(EndingOutcome.Peace);
                Assert.AreEqual(EndingOutcome.Dominion, epilogue.Outcome, "The explicit choice is locked.");
                yield return new WaitForSecondsRealtime(1.2f);
                yield return Capture("08-dominion-resolution.png");
                Assert.IsFalse(published, "The concluding beat precedes the statistics.");
                epilogue.FinishResolution();
                yield return new WaitForSecondsRealtime(1.3f);
                Assert.IsTrue(published); Assert.AreEqual(EndingOutcome.Dominion, result.outcome);
                Assert.AreEqual(GameState.Ended, gm.State);
                yield return Capture("09-dominion-summary.png");

                Object.Destroy(epilogue.gameObject);
                yield return null; yield return null;
                Assert.IsFalse(LakesideEpilogue.Instance);
                Assert.IsNull(GameObject.Find("Lakeside film set"));
                Assert.AreEqual(cameraEnabled, oldCamera.enabled);
                Assert.AreEqual(fog, RenderSettings.fog);
                foreach (var state in oldLights) if (state.light) Assert.AreEqual(state.enabled, state.light.enabled);
                foreach (var state in oldAudio) if (state.source) Assert.AreEqual(state.mute, state.source.mute);
                Assert.IsTrue(HUDController.Instance.GetComponentInChildren<Canvas>().enabled);
                Debug.Log("[SettingsUI] PASS explicit lakeside choice, held-input debounce, seated host silhouette, both-body stats isolation and camera/light/audio cleanup (UI fixture)");
            }
            finally { GameEvents.Ending -= OnEnding; }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator CtrlMStartsFinalDrainSequenceWithoutChoosingFuture()
        {
            foreach (var control in new[] { Key.LeftCtrl, Key.RightCtrl })
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return AutoTest.LoadCity();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.M));
                yield return null; yield return null;
                Assert.AreEqual(GameState.Playing, GameManager.Instance.State, "M without Ctrl must not skip the campaign.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null; yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(control, Key.M));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Assert.IsTrue(MissionManager.Instance.EndingStarted, control + "+M starts the existing drain ending.");
                Assert.AreEqual(GameState.Ending, GameManager.Instance.State);
                Assert.That(Time.timeScale, Is.Zero);
                Assert.IsNull(PossessionManager.Instance.Active.Host);
                Assert.That(Vector3.Distance(PossessionManager.Instance.Active.Position, Parasite.World.MissionLayout.Extraction), Is.LessThan(0.5f));
                Assert.IsFalse(RunState.Has(MissionFlags.Escaped), "The shortcut does not commit an unchosen ending.");
                GameInput.Simulated = true; GameInput.SimInteract = true;
                float deadline = Time.realtimeSinceStartup + 12f;
                while (!LakesideEpilogue.Instance && Time.realtimeSinceStartup < deadline) yield return null;
                ClearInput();
                var epilogue = LakesideEpilogue.Instance;
                Assert.IsNotNull(epilogue, "The real drain dialogue leads to the lakeside scene.");
                epilogue.RevealChoices();
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.IsTrue(epilogue.ChoicesReady);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(control, Key.M));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Assert.AreSame(epilogue, LakesideEpilogue.Instance, "Repeating the shortcut cannot replace the current ending.");
                Assert.AreEqual(1, Object.FindObjectsByType<LakesideEpilogue>(FindObjectsSortMode.None).Length);
                Assert.AreEqual(EndingOutcome.Unchosen, epilogue.Outcome, "The shortcut never chooses Peace or Dominion.");
                epilogue.Choose(EndingOutcome.Peace);
                yield return new WaitForSecondsRealtime(0.9f);
                epilogue.FinishResolution();
                Assert.AreEqual(GameState.Ended, GameManager.Instance.State);
                Assert.IsTrue(RunState.Has(MissionFlags.Escaped));
            }
            Debug.Log("[SettingsUI] PASS left/right Ctrl+M demo shortcut, ordinary M isolation, protected drain story, no duplicate/no automatic choice and explicit resolution");
        }

        IEnumerator PressEscape()
        {
            GameInput.Ensure();
            Assert.IsTrue(GameInput.Pause.enabled, "The game's real Pause action is enabled.");
            int performed = 0;
            void OnPause(InputAction.CallbackContext context)
            {
                if (context.control.device == keyboard) performed++;
            }
            GameInput.Pause.performed += OnPause;
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                for (int frame = 0; frame < 10 && performed == 0; frame++) yield return null;
                Assert.AreEqual(1, performed, $"Queued Escape reached the real Pause binding (keyboard enabled: {keyboard.enabled}, focus: {Application.isFocused}).");
                Assert.IsTrue(keyboard.escapeKey.isPressed, "The test keyboard processed the Escape state.");
                yield return null; // allow GameManager.Update to consume WasPressedThisFrame
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                yield return null;
                Assert.IsFalse(keyboard.escapeKey.isPressed, "Escape release was processed.");
                Debug.Log("[SettingsUI] Queued Escape performed the real Pause action and was released.");
            }
            finally
            {
                GameInput.Pause.performed -= OnPause;
            }
        }

        static void ClearInput()
        {
            GameInput.Simulated = false; GameInput.SimMove = Vector2.zero;
            GameInput.SimSprint = GameInput.SimJump = GameInput.SimInteract = GameInput.SimLeave = GameInput.SimSplit = GameInput.SimSwitch = GameInput.SimAbility = false;
        }

        static Slider SliderFor(SettingsOverlay overlay, string name) => overlay.GetComponentsInChildren<Slider>(true).Single(slider => slider.name == name);

        static void Click(Transform root, string name)
        {
            var button = root.GetComponentsInChildren<Button>(true).Single(candidate => candidate.name == name);
            Assert.IsTrue(button.gameObject.activeInHierarchy, name + " is visible.");
            Assert.IsTrue(button.IsInteractable(), name + " is interactable.");
            button.onClick.Invoke();
        }

        static void AssertInsideScreen(RectTransform panel)
        {
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4]; panel.GetWorldCorners(corners);
            Assert.That(corners[0].x, Is.GreaterThanOrEqualTo(-1f), panel.name + " left edge");
            Assert.That(corners[0].y, Is.GreaterThanOrEqualTo(-1f), panel.name + " bottom edge");
            Assert.That(corners[2].x, Is.LessThanOrEqualTo(Screen.width + 1f), panel.name + " right edge");
            Assert.That(corners[2].y, Is.LessThanOrEqualTo(Screen.height + 1f), panel.name + " top edge");
        }

        internal static IEnumerator Capture(string name)
        {
            const int width = 1600, height = 900;
            string path = Path.Combine(Application.dataPath, "..", "Logs", "ui", name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var camera = Camera.main;
            Assert.IsNotNull(camera, "The generated scene has a camera for the UI render.");
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            float oldAspect = camera.aspect;
            Rect oldRect = camera.rect;
            int oldMask = camera.cullingMask;
            // ScreenCapture and WaitForEndOfFrame are not dispatched by Editor batch mode.
            // Temporarily render the real overlay canvases through the scene camera instead.
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Where(canvas => canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                .Select(canvas => (canvas, worldCamera: canvas.worldCamera, mode: canvas.renderMode,
                    plane: canvas.planeDistance, scale: canvas.scaleFactor,
                    rt: (RectTransform)canvas.transform, position: canvas.transform.localPosition,
                    rotation: canvas.transform.localRotation, localScale: canvas.transform.localScale,
                    size: ((RectTransform)canvas.transform).sizeDelta)).ToArray();
            Assert.IsNotEmpty(canvases, "The real menu overlay is included in the render.");
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                Assert.IsTrue(target.Create());
                camera.targetTexture = target;
                camera.aspect = (float)width / height;
                camera.rect = new Rect(0, 0, 1, 1);
                camera.cullingMask |= 1 << 5;
                foreach (var state in canvases)
                {
                    state.canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    state.canvas.worldCamera = camera;
                    state.canvas.planeDistance = camera.nearClipPlane + 0.5f;
                }
                // Let CanvasScaler update from the target camera's 1600x900 pixel rect.
                yield return null;
                Canvas.ForceUpdateCanvases();
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                Assert.IsTrue(RenderPipeline.SupportsRenderRequest(camera, request), "URP supports the explicit UI render request.");
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                byte[] png = pixels.EncodeToPNG();
                Assert.That(png.Length, Is.GreaterThan(1024), "The UI render contains image data.");
                File.WriteAllBytes(path, png);
            }
            finally
            {
                camera.targetTexture = oldTarget;
                camera.aspect = oldAspect;
                camera.rect = oldRect;
                camera.cullingMask = oldMask;
                foreach (var state in canvases)
                {
                    if (!state.canvas) continue;
                    state.canvas.renderMode = state.mode;
                    state.canvas.worldCamera = state.worldCamera;
                    state.canvas.planeDistance = state.plane;
                    state.canvas.scaleFactor = state.scale;
                    state.rt.localPosition = state.position;
                    state.rt.localRotation = state.rotation;
                    state.rt.localScale = state.localScale;
                    state.rt.sizeDelta = state.size;
                }
                RenderTexture.active = oldActive;
                target.Release();
                Object.Destroy(target); Object.Destroy(pixels);
                Canvas.ForceUpdateCanvases();
            }
            Assert.IsTrue(File.Exists(path) && new FileInfo(path).Length > 1024, "Unity wrote screenshot " + name);
            Debug.Log($"[SettingsUI] Screenshot {width}x{height}: {path}");
        }
    }
}
