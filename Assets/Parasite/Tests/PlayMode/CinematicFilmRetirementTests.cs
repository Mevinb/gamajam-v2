using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.Video;
using Object = UnityEngine.Object;

namespace Parasite.Tests
{
    /// <summary>Real published movies and decoder acknowledgements; no synthetic footage or fake seek callbacks.</summary>
    public class CinematicFilmRetirementTests
    {
        GameObject canvas;
        CinematicFilm film;
        float oldScale;
        [UnitySetUp]
        public IEnumerator Setup()
        {
            oldScale = Time.timeScale;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Actual native seek/retirement needs a GPU.");
            Time.timeScale = 0;
            CinematicFilmLifetime.RetireAll();
            bool clean = false;
            yield return CinematicFilmLifetime.Drain(CinematicFilmLifetime.DrainTimeout, result => clean = result);
            Assert.IsTrue(clean); Assert.AreEqual(0, CinematicFilmLifetime.ResourceCount);
            canvas = new GameObject("Retirement test scene UI", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        }
        void CreateRealFilm()
        {
            var clip = Resources.Load<VideoClip>(CinematicStoryPlan.Arrival.ResourcePath);
            Assert.IsNotNull(clip, "Actual15second1920x1080 published lake movie is mandatory.");
            Assert.AreEqual(0, clip.audioTrackCount);
            Assert.IsTrue(CinematicFilm.TryCreate(CinematicStoryPlan.Arrival, canvas.transform, out film));
            Assert.AreEqual(VideoAudioOutputMode.None, film.Player.audioOutputMode);
            Assert.AreNotEqual(canvas.scene, film.Player.gameObject.scene, "Native owner is independent of destroyable scene UI.");
        }
        IEnumerator DecodeFirstFrames()
        {
            float until = Time.realtimeSinceStartup + 15;
            while ((!film.HasDecodedFrame || film.Player.frame < 2 || film.Seeking) && Time.realtimeSinceStartup < until)
            { film.Tick(.1f); yield return null; }
            film.Tick(.1f);
            Assert.IsTrue(film.HasDecodedFrame); Assert.That(film.Player.frame, Is.GreaterThanOrEqualTo(2));
            Assert.IsTrue(film.Picture.enabled); Assert.AreEqual(0, Time.timeScale);
        }
        IEnumerator PauseBeforeTerminalSeek()
        {
            film.Hide(); // Hold a real decoded image while any preceding request and the normal throttle settle.
            float acknowledgementUntil = Time.realtimeSinceStartup + CinematicFilmLifetime.OperationTimeout;
            while (film.Seeking && Time.realtimeSinceStartup < acknowledgementUntil) yield return null;
            Assert.IsFalse(film.Seeking, "The preceding actual seek must acknowledge before this fixture requests its terminal seek.");
            film.Hide(); // Reassert pause on the settled image before measuring its frozen time.
            double pausedTime = film.Player.time;
            float throttleUntil = Time.realtimeSinceStartup + .4f;
            while (Time.realtimeSinceStartup < throttleUntil) yield return null;
            Assert.IsFalse(film.Player.isPlaying, "The decoder stays paused while the existing seek throttle clears.");
            Assert.That(film.Player.time, Is.EqualTo(pausedTime).Within(1 / CinematicFilmSpec.Fps));
            Assert.That(film.Player.time, Is.LessThan(CinematicStoryPlan.Arrival.Seconds - 1), "The terminal request has real drift from the paused first image.");
        }
        IEnumerator ExpectCleanRetirement()
        {
            bool drained = false;
            yield return CinematicFilmLifetime.Drain(CinematicFilmLifetime.DrainTimeout, result => drained = result);
            Assert.IsTrue(drained); Assert.IsTrue(film.RetirementComplete); Assert.IsFalse(film.RetirementQuarantined);
            Assert.IsNull(film.RetirementError); Assert.AreEqual(0, CinematicFilmLifetime.PendingCount);
            Assert.AreEqual(0, CinematicFilmLifetime.ResourceCount);
        }

        [UnityTest, Timeout(45000)]
        public IEnumerator ForcedTerminalSeekIsAcknowledgedBeforeStopAndTargetRelease()
        {
            CreateRealFilm(); yield return DecodeFirstFrames();
            yield return PauseBeforeTerminalSeek();
            var player = film.Player; var image = film.Picture; var texture = player.targetTexture;
            film.Tick(CinematicStoryPlan.Arrival.Seconds); // Preserve the exact original native stress.
            Assert.IsTrue(film.Seeking, "The real end-seek is outstanding before retirement.");
            film.Dispose();
            Assert.IsFalse(image.enabled); Assert.IsNull(image.texture, "Visible skip is immediate.");
            Assert.IsTrue(player && player.enabled && player.gameObject.activeInHierarchy);
            Assert.AreSame(texture, player.targetTexture); Assert.IsTrue(texture.IsCreated(), "Do not Stop/detach/free under an owned pending seek.");
            Assert.IsTrue(film.RetirementSawPendingSeek);
            yield return ExpectCleanRetirement();
            Assert.IsTrue(film.RetirementSeekAcknowledged, "The actual seekCompleted callback was observed while native resources remained owned.");
            Assert.IsFalse(player); Assert.IsFalse(texture && texture.IsCreated());
        }

        [UnityTest, Timeout(45000)]
        public IEnumerator LegacyInsertEndRetiresItsPendingDriftSeekWithoutStoppingEarly()
        {
            Assert.IsTrue(CinematicFilm.TryCreate(CinematicFilmSpec.Legacy("ContainmentOpening"), canvas.transform, out film));
            yield return DecodeFirstFrames();
            var player = film.Player; var texture = player.targetTexture;
            film.Hide(); // Freeze the real first image so the later request has definite drift.
            float until = Time.realtimeSinceStartup + .4f;
            while (Time.realtimeSinceStartup < until) yield return null; // Clear the normal seek throttle without changing the story clock.
            film.Tick(1.5f, 0); Assert.IsTrue(film.Seeking);
            film.Tick(CinematicFilm.Duration);
            Assert.IsFalse(film.Picture.enabled); Assert.IsNull(film.Picture.texture);
            Assert.IsTrue(player && player.enabled); Assert.AreSame(texture, player.targetTexture);
            Assert.IsTrue(film.RetirementSawPendingSeek);
            yield return ExpectCleanRetirement();
            Assert.IsTrue(film.RetirementSeekAcknowledged);
        }

        [UnityTest, Timeout(45000)]
        public IEnumerator DisabledAndDestroyedSceneOwnerCannotDestroyPendingNativePlayer()
        {
            CreateRealFilm(); yield return DecodeFirstFrames();
            yield return PauseBeforeTerminalSeek();
            var player = film.Player; var texture = player.targetTexture;
            film.Tick(CinematicStoryPlan.Arrival.Seconds); Assert.IsTrue(film.Seeking);
            canvas.SetActive(false);
            Assert.IsTrue(player.gameObject.activeInHierarchy, "Disabling UI cannot implicitly Stop the native decoder.");
            Object.Destroy(canvas); canvas = null;
            Assert.IsTrue(player); Assert.IsTrue(texture.IsCreated());
            SceneManager.LoadScene(GameManager.MenuScene); yield return null;
            yield return ExpectCleanRetirement();
            if (film.RetirementSawPendingSeek) Assert.IsTrue(film.RetirementSeekAcknowledged);
            Assert.IsFalse(player); Assert.IsFalse(texture && texture.IsCreated());
        }

        [UnityTest, Timeout(45000)]
        public IEnumerator CancelBeforeFirstImageDrainsRealPreparationWithoutPlayingAudio()
        {
            CreateRealFilm();
            var player = film.Player; var image = film.Picture; var texture = player.targetTexture;
            film.Dispose();
            Assert.IsFalse(image.enabled); Assert.IsNull(image.texture); Assert.AreEqual(VideoAudioOutputMode.None, player.audioOutputMode);
            Assert.IsFalse(player.isPlaying, "Retirement must not start a movie just to complete preparation.");
            yield return ExpectCleanRetirement();
            Assert.IsFalse(player); Assert.IsFalse(texture && texture.IsCreated());
        }

        [UnityTest, Timeout(45000)]
        public IEnumerator ZeroBudgetDrainReturnsFailureWithoutAbortingOwnedRetirement()
        {
            CreateRealFilm(); yield return DecodeFirstFrames();
            yield return PauseBeforeTerminalSeek();
            var texture = film.Player.targetTexture;
            film.Tick(CinematicStoryPlan.Arrival.Seconds); Assert.IsTrue(film.Seeking); film.Dispose();
            bool observed = true; float began = Time.realtimeSinceStartup;
            yield return CinematicFilmLifetime.Drain(0, result => observed = result);
            Assert.IsFalse(observed); Assert.That(Time.realtimeSinceStartup - began, Is.LessThan(.25f));
            Assert.IsTrue(film.RetirementComplete || texture.IsCreated(), "A cancelled observer cannot prematurely free native resources.");
            yield return ExpectCleanRetirement();
        }

        [UnityTest, Timeout(45000)]
        public IEnumerator MissingAndDisabledMediaRemainBoundedAndSilent()
        {
            int before = CinematicFilmLifetime.ResourceCount;
            Assert.IsFalse(CinematicFilm.TryCreate(new CinematicFilmSpec("Cinematics/FullStory/DefinitelyMissing", 15, 1920, 1080), canvas.transform, out _));
            Assert.AreEqual(before, CinematicFilmLifetime.ResourceCount);
            CreateRealFilm(); yield return DecodeFirstFrames();
            var player = film.Player; var texture = player.targetTexture;
            Assert.IsFalse(film.Seeking); player.enabled = false; film.Dispose();
            Assert.IsFalse(film.Picture.enabled); Assert.AreEqual(VideoAudioOutputMode.None, player.audioOutputMode);
            yield return ExpectCleanRetirement();
            Assert.IsFalse(player); Assert.IsFalse(texture && texture.IsCreated());
        }
        [UnityTest, Timeout(45000)]
        public IEnumerator RealLocalBackendErrorRetiresWithoutKeepingAnAudibleOrVisiblePlayer()
        {
            CreateRealFilm(); yield return DecodeFirstFrames();
            var player = film.Player; var texture = player.targetTexture;
            string missingName = "parasite-retirement-missing-" + Guid.NewGuid().ToString("N") + ".webm";
            string path = Path.Combine(Application.temporaryCachePath, missingName);
            Assert.IsFalse(File.Exists(path));
            bool backendError = false;
            void Error(VideoPlayer source, string message) { if (source == player) backendError = true; }
            var errors = new List<string>();
            void Log(string message, string stack, LogType kind) { if (kind == LogType.Error || kind == LogType.Exception) errors.Add(message); }
            player.errorReceived += Error; Application.logMessageReceived += Log;
            bool oldIgnore = LogAssert.ignoreFailingMessages;
            try
            {
                // A nonexistent local file exercises the real backend error callback; no file/media is fabricated.
                LogAssert.ignoreFailingMessages = true;
                player.source = VideoSource.Url; player.url = new Uri(path).AbsoluteUri; player.Prepare();
                float until = Time.realtimeSinceStartup + 8;
                while (!backendError && Time.realtimeSinceStartup < until) yield return null;
                Assert.IsTrue(backendError); Assert.IsTrue(film.Failed);
                yield return ExpectCleanRetirement();
            }
            finally
            {
                LogAssert.ignoreFailingMessages = oldIgnore; Application.logMessageReceived -= Log;
                if (player) player.errorReceived -= Error;
            }
            foreach (string error in errors)
                Assert.IsTrue(error.Contains(missingName) || error.Contains("VideoPlayer") || error.Contains("movie"), "Only expected local-media backend errors may be suppressed: " + error);
            Assert.IsFalse(player); Assert.IsFalse(texture && texture.IsCreated());
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            film?.Dispose(); if (canvas) Object.Destroy(canvas);
            CinematicFilmLifetime.RetireAll();
            bool clean = false;
            yield return CinematicFilmLifetime.Drain(CinematicFilmLifetime.DrainTimeout, result => clean = result);
            Assert.IsTrue(clean, "No native video retirement remains for Editor shutdown.");
            Time.timeScale = oldScale;
        }
    }
}
