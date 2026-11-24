using System.Collections;
using System.Linq;
using Parasite.Characters;
using Parasite.Missions;
using Parasite.Possession;
using Parasite.Stealth;
using Parasite.UI;
using Parasite.World;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace Parasite.Testing
{
    /// <summary>Shared Editor and real-player acceptance for the chapter beyond the explicit lakeside choice.</summary>
    public static class AfterlakeAcceptance
    {
        static PossessionManager PM => PossessionManager.Instance;
        public static IEnumerator RunChapter(EndingOutcome outcome, System.Func<string, IEnumerator> capture = null)
        {
            yield return AutoTest.LoadCity();
            GameInput.Simulated = false;
            var gm = GameManager.Instance;
            Require.IsFalse(gm.ContinueAfterLake(), "Gameplay alone cannot bypass the lake choice.");
            gm.FinishEnding(new EndingData { route = EntryRoute.MainGate });
            yield return null;
            var lake = LakesideEpilogue.Instance;
            Require.IsNotNull(lake);
            lake.RevealChoices();
            yield return new WaitForSecondsRealtime(0.45f);
            Require.IsTrue(lake.ChoicesReady);
            var choice = lake.GetComponentsInChildren<Button>(true).Single(b => b.name == (outcome == EndingOutcome.Peace ? "Btn_Live quietly" : "Btn_Become the world"));
            Require.IsTrue(choice.IsInteractable()); choice.onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.9f);
            lake.FinishResolution();
            Require.AreEqual(GameState.Ended, gm.State);
            var banner = HUDController.Instance.GetComponentsInChildren<CanvasGroup>(true).Single(group => group.name == "MissionBanner");
            Require.AreEqual(0f, banner.alpha, "An active mission card must be cleared before the ending summary fades in.");
            Require.AreEqual(outcome, RunState.LakesideOutcome);
            Require.IsFalse(RunState.AfterlakeStarted, "Resolving the lake does not automatically choose to continue.");
            var button = HUDController.Instance.GetComponentsInChildren<Button>(true).Single(b => b.name == "Btn_Continue · After the lake");
            yield return new WaitForSecondsRealtime(0.85f);
            if (capture != null) yield return capture("afterlake-" + outcome + "-continue-summary.png");
            Require.IsTrue(button.IsInteractable()); button.onClick.Invoke();
            yield return WaitForCity();
            Require.IsTrue(RunState.AfterlakeStarted);
            Require.AreEqual(outcome, RunState.LakesideOutcome);
            Require.AreEqual(17, MissionManager.Instance.Missions.Count);
            Require.AreEqual(14, RunState.MissionIndex);
            Require.AreEqual(GameState.Playing, GameManager.Instance.State);
            Require.IsNull(LakesideEpilogue.Instance, "The cinematic set is released when entering the playable chapter.");
            Require.IsTrue(Vector3.Distance(PM.Active.Position, AfterlakeCampaign.Spawn) < 1, "Continuation starts at the riverside landing.");
            Require.IsFalse(MissionManager.Instance.TryPresentationEscape(), "The city shortcut cannot teleport the annex back to an old ending.");
            GameManager.Instance.SetPaused(true);
            PresentationShortcut.Request();
            yield return null; yield return null;
            Require.IsFalse(PresentationShortcut.IsPending);
            Require.AreEqual(GameState.Paused, GameManager.Instance.State, "Ignored presentation input must not unpause a later chapter.");
            GameManager.Instance.SetPaused(false);
            Require.IsNotNull(GameObject.Find(AfterlakeDistrict.RootName));
            if (capture != null) yield return capture("afterlake-" + outcome + "-landing.png");
            foreach (var p in new[] { AfterlakeCampaign.Receiver, AfterlakeCampaign.FloodgatePanel, AfterlakeCampaign.Analysis, AfterlakeCampaign.Dispatch, AfterlakeCampaign.Records, AfterlakeCampaign.RadioPower, AfterlakeCampaign.Radio, AfterlakeCampaign.Ferry })
            {
                Require.IsTrue(NavMesh.SamplePosition(p + Vector3.back * 1.3f, out var hit, 2, NavMesh.AllAreas), "Navigable ground at " + p);
                var path = new NavMeshPath();
                Require.IsTrue(NavMesh.CalculatePath(AfterlakeCampaign.Spawn, hit.position, NavMesh.AllAreas, path));
                Require.AreEqual(NavMeshPathStatus.PathComplete, path.status, "Every objective is physically reachable from the landing.");
            }
            GameInput.Simulated = true; Time.timeScale = 3;
            Take(Profession.Civilian);
            var receiver = Object.FindObjectsByType<MissionProp>(FindObjectsSortMode.None).Single(p => p.flag == AfterlakeCampaign.Signal);
            yield return Use(receiver, receiver.transform.position);
            yield return WaitForFlag(AfterlakeCampaign.Signal, 5);
            Require.IsTrue(RunState.Has(AfterlakeCampaign.Signal));
            var floodgate = Operation(AfterlakeCampaign.Floodgate);
            var ctx = Context();
            Require.IsFalse(floodgate.CanInteract(ctx, out _), "A civilian cannot operate the floodgate controls.");
            Take(Profession.Maintenance);
            // The actual hold is cancelled by pursuit and must be started again.
            yield return Use(floodgate, floodgate.transform.position);
            Require.IsTrue(floodgate.Busy);
            SuspicionSystem.Instance.SetValue(100);
            yield return null; yield return null;
            Require.IsFalse(floodgate.Busy);
            Require.IsFalse(RunState.Has(AfterlakeCampaign.Floodgate));
            Calm();
            yield return Finish(floodgate);
            Require.AreEqual(15, RunState.MissionIndex);
            Take(Profession.Scientist);
            yield return Finish(Operation(AfterlakeCampaign.WaterAnalysed));
            Require.IsTrue(PM.TrySplit());
            PM.SwitchActive(); Take(Profession.Guard);
            var dispatch = CooperativeConsole.Find(AfterlakeCampaign.CorridorControl);
            yield return Use(dispatch, dispatch.transform.position);
            Require.IsTrue(dispatch.Held);
            // A real checkpoint reset must retain evidence/choice but discard temporary console ownership.
            GameManager.Instance.ResetToCheckpoint("Afterlake checkpoint regression");
            yield return new WaitForSecondsRealtime(2.4f);
            yield return WaitForCity();
            Require.IsTrue(RunState.Has(AfterlakeCampaign.WaterAnalysed));
            Require.AreEqual(outcome, RunState.LakesideOutcome);
            Require.AreEqual(15, RunState.MissionIndex);
            Require.IsFalse(CooperativeConsole.Find(AfterlakeCampaign.CorridorControl).Held);
            Require.AreEqual(1, PM.Agents.Count);
            GameInput.Simulated = true; Time.timeScale = 3;
            Take(Profession.Guard);
            dispatch = CooperativeConsole.Find(AfterlakeCampaign.CorridorControl);
            yield return Use(dispatch, dispatch.transform.position);
            Require.IsTrue(PM.TrySplit()); PM.SwitchActive(); Take(Profession.Scientist);
            var records = Operation(AfterlakeCampaign.CorridorOpen);
            yield return Finish(records);
            Require.AreEqual(16, RunState.MissionIndex);
            PM.SwitchActive(); Take(Profession.Maintenance);
            var power = CooperativeConsole.Find(AfterlakeCampaign.RadioControl);
            yield return Use(power, power.transform.position);
            PM.SwitchActive(); Take(Profession.Scientist);
            var radio = Operation(AfterlakeCampaign.ChannelOpened);
            Require.IsTrue((outcome == EndingOutcome.Peace ? new[] { Profession.Civilian, Profession.Scientist } : new[] { Profession.Guard, Profession.Scientist }).OrderBy(p => p).SequenceEqual(radio.allowedProfessions.OrderBy(p => p)), "The relay accepts the roles promised by the chosen future.");
            yield return Finish(radio);
            Require.AreEqual(outcome, RunState.LakesideOutcome);
            Require.IsFalse(RunState.RecordLakesideOutcome(outcome == EndingOutcome.Peace ? EndingOutcome.Dominion : EndingOutcome.Peace));
            var ferry = Object.FindFirstObjectByType<AfterlakeDeparture>();
            ctx = Context(); Require.IsFalse(ferry.CanInteract(ctx, out _), "A human host cannot board through the intake.");
            Require.IsTrue(PM.LeaveHost()); Calm();
            yield return Use(ferry, ferry.transform.position);
            Require.AreEqual(GameState.Ended, GameManager.Instance.State);
            Require.IsTrue(RunState.Has(AfterlakeCampaign.Departed));
            Require.AreEqual(17, RunState.MissionIndex);
            Require.AreEqual(outcome, RunState.LakesideOutcome);
            Require.IsFalse(GameManager.Instance.ContinueAfterLake(), "The completed continuation cannot reset its own checkpoint.");
            Require.IsFalse(HUDController.Instance.GetComponentsInChildren<Button>(true).Single(b => b.name == "Btn_Continue · After the lake").gameObject.activeSelf);
            yield return new WaitForSecondsRealtime(0.85f);
            if (capture != null) yield return capture("afterlake-" + outcome + "-final-summary.png");
            Debug.Log("[Afterlake] PASS " + outcome + " · lake choice → summary Continue → three missions → checkpoint recovery → ferry → branch summary");
        }

        static CampaignOperation Operation(string flag) => Object.FindObjectsByType<CampaignOperation>(FindObjectsSortMode.None).Single(o => o.flag == flag);
        static InteractContext Context() => new() { agent = PM.Active, host = PM.Active.Host, permissions = PM.Active.Host ? PM.Active.Host.Permissions : AccessPermission.None, position = PM.Active.Position };
        static void Calm() { SuspicionSystem.Instance.SetValue(0); SuspicionSystem.Instance.OnSuspectGone(); }

        static void Take(Profession profession)
        {
            if (PM.Active.Host && PM.Active.Host.Profession == profession) return;
            var host = HostCharacter.All.Where(h => h && h.CanBePossessed && h.Profession == profession && h.transform.position.x > 180).OrderBy(h => Vector3.Distance(h.transform.position, PM.Active.Position)).FirstOrDefault();
            Require.IsNotNull(host, "A riverside " + profession + " host is available.");
            PM.ForcePossessImmediate(host); Require.AreSame(host, PM.Active.Host);
        }

        static IEnumerator WaitForCity()
        {
            yield return null;
            float deadline = Time.realtimeSinceStartup + 30;
            while ((!CityDirector.Instance || !CityDirector.Instance.Ready) && Time.realtimeSinceStartup < deadline) yield return null;
            Require.IsTrue(CityDirector.Instance && CityDirector.Instance.Ready);
            yield return null; yield return null;
        }

        static IEnumerator Use(IInteractable target, Vector3 ground)
        {
            var stand = ground + Vector3.back * 1.35f;
            Require.IsTrue(NavMesh.SamplePosition(stand, out var hit, 1.5f, NavMesh.AllAreas));
            var body = PM.ActiveBody;
            if (body is HostCharacter host)
            {
                host.Controller.enabled = false; host.transform.SetPositionAndRotation(hit.position, Quaternion.identity); host.Controller.enabled = true;
            }
            else if (body is AmoebaController blob) blob.Place(hit.position);
            Physics.SyncTransforms(); Calm();
            for (float t = 0; t < 0.45f; t += Time.unscaledDeltaTime) { PlayerCamera.Instance.SetYaw(0); yield return null; }
            var aimed = PM.ComputeTarget();
            Require.AreEqual(TargetKind.Interact, aimed.kind, aimed.prompt);
            Require.AreSame(target, aimed.interactable, "Real E targeting picks " + target.GetPrompt(Context()));
            Require.IsTrue(aimed.allowed, aimed.prompt);
            GameInput.SimInteract = true; yield return null; GameInput.SimInteract = false; yield return null;
        }

        static IEnumerator WaitForFlag(string flag, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!RunState.Has(flag) && Time.realtimeSinceStartup < deadline) { Calm(); yield return null; }
            Require.IsTrue(RunState.Has(flag), flag + " completed through the actual E interaction/hold.");
        }

        static IEnumerator Finish(CampaignOperation operation)
        {
            yield return Use(operation, operation.transform.position);
            yield return WaitForFlag(operation.flag, operation.seconds + 3);
        }

        static class Require
        {
            public static void IsTrue(bool condition, string message = "Expected true") { if (!condition) throw new System.InvalidOperationException(message); }
            public static void IsFalse(bool condition, string message = "Expected false") => IsTrue(!condition, message);
            public static void IsNotNull(object value, string message = "Expected an existing scene object") => IsTrue(value is Object item ? item : value != null, message);
            public static void IsNull(object value, string message = "Expected no scene object") => IsTrue(value is Object item ? !item : value == null, message);
            public static void AreEqual<T>(T expected, T actual, string message = "") => IsTrue(System.Collections.Generic.EqualityComparer<T>.Default.Equals(expected, actual), message + " Expected " + expected + ", actual " + actual);
            public static void AreSame(object expected, object actual, string message = "Expected the same scene object") => IsTrue(ReferenceEquals(expected, actual), message);
        }
    }
}
