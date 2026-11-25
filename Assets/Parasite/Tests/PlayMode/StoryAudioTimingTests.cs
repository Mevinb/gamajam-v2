using System.Collections;
using System.Linq;
using NUnit.Framework;
using Parasite.Audio;
using Parasite.FX;
using Parasite.Testing;
using Parasite.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Parasite.Tests
{
    /// <summary>Actual shipped music/voice playback: no synthetic movies or substituted audio clips.</summary>
    public class StoryAudioTimingTests
    {
        float oldVoice, oldMaster, oldScale;
        bool oldPause;
        GameObject fixture;
        VoiceOver disabledVoice;

        [UnitySetUp]
        public IEnumerator LoadRealAudioScene()
        {
            oldVoice = Sfx.VoiceVolume; oldMaster = AudioListener.volume; oldPause = AudioListener.pause; oldScale = Time.timeScale;
            yield return AutoTest.LoadCity();
            AudioDirector.Ensure(); AudioBus.InvalidateListener();
            Sfx.VoiceVolume = 1; AudioListener.volume = 1; AudioListener.pause = false;
            Assert.IsNotNull(AudioBus.Listener, "A real enabled listener exists for the shipped AudioSources.");
            Assert.IsNotNull(VoiceOver.Instance);
        }

        [UnityTearDown]
        public IEnumerator RestoreAudioAndScene()
        {
            if (disabledVoice) disabledVoice.enabled = true;
            if (LakesideEpilogue.Instance) Object.Destroy(LakesideEpilogue.Instance.gameObject);
            VoiceOver.StopSpeaking();
            if (fixture) Object.Destroy(fixture);
            yield return null;
            Time.timeScale = 1; GameInput.Simulated = false;
            SceneManager.LoadScene(GameManager.MenuScene);
            yield return null;
            Sfx.VoiceVolume = oldVoice; AudioListener.volume = oldMaster; AudioListener.pause = oldPause; Time.timeScale = oldScale;
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator OpeningScoreWaitsWithoutPlayingOrSeekingAndStartsAtReadyClock()
        {
            Time.timeScale = 0;
            var clip = Resources.Load<AudioClip>("Audio/Music/mus_opening");
            Assert.IsNotNull(clip); Assert.That(clip.length, Is.GreaterThanOrEqualTo(80));
            clip.LoadAudioData();
            float loadUntil = Time.realtimeSinceStartup + 5;
            while (clip.loadState == AudioDataLoadState.Loading && Time.realtimeSinceStartup < loadUntil) yield return null;
            Assert.AreEqual(AudioDataLoadState.Loaded, clip.loadState);
            fixture = new GameObject("Real opening-score readiness fixture");
            var source = fixture.AddComponent<AudioSource>();
            source.clip = clip; source.playOnAwake = false; source.ignoreListenerPause = true; source.volume = .001f;
            float began = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - began < .9f)
            {
                MusicDirector.DriveOpeningScore(source, false, 0);
                Assert.IsFalse(source.isPlaying); Assert.AreEqual(0, source.timeSamples);
                yield return null;
            }
            MusicDirector.DriveOpeningScore(source, true, 3.25f);
            Assert.IsTrue(source.isPlaying, "Readiness starts the actual score immediately, without the one-second restart timer.");
            Assert.That(source.time, Is.EqualTo(3.25f).Within(.10f));
            began = Time.realtimeSinceStartup; float previous = source.time;
            while (Time.realtimeSinceStartup - began < .9f)
            {
                MusicDirector.DriveOpeningScore(source, true, 3.25f + Time.realtimeSinceStartup - began);
                Assert.That(source.time, Is.GreaterThanOrEqualTo(previous - .05f), "The playing score must not rewind while presentation advances.");
                previous = source.time; yield return null;
            }
            Assert.That(source.time, Is.GreaterThan(4.0f));
            MusicDirector.DriveOpeningScore(source, false, previous);
            float held = source.time; began = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - began < .8f)
            {
                MusicDirector.DriveOpeningScore(source, false, held);
                Assert.IsFalse(source.isPlaying); Assert.That(source.time, Is.EqualTo(held).Within(.02f));
                yield return null;
            }
            MusicDirector.DriveOpeningScore(source, true, held);
            Assert.IsTrue(source.isPlaying); Assert.That(source.time, Is.EqualTo(held).Within(.1f));
            Assert.That(Time.timeScale, Is.Zero);
        }

        static string Caption(EndingOutcome outcome) => outcome == EndingOutcome.Peace
            ? "A small life. An ordinary name.\nA promise to leave every borrowed heart free."
            : "One mind had learned to live in two bodies.\nNext: a city of hosts. Then the world.";
        static string Beat(EndingOutcome outcome) => outcome == EndingOutcome.Peace
            ? "For once, there was nothing to prove." : "What began as escape would become conquest.";

        static IEnumerator BeginOutcome(EndingOutcome outcome)
        {
            GameManager.Instance.FinishEnding(new EndingData());
            var epilogue = LakesideEpilogue.Instance; Assert.IsNotNull(epilogue);
            epilogue.RevealChoices();
            float until = Time.realtimeSinceStartup + 12;
            while (!epilogue.ChoicesReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(epilogue.ChoicesReady);
            epilogue.Choose(outcome);
            var caption = epilogue.GetComponentsInChildren<Text>(true).Single(text => text.name == "Caption");
            until = Time.realtimeSinceStartup + 12;
            while (caption.text != Caption(outcome) && Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual(Caption(outcome), caption.text, "Resolution is presented after its image/fallback becomes ready.");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator NaturalResolutionsPlayTheirRealCaptionAndBeatBeforeAutomaticSummary()
        {
            foreach (var outcome in new[] { EndingOutcome.Peace, EndingOutcome.Dominion })
            {
                if (outcome == EndingOutcome.Dominion) yield return AutoTest.LoadCity();
                yield return BeginOutcome(outcome);
                var epilogue = LakesideEpilogue.Instance; var manager = GameManager.Instance;
                string captionKey = VoiceOver.KeyFor(LakesideNarration.Speaker, Caption(outcome));
                string beatKey = VoiceOver.KeyFor(LakesideNarration.Speaker, Beat(outcome));
                var captionClip = VoiceOver.ClipFor(LakesideNarration.Speaker, Caption(outcome));
                var beatClip = VoiceOver.ClipFor(LakesideNarration.Speaker, Beat(outcome));
                Assert.IsNotNull(captionClip); Assert.IsNotNull(beatClip);
                float expected = captionClip.length + LakesideNarration.BeatDelay + beatClip.length;
                Assert.That(expected, Is.GreaterThan(7.5f), "The real shipped lines reproduce the old seven-second cutoff.");
                float until = Time.realtimeSinceStartup + 18, captionAt = -1, beatAt = -1;
                bool observedPastSeven = false;
                while (manager.State != GameState.Ended && Time.realtimeSinceStartup < until)
                {
                    if (VoiceOver.CurrentKey == captionKey && captionAt < 0) captionAt = Time.realtimeSinceStartup;
                    if (VoiceOver.CurrentKey == beatKey && beatAt < 0) beatAt = Time.realtimeSinceStartup;
                    if (!observedPastSeven && captionAt >= 0 && Time.realtimeSinceStartup - captionAt > 7.05f)
                    {
                        observedPastSeven = true;
                        Assert.AreEqual(GameState.Ending, manager.State, "The movie may hold its final frame while the real beat finishes.");
                        Assert.That(beatAt, Is.GreaterThan(0)); Assert.IsTrue(VoiceOver.Speaking);
                    }
                    yield return null;
                }
                Assert.That(captionAt, Is.GreaterThan(0)); Assert.That(beatAt, Is.GreaterThan(captionAt));
                Assert.IsTrue(observedPastSeven); Assert.AreEqual(GameState.Ended, manager.State);
                Assert.That(Time.realtimeSinceStartup - captionAt, Is.GreaterThanOrEqualTo(expected - .10f));
                Assert.IsFalse(VoiceOver.Speaking); Assert.IsFalse(LakesideNarration.HasPendingFor(epilogue));
                Object.Destroy(epilogue.gameObject); yield return null;
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator MutedDisabledAndMissingVoicesDoNotTrapAutomaticCompletion()
        {
            foreach (string mode in new[] { "muted", "disabled", "disabled-mid-beat", "missing-clips" })
            {
                yield return AutoTest.LoadCity(); Sfx.VoiceVolume = 1; AudioListener.volume = 1;
                if (disabledVoice) { disabledVoice.enabled = true; disabledVoice = null; }
                VoiceOver.StopSpeaking(); yield return new WaitForSecondsRealtime(.15f);
                if (mode == "muted") Sfx.VoiceVolume = 0;
                if (mode == "disabled") { disabledVoice = VoiceOver.Instance; disabledVoice.enabled = false; }
                yield return BeginOutcome(EndingOutcome.Dominion);
                var epilogue = LakesideEpilogue.Instance;
                if (mode == "disabled-mid-beat")
                {
                    string beatKey = VoiceOver.KeyFor(LakesideNarration.Speaker, Beat(EndingOutcome.Dominion));
                    float beatUntil = Time.realtimeSinceStartup + 8;
                    while (VoiceOver.CurrentKey != beatKey && Time.realtimeSinceStartup < beatUntil) yield return null;
                    Assert.AreEqual(beatKey, VoiceOver.CurrentKey);
                    disabledVoice = VoiceOver.Instance; disabledVoice.enabled = false;
                }
                if (mode == "missing-clips")
                {
                    VoiceOver.StopSpeaking();
                    foreach (var text in epilogue.GetComponentsInChildren<Text>(true))
                        if (text.name == "Caption" || text.name == "Beat") text.text = "Missing narration fixture: " + text.name;
                    Assert.IsNull(VoiceOver.ClipFor(LakesideNarration.Speaker, "Missing narration fixture: Caption"));
                }
                float began = Time.realtimeSinceStartup, until = began + 9;
                while (GameManager.Instance.State != GameState.Ended && Time.realtimeSinceStartup < until) yield return null;
                Assert.AreEqual(GameState.Ended, GameManager.Instance.State, mode + " narration must not wait indefinitely.");
                Assert.That(Time.realtimeSinceStartup - began, Is.LessThan(8.5f));
                Assert.IsFalse(LakesideNarration.HasPendingFor(epilogue));
                yield return new WaitForSecondsRealtime(.2f);
                var captionClip = VoiceOver.ClipFor(LakesideNarration.Speaker, Caption(EndingOutcome.Dominion));
                var beatClip = VoiceOver.ClipFor(LakesideNarration.Speaker, Beat(EndingOutcome.Dominion));
                Assert.IsFalse(AudioDirector.Instance.GetComponentsInChildren<AudioSource>().Any(source =>
                    source && source.isPlaying && (source.clip == captionClip || source.clip == beatClip)), mode + " completion must stop stale outcome speech.");
                Object.Destroy(epilogue.gameObject); yield return null;
            }
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ExplicitResolutionSkipCancelsCurrentAndQueuedRealSpeech()
        {
            foreach (bool disableMidLine in new[] { false, true })
            {
                if (disableMidLine) yield return AutoTest.LoadCity();
                yield return BeginOutcome(EndingOutcome.Dominion);
                var epilogue = LakesideEpilogue.Instance;
                string expected = VoiceOver.KeyFor(LakesideNarration.Speaker, Caption(EndingOutcome.Dominion));
                float until = Time.realtimeSinceStartup + 3;
                while (VoiceOver.CurrentKey != expected && Time.realtimeSinceStartup < until) yield return null;
                Assert.AreEqual(expected, VoiceOver.CurrentKey); yield return new WaitForSecondsRealtime(.9f);
                if (disableMidLine) { disabledVoice = VoiceOver.Instance; disabledVoice.enabled = false; }
                epilogue.FinishResolution();
                Assert.AreEqual(GameState.Ended, GameManager.Instance.State); Assert.IsFalse(VoiceOver.Speaking);
                yield return new WaitForSecondsRealtime(.2f);
                var captionClip = VoiceOver.ClipFor(LakesideNarration.Speaker, Caption(EndingOutcome.Dominion));
                var beatClip = VoiceOver.ClipFor(LakesideNarration.Speaker, Beat(EndingOutcome.Dominion));
                Assert.IsFalse(AudioDirector.Instance.GetComponentsInChildren<AudioSource>().Any(source =>
                    source && source.isPlaying && (source.clip == captionClip || source.clip == beatClip)), "No stale caption or delayed beat remains after explicit skip, including disabled VO.");
                Assert.IsFalse(LakesideNarration.HasPendingFor(epilogue));
                if (disabledVoice) { disabledVoice.enabled = true; disabledVoice = null; }
                Object.Destroy(epilogue.gameObject); yield return null;
            }
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator DestroyingOutcomeStopsSpeechWhenVoiceUpdateIsDisabled()
        {
            yield return BeginOutcome(EndingOutcome.Dominion);
            var epilogue = LakesideEpilogue.Instance;
            string key = VoiceOver.KeyFor(LakesideNarration.Speaker, Caption(EndingOutcome.Dominion));
            float until = Time.realtimeSinceStartup + 3;
            while (VoiceOver.CurrentKey != key && Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual(key, VoiceOver.CurrentKey);
            var captionClip = VoiceOver.ClipFor(LakesideNarration.Speaker, Caption(EndingOutcome.Dominion));
            var beatClip = VoiceOver.ClipFor(LakesideNarration.Speaker, Beat(EndingOutcome.Dominion));
            disabledVoice = VoiceOver.Instance;
            disabledVoice.enabled = false;
            Object.Destroy(epilogue.gameObject);
            yield return null;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.IsFalse(AudioDirector.Instance.GetComponentsInChildren<AudioSource>().Any(source =>
                source && source.isPlaying && (source.clip == captionClip || source.clip == beatClip)),
                "Destroying the story must stop its persistent speech even without VoiceOver.Update.");
            Assert.IsFalse(LakesideEpilogue.Instance);
        }
    }
}
