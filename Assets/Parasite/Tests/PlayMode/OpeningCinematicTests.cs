using System.Collections;
using System.Linq;
using NUnit.Framework;
using Parasite.Audio;
using Parasite.FX;
using Parasite.Characters;
using Parasite.Possession;
using Parasite.UI;
using Parasite.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.Video;

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

        [UnityTest, Timeout(150000)]
        public IEnumerator PublishedFullOpeningPlaysContinuouslyAtNormalSpeedAndReturnsToGameplay()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Continuous full-opening playback needs a real graphics device.");
            var clip = Resources.Load<VideoClip>(CinematicStoryPlan.Opening.ResourcePath);
            if (!clip) Assert.Ignore("The rendered full opening has not been published yet.");
            Assert.IsTrue(CinematicStoryPlan.Opening.Validate(clip, out string reason), reason);
            float oldVoice = Sfx.VoiceVolume, oldMusic = Sfx.MusicVolume, oldMaster = AudioListener.volume;
            bool oldPause = AudioListener.pause;
            OpeningCinematic opening = null;
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                yield return BeginFromActualTitle();
                Sfx.VoiceVolume = .5f; Sfx.MusicVolume = .5f; AudioListener.volume = .5f; AudioListener.pause = false;
                opening = OpeningCinematic.Instance;
                Assert.IsTrue(opening.UsingFullStoryFilm, "Title entry must use the actual full movie, not a legacy insert or live fallback.");
                Assert.That(opening.PlaybackSpeed, Is.EqualTo(1f), "This proof must retain ordinary playback speed throughout.");
                var player = Object.FindObjectsByType<VideoPlayer>(FindObjectsSortMode.None).Single(value => value.clip == clip);
                var picture = opening.GetComponentsInChildren<RawImage>(true)
                    .Single(value => value.name == "RTX Cycles Film - " + clip.name);
                float began = Time.realtimeSinceStartup, finishDeadline = began + 100f;
                float prepareDeadline = began + 15f;
                while ((!opening.PresentationReady || player.frame < 2) && Time.realtimeSinceStartup < prepareDeadline) yield return null;
                Assert.IsTrue(opening.PresentationReady && player.isPrepared && player.frame >= 2,
                    "The real decoder must present its first frames before the story advances.");
                Assert.AreEqual(VideoAudioOutputMode.None, player.audioOutputMode, "Live game sources own the soundtrack.");
                Assert.IsTrue(picture.enabled && picture.texture == player.targetTexture);
                float clockOrigin = Time.realtimeSinceStartup - opening.ElapsedSeconds;
                float lastProgressAt = Time.realtimeSinceStartup, maximumDrift = 0f;
                long lastFrame = player.frame, previousCheckpointFrame = -1;
                int checkpoint = 0;
                // Independent authored-story observations. No preview clock changes, seeks or synthetic clips are used.
                float[] checkpointTimes = { 2f, 7f, 17f, 23f, 28f, 40f, 45f, 54f, 62f, 69f, 76f };
                string[] expectedCaptions =
                {
                    "Before faces, there was a rhythm.", "Four taps. Then we begin.", "You remembered.",
                    "It knew I was here.", "Two places. The same remembering.", "A hand that wasn't mine. A heart that was.",
                    "Seven. Stay with the rhythm.", "Who released the interlock?", "",
                    "Outside, nobody knew the rhythm.", "I could make the next beat."
                };
                var caption = opening.GetComponentsInChildren<Text>(true).Single(value => value.name == "Caption");
                while (opening && opening.Running && Time.realtimeSinceStartup < finishDeadline)
                {
                    float story = opening.ElapsedSeconds, now = Time.realtimeSinceStartup;
                    Assert.IsTrue(opening.UsingFullStoryFilm && player && player.isActiveAndEnabled,
                        "A native decode failure must not silently substitute the live set during this proof.");
                    Assert.That(opening.PlaybackSpeed, Is.EqualTo(1f));
                    Assert.That(Time.timeScale, Is.Zero);
                    Assert.IsTrue(PossessionManager.Instance.InputLocked);
                    Assert.AreEqual(GameState.Ending, GameManager.Instance.State);
                    Assert.That(Mathf.Abs(story - (now - clockOrigin)), Is.LessThan(1f),
                        "The normal story clock must keep pace with real elapsed time after the first image.");
                    if (player.frame > lastFrame) { lastFrame = player.frame; lastProgressAt = now; }
                    if (story < 79.5f)
                        Assert.That(now - lastProgressAt, Is.LessThan(2f), "The movie cannot freeze while captions continue.");
                    if (checkpoint < checkpointTimes.Length && story >= checkpointTimes[checkpoint])
                    {
                        float drift = Mathf.Abs((float)player.time - story);
                        maximumDrift = Mathf.Max(maximumDrift, drift);
                        Assert.That(drift, Is.LessThan(.8f), "Decoded image and story diverged at shot " + (checkpoint + 1));
                        Assert.That(player.frame, Is.GreaterThan(previousCheckpointFrame), "Each shot needs newly decoded frames.");
                        Assert.IsTrue(player.isPlaying && picture.enabled && picture.color.a > .9f);
                        Assert.AreEqual(expectedCaptions[checkpoint], caption.text, "Authored caption at shot " + (checkpoint + 1));
                        previousCheckpointFrame = player.frame;
                        Assert.IsNotNull(MusicDirector.Instance);
                        Assert.AreEqual(MusicContext.Opening, MusicDirector.Instance.Context);
                        var score = MusicDirector.Instance.GetComponentsInChildren<AudioSource>(true)
                            .Single(value => value.clip && value.clip.name == "mus_opening" && value.isPlaying);
                        Assert.That(Mathf.Abs(score.time - story), Is.LessThan(.8f), "The actual score must follow the full movie without restarting.");
                        Assert.IsTrue(score.volume > 0 && !score.mute);
                        if (checkpoint == 1 || checkpoint == 6 || checkpoint == 10)
                        {
                            string who = checkpoint == 10 ? "SPECIMEN 7 · MEMORY" : "DR. HANA PETROV";
                            string line = expectedCaptions[checkpoint];
                            var voiceClip = VoiceOver.ClipFor(who, line);
                            Assert.IsNotNull(voiceClip, "The shipped narration clip must exist.");
                            Assert.AreEqual(VoiceOver.KeyFor(who, line), VoiceOver.CurrentKey);
                            var voice = VoiceOver.Instance.GetComponentsInChildren<AudioSource>(true)
                                .Single(value => value.clip == voiceClip && value.isPlaying);
                            Assert.IsTrue(voice.volume > 0 && !voice.mute);
                            int samples = voice.timeSamples;
                            yield return new WaitForSecondsRealtime(.1f);
                            Assert.IsTrue(voice && voice.isPlaying && voice.clip == voiceClip);
                            Assert.That(voice.timeSamples, Is.GreaterThan(samples), "The real voice source must advance while gameplay is frozen.");
                        }
                        if (checkpoint == 0 || checkpoint == 5 || checkpoint == 10)
                        {
                            AssertNonemptyDecodedFrame(player.targetTexture, checkpoint + 1);
                            yield return SettingsUITests.Capture($"11-full-opening-normal-{checkpoint + 1:00}.png");
                        }
                        Debug.Log($"[OpeningFullNormal] shot={checkpoint + 1}/11 story={story:F3} native={player.time:F3} frame={player.frame} drift={drift:F3}");
                        checkpoint++;
                    }
                    yield return null;
                }
                Assert.That(checkpoint, Is.EqualTo(11), "All eleven authored shots must occur through continuous normal playback.");
                Assert.That(lastFrame, Is.GreaterThanOrEqualTo((long)clip.frameCount - 12),
                    "The actual decoder must reach the final half-second, not stop after the last sampled shot.");
                Assert.That(Time.realtimeSinceStartup - clockOrigin, Is.InRange(79.8f, 100f));
                yield return null;
                Assert.AreEqual("Watched", OpeningCinematic.LastExitReason, "Only natural completion counts here.");
                AssertCleanHandoff();
                bool drained = false;
                yield return CinematicFilmLifetime.Drain(CinematicFilmLifetime.DrainTimeout, value => drained = value);
                Assert.IsTrue(drained, "Actual native operations must finish after the natural story handoff.");
                Assert.That(CinematicFilmLifetime.ResourceCount, Is.Zero);
                Assert.That(CinematicFilmLifetime.PendingCount, Is.Zero);
                Assert.That(CinematicFilmLifetime.QuarantinedCount, Is.Zero);
                Assert.IsFalse(VoiceOver.Speaking);
                Debug.Log($"[OpeningFullNormal] PASS real80s normal playback;11shots/3decoded readbacks/3captures/3actualvoice progress checks; max sampled drift={maximumDrift:F3}s; natural gameplay handoff and zero owned media. Physical speaker output is outside this proof.");
            }
            finally
            {
                if (opening && opening.Running) Object.Destroy(opening.gameObject);
                Sfx.VoiceVolume = oldVoice; Sfx.MusicVolume = oldMusic;
                AudioListener.volume = oldMaster; AudioListener.pause = oldPause;
            }
        }

        static void AssertNonemptyDecodedFrame(RenderTexture target, int shot)
        {
            Assert.IsNotNull(target);
            Assert.IsTrue(target.IsCreated());
            var previous = RenderTexture.active;
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
                float minimum = 1f, maximum = 0f, total = 0f; int count = 0;
                for (int y = 17; y < pixels.height; y += 31)
                    for (int x = 17; x < pixels.width; x += 31)
                    {
                        float value = pixels.GetPixel(x, y).grayscale;
                        minimum = Mathf.Min(minimum, value); maximum = Mathf.Max(maximum, value); total += value; count++;
                    }
                Assert.That(maximum - minimum, Is.GreaterThan(.10f), "Actual decoded spatial detail at shot " + shot);
                Assert.That(total / count, Is.GreaterThan(.03f), "The decoded target cannot be black/empty at shot " + shot);
            }
            finally { RenderTexture.active = previous; Object.Destroy(pixels); }
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
