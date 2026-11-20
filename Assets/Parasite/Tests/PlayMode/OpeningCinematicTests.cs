using System.Collections;
using System.Linq;
using NUnit.Framework;
using Parasite.Characters;
using Parasite.Possession;
using Parasite.UI;
using Parasite.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Parasite.Tests
{
    /// <summary>Real title-menu entry, unscaled completion, physical skip bindings and clean gameplay handoff.</summary>
    public class OpeningCinematicTests
    {
        Keyboard keyboard;
        InputSettings.BackgroundBehavior oldBackground;
        InputSettings.UpdateMode oldUpdate;
#if UNITY_EDITOR
        InputSettings.EditorInputBehaviorInPlayMode oldEditor;
#endif

        [SetUp]
        public void RouteTestKeyboard()
        {
            oldBackground = InputSystem.settings.backgroundBehavior;
            oldUpdate = InputSystem.settings.updateMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
#if UNITY_EDITOR
            oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            keyboard = InputSystem.AddDevice<Keyboard>("Opening cinematic test keyboard");
        }

        [UnityTearDown]
        public IEnumerator RestoreInputAndScene()
        {
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = oldBackground;
            InputSystem.settings.updateMode = oldUpdate;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
#endif
            GameInput.Simulated = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene(GameManager.MenuScene);
            yield return null;
            Time.timeScale = 1f;
        }

        static IEnumerator BeginFromActualTitle()
        {
            Time.timeScale = 1f; GameInput.Simulated = false;
            SceneManager.LoadScene(GameManager.MenuScene);
            yield return null;
            var menu = Object.FindFirstObjectByType<MainMenuController>();
            Assert.IsNotNull(menu);
            var button = menu.GetComponentsInChildren<Button>(true).Single(b => b.name == "Btn_Begin Infiltration");
            Assert.IsTrue(button.IsInteractable());
            button.onClick.Invoke();
            float deadline = Time.realtimeSinceStartup + 20f;
            while ((!OpeningCinematic.Instance || !OpeningCinematic.Instance.Running) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(OpeningCinematic.Instance && OpeningCinematic.Instance.Running, "Only deliberate title entry requests the opening.");
            Assert.AreEqual(GameState.Ending, GameManager.Instance.State);
            Assert.That(Time.timeScale, Is.Zero, "Story presentation freezes the live game.");
        }

        static void AssertCleanHandoff()
        {
            Assert.IsFalse(OpeningCinematic.Instance && OpeningCinematic.Instance.Running);
            Assert.AreEqual(GameState.Playing, GameManager.Instance.State);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            var pm = PossessionManager.Instance;
            Assert.IsNotNull(pm); Assert.AreEqual(1, pm.Agents.Count); Assert.IsTrue(pm.Active.IsFree);
            Assert.IsFalse(pm.InputLocked);
            var delta = pm.Active.Position - CityLayout.PlayerSpawn; delta.y = 0;
            Assert.That(delta.magnitude, Is.LessThan(0.15f));
            Assert.That(RunState.HostsPossessed, Is.Zero);
            Assert.That(RunState.Captures, Is.Zero);
            Assert.That(RunState.Resets, Is.Zero);
            Assert.That(RunState.MissionIndex, Is.Zero);
            Assert.IsEmpty(RunState.Flags);
            Assert.IsTrue(PlayerCamera.Instance.enabled);
            Assert.IsTrue(PlayerCamera.Instance.Cam.GetComponents<Behaviour>().Single(b => b.GetType().Name == "CinemachineBrain").enabled);
            Assert.IsTrue(HUDController.Instance.GetComponentInChildren<Canvas>(true).enabled);
            Assert.That(PlayerCamera.Instance.Cam.nearClipPlane, Is.EqualTo(0.08f).Within(0.001f));
            Assert.IsNull(GameObject.Find("Opening Historical Set"));
            Assert.IsNull(GameObject.Find("Opening Canvas"));
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator FullOpeningUsesUnscaledTimeAndPreservesCampaignState()
        {
            yield return BeginFromActualTitle();
            var opening = OpeningCinematic.Instance;
            int realNpcs = HostCharacter.All.Count;
            Assert.That(realNpcs, Is.GreaterThan(0));
            Assert.That(Object.FindObjectsByType<HumanoidRig>(FindObjectsSortMode.None).Length, Is.GreaterThan(realNpcs), "Historical rigs are visual stand-ins, not gameplay hosts.");
            // Exercise every scheduled shot/caption and the normal end path without waiting 80 wall-clock seconds.
            opening.PlaybackSpeed = 80f;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (opening && opening.Running && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.AreEqual("Watched", OpeningCinematic.LastExitReason);
            AssertCleanHandoff();
            Assert.AreEqual(realNpcs, HostCharacter.All.Count, "The cinematic never registers historical characters as hosts.");
            Debug.Log("[OpeningUI] PASS full unscaled story timeline, camera/HUD restoration, clean one-amoeba spawn and untouched campaign statistics");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator EveryKeyboardSkipBindingReturnsToPlayAndReloadsDoNotReplay()
        {
            foreach (var key in new[] { Key.Space, Key.Escape, Key.Enter })
            {
                yield return BeginFromActualTitle();
                yield return new WaitForSecondsRealtime(0.35f); // menu submit cannot skip before release/debounce
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                yield return null; yield return null;
                Assert.IsTrue(OpeningCinematic.Instance && OpeningCinematic.Instance.Running, "Wait for release before exposing gameplay to the skip key.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                float deadline = Time.realtimeSinceStartup + 5f;
                while (OpeningCinematic.Instance && OpeningCinematic.Instance.Running && Time.realtimeSinceStartup < deadline) yield return null;
                yield return null;
                Assert.AreEqual("Skipped", OpeningCinematic.LastExitReason, key + " skips the real controller.");
                AssertCleanHandoff();
            }

            var previous = CityDirector.Instance;
            GameManager.Instance.RestartGame();
            yield return WaitForNewCity(previous);
            Assert.IsNull(OpeningCinematic.Instance, "Restart does not request an opening.");

            previous = CityDirector.Instance;
            GameManager.Instance.SetCheckpoint(CityLayout.PlazaSpot, "Opening test checkpoint");
            GameManager.Instance.ResetToCheckpoint("Opening checkpoint test");
            yield return WaitForNewCity(previous);
            Assert.IsNull(OpeningCinematic.Instance, "A checkpoint reload does not request an opening.");

            RunState.NewGame();
            SceneManager.LoadScene(GameManager.CityScene);
            yield return null;
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.IsNull(OpeningCinematic.Instance, "Direct scene acceptance/benchmark loads never request an opening.");
            Assert.AreEqual(GameState.Playing, GameManager.Instance.State);
            Debug.Log("[OpeningUI] PASS Space/Escape/Enter skip through real input; restart, checkpoint and direct loads do not replay");
        }

        static IEnumerator WaitForNewCity(CityDirector previous)
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while ((!CityDirector.Instance || CityDirector.Instance == previous || !CityDirector.Instance.Ready) && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.IsTrue(CityDirector.Instance && CityDirector.Instance != previous && CityDirector.Instance.Ready);
        }
    }
}
