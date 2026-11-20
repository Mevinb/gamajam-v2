using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Parasite.FX;
using Parasite.Possession;
using Parasite.Testing;
using Parasite.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
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
            keyboard = InputSystem.AddDevice<Keyboard>("Settings UI test keyboard");
        }

        [UnityTearDown]
        public IEnumerator RestorePreferences()
        {
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
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

        IEnumerator PressEscape()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
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

        static IEnumerator Capture(string name)
        {
            Canvas.ForceUpdateCanvases();
            // Let the normal render finish before asking Unity to capture the next screen frame.
            // WaitForEndOfFrame is deliberately avoided: it is not dispatched in Editor batch mode.
            yield return null;
            string path = Path.Combine(Application.dataPath, "..", "Logs", "ui", name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            float deadline = Time.realtimeSinceStartup + 10f;
            while ((!File.Exists(path) || new FileInfo(path).Length == 0) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(File.Exists(path) && new FileInfo(path).Length > 0, "Unity wrote screenshot " + name);
            Debug.Log($"[SettingsUI] Screenshot {Screen.width}x{Screen.height}: {path}");
        }
    }
}
