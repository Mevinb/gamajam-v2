using System;
using System.Collections;
using NUnit.Framework;
using Parasite.Missions;
using Parasite.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Parasite.Tests
{
    /// <summary>Prepared Harbor presentation fixture only. These tests do not exercise or grant any Harbor mission completion.</summary>
    public sealed class HarborArrivalLifecycleTests
    {
        bool previousSimulated;
        [UnitySetUp] public IEnumerator PreparePresentationOnly()
        {
            previousSimulated = GameInput.Simulated; ClearInput(); Time.timeScale = 1;
            RunState.NewGame(); RunState.RecordLakesideOutcome(EndingOutcome.Peace);
            // Earlier chapters are a fixture. ArrivalSeen skips the automatic intro so the normal factory can be controlled below.
            foreach (string flag in new[] { MissionFlags.Escaped, AfterlakeCampaign.Started, AfterlakeCampaign.Departed,
                IslandCampaign.Started, IslandRoster.Departed, HarborCampaign.Started, HarborCampaign.ArrivalSeen }) RunState.Flags.Add(flag);
            RunState.MissionIndex = HarborCampaign.FirstMissionIndex; RunState.HasCheckpoint = true;
            RunState.CheckpointPosition = HarborLayout.Spawn; RunState.CheckpointName = "Harbor arrival lifecycle fixture";
            SceneManager.LoadScene(GameManager.CityScene); yield return null;
            yield return Wait(() => CityDirector.Instance && CityDirector.Instance.Ready && GameManager.Instance && GameManager.Instance.IsPlaying, 15, "Prepared Harbor world becomes playable");
            yield return null; yield return null;
            GameInput.Simulated = true;
            Assert.That(HarborArrival.Instance, Is.Null, "The already-seen fixture skips automatic arrival creation.");
            AssertNoHarborCompletion();
            Assert.That(RunState.Flags.Remove(HarborCampaign.ArrivalSeen), Is.True);
            Debug.Log("[HarborArrivalLifecycle] FIXTURE prior chapters and already-seen presentation prepared; no Harbor completion flags granted.");
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            ClearInput(); Time.timeScale = 1; RunState.NewGame();
            SceneManager.LoadScene(GameManager.MenuScene); yield return null;
            GameInput.Simulated = previousSimulated;
        }
        [UnityTest, Timeout(45000)] public IEnumerator DisablingRunningArrivalCancelsEveryLaterPresentationEffect()
        {
            bool observing = false; int lateCaptions = 0, lateBanners = 0;
            void Caption(string who, string line) { if (observing) lateCaptions++; }
            void Banner(string title, string detail) { if (observing) lateBanners++; }
            GameEvents.Subtitle += Caption; MissionManager.Banner += Banner;
            try
            {
                HarborArrival.BeginIfNeeded(); var owner = HarborArrival.Instance;
                Assert.That(owner, Is.Not.Null);
                yield return Wait(() => owner && owner.Running && GameManager.Instance.State == GameState.Ending, 2, "Normal factory begins its protected arrival");
                Assert.That(Time.timeScale, Is.Zero);
                double disabledAt = Time.realtimeSinceStartupAsDouble;
                owner.enabled = false; observing = true;
                Assert.That(GameManager.Instance.IsPlaying && Time.timeScale == 1f && !RunState.Has(HarborCampaign.ArrivalSeen), Is.True,
                    "Cancellation immediately releases the captured presentation without completing ArrivalSeen.");
                // Keep the original full window: a coroutine left alive by component disable must reveal its later cue and flag.
                yield return new WaitForSecondsRealtime(HarborArrival.PresentationSeconds + .35f);
                Debug.Log($"[HarborArrivalLifecycle] disabled-running elapsed={Time.realtimeSinceStartupAsDouble-disabledAt:F3} lateCaptions={lateCaptions} lateBanners={lateBanners} seen={RunState.Has(HarborCampaign.ArrivalSeen)} state={GameManager.Instance.State} timeScale={Time.timeScale:R}");
                Assert.That(lateCaptions == 0 && lateBanners == 0 && !RunState.Has(HarborCampaign.ArrivalSeen) && GameManager.Instance.IsPlaying && Time.timeScale == 1f, Is.True,
                    "Cancellation must preserve all strict outcomes: no later cue, no banner, no ArrivalSeen, Playing and timeScale1. " +
                    $"Observed captions={lateCaptions}, banners={lateBanners}, seen={RunState.Has(HarborCampaign.ArrivalSeen)}, state={GameManager.Instance.State}, timeScale={Time.timeScale:R}");
                AssertNoHarborCompletion();
            }
            finally { GameEvents.Subtitle -= Caption; MissionManager.Banner -= Banner; }
        }
        [UnityTest, Timeout(30000)] public IEnumerator DisableAndReenableBeforeStartCannotReacquirePresentation()
        {
            HarborArrival.BeginIfNeeded(); var first = HarborArrival.Instance;
            Assert.That(first, Is.Not.Null); Assert.That(first.Running, Is.False, "The factory has not yielded to Start yet.");
            first.enabled = false;
            if (first) first.enabled = true; // A correctly retired owner may already be scheduled for destruction.
            bool acquiredEnding = false; double until = Time.realtimeSinceStartupAsDouble + 1.1;
            while (Time.realtimeSinceStartupAsDouble < until)
            {
                yield return null;
                if (GameManager.Instance.State == GameState.Ending || Time.timeScale != 1f) acquiredEnding = true;
            }
            Debug.Log($"[HarborArrivalLifecycle] pre-start-reenable reacquired={acquiredEnding} state={GameManager.Instance.State} timeScale={Time.timeScale:R} seen={RunState.Has(HarborCampaign.ArrivalSeen)}");
            Assert.That(!acquiredEnding && GameManager.Instance.IsPlaying && Time.timeScale == 1f && !RunState.Has(HarborCampaign.ArrivalSeen), Is.True,
                "Pre-Start cancellation cannot reacquire presentation or leave gameplay stuck; it must remain Playing/timeScale1 with no ArrivalSeen. " +
                $"Observed reacquired={acquiredEnding}, state={GameManager.Instance.State}, timeScale={Time.timeScale:R}, seen={RunState.Has(HarborCampaign.ArrivalSeen)}");
            // The retired singleton must not permanently block another normal factory request.
            HarborArrival.BeginIfNeeded(); var replacement = HarborArrival.Instance;
            Assert.That(replacement, Is.Not.Null); Assert.That(ReferenceEquals(replacement, first), Is.False);
            yield return Wait(() => replacement && replacement.Running, 2, "A fresh factory owner can acquire the released context");
            replacement.enabled = false; yield return null;
            Assert.That(GameManager.Instance.IsPlaying, Is.True); Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(RunState.Has(HarborCampaign.ArrivalSeen), Is.False); AssertNoHarborCompletion();
        }
        static IEnumerator Wait(Func<bool> condition, float seconds, string message)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!condition() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
        }
        static void AssertNoHarborCompletion()
        {
            Assert.That(RunState.Has(HarborCampaign.CanalOpen), Is.False);
            Assert.That(RunState.Has(HarborCampaign.ManifestComplete), Is.False);
            Assert.That(RunState.Has(HarborCampaign.RelayComplete), Is.False);
        }
        static void ClearInput()
        {
            GameInput.Simulated = false; GameInput.SimMove = Vector2.zero;
            GameInput.SimSprint = GameInput.SimJump = GameInput.SimInteract = GameInput.SimLeave = GameInput.SimSplit = GameInput.SimSwitch = GameInput.SimAbility = false;
        }
    }
}
