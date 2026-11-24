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
            // Macro shots deliberately hide actors that could cross the lens; they still belong to the authored set.
            var historicalActors = opening.GetComponentsInChildren<HumanoidRig>(true);
            Assert.That(historicalActors.Length, Is.EqualTo(3), "Petrov, Vale and the worker remain authored visual stand-ins.");
            foreach (var actor in historicalActors)
                Assert.IsNull(actor.GetComponentInParent<HostCharacter>(), "Historical actors never register as possessable gameplay hosts.");
            // Render each scheduled shot through the same real-camera path as the settings UI QA.
            float[] shotStarts = { 0f, 5f, 12f, 19f, 26f, 34f, 43f, 51f, 59f, 67f, 75f, OpeningCinematic.Duration };
            string[] shotNames = { "subject", "petrov", "recognition", "vale", "two-bodies", "contact", "restraint", "release", "service-conduit", "storm-drain", "handoff" };
            for (int i = 0; i < shotNames.Length; i++)
            {
                float targetTime = (shotStarts[i] + shotStarts[i + 1]) * 0.5f;
                opening.PlaybackSpeed = 2f;
                float shotDeadline = Time.realtimeSinceStartup + 15f;
                while (opening && opening.Running && opening.ElapsedSeconds < targetTime && Time.realtimeSinceStartup < shotDeadline) yield return null;
                Assert.IsTrue(opening && opening.Running, "The opening remains active for shot " + (i + 1));
                Assert.That(opening.ElapsedSeconds, Is.GreaterThanOrEqualTo(targetTime), "The unscaled clock reached shot " + (i + 1));
                opening.PlaybackSpeed = 0f;
                yield return SettingsUITests.Capture($"10-opening-{i + 1:00}-{shotNames[i]}.png");
                Assert.That(opening.ElapsedSeconds, Is.LessThan(shotStarts[i + 1]), "The render remains inside scheduled shot " + (i + 1));
                Assert.That(Time.timeScale, Is.Zero, "Shot rendering keeps the live world frozen.");
            }
            // Retain the natural completion path after the final shot, without an 80-second wall-clock wait.
            opening.PlaybackSpeed = 40f;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (opening && opening.Running && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.AreEqual("Watched", OpeningCinematic.LastExitReason);
            AssertCleanHandoff();
            Assert.AreEqual(realNpcs, HostCharacter.All.Count, "The cinematic never registers historical characters as hosts.");
            Debug.Log("[OpeningUI] PASS all 11 rendered shots, full unscaled story timeline, camera/HUD restoration, clean one-amoeba spawn and untouched campaign statistics");
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
