using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Video;
using Parasite.UI;
using Parasite.Testing;

namespace Parasite.Tests
{
    public class CinematicFilmTests
    {
        [UnityTest, Timeout(90000)]
        public IEnumerator RtxFilmsDecodeAndAdvanceWhileGameplayTimeIsFrozen()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Video decode verification requires a real GPU run.");
            float oldTimeScale = Time.timeScale;
            var canvas = new GameObject("Film decode verification", typeof(RectTransform), typeof(Canvas));
            try
            {
                Time.timeScale = 0;
                foreach (string resource in new[] { "ContainmentOpening", "LakesideEnding" })
                {
                    var clip = Resources.Load<VideoClip>("Cinematics/Films/" + resource);
                    Assert.IsNotNull(clip, "Actual encoded RTX footage is bundled: " + resource);
                    Assert.AreEqual(1280u, clip.width); Assert.AreEqual(720u, clip.height);
                    Assert.That(clip.length, Is.EqualTo(3f).Within(.1f));
                    using (var film = new CinematicFilm(resource, canvas.transform))
                    {
                        var player = canvas.GetComponentInChildren<VideoPlayer>();
                        float deadline = Time.realtimeSinceStartup + 20f;
                        while (player.frame < 2 && Time.realtimeSinceStartup < deadline)
                        { film.Tick(.1f); yield return null; }
                        Assert.IsTrue(player.isPrepared, "Linux VideoPlayer prepared " + resource);
                        Assert.That(player.frame, Is.GreaterThanOrEqualTo(2), "The decoder produced several frames: " + resource);
                        // Decode is asynchronous: its first available image may arrive after the last Tick above.
                        // Drive the same per-frame lifecycle as OpeningCinematic/LakesideEpilogue before observing UI state.
                        film.Tick(.1f);
                        Assert.IsTrue(film.Ready, "The owning story can advance after its first decoded image: " + resource);
                        var picture = player.GetComponent<RawImage>();
                        Assert.IsTrue(picture.enabled, "A decoded film is visible after its owner updates: " + resource);
                        long frame = player.frame;
                        float observationStart = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - observationStart < .25f)
                        {
                            film.Tick(.1f + Time.realtimeSinceStartup - observationStart);
                            yield return null;
                        }
                        film.Tick(.1f + Time.realtimeSinceStartup - observationStart);
                        Assert.That(player.frame, Is.GreaterThan(frame), "Unscaled playback advances with timeScale=0: " + resource);
                        Assert.IsTrue(picture.enabled, "The movie remains visible while its owner updates: " + resource);
                        var decoded = player.texture as RenderTexture;
                        Assert.IsNotNull(decoded);
                        var oldActive = RenderTexture.active;
                        var pixels = new Texture2D(decoded.width, decoded.height, TextureFormat.RGB24, false);
                        try
                        {
                            RenderTexture.active = decoded;
                            pixels.ReadPixels(new Rect(0, 0, decoded.width, decoded.height), 0, 0); pixels.Apply();
                            float minimum = 1f, maximum = 0f, sum = 0f; int count = 0;
                            for (int y = 10; y < pixels.height; y += 23)
                                for (int x = 10; x < pixels.width; x += 23)
                                {
                                    float value = pixels.GetPixel(x, y).grayscale;
                                    minimum = Mathf.Min(minimum, value); maximum = Mathf.Max(maximum, value); sum += value; count++;
                                }
                            Assert.That(maximum, Is.GreaterThan(.15f), "Decoded film contains illuminated image data: " + resource);
                            Assert.That(maximum - minimum, Is.GreaterThan(.10f), "Decoded film contains spatial image detail: " + resource);
                            Assert.That(sum / count, Is.GreaterThan(.03f), "The output is visibly populated, not an empty target: " + resource);
                        }
                        finally { RenderTexture.active = oldActive; Object.Destroy(pixels); }
                        film.Tick(CinematicFilm.Duration);
                        Assert.IsFalse(picture.enabled, "The insert yields to the interactive film set.");
                        Debug.Log("[CinematicFilm] PASS decoded 1280x720 RTX/Cycles film at frozen gameplay time: " + resource);
                    }
                    yield return null;
                }
            }
            finally { Time.timeScale = oldTimeScale; Object.Destroy(canvas); }
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator LakesideArrivalShowsTheDecodedFilmUnderStoryCaptions()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Film composition capture requires a real GPU run.");
            yield return AutoTest.LoadCity();
            var manager = GameManager.Instance;
            manager.BeginEnding(); manager.FinishEnding(new EndingData { timeSeconds = 120f });
            var epilogue = LakesideEpilogue.Instance;
            try
            {
                Assert.IsNotNull(epilogue);
                var movie = Object.FindFirstObjectByType<VideoPlayer>();
                Assert.IsNotNull(movie);
                float deadline = Time.realtimeSinceStartup + 8f;
                while (movie.frame < 2 && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(movie.frame, Is.GreaterThanOrEqualTo(2));
                var picture = movie.GetComponent<RawImage>();
                Assert.IsTrue(picture.enabled && picture.color.a > .9f, "The actual arrival movie is visible beneath the caption canvas.");
                Assert.AreEqual(EndingOutcome.Unchosen, epilogue.Outcome);
                Assert.IsFalse(epilogue.ChoicesReady);
                yield return SettingsUITests.Capture("05a-lakeside-rtx-arrival.png");
                Assert.AreEqual(GameState.Ending, manager.State);
            }
            finally
            {
                if (epilogue) Object.Destroy(epilogue.gameObject);
                GameInput.Simulated = false;
                if (manager) manager.ResumeAfterDialogue();
            }
            yield return null;
        }

    }
}
