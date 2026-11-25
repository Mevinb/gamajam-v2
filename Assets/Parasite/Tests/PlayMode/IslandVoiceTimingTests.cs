using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Parasite.Audio;
using Parasite.FX;
using Parasite.Missions;
using Parasite.Possession;
using Parasite.Stealth;
using Parasite.Testing;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Parasite.Tests
{
    /// <summary>Published island clips and real protected controllers; no fabricated audio or media.</summary>
    public class IslandVoiceTimingTests
    {
        const string Specimen = "Two currents met beneath the hull. One specimen reached the far shore.";
        const string Radio = "Low water opens the culvert. Maintenance can take the service gate. Find the dry refuge beyond the fence.";
        const string PeaceDeparture = "Everyone aboard. No borrowed heart left behind.";
        const string DominionDeparture = "Every courier answered. The next shore is listening.";
        const string PeaceRefuge = "You made it to the dry refuge. No one here belongs to Vale. Let us keep it that way.";
        const string DominionRefuge = "The island has more than one route out. From this refuge, every route can become a connection.";
        const string PeaceRoster = "All three reached the clinic. The rescue crew needs a mounted signal and everyone at the northern launch.";
        const string DominionRoster = "Three couriers carry the relay. Mount the signal, then let the handoff routes converge at the northern launch.";
        sealed class ClipEvidence
        {
            public string Key; public AudioClip Clip;
            public int Raised, MaxSamples, Cleared; public bool Played, Muted, ObservedCurrentKey, PlayingAtClear;
            public string ActualSpeaker, ActualLine;
            public float RaisedAt = -1, ClearedAt = -1;
            public void Reset()
            {
                Raised = MaxSamples = Cleared = 0; Played = Muted = ObservedCurrentKey = PlayingAtClear = false;
                RaisedAt = ClearedAt = -1; ActualSpeaker = ActualLine = null;
            }
        }
        readonly Dictionary<string, ClipEvidence> evidence = new();
        readonly Dictionary<string, (bool existed, AudioClip clip)> suppressed = new();
        Dictionary<string, AudioClip> lookup;
        AudioDirector audioRoot;
        AudioSource[] voiceSources;
        Coroutine observer;
        string observedSubtitleKey;
        bool recording, frozenViolation, firstSilentBeforeRadio, oldPause, oldVoiceEnabled;
        float oldVoice, oldMaster, oldScale;

        [UnitySetUp]
        public IEnumerator PreparePublishedAudio()
        {
            oldVoice = Sfx.VoiceVolume; oldMaster = AudioListener.volume; oldPause = AudioListener.pause; oldScale = Time.timeScale;
            audioRoot = AudioDirector.Ensure(); Assert.IsNotNull(audioRoot); Assert.IsNotNull(VoiceOver.Instance);
            oldVoiceEnabled = VoiceOver.Instance.enabled; VoiceOver.Instance.enabled = true;
            Sfx.VoiceVolume = 1; AudioListener.volume = 1; AudioListener.pause = false; Time.timeScale = 1;
            GameInput.Simulated = true; ClearInput(); VoiceOver.StopSpeaking();
            voiceSources = audioRoot.GetComponentsInChildren<AudioSource>(true).Where(s => s.name == "Voice A" || s.name == "Voice B").ToArray();
            Assert.AreEqual(2, voiceSources.Length, "Observe the actual persistent VO sources.");
            AddClip("SPECIMEN 7", Specimen); AddClip("ISLAND RADIO", Radio);
            AddClip("RESCUE RADIO", PeaceDeparture); AddClip("RESCUE RADIO", DominionDeparture);
            AddClip("ISLAND RADIO", PeaceRefuge); AddClip("ISLAND RADIO", DominionRefuge);
            AddClip("ISLAND RADIO", PeaceRoster); AddClip("ISLAND RADIO", DominionRoster);
            SceneManager.sceneLoaded += OnSceneLoaded;
            observer = audioRoot.StartCoroutine(ObserveSources());
            yield return new WaitForSecondsRealtime(.15f);
        }
        void AddClip(string who, string line)
        {
            var clip = VoiceOver.ClipFor(who, line);
            Assert.IsNotNull(clip, "This test requires the real published clip: " + VoiceOver.KeyFor(who, line));
            string key = VoiceOver.KeyFor(who, line);
            evidence[key] = new ClipEvidence { Key = key, Clip = clip };
        }
        ClipEvidence Cue(string who, string line) => evidence[VoiceOver.KeyFor(who, line)];
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!RunState.IslandStarted || scene.name != GameManager.CityScene) return;
            // CityDirector clears the event hub during boot; bind after the actual island scene loads.
            GameEvents.Subtitle -= OnSubtitle; GameEvents.Subtitle += OnSubtitle;
        }
        void OnSubtitle(string who, string line)
        {
            if (!recording) return;
            if (string.IsNullOrEmpty(line))
            {
                if (observedSubtitleKey != null && evidence.TryGetValue(observedSubtitleKey, out var cleared))
                {
                    cleared.Cleared++; cleared.ClearedAt = Time.realtimeSinceStartup; cleared.PlayingAtClear |= Playing(cleared.Clip);
                }
                observedSubtitleKey = null; return;
            }
            observedSubtitleKey = VoiceOver.KeyFor(who, line);
            if (!evidence.TryGetValue(observedSubtitleKey, out var cue)) return;
            cue.ActualSpeaker = who; cue.ActualLine = line;
            cue.Raised++; if (cue.RaisedAt < 0) cue.RaisedAt = Time.realtimeSinceStartup;
            if (who == "ISLAND RADIO" && line == Radio)
                firstSilentBeforeRadio = !Playing(Cue("SPECIMEN 7", Specimen).Clip);
        }
        IEnumerator ObserveSources()
        {
            while (true)
            {
                if (recording)
                    foreach (var source in voiceSources)
                    {
                        if (!source || !source.isPlaying || !source.clip) continue;
                        var cue = evidence.Values.FirstOrDefault(e => e.Clip == source.clip);
                        if (cue == null) continue;
                        cue.Played = true; cue.MaxSamples = Mathf.Max(cue.MaxSamples, source.timeSamples);
                        cue.ObservedCurrentKey |= VoiceOver.CurrentKey == cue.Key;
                        cue.Muted |= source.mute || source.volume <= 0 || AudioListener.volume <= 0;
                        if (GameManager.Instance && GameManager.Instance.State == GameState.Ending && Time.timeScale != 0) frozenViolation = true;
                    }
                yield return null;
            }
        }
        bool Playing(AudioClip clip) => voiceSources.Any(source => source && source.isPlaying && source.clip == clip);
        void ResetEvidence()
        {
            foreach (var item in evidence.Values) item.Reset();
            frozenViolation = false; firstSilentBeforeRadio = false; observedSubtitleKey = null; recording = true;
        }
        static void ClearInput()
        {
            GameInput.SimMove = Vector2.zero;
            GameInput.SimJump = GameInput.SimInteract = GameInput.SimLeave = GameInput.SimSplit = GameInput.SimSwitch = GameInput.SimSprint = false;
        }
        static void AssertFullPlayback(ClipEvidence cue)
        {
            Assert.IsTrue(cue.Played, "A real source played " + cue.Key);
            Assert.IsFalse(cue.Muted, "The actual voice was audible through the enabled voice/master buses.");
            Assert.That(cue.MaxSamples, Is.GreaterThanOrEqualTo(cue.Clip.samples - Mathf.CeilToInt(cue.Clip.frequency * .15f)),
                "The source reached the real clip tail before the next cue/summary: " + cue.Key);
        }
        IEnumerator PrepareArrivalReplay()
        {
            yield return IslandEntryAcceptance.EnterFromFerry(EndingOutcome.Peace, true);
            VoiceOver.StopSpeaking(); yield return new WaitForSecondsRealtime(.2f);
            ClearInput(); ResetEvidence();
            Assert.IsNotNull(AudioBus.Listener);
            RunState.Flags.Remove(IslandCampaign.ArrivalSeen);
        }
        IEnumerator AwaitPlaying(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (GameManager.Instance.State != GameState.Playing && Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual(GameState.Playing, GameManager.Instance.State);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator NaturalArrivalPlaysBothRealLinesBeforeReturningControl()
        {
            ResetEvidence();
            yield return IslandEntryAcceptance.EnterFromFerry(EndingOutcome.Peace, false);
            var first = Cue("SPECIMEN 7", Specimen); var second = Cue("ISLAND RADIO", Radio);
            Debug.Log($"[IslandVoiceTest] natural firstDuration={first.Clip.length:F4} firstToRadio={second.RaisedAt - first.RaisedAt:F4} radioDuration={second.Clip.length:F4} radioToControl={Time.realtimeSinceStartup - second.RaisedAt:F4} firstTail={first.MaxSamples}/{first.Clip.samples} radioTail={second.MaxSamples}/{second.Clip.samples}");
            Assert.AreEqual(1, first.Raised); Assert.AreEqual(1, second.Raised);
            Assert.That(second.RaisedAt - first.RaisedAt, Is.GreaterThanOrEqualTo(first.Clip.length + .10f));
            Assert.That(Time.realtimeSinceStartup - second.RaisedAt, Is.GreaterThanOrEqualTo(second.Clip.length + .10f));
            Assert.IsTrue(firstSilentBeforeRadio, "Radio starts after the actual specimen source has naturally ended.");
            AssertFullPlayback(first); AssertFullPlayback(second);
            Assert.IsFalse(Playing(first.Clip) || Playing(second.Clip)); Assert.IsFalse(frozenViolation);
            Assert.IsTrue(RunState.Has(IslandCampaign.ArrivalSeen));
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator ArrivalRequiresReleaseThenSkipsPromptlyAndCancelsSpeech()
        {
            yield return PrepareArrivalReplay();
            GameInput.SimJump = true; IslandArrival.BeginIfNeeded(); yield return null;
            float holdUntil = Time.realtimeSinceStartup + 1.1f;
            while (Time.realtimeSinceStartup < holdUntil)
            {
                Assert.AreEqual(GameState.Ending, GameManager.Instance.State);
                Assert.IsFalse(RunState.Has(IslandCampaign.ArrivalSeen));
                yield return null;
            }
            GameInput.SimJump = false; yield return null; yield return null;
            Assert.AreEqual(GameState.Ending, GameManager.Instance.State);
            float pressedAt = Time.realtimeSinceStartup;
            GameInput.SimJump = true; yield return AwaitPlaying(.5f); GameInput.SimJump = false;
            Assert.That(Time.realtimeSinceStartup - pressedAt, Is.LessThan(.35f), "A new skip after release does not wait for the long clip.");
            Assert.IsTrue(RunState.Has(IslandCampaign.ArrivalSeen));
            yield return new WaitForSecondsRealtime(.2f);
            Assert.IsFalse(Playing(Cue("SPECIMEN 7", Specimen).Clip) || Playing(Cue("ISLAND RADIO", Radio).Clip));
            Assert.AreEqual(0, Cue("ISLAND RADIO", Radio).Raised, "Skip cannot leak a delayed second line.");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator DisabledAndMissingArrivalNarrationKeepFiniteSilentTiming()
        {
            foreach (bool missing in new[] { false, true })
            {
                VoiceOver.Instance.enabled = true;
                yield return PrepareArrivalReplay();
                if (missing)
                {
                    // Override only the runtime lookup boundary, matching a failed Resources load.
                    // The actual shipped files and clips remain intact and are restored in teardown.
                    Suppress("SPECIMEN 7", Specimen); Suppress("ISLAND RADIO", Radio);
                    Assert.IsNull(VoiceOver.ClipFor("SPECIMEN 7", Specimen)); Assert.IsNull(VoiceOver.ClipFor("ISLAND RADIO", Radio));
                }
                else VoiceOver.Instance.enabled = false;
                float began = Time.realtimeSinceStartup; IslandArrival.BeginIfNeeded(); yield return null;
                Assert.AreEqual(GameState.Ending, GameManager.Instance.State);
                yield return AwaitPlaying(5.5f);
                Assert.That(Time.realtimeSinceStartup - began, Is.InRange(3.95f, 4.6f), "Unavailable VO preserves the original four-second arrival.");
                Assert.AreEqual(1, Cue("SPECIMEN 7", Specimen).Raised); Assert.AreEqual(1, Cue("ISLAND RADIO", Radio).Raised);
                Assert.IsFalse(Cue("SPECIMEN 7", Specimen).Played || Cue("ISLAND RADIO", Radio).Played);
                RestoreSuppressed(); VoiceOver.Instance.enabled = true;
            }
        }
        void Suppress(string who, string line)
        {
            if (lookup == null)
            {
                var field = typeof(VoiceOver).GetField("cache", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.IsNotNull(field, "The missing-clip fixture needs the actual VO lookup boundary.");
                lookup = (Dictionary<string, AudioClip>)field.GetValue(null);
            }
            string key = VoiceOver.KeyFor(who, line);
            bool existed = lookup.TryGetValue(key, out var clip); suppressed[key] = (existed, clip); lookup[key] = null;
        }
        void RestoreSuppressed()
        {
            if (lookup != null)
                foreach (var item in suppressed)
                    if (item.Value.existed) lookup[item.Key] = item.Value.clip; else lookup.Remove(item.Key);
            suppressed.Clear();
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PeaceRefugeAndRosterUseSourceCuesPublishedTailsAndFiniteClearing() => RefugeAndRoster(EndingOutcome.Peace);
        [UnityTest, Timeout(180000)]
        public IEnumerator DominionRefugeAndRosterUseSourceCuesPublishedTailsAndFiniteClearing() => RefugeAndRoster(EndingOutcome.Dominion);
        IEnumerator RefugeAndRoster(EndingOutcome outcome)
        {
            yield return IslandEntryAcceptance.EnterFromFerry(outcome, true);
            VoiceOver.StopSpeaking(); yield return new WaitForSecondsRealtime(.2f); ClearInput(); ResetEvidence();
            Assert.IsFalse(RunState.Has(IslandCampaign.InnerRefuge)); Assert.IsFalse(RunState.Has(IslandRoster.Ready));
            Assert.IsFalse(MissionManager.Instance.DialoguePlaying); Assert.IsNotNull(AudioBus.Listener);
            string refuge = outcome == EndingOutcome.Peace ? PeaceRefuge : DominionRefuge;
            string roster = outcome == EndingOutcome.Peace ? PeaceRoster : DominionRoster;
            // Presentation-only prerequisite seeding. The physical entry and both full branches have their own acceptance.
            // SetFlag invokes the actual IslandCampaign.OnFlag -> MissionManager.Say -> source-raised subtitle path.
            MissionManager.SetFlag(IslandCampaign.InnerRefuge);
            yield return AmbientCue(refuge);
            Assert.IsNotNull(IslandTravelers.Instance); Assert.AreEqual(3, IslandTravelers.Instance.Members.Count);
            Assert.IsFalse(RunState.Has(IslandRoster.Ready));
            foreach (var person in IslandTravelers.Instance.Members) MissionManager.SetFlag(IslandRoster.Delivered(person.StableId));
            // Do not set Ready or call Speak/Say here: the real IslandTravelers.Update must publish its own branch cue.
            yield return AmbientCue(roster);
            Assert.IsTrue(RunState.Has(IslandRoster.Ready)); Assert.AreEqual(outcome, RunState.LakesideOutcome);
            Assert.AreEqual(0, Cue("ISLAND RADIO", outcome == EndingOutcome.Peace ? DominionRefuge : PeaceRefuge).Raised);
            Assert.AreEqual(0, Cue("ISLAND RADIO", outcome == EndingOutcome.Peace ? DominionRoster : PeaceRoster).Raised);
            Assert.IsFalse(Playing(Cue("ISLAND RADIO", refuge).Clip) || Playing(Cue("ISLAND RADIO", roster).Clip));
        }
        IEnumerator AmbientCue(string line)
        {
            var cue = Cue("ISLAND RADIO", line);
            float sourceHold = 1.6f + line.Length * .045f;
            float until = Time.realtimeSinceStartup + Mathf.Max(cue.Clip.length, sourceHold) + 2;
            while ((cue.Raised == 0 || cue.ClearedAt < 0 || Playing(cue.Clip)) && Time.realtimeSinceStartup < until)
            {
                Assert.AreEqual(GameState.Playing, GameManager.Instance.State, "Non-blocking refuge/roster speech preserves gameplay control.");
                Assert.That(Time.timeScale, Is.EqualTo(1)); yield return null;
            }
            Assert.AreEqual(1, cue.Raised); Assert.AreEqual("ISLAND RADIO", cue.ActualSpeaker); Assert.AreEqual(line, cue.ActualLine);
            Assert.IsTrue(cue.ObservedCurrentKey, "The published source used the exact source-raised key: " + cue.Key);
            AssertFullPlayback(cue);
            Assert.AreEqual(1, cue.Cleared, "The actual ambient coroutine publishes its finite clear event.");
            Assert.IsFalse(cue.PlayingAtClear, "Subtitle clearing must not cut off the real source tail.");
            Assert.That(cue.ClearedAt - cue.RaisedAt, Is.InRange(cue.Clip.length - .05f, sourceHold + .5f));
            Assert.IsFalse(Playing(cue.Clip));
            Debug.Log($"[IslandVoiceTest] ambient {cue.Key} raised={cue.Raised} cleared={cue.Cleared} clip={cue.Clip.length:F4} clearAfter={cue.ClearedAt-cue.RaisedAt:F4} tail={cue.MaxSamples}/{cue.Clip.samples}");
        }

        [UnityTest, Timeout(180000)] public IEnumerator PeaceDepartureStopsMotionBeforeItsRealLineFinishes() => Departure(EndingOutcome.Peace);
        [UnityTest, Timeout(180000)] public IEnumerator DominionDepartureStopsMotionBeforeItsRealLineFinishes() => Departure(EndingOutcome.Dominion);
        IEnumerator Departure(EndingOutcome outcome)
        {
            yield return IslandEntryAcceptance.EnterFromFerry(outcome, true);
            // Isolate final presentation from the separately tested full escort/courier routes.
            // Actual roster, awake/unoccupied people, deck positions and public departure gate remain mandatory.
            MissionManager.SetFlag(IslandCampaign.InnerRefuge);
            float readyUntil = Time.realtimeSinceStartup + 5;
            while ((!IslandTravelers.Instance || IslandTravelers.Instance.Members.Count != 3) && Time.realtimeSinceStartup < readyUntil) yield return null;
            Assert.IsNotNull(IslandTravelers.Instance); Assert.AreEqual(3, IslandTravelers.Instance.Members.Count);
            foreach (var person in IslandTravelers.Instance.Members)
            {
                int slot = Array.IndexOf(IslandRoster.Ids, person.StableId);
                Assert.That(slot, Is.GreaterThanOrEqualTo(0)); Assert.AreEqual(HostState.Normal, person.Host.State); Assert.IsNull(person.Host.Possessor);
                MissionManager.SetFlag(IslandRoster.Delivered(person.StableId)); MissionManager.SetFlag(IslandRoster.Boarded(person.StableId));
                Assert.IsTrue(person.Host.Nav.Warp(IslandRoster.Board[slot]));
            }
            MissionManager.SetFlag(IslandRoster.Ready); MissionManager.SetFlag(IslandRoster.SignalMounted);
            var pm = PossessionManager.Instance;
            Assert.AreEqual(1, pm.Agents.Count); Assert.IsTrue(pm.Active.IsFree);
            pm.Active.Blob.Place(IslandCampaign.At(34, .55f, 46)); Physics.SyncTransforms();
            SuspicionSystem.Instance.SetValue(0); SuspicionSystem.Instance.OnSuspectGone();
            VoiceOver.StopSpeaking(); yield return new WaitForSecondsRealtime(.2f); ClearInput(); ResetEvidence();
            Assert.IsTrue(IslandExtraction.ReadyToDepart(out string denied), denied);
            var ship = GameObject.Find("Island rescue launch"); Assert.IsNotNull(ship);
            Vector3 start = ship.transform.position; float began = Time.realtimeSinceStartup, lastMotion = -1;
            var cue = Cue("RESCUE RADIO", outcome == EndingOutcome.Peace ? PeaceDeparture : DominionDeparture);
            GameManager.Instance.FinishIsland(); Assert.AreEqual(GameState.Ending, GameManager.Instance.State);
            Vector3 previous = ship.transform.position; int heldFrames = 0; float until = began + 8;
            while (GameManager.Instance.State == GameState.Ending && Time.realtimeSinceStartup < until)
            {
                float elapsed = Time.realtimeSinceStartup - began;
                if (Vector3.Distance(previous, ship.transform.position) > .00001f) lastMotion = elapsed;
                previous = ship.transform.position;
                Assert.AreEqual(0, Time.timeScale);
                if (elapsed > 2.65f && elapsed < cue.Clip.length - .10f)
                {
                    heldFrames++;
                    Assert.That(ship.transform.position.z - start.z, Is.EqualTo(5).Within(.03f), "Vessel stops after exactly its original2.5s of movement.");
                    Assert.IsTrue(Playing(cue.Clip), "Real narration continues while the vessel holds.");
                    Assert.IsFalse(RunState.Has(IslandRoster.Departed));
                }
                yield return null;
            }
            Assert.AreEqual(GameState.Ended, GameManager.Instance.State);
            Assert.That(heldFrames, Is.GreaterThan(0)); Assert.That(lastMotion, Is.InRange(2.35f, 2.60f));
            Assert.That(ship.transform.position.z - start.z, Is.EqualTo(5).Within(.03f));
            Assert.That(Time.realtimeSinceStartup - cue.RaisedAt, Is.GreaterThanOrEqualTo(cue.Clip.length + .10f));
            AssertFullPlayback(cue); Assert.IsFalse(Playing(cue.Clip)); Assert.IsFalse(frozenViolation);
            Assert.IsTrue(RunState.Has(IslandRoster.Departed)); Assert.AreEqual(outcome, RunState.LakesideOutcome);
        }

        [UnityTearDown]
        public IEnumerator RestoreSceneAndAudio()
        {
            recording = false;
            if (observer != null && audioRoot) audioRoot.StopCoroutine(observer);
            SceneManager.sceneLoaded -= OnSceneLoaded; GameEvents.Subtitle -= OnSubtitle;
            RestoreSuppressed();
            if (VoiceOver.Instance) VoiceOver.Instance.enabled = true;
            VoiceOver.StopSpeaking();
            if (voiceSources != null) foreach (var source in voiceSources) if (source) source.Stop();
            ClearInput(); GameInput.Simulated = false; Time.timeScale = 1; RunState.NewGame();
            SceneManager.LoadScene(GameManager.MenuScene); yield return null;
            Sfx.VoiceVolume = oldVoice; AudioListener.volume = oldMaster; AudioListener.pause = oldPause; Time.timeScale = oldScale;
            if (VoiceOver.Instance) VoiceOver.Instance.enabled = oldVoiceEnabled;
        }
    }
}
