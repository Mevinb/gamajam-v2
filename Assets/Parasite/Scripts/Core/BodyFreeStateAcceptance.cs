using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Parasite.Missions;
using Parasite.Possession;
using Parasite.UI;
using Parasite.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Parasite.Testing
{
    // Shared prepared radio/deck fixture; this does not earn the earlier riverside chapter.
    // NUnit and exact opt-in native96 execute the same actual-body/readiness/recovery predicates.
    public static class BodyFreeStateAcceptance
    {
        static void Check(bool condition, string reason)
        {
            if (!condition) throw new InvalidOperationException(reason);
        }
        static void ClearInput()
        {
            GameInput.SimMove = Vector2.zero;
            GameInput.SimInteract = GameInput.SimLeave = GameInput.SimSplit = GameInput.SimSwitch = GameInput.SimJump = GameInput.SimSprint = GameInput.SimAbility = false;
        }
        static IEnumerator WaitFor(Func<bool> condition, float seconds, string reason)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Check(condition(), reason);
        }
        static InteractContext Context(PossessionManager pm) => new()
        { agent = pm.Active, host = pm.Active.Host, permissions = AccessPermission.None, position = pm.Active.Position };

        public static IEnumerator DestroyedInactiveBodyCannotAuthorizeTheFerryOrFinalization(Action<string> observer = null)
        {
            // Declared slice fixture: earlier river radio work and a real ferry-deck checkpoint precede fresh-scene creation.
            // Neither departure nor body loss/recovery is granted; no body is warped, no guard is parked/stunned or calmed.
            RunState.NewGame(); Time.timeScale = 1; GameInput.Simulated = true; ClearInput();
            Check(RunState.RecordLakesideOutcome(EndingOutcome.Peace), "Required: RunState.RecordLakesideOutcome(EndingOutcome.Peace) Is.True");
            RunState.Flags.Add(MissionFlags.Escaped); Check(RunState.BeginAfterlake(), "Required: RunState.BeginAfterlake() Is.True");
            foreach (var flag in new[] { AfterlakeCampaign.Signal, AfterlakeCampaign.Floodgate, AfterlakeCampaign.WaterAnalysed,
                AfterlakeCampaign.CorridorOpen, AfterlakeCampaign.ChannelOpened }) RunState.Flags.Add(flag);
            RunState.MissionIndex = 16;
            RunState.CheckpointPosition = new Vector3(269.1f, .55f, -26.8f); RunState.CheckpointName = "Prepared river ferry body-readiness slice";
            SceneManager.LoadScene(GameManager.CityScene); yield return null;
            yield return WaitFor(() => CityDirector.Instance && CityDirector.Instance.Ready && GameManager.Instance && GameManager.Instance.IsPlaying
                && PossessionManager.Instance && PossessionManager.Instance.Active != null, 30, "The actual river world/navigation/player is ready.");
            yield return null; yield return null;
            GameInput.Simulated = true; ClearInput(); // CityDirector resets the isolated driver on every scene load.
            var manager = GameManager.Instance; var pm = PossessionManager.Instance; var active = pm.Active; var activeBlob = active.Blob;
            var ferry = Object.FindFirstObjectByType<AfterlakeDeparture>(); Check(ferry != null, "Required: ferry Is.Not.Null");
            Check(pm.Agents.Count == 1, "Required: pm.Agents.Count Is.EqualTo(1)"); Check(pm.TrySplit(), "Normal V logic creates the actual second body.");
            yield return WaitFor(() => pm.Agents.Count == 2 && pm.Agents.All(a => a.Blob && a.Blob.Controller.enabled && a.Blob.Controller.isGrounded),
                2, "Both actual free capsules settle on physical ferry footing before the destruction fixture.");
            var inactive = pm.Agents.Single(a => !ReferenceEquals(a, active)); var inactiveBlob = inactive.Blob; IControllable retainedBody = inactive.Body;
            Check(ReferenceEquals(retainedBody, inactiveBlob), "Required: ReferenceEquals(retainedBody, inactiveBlob) Is.True");
            Check(Physics.Raycast(active.Position + Vector3.up * .5f, Vector3.down, out _, 2, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore), "Required: Physics.Raycast(active.Position + Vector3.up * .5f, Vector3.down, out _, 2, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore) Is.True");
            Check(Vector3.Distance(active.Position, ferry.InteractPoint) < ferry.InteractRange, "The surviving actor is physically in reach.");
            Check(AfterlakeDeparture.AllAmoebaeFree(out var beforeReason), beforeReason);
            Check(ferry.CanInteract(Context(pm), out beforeReason), beforeReason);
            int count = pm.Agents.Count, resets = RunState.Resets, captures = RunState.Captures, hosts = RunState.HostsPossessed, mission = RunState.MissionIndex;
            var flags = new HashSet<string>(RunState.Flags);
            Check(!(RunState.Has(AfterlakeCampaign.Departed)), "Required: RunState.Has(AfterlakeCampaign.Departed) Is.False");

            Object.Destroy(inactiveBlob.gameObject); yield return null; yield return null;
            Check(inactiveBlob == null, "Required: inactiveBlob == null Is.True"); Check(!(ReferenceEquals(retainedBody, null)), "Required: ReferenceEquals(retainedBody, null) Is.False");
            Check(ReferenceEquals(inactive.Body, null), "The existing lifetime guard rejects the actual destroyed Unity body.");
            bool freeAfterLoss = inactive.IsFree;
            bool allFreeAfterLoss = AfterlakeDeparture.AllAmoebaeFree(out var allFreeReason);
            bool ferryAfterLoss = ferry.CanInteract(Context(pm), out var ferryReason);
            manager.FinishAfterlake(); // Test the production defensive finalizer BEFORE assertions, exposing actual wrong credit on the old source.
            string observation = "[BodyFreeState] actualInactiveDestroyed=true registry=" + pm.Agents.Count + " deadIsFree=" + freeAfterLoss
                + " allFree=" + allFreeAfterLoss + " allFreeReason=" + allFreeReason + " ferryAllowed=" + ferryAfterLoss + " ferryReason=" + ferryReason
                + " afterFinalizerState=" + manager.State + " departed=" + RunState.Has(AfterlakeCampaign.Departed) + " missionIndex=" + RunState.MissionIndex;
            observer?.Invoke(observation); // Caller owns logging/expected-log guards; the predicates are shared.
            Check(manager.IsPlaying && !RunState.Has(AfterlakeCampaign.Departed) && RunState.MissionIndex == mission && flags.SetEquals(RunState.Flags), "Actual inactive-body destruction/finalization must not grant credit: deadIsFree=" + freeAfterLoss + " allFree=" + allFreeAfterLoss
                    + " ferryAllowed=" + ferryAfterLoss + " state=" + manager.State + " departed=" + RunState.Has(AfterlakeCampaign.Departed)
                    + " missionIndex=" + RunState.MissionIndex);
            Check(!(freeAfterLoss), "A destroyed inactive body cannot remain a free amoeba.");
            Check(!(allFreeAfterLoss), "A stale registry entry cannot satisfy all-free readiness.");
            Check(!(ferryAfterLoss), "The same actual ferry must reject the missing owned body.");
            Check(pm.Agents.Count == count, "Required: pm.Agents.Count Is.EqualTo(count)"); Check(pm.Agents.Contains(inactive), "Required: pm.Agents.Contains(inactive) Is.True");
            Check(ReferenceEquals(pm.Active, active) && ReferenceEquals(pm.ActiveBody, activeBlob), "Required: ReferenceEquals(pm.Active, active) && ReferenceEquals(pm.ActiveBody, activeBlob) Is.True");
            Check(RunState.Resets == resets && RunState.Captures == captures && RunState.HostsPossessed == hosts, "Required: RunState.Resets == resets && RunState.Captures == captures && RunState.HostsPossessed == hosts Is.True");

            manager.SetPaused(true);
            Check(manager.State == GameState.Paused && HUDController.Instance, "The actual pause menu is available before requesting recovery.");
            var pause = HUDController.Instance.transform.Find("HUD Canvas/Pause");
            Check(pause, "The actual HUD owns its pause menu.");
            var restart = pause.GetComponentsInChildren<Button>(true).Single(b => b.name == "Btn_Restart checkpoint");
            var pauseGroup = pause.GetComponent<CanvasGroup>();
            yield return WaitFor(() => pause.gameObject.activeInHierarchy && pauseGroup && pauseGroup.alpha >= .99f && restart.IsInteractable(),
                2, "The real checkpoint action is visible and interactable in the pause menu.");
            restart.onClick.Invoke(); // Actual player-facing action; it calls the ordinary checkpoint reset, not NewGame.
            Check(manager.State == GameState.Resetting, "Required: manager.State Is.EqualTo(GameState.Resetting)");
            yield return WaitFor(() => GameManager.Instance && GameManager.Instance != manager && CityDirector.Instance && CityDirector.Instance.Ready,
                30, "Only explicit normal checkpoint recovery replaces the damaged registry.");
            yield return null; yield return null;
            GameInput.Simulated = true; ClearInput();
            var restored = PossessionManager.Instance;
            Check(restored && restored != pm && restored.Agents.Count == 1 && restored.Active != null, "Required: restored && restored != pm && restored.Agents.Count == 1 && restored.Active != null Is.True");
            Check(!(restored.Agents.Contains(inactive) || restored.Agents.Contains(active)), "Required: restored.Agents.Contains(inactive) || restored.Agents.Contains(active) Is.False");
            Check(restored.Active.IsFree && restored.Active.Blob && restored.Active.Blob.Controller.enabled, "Required: restored.Active.IsFree && restored.Active.Blob && restored.Active.Blob.Controller.enabled Is.True");
            Check(RunState.Resets == resets + 1, "Required: RunState.Resets Is.EqualTo(resets + 1)");
            Check(AfterlakeDeparture.AllAmoebaeFree(out var restoredReason), restoredReason);
            ferry = Object.FindFirstObjectByType<AfterlakeDeparture>(); Check(ferry != null, "Required: ferry Is.Not.Null");
            yield return WaitFor(() => restored.Active.Blob.Controller.isGrounded, 2, "The new actual free body settles at the restored ferry checkpoint.");
            Check(Vector3.Distance(restored.Active.Position, ferry.InteractPoint) < ferry.InteractRange, "Required: Vector3.Distance(restored.Active.Position, ferry.InteractPoint) Is.LessThan(ferry.InteractRange)");
            Check(ferry.CanInteract(Context(restored), out restoredReason), restoredReason);
            float aimUntil = Time.realtimeSinceStartup + .35f;
            while (Time.realtimeSinceStartup < aimUntil)
            {
                Vector3 look = ferry.InteractPoint - PlayerCamera.Instance.Position;
                PlayerCamera.Instance.SetYaw(Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg);
                PlayerCamera.Instance.SetPitch(-Mathf.Atan2(look.y, new Vector2(look.x, look.z).magnitude) * Mathf.Rad2Deg);
                yield return null;
            }
            var target = restored.ComputeTarget();
            Check(target.kind == TargetKind.Interact && ReferenceEquals(target.interactable, ferry) && target.allowed, "Fresh ordinary E targets the restored real ferry.");
            Check(GameInput.Simulated && !restored.InputLocked, "The isolated driver is active after the normal scene reload.");
            GameInput.SimInteract = true; yield return null; GameInput.SimInteract = false; yield return null;
            Check(GameManager.Instance.State == GameState.Ended && RunState.Has(AfterlakeCampaign.Departed) && RunState.MissionIndex == 17, "A real new free body and fresh E can earn departure after explicit recovery.");
            Check(RunState.LakesideOutcome == EndingOutcome.Peace, "Required: RunState.LakesideOutcome Is.EqualTo(EndingOutcome.Peace)");
        }

        public static void LiveHiddenAndDisabledBlobRemainsFreeWithoutChangingIdentity()
        {
            var agent = new AmoebaAgent { Id = 92 }; AmoebaController blob = null;
            try
            {
                blob = AmoebaController.Create(new Vector3(20000, 5000, 20000), agent); IControllable body = blob;
                Check(agent.IsFree && ReferenceEquals(agent.Body, body), "Required: agent.IsFree && ReferenceEquals(agent.Body, body) Is.True");
                blob.enabled = false; blob.Controller.enabled = false; blob.gameObject.SetActive(false);
                Check(agent.IsFree && ReferenceEquals(agent.Body, body), "Live disabled/hidden bodies retain their free identity.");
                agent.InTransit = true; Check(!(agent.IsFree), "Required: agent.IsFree Is.False"); agent.InTransit = false;
                Check(agent.IsFree && ReferenceEquals(agent.Body, body), "Required: agent.IsFree && ReferenceEquals(agent.Body, body) Is.True");
            }
            finally { if (blob) { blob.gameObject.SetActive(false); Object.DestroyImmediate(blob.gameObject); } Physics.SyncTransforms(); }
        }

    }
}
