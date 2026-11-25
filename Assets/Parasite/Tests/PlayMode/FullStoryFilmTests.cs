using System.Collections;
using System.Linq;
using NUnit.Framework;
using Parasite.Audio;
using Parasite.FX;
using Parasite.Testing;
using Parasite.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Parasite.Tests
{
    public class FullStoryFilmTests
    {
        [UnityTearDown]
        public IEnumerator DrainNativeMedia()
        {
            CinematicFilmLifetime.RetireAll();
            bool drained=false;
            yield return CinematicFilmLifetime.Drain(CinematicFilmLifetime.DrainTimeout,result=>drained=result);
            Assert.IsTrue(drained,"Retiring real footage cannot be left for Editor/native class shutdown.");
        }
        [UnityTest, Timeout(60000)]
        public IEnumerator OptionalFootageFallbackPreservesChoiceAudioAndCleanup()
        {
            yield return AutoTest.LoadCity();
            float effects=Sfx.Volume, ambience=Sfx.AmbienceVolume, music=Sfx.MusicVolume, voice=Sfx.VoiceVolume, master=AudioListener.volume;
            var sentinels=new GameObject("Pre-lake audio fixture");
            var audible=sentinels.AddComponent<AudioSource>(); audible.mute=false;
            var muted=sentinels.AddComponent<AudioSource>(); muted.mute=true;
            var manager=GameManager.Instance;
            int endings=0;
            void Ending(EndingData data) { endings++; }
            GameEvents.Ending += Ending;
            LakesideEpilogue epilogue=null;
            try
            {
                Assert.IsFalse(CinematicFilm.TryCreate(new CinematicFilmSpec("Cinematics/FullStory/DefinitelyAbsent",80,1920,1080),sentinels.transform,out _));
                manager.FinishEnding(new EndingData()); epilogue=LakesideEpilogue.Instance;
                Assert.IsNotNull(epilogue.SceneCamera); Assert.IsNotNull(epilogue.SeatedGirl);
                Assert.IsTrue(audible.mute); Assert.IsTrue(muted.mute);
                epilogue.RevealChoices();
                float until=Time.realtimeSinceStartup+12;
                while (!epilogue.ChoicesReady && Time.realtimeSinceStartup<until) yield return null;
                Assert.IsTrue(epilogue.ChoicesReady);
                yield return new WaitForSecondsRealtime(.6f);
                Assert.AreEqual(EndingOutcome.Unchosen,epilogue.Outcome); Assert.AreEqual(0,endings);
                epilogue.Choose((EndingOutcome)999); Assert.AreEqual(EndingOutcome.Unchosen,epilogue.Outcome);
                epilogue.Choose(EndingOutcome.Peace);
                epilogue.Choose(EndingOutcome.Dominion);
                Assert.AreEqual(EndingOutcome.Peace,epilogue.Outcome);
                until=Time.realtimeSinceStartup+12;
                var summary=epilogue.GetComponentsInChildren<Button>(true).Single(button=>button.name=="Btn_View your journey");
                while ((!summary.gameObject.activeInHierarchy || !summary.interactable) && Time.realtimeSinceStartup<until) yield return null;
                Assert.IsTrue(summary.gameObject.activeInHierarchy && summary.interactable);
                epilogue.FinishResolution(); epilogue.FinishResolution();
                Assert.AreEqual(1,endings); Assert.IsFalse(VoiceOver.Speaking);
                Assert.AreEqual(effects,Sfx.Volume); Assert.AreEqual(ambience,Sfx.AmbienceVolume);
                Assert.AreEqual(music,Sfx.MusicVolume); Assert.AreEqual(voice,Sfx.VoiceVolume); Assert.AreEqual(master,AudioListener.volume);
                Object.Destroy(epilogue.gameObject); yield return null; yield return null;
                Assert.IsFalse(audible.mute); Assert.IsTrue(muted.mute);
            }
            finally
            {
                GameEvents.Ending -= Ending;
                if (epilogue) Object.Destroy(epilogue.gameObject);
                Object.Destroy(sentinels); Time.timeScale=1; GameInput.Simulated=false;
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator FinalFullFootageDecodesAtItsDeclaredFormatAndHoldLoops() => VerifyFootage(CinematicStoryPlan.FullSet);

        [UnityTest, Timeout(120000)]
        public IEnumerator CompleteLakeFootageDecodesWhileOpeningMayUseFallback() => VerifyFootage(CinematicStoryPlan.LakeSet);

        IEnumerator VerifyFootage(CinematicFilmSpec[] definitions)
        {
            if (SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null) Assert.Ignore("Actual full-story decode needs a GPU.");
            foreach (var spec in definitions)
                if (!Resources.Load<VideoClip>(spec.ResourcePath)) Assert.Ignore("Full footage is not bundled yet: "+spec.ResourcePath);
            var canvas=new GameObject("Full footage decode fixture",typeof(RectTransform),typeof(Canvas));
            float old=Time.timeScale;
            try
            {
                Time.timeScale=0;
                foreach (var spec in definitions)
                {
                    var clip=Resources.Load<VideoClip>(spec.ResourcePath);
                    Assert.IsTrue(spec.Validate(clip,out string reason),reason);
                    Assert.IsTrue(CinematicFilm.TryCreate(spec,canvas.transform,out var film));
                    using (film)
                    {
                        float until=Time.realtimeSinceStartup+15;
                        while ((!film.HasDecodedFrame || film.Player.frame<2) && Time.realtimeSinceStartup<until)
                        { film.Tick(.1f); yield return null; }
                        Assert.IsTrue(film.HasDecodedFrame,spec.ResourcePath);
                        var target=film.Player.texture as RenderTexture;
                        Assert.IsNotNull(target); Assert.AreEqual(spec.Width,(uint)target.width); Assert.AreEqual(spec.Height,(uint)target.height);
                        Assert.AreEqual(0,clip.audioTrackCount,"Live VO owns all dialogue; no movie soundtrack may be silently discarded.");
                        if (spec.Loop)
                        {
                            float seekUntil=Time.realtimeSinceStartup+8;
                            while ((film.Player.frame<130 || film.Seeking) && Time.realtimeSinceStartup<seekUntil)
                            { film.Tick(5.5f,0); yield return null; }
                            Assert.That(film.Player.frame,Is.GreaterThanOrEqualTo(130));
                            int previousLoops=film.LoopCount;
                            float loopStart=Time.realtimeSinceStartup;
                            while (Time.realtimeSinceStartup-loopStart<1.1f)
                            { film.Tick(5.5f+Time.realtimeSinceStartup-loopStart); yield return null; }
                            Assert.That(film.LoopCount,Is.GreaterThan(previousLoops)); Assert.IsTrue(film.Player.isPlaying);
                            Assert.AreEqual(0,Time.timeScale);
                        }
                    }
                    yield return null;
                }
            }
            finally { Time.timeScale=old; Object.Destroy(canvas); }
        }

        [UnityTest, Timeout(45000)]
        public IEnumerator OutcomeFramesRemainHiddenUntilExplicitOutcomeSelection()
        {
            if (SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null) Assert.Ignore("Actual branch decode needs a GPU.");
            foreach (var spec in CinematicStoryPlan.LakeSet)
                if (!Resources.Load<VideoClip>(spec.ResourcePath)) Assert.Ignore("Full branch footage is not bundled yet: "+spec.ResourcePath);
            var canvas=new GameObject("Explicit film outcome fixture",typeof(RectTransform),typeof(Canvas));
            float old=Time.timeScale;
            using (var sequence=new LakesideFilmSequence(canvas.transform))
            {
                try
                {
                    Time.timeScale=0;
                    float start=Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup-start<7.1f)
                    { sequence.TickChoice(Time.realtimeSinceStartup-start,Time.unscaledDeltaTime); yield return null; }
                    var peace=Object.FindObjectsByType<VideoPlayer>(FindObjectsSortMode.None).Single(p=>p.clip==Resources.Load<VideoClip>(CinematicStoryPlan.Peace.ResourcePath));
                    var dominion=Object.FindObjectsByType<VideoPlayer>(FindObjectsSortMode.None).Single(p=>p.clip==Resources.Load<VideoClip>(CinematicStoryPlan.Dominion.ResourcePath));
                    Assert.IsFalse(peace.isPlaying); Assert.IsFalse(dominion.isPlaying);
                    var peacePicture=canvas.GetComponentsInChildren<RawImage>(true).Single(image=>image.name=="RTX Cycles Film - LakePeace");
                    var dominionPicture=canvas.GetComponentsInChildren<RawImage>(true).Single(image=>image.name=="RTX Cycles Film - LakeDominion");
                    Assert.IsFalse(peacePicture.enabled); Assert.IsFalse(dominionPicture.enabled);
                    start=Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup-start<1f)
                    { sequence.TickResolution(EndingOutcome.Peace,Time.realtimeSinceStartup-start,Time.unscaledDeltaTime); yield return null; }
                    Assert.IsTrue(peacePicture.enabled); Assert.IsFalse(dominion.isPlaying);
                }
                finally { Time.timeScale=old; Object.Destroy(canvas); }
            }
        }
    }
}
