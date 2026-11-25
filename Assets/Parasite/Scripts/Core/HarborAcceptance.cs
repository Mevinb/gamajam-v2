using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Parasite.Characters;
using Parasite.Missions;
using Parasite.Possession;
using Parasite.Stealth;
using Parasite.UI;
using Parasite.World;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Parasite.Testing
{
    /// <summary>
    /// Actual-input harbor acceptance, shared by Editor tests and opt-in native scenarios.
    /// The earlier twenty-mission summary and initial role ownership are prepared fixtures.
    /// No harbor completion flag, crate position, player position or collision rule is granted.
    /// </summary>
    public static class HarborAcceptance
    {
        static PossessionManager PM => PossessionManager.Instance;
        static PlayerCamera Camera => PlayerCamera.Instance;

        public static IEnumerator ContinuationRules()
        {
            yield return AutoTest.LoadCity();
            Check(!GameManager.Instance.ContinueHarbor() && !RunState.HarborStarted, "Ordinary city play cannot continue the harbor.");
            foreach (string missing in new[] { "outcome", MissionFlags.Escaped, AfterlakeCampaign.Started, IslandCampaign.Started, IslandRoster.Departed })
            {
                RunState.NewGame();
                SeedPriorChapter(EndingOutcome.Peace, missing);
                Check(!RunState.BeginHarbor() && !RunState.HarborStarted, "Harbor continuation rejects missing prior prerequisite: " + missing);
            }
            RunState.NewGame(); SeedPriorChapter(EndingOutcome.Peace);
            Check(!GameManager.Instance.ContinueHarbor() && !RunState.HarborStarted, "Even complete prior flags cannot skip the explicit completed summary.");
            yield return EnterFromIsland(EndingOutcome.Peace);
            Check(!RunState.BeginHarbor() && !GameManager.Instance.ContinueHarbor(), "Continuation is one-shot and cannot reset the harbor checkpoint.");
            Check(!GameManager.Instance.FinishHarbor(), "Direct finalization cannot skip the physical harbor relay.");
            Check(!RunState.RecordLakesideOutcome(EndingOutcome.Dominion), "The first lake outcome remains immutable.");
            Debug.Log("[Harbor] PASS continuation prerequisites · explicit prepared M20 summary → real Continue → mission21");
        }

        public static IEnumerator PressureOverflowPauseAndRecovery()
        {
            yield return EnterFromIsland(EndingOutcome.Peace);
            yield return Pressure(true);
            Debug.Log("[Harbor] PASS pressure · role denial → A+B overflow → C+A+B retry → pause → checkpoint → physical open gate");
        }

        public static IEnumerator RunChapter(EndingOutcome outcome, Func<string, IEnumerator> capture = null, bool continueCurrentSummary = false)
        {
            yield return EnterFromIsland(outcome, continueCurrentSummary);
            if (capture != null) yield return capture("harbor-" + outcome + "-landing.png");
            yield return Pressure(false);
            yield return Cargo(outcome, true);
            if (capture != null) yield return capture("harbor-" + outcome + "-delivered.png");
            yield return Relay(outcome, true);
            float deadline = Time.realtimeSinceStartup + 7;
            while (GameManager.Instance.State != GameState.Ended && Time.realtimeSinceStartup < deadline) yield return null;
            Check(GameManager.Instance.State == GameState.Ended, "The actual three-second relay leads through its finite resolution to the summary.");
            Check(RunState.MissionIndex == 23 && MissionManager.Instance.Missions.Count == 23, "Only the implemented twenty-three missions are advertised and completed.");
            Check(RunState.LakesideOutcome == outcome && PM.Agents.Count == 2 && PM.Agents.All(a => a.IsFree && !a.CapturedPendingEject), "The original outcome and both free surviving bodies reach the summary.");
            Check(!GameManager.Instance.ContinueHarbor(), "The completed harbor cannot replay its continuation.");
            string headline = outcome == EndingOutcome.Peace ? "A CITY THAT CHOOSES." : "EVERY FREQUENCY ANSWERS.";
            Check(HUDController.Instance.GetComponentsInChildren<Text>(true).Any(text => text.gameObject.activeInHierarchy && text.text == headline),
                "The actual final summary displays the earned harbor branch headline.");
            Check(!HUDController.Instance.GetComponentsInChildren<Button>(true).Single(button => button.name == "Btn_Continue · The Working Harbor").gameObject.activeSelf,
                "The completed harbor summary removes its one-shot Continue button.");
            if (capture != null) { yield return new WaitForSecondsRealtime(.85f); yield return capture("harbor-" + outcome + "-summary.png"); }
            Debug.Log("[Harbor] PASS " + outcome + (continueCurrentSummary ? " · earned M20 summary" : " · prepared M20 summary") + " → actual Continue → pressure → physical branch cargo → two free bodies → real E relay → summary");
        }

        public static IEnumerator EnterFromIsland(EndingOutcome outcome, bool continueCurrentSummary = false)
        {
            if (!continueCurrentSummary)
            {
                yield return AutoTest.LoadCity();
                SeedPriorChapter(outcome);
                RunState.MissionIndex = 20;
                SceneManager.LoadScene(GameManager.CityScene); yield return Ready();
                Check(!RunState.HarborStarted && MissionManager.Instance.Missions.Count == 20, "Harbor missions remain absent before explicit continuation.");
                // Test-only prior chapter fixture approved for bounded chapter coverage. This does not
                // exercise island departure, nor grant any of this chapter's progress or geometry.
                typeof(GameManager).GetProperty(nameof(GameManager.State), BindingFlags.Public | BindingFlags.Instance)
                    .GetSetMethod(true).Invoke(GameManager.Instance, new object[] { GameState.Ended });
                Time.timeScale = 0;
                GameEvents.RaiseEnding(new EndingData { route = RunState.Route, outcome = outcome, timeSeconds = RunState.PlayTime,
                    hostsPossessed = RunState.HostsPossessed, captures = RunState.Captures, peakSuspicion = RunState.PeakSuspicion });
            }
            else
            {
                Check(GameManager.Instance && GameManager.Instance.State == GameState.Ended && RunState.MissionIndex == 20
                    && MissionManager.Instance.Missions.Count == 20 && RunState.IslandStarted
                    && RunState.Has(IslandRoster.Departed) && RunState.Has(IslandRoster.SignalMounted) && IslandRoster.AllBoarded
                    && RunState.LakesideOutcome == outcome && AfterlakeDeparture.AllAmoebaeFree(out _) && !RunState.HarborStarted,
                    "Continuous harbor entry requires the actual completed M20 launch summary and its carried people/signal flags.");
            }
            yield return new WaitForSecondsRealtime(.9f);
            var button = HUDController.Instance.GetComponentsInChildren<Button>(true).Single(b => b.name == "Btn_Continue · The Working Harbor");
            Check(button.gameObject.activeInHierarchy && button.IsInteractable(), (continueCurrentSummary ? "The earned M20 summary" : "The prepared M20 summary") + " offers its actual harbor Continue button.");
            var oldFlags = new HashSet<string>(RunState.Flags);
            button.onClick.Invoke(); yield return Ready();
            yield return Wait(() => GameManager.Instance.IsPlaying && RunState.Has(HarborCampaign.ArrivalSeen), HarborArrival.PresentationSeconds + 3,
                "Protected caption-only harbor arrival finishes through its actual finite handoff.");
            Check(RunState.HarborStarted && GameManager.Instance.IsPlaying && RunState.MissionIndex == 20 && MissionManager.Instance.Missions.Count == 23,
                "Real summary Continue appends missions21–23 and starts playable pressure mission.");
            Check(oldFlags.IsSubsetOf(RunState.Flags) && RunState.LakesideOutcome == outcome && RunState.CurrentChapterSpawn == HarborLayout.Spawn,
                "Continuation retains the earlier choice and flags while selecting the harbor checkpoint.");
            Check(PM.Active.IsFree && PM.Agents.Count == 1 && FlatDistance(PM.Active.Position, HarborLayout.Spawn) < 1.5f,
                "The harbor starts with one free body at its authored landing.");
            Check(GameObject.Find(HarborLayout.RootName) && !GameObject.Find(IslandDistrict.RootName), "Only the harbor annex is built for the current chapter.");
            Check(!RunState.Has(HarborCampaign.CanalOpen) && !RunState.Has(HarborCampaign.ManifestComplete) && !RunState.Has(HarborCampaign.RelayComplete),
                "No harbor completion flag is prepared by the fixture.");
            GameInput.Simulated = true; Time.timeScale = 1;
            Debug.Log(continueCurrentSummary
                ? "[Harbor] CONTINUOUS earned M20 summary carried into the real Continue; no prior flag or summary state prepared at this boundary."
                : "[Harbor] FIXTURE prior20 summary prepared; initial roles may be borrowed immediately; every harbor objective uses ordinary movement/E and physical timing.");
        }

        static void SeedPriorChapter(EndingOutcome outcome, string omitted = null)
        {
            if (omitted != "outcome") RunState.RecordLakesideOutcome(outcome);
            foreach (string flag in new[] { MissionFlags.Escaped, AfterlakeCampaign.Started, AfterlakeCampaign.Departed,
                IslandCampaign.Started, IslandCampaign.ArrivalSeen, IslandRoster.Departed })
                if (flag != omitted) RunState.Flags.Add(flag);
        }

        static IEnumerator Pressure(bool regression)
        {
            var works = HarborPressureWorks.Instance;
            Check(works && works.Configured && works.gate && works.gate.GetComponent<Collider>().enabled, "The pressure gate retains its physical collider.");
            Vector3 closed = works.gate.position;
            Check(works.Pressure == 0 && works.ValveMask == 0 && RunState.MissionIndex == 20, "The new works begin unbalanced with all handles closed.");
            yield return Walk(HarborLayout.World(HarborLayout.ValveLocal[0]) + Vector3.back * 1.4f);
            var valve = Valve(0);
            yield return Aim(valve);
            Check(!valve.CanInteract(Context(), out _), "A free amoeba cannot operate a maintenance valve.");
            yield return PressAt(valve, false);
            Check(works.ValveMask == 0, "Denied ordinary E leaves the real valve unchanged.");
            Take(Profession.Maintenance);
            if (regression)
            {
                yield return UseValve(0); Check(works.Pressure == 3 && works.ValveMask == 1, "A adds3 through actual E.");
                yield return UseValve(1);
                Check(works.OverflowCount == 1 && works.Pressure == 0 && works.ValveMask == 0 && works.BalancedSeconds == 0,
                    "A+B overflow safely resets all three actual valve states.");
                for (int i = 0; i < 3; i++) Check(!RunState.Has(HarborCampaign.ValveFlag(i)), "Overflow clears durable handle " + i);
                Check(!RunState.Has(HarborCampaign.CanalOpen) && Vector3.Distance(closed, works.gate.position) < .001f, "Overflow cannot open or advance the physical lock.");
            }
            yield return UseValve(2); yield return UseValve(0); yield return UseValve(1);
            Check(works.Pressure == 4 && works.ValveMask == 7 && !RunState.Has(HarborCampaign.CanalOpen), "C+A+B reaches4 without granting immediate completion.");
            if (regression)
            {
                yield return Wait(() => works.BalancedSeconds > .2f, 1, "Balanced pressure starts its actual timer.");
                GameManager.Instance.SetPaused(true); float timer = works.BalancedSeconds; Vector3 gatePosition = works.gate.position;
                yield return new WaitForSecondsRealtime(.6f);
                Check(Mathf.Approximately(timer, works.BalancedSeconds) && works.gate.position == gatePosition && !RunState.Has(HarborCampaign.CanalOpen), "Pause freezes balance progress and gate geometry.");
                GameManager.Instance.SetPaused(false);
                float restoredBalance = -1;
                void ObserveBalance(Scene scene, LoadSceneMode mode) { if (scene.name == GameManager.CityScene && HarborPressureWorks.Instance) restoredBalance = HarborPressureWorks.Instance.BalancedSeconds; }
                SceneManager.sceneLoaded += ObserveBalance;
                try { GameManager.Instance.ResetToCheckpoint("Harbor transient balance acceptance"); yield return Reload(); }
                finally { SceneManager.sceneLoaded -= ObserveBalance; }
                works = HarborPressureWorks.Instance;
                Check(works.ValveMask == 7 && works.Pressure == 4 && restoredBalance == 0 && !RunState.Has(HarborCampaign.CanalOpen),
                    "Checkpoint restores earned handles but restarts the transient balance timer.");
            }
            yield return Wait(() => RunState.Has(HarborCampaign.CanalOpen) && HarborPressureWorks.Instance.GateOpenFraction >= .999f, 5, "Two actual scaled seconds balance and lift the gate.");
            works = HarborPressureWorks.Instance;
            Check(RunState.MissionIndex == 21 && Vector3.Distance(works.gate.position, closed + works.openOffset) < .01f && works.gate.GetComponent<Collider>().enabled,
                "The collision gate physically lifts four metres and advances only to cargo.");
            yield return Walk(HarborLayout.At(-10, .15f, -3));
            yield return Walk(HarborLayout.LockCheckpoint);
            Check(PM.Active.Position.z > HarborLayout.Origin.z + 3, "Normal body movement crosses the opened physical lock.");
        }

        static HarborValve Valve(int slot) => Object.FindObjectsByType<HarborValve>(FindObjectsSortMode.None).Single(v => v.slot == slot);
        static IEnumerator UseValve(int slot)
        {
            var valve = Valve(slot); yield return Walk(HarborLayout.World(HarborLayout.ValveLocal[slot]) + Vector3.back * 1.4f);
            yield return PressAt(valve, true);
        }

        static IEnumerator Cargo(EndingOutcome outcome, bool regression)
        {
            var director = HarborCargoDirector.Instance;
            Check(director && director.Configured && director.PhysicalCrateCount == 4, "Four stable colliding cargo bodies exist.");
            var originalBodies = HarborCargoDirector.Ids.Select(director.Crate).ToArray();
            Check(originalBodies.Distinct().Count() == 4, "Each manifest ID maps to its own physical crate.");
            var scanner = Object.FindFirstObjectByType<HarborCargoScanner>();
            var brake = Object.FindFirstObjectByType<HarborBrake>();
            Check(scanner && brake, "Both optional maintenance brake and scientist scanner are ordinary registered controls.");
            if (regression)
            {
                yield return Walk(HarborLayout.World(HarborLayout.ScannerLocal) + Vector3.left * 1.4f);
                yield return PressAt(scanner, false);
                Check(!director.CanScan(Context(), out _), "Maintenance cannot scan scientist lots.");
                yield return Walk(HarborLayout.World(HarborLayout.BrakeLocal) + Vector3.left * 1.4f);
                float requestedAt = -1, acknowledgedAt = -1;
                yield return PressAt(brake, true, true, (requested, acknowledged) => { requestedAt = requested; acknowledgedAt = acknowledged; });
                float earliestRemaining = HarborCargoDirector.BrakeSeconds - (Time.time - requestedAt) - Time.fixedDeltaTime;
                float latestRemaining = HarborCargoDirector.BrakeSeconds - (Time.time - acknowledgedAt) + Time.fixedDeltaTime;
                Check(requestedAt >= 0 && acknowledgedAt >= requestedAt && director.BrakeRemaining > 0 && director.BrakeRemaining <= HarborCargoDirector.BrakeSeconds
                    && director.BrakeRemaining >= earliestRemaining - .005f && director.BrakeRemaining <= latestRemaining + .005f,
                    "Actual maintenance E engages five scaled seconds, measured from its input request/acknowledgement with fixed-step quantization.");
                Vector3[] frozen = originalBodies.Select(t => t.position).ToArray();
                var brakeBodies = HarborCargoDirector.Ids.Select(director.CrateBody).ToArray();
                Vector3[] physicalBefore = brakeBodies.Select(body => body.position).ToArray();
                Vector3[] velocityBefore = brakeBodies.Select(body => body.linearVelocity).ToArray();
                float beforeTime = Time.time, beforeFixed = Time.fixedTime, beforeBrake = director.BrakeRemaining;
                int beforeFrame = Time.frameCount;
                yield return new WaitForSeconds(.2f);
                for (int i = 0; i < frozen.Length; i++)
                {
                    var body = brakeBodies[i];
                    Debug.Log("[HarborBrakeDiagnostic] id=" + HarborCargoDirector.Ids[i]
                        + " interpolation=" + body.interpolation
                        + " beforeFrame=" + beforeFrame + " afterFrame=" + Time.frameCount
                        + " beforeTime=" + beforeTime.ToString("F6") + " afterTime=" + Time.time.ToString("F6")
                        + " beforeFixed=" + beforeFixed.ToString("F6") + " afterFixed=" + Time.fixedTime.ToString("F6")
                        + " beforeBrake=" + beforeBrake.ToString("F6") + " afterBrake=" + director.BrakeRemaining.ToString("F6")
                        + " bodyBefore=" + physicalBefore[i].ToString("F6") + " bodyAfter=" + body.position.ToString("F6")
                        + " transformBefore=" + frozen[i].ToString("F6") + " transformAfter=" + originalBodies[i].position.ToString("F6")
                        + " velocityBefore=" + velocityBefore[i].ToString("F6") + " velocityAfter=" + body.linearVelocity.ToString("F6")
                        + " bodyDelta=" + Vector3.Distance(physicalBefore[i], body.position).ToString("F6")
                        + " transformDelta=" + Vector3.Distance(frozen[i], originalBodies[i].position).ToString("F6")
                        + " beforeGap=" + Vector3.Distance(physicalBefore[i], frozen[i]).ToString("F6")
                        + " afterGap=" + Vector3.Distance(body.position, originalBodies[i].position).ToString("F6"));
                }
                for (int i = 0; i < frozen.Length; i++) Check(Vector3.Distance(physicalBefore[i], brakeBodies[i].position) < .001f,
                    "Actual E immediately freezes unsorted rigidbody " + i + " within one millimetre over .2 scaled seconds.");
                yield return Wait(() => brakeBodies.Select((body, i) => Vector3.Distance(body.position, originalBodies[i].position) < .001f).All(value => value), .25f,
                    "Interpolated visual poses naturally settle onto their stopped physical bodies within the bounded observation window.");
                frozen = originalBodies.Select(t => t.position).ToArray();
                yield return new WaitForSeconds(.2f);
                for (int i = 0; i < frozen.Length; i++) Check(Vector3.Distance(frozen[i], originalBodies[i].position) < .001f, "The engaged brake physically stops unsorted crate " + i);
                for (int i = 0; i < frozen.Length; i++) Check(Vector3.Distance(physicalBefore[i], brakeBodies[i].position) < .001f,
                    "Physical crate " + i + " remains stopped throughout the visual stability observation.");
                GameManager.Instance.SetPaused(true); float remaining = director.BrakeRemaining;
                yield return new WaitForSecondsRealtime(.4f);
                Check(Mathf.Approximately(remaining, director.BrakeRemaining), "Pause freezes the optional brake duration.");
                GameManager.Instance.SetPaused(false);
            }
            Take(Profession.Scientist);
            yield return Walk(HarborLayout.World(HarborLayout.ScannerLocal) + Vector3.left * 1.4f);
            Check(!brake.CanInteract(Context(), out _), "Scientist credentials cannot operate the maintenance brake.");
            if (regression)
            {
                yield return Aim(scanner);
                yield return Wait(() => director.CurrentId.Length == 0, 12, "A real empty arch window occurs.");
                yield return PressAt(scanner, false, false);
                Check(!HarborCargoDirector.Ids.Any(director.Rerouted), "E during the empty physical arch grants no lot.");
                yield return Wait(() => director.CurrentId.Length > 0 && !HarborCampaign.RequiredCargoIds.Contains(director.CurrentId) && director.WindowRemaining > .2f, 30, "A wrong-branch physical lot enters the arch.");
                string wrong = director.CurrentId;
                Check(!scanner.CanInteract(Context(), out string wrongReason) && wrongReason.Contains(wrong), "The live scanner identifies the actual wrong branch lot before E.");
                yield return PressAt(scanner, false, false);
                Check(!director.Rerouted(wrong) && !director.Delivered(wrong), "Wrong-branch lot " + wrong + " cannot be scanned into this manifest.");
            }
            foreach (string id in HarborCampaign.RequiredCargoIds)
            {
                yield return Aim(scanner);
                yield return Wait(() => director.CurrentId == id && director.WindowRemaining > .2f, 32, "Required physical lot reaches the actual scan arch: " + id);
                Vector3 before = director.Crate(id).position;
                yield return PressAt(scanner, true, false);
                Check(director.Rerouted(id) && !director.Delivered(id) && !RunState.Has(HarborCampaign.ManifestComplete), "Scan reroutes " + id + " without granting physical arrival.");
                yield return Wait(() => director.Delivered(id), 30, "Rerouted crate reaches its actual bay: " + id);
                int slot = HarborCargoDirector.Ids.ToList().IndexOf(id);
                Check(Vector3.Distance(director.Crate(id).position, HarborLayout.World(HarborLayout.CargoBayLocal[slot])) < .03f
                    && Vector3.Distance(before, director.Crate(id).position) > 5, "Sealed " + id + " physically travels from arch to its matching stable bay.");
            }
            Check(RunState.Has(HarborCampaign.ManifestComplete) && RunState.MissionIndex == 22 && RunState.LakesideOutcome == outcome, "Only the correct physically delivered branch manifest advances to relay.");
            yield return Walk(HarborLayout.ManifestCheckpoint);
            GameManager.Instance.ResetToCheckpoint("Harbor delivered cargo checkpoint acceptance"); yield return Reload();
            director = HarborCargoDirector.Instance;
            Check(director.PhysicalCrateCount == 4 && director.BrakeRemaining == 0 && RunState.Has(HarborCampaign.CanalOpen) && RunState.MissionIndex == 22,
                "Reload preserves durable lock/manifest and recreates exactly four crates, while clearing transient brake.");
            foreach (string id in HarborCampaign.RequiredCargoIds)
            {
                int slot = HarborCargoDirector.Ids.ToList().IndexOf(id);
                Check(director.Delivered(id) && Vector3.Distance(director.Crate(id).position, HarborLayout.World(HarborLayout.CargoBayLocal[slot])) < .03f,
                    "Reload restores earned " + id + " at its real stable bay.");
            }
        }

        static IEnumerator Relay(EndingOutcome outcome, bool regression)
        {
            var relay = HarborRelay.Instance;
            Check(relay && relay.Configured && !relay.Running && relay.Progress == 0 && PM.Agents.Count == 1,
                "Delivered-manifest checkpoint starts with one free body and no transient relay attempt.");
            yield return Walk(HarborLayout.World(HarborLayout.RelayLocal[0]), .08f);
            var nodeA = Node(0);
            yield return PressAt(nodeA, false);
            Check(!relay.Ready, "One free body cannot satisfy two separate physical pads.");
            GameInput.SimSplit = true; yield return null; GameInput.SimSplit = false; yield return null;
            Check(PM.Agents.Count == 2 && PM.Agents[0] != PM.Agents[1] && PM.Agents[0].Id != PM.Agents[1].Id,
                "Normal V creates two distinct registered bodies within the existing two-body limit.");
            yield return Switch();
            yield return Walk(HarborLayout.World(HarborLayout.RelayLocal[0]) + Vector3.right * .72f, .12f);
            yield return PressAt(nodeA, false);
            Check(!relay.Ready, "Two distinct free bodies on the same pad cannot link.");
            yield return Walk(HarborLayout.World(HarborLayout.RelayLocal[1]) + Vector3.right * 1.04f, .04f);
            var nodeB = Node(1);
            Check(HarborLayout.RelayPad(1).Contains(PM.Active.Blob.Center) && !HarborLayout.RelayPad(1).Contains(PM.Active.Blob.Controller.bounds.max),
                "The edge fixture places the centre on the pad while part of the actual capsule lies outside.");
            yield return PressAt(nodeB, false);
            Check(!relay.Ready, "A centre point on a pad cannot substitute for full-body containment.");
            yield return Walk(HarborLayout.World(HarborLayout.RelayLocal[1]), .12f);
            yield return Wait(() => relay.Ready, 3, "Two actual grounded free capsules settle fully on distinct pads.");
            if (regression)
            {
                // Real role ownership and the ordinary capture/eject transitions are negative fixtures;
                // a guard capture is injected through the same public callbacks, never a completion flag.
                Take(Profession.Scientist);
                yield return Walk(HarborLayout.World(HarborLayout.RelayLocal[1]), .12f);
                Check(PM.Active.Host && !relay.Ready && !relay.CanBegin(Context(), out _), "A possessed body on the second pad cannot stand in for a free amoeba.");
                yield return Walk(HarborLayout.World(HarborLayout.RelayLocal[1]) + Vector3.forward * 3, .2f);
                var capturedHost = PM.Active.Host;
                var guard = Object.FindFirstObjectByType<GuardAI>();
                Check(guard, "The controlled capture fixture has an actual authored guard callback owner.");
                capturedHost.Capture(); PM.OnHostCaptured(capturedHost, guard);
                Check(PM.Active.CapturedPendingEject && !relay.Ready && !relay.CanBegin(Context(), out _), "Captured ownership cannot begin a link.");
                Check(PM.ComputeTarget().kind == TargetKind.Eject, "Actual E targets the captured-host eject action.");
                GameInput.SimInteract = true; yield return null; GameInput.SimInteract = false; yield return null;
                Check(PM.Active.IsFree && !PM.Active.CapturedPendingEject && capturedHost.Possessor == null, "Normal E eject clears captured ownership before retry.");
                yield return Walk(HarborLayout.World(HarborLayout.RelayLocal[1]), .12f);
                yield return Wait(() => relay.Ready, 3, "Both bodies settle again after captured-host eject.");

                SuspicionSystem.Instance.SetValue(100);
                Check(SuspicionSystem.Instance.Level == SuspicionLevel.Pursuing && !relay.Ready && !relay.CanBegin(Context(), out _), "A real pursuing suspicion level denies the relay.");
                SuspicionSystem.Instance.SetValue(0); SuspicionSystem.Instance.OnSuspectGone();
                yield return Wait(() => relay.Ready, 3, "Clearing the controlled pursuit fixture permits a fresh attempt.");

                var inactive = PM.Agents.Single(agent => agent != PM.Active);
                inactive.InTransit = true;
                try { Check(!relay.Ready && !relay.CanBegin(Context(), out _), "A registered inactive pending leap cannot be treated as a free pad body."); }
                finally { inactive.InTransit = false; }

                yield return PressAt(Node(1), true);
                yield return Wait(() => relay.Running && relay.Progress > .2f, 1, "Ordinary E starts the actual three-second handshake.");
                float progress = relay.Progress; Vector3 first = PM.Agents[0].Position, second = PM.Agents[1].Position;
                GameManager.Instance.SetPaused(true); yield return new WaitForSecondsRealtime(.55f);
                Check(relay.Running && Mathf.Approximately(progress, relay.Progress) && first == PM.Agents[0].Position && second == PM.Agents[1].Position,
                    "Pause freezes both handshake time and both actual body positions.");
                GameManager.Instance.SetPaused(false);
                GameInput.SimMove = Vector2.down; yield return new WaitForSeconds(.12f); GameInput.SimMove = Vector2.zero;
                yield return Wait(() => !relay.Running, 1, "Normal movement beyond the unchanged tolerance cancels the link.");
                Check(relay.Progress == 0 && !RunState.Has(HarborCampaign.RelayComplete), "Movement discards transient progress and grants no completion.");

                // Put the beam crossing outside the actual capsule. The extra collider exists only
                // to prove physical LOS denial and cancellation; gameplay geometry is left intact.
                yield return Walk(HarborLayout.World(HarborLayout.RelayLocal[1]) + Vector3.back * .72f, .04f);
                yield return Wait(() => relay.Ready, 2, "The full body fits on the rear inset before the LOS fixture.");
                yield return PressAt(Node(1), true);
                yield return Wait(() => relay.Progress > .2f, 1, "A new E starts a separate, fresh handshake.");
                var obstruction = new GameObject("Harbor acceptance temporary LOS obstruction");
                obstruction.transform.position = Vector3.Lerp(PM.Active.Blob.Center, relay.sightPoints[1].position, .58f);
                var collider = obstruction.AddComponent<BoxCollider>(); collider.size = Vector3.one * .08f;
                Physics.SyncTransforms();
                try
                {
                    Check(!collider.bounds.Intersects(PM.Active.Blob.Controller.bounds), "The LOS fixture obstructs the beam without touching the player capsule.");
                    yield return Wait(() => !relay.Running, 1, "The actual physical obstruction cancels an in-progress handshake.");
                    Check(!relay.Ready && relay.Progress == 0 && !RunState.Has(HarborCampaign.RelayComplete), "Blocked physical LOS cannot complete or preserve old progress.");
                    yield return PressAt(Node(1), false);
                }
                finally { Object.Destroy(obstruction); }
                yield return null; Physics.SyncTransforms();
                yield return Walk(HarborLayout.World(HarborLayout.RelayLocal[1]), .12f);
                yield return Wait(() => relay.Ready, 3, "Removing the real obstruction restores the unchanged LOS rule.");

                yield return PressAt(Node(1), true);
                yield return Wait(() => relay.Running && relay.Progress > .2f, 1, "A new attempt exists before checkpoint recovery.");
                GameManager.Instance.ResetToCheckpoint("Harbor transient handshake acceptance"); yield return Reload();
                relay = HarborRelay.Instance;
                Check(!relay.Running && relay.Progress == 0 && !RunState.Has(HarborCampaign.RelayComplete) && PM.Agents.Count == 1,
                    "Checkpoint retains the manifest while discarding transient handshake progress and temporary split bodies.");
                Check(RunState.MissionIndex == 22 && HarborCampaign.RequiredCargoDelivered && RunState.LakesideOutcome == outcome,
                    "Handshake recovery cannot erase the earned cargo or change the original branch.");
                yield return Walk(HarborLayout.World(HarborLayout.RelayLocal[0]), .12f);
                GameInput.SimSplit = true; yield return null; GameInput.SimSplit = false; yield return null;
                Check(PM.Agents.Count == 2, "A fresh normal split recreates only the second body after reload.");
                yield return Switch();
                yield return Walk(HarborLayout.World(HarborLayout.RelayLocal[1]), .12f);
                yield return Wait(() => relay.Ready, 3, "Both fresh free bodies physically regain distinct pads.");
            }
            Check(!GameManager.Instance.FinishHarbor() && !RunState.Has(HarborCampaign.RelayComplete), "Even valid pad geometry cannot directly bypass the three-second E handshake.");
            yield return PressAt(Node(1), true);
            Check(relay.Running && relay.Progress < HarborRelay.HandshakeSeconds && !RunState.Has(HarborCampaign.RelayComplete), "Final ordinary E starts a new handshake at zero.");
            yield return Wait(() => RunState.Has(HarborCampaign.RelayComplete), 5, "The uninterrupted physical three-second link earns the final harbor flag.");
            Check(relay.Progress >= HarborRelay.HandshakeSeconds && PM.Agents.All(a => a.IsFree && !a.CapturedPendingEject), "Full elapsed progress and both free bodies are present at actual completion.");
        }

        static HarborRelayNode Node(int index) => Object.FindObjectsByType<HarborRelayNode>(FindObjectsSortMode.None)
            .OrderBy(node => Vector3.Distance(node.transform.position, HarborLayout.World(HarborLayout.RelayLocal[index]))).First();
        static IEnumerator Switch()
        {
            var previous = PM.Active;
            GameInput.SimSwitch = true; yield return null; GameInput.SimSwitch = false; yield return null;
            Check(PM.Active != previous, "Normal Tab changes the active registered body.");
        }

        /// <summary>Two actual same-cycle scans must survive a checkpoint without stacked physical crates.</summary>
        public static IEnumerator TwoInflightCargoReroutesRecoverWithoutOverlap()
        {
            yield return EnterFromIsland(EndingOutcome.Peace);
            yield return Pressure(false);
            Take(Profession.Scientist);
            yield return Walk(HarborLayout.World(HarborLayout.ScannerLocal) + Vector3.left * 1.4f);
            var scanner = Object.FindFirstObjectByType<HarborCargoScanner>();
            var director = HarborCargoDirector.Instance;
            Check(scanner && director && director.PhysicalCrateCount == 4, "Four real cargo bodies and the ordinary scanner exist before the recovery fixture.");
            Check(!HarborCargoDirector.Ids.Any(id => director.Rerouted(id) || director.Delivered(id)), "No cargo reroute or delivery is prepared by this fixture.");
            // Positive loop distances make A32 immediately precede A17 at this physical arch.
            // This adjacent pair is about5.45s apart; the reverse order is about16.36s apart.
            string[] scanOrder = { "A32", "A17" };
            foreach (string id in scanOrder)
            {
                yield return Aim(scanner);
                yield return Wait(() => director.CurrentId == id && director.WindowRemaining > .2f, 32,
                    "The actual same-cycle Peace lot reaches the scan arch: " + id);
                if (id == "A17") Check(director.Rerouted("A32") && !director.Delivered("A32"), "The earlier A32 scan is genuinely still in flight when adjacent A17 arrives.");
                yield return PressAt(scanner, true, false);
                Check(director.Rerouted(id) && !director.Delivered(id), "Actual E reroutes " + id + " without substituting a delivery flag.");
                LogCargoRecovery("after actual scan " + id, director);
            }
            Check(scanOrder.All(id => director.Rerouted(id) && !director.Delivered(id)) && !RunState.Has(HarborCampaign.ManifestComplete),
                "Two separately scanned physical crates are both in flight before the checkpoint reset.");
            Check(RunState.MissionIndex == 21, "Both in-flight flags alone cannot advance the physical-delivery mission.");
            LogCargoRecovery("before checkpoint", director);
            GameManager.Instance.ResetToCheckpoint("Harbor two in-flight reroutes acceptance");
            Check(GameManager.Instance.State == GameState.Resetting, "The checkpoint enters the real reset state before either in-flight crate arrives.");
            yield return Reload();
            director = HarborCargoDirector.Instance;
            Check(director && director.PhysicalCrateCount == 4 && HarborCargoDirector.Ids.Select(director.Crate).Distinct().Count() == 4
                && HarborCargoDirector.Ids.Select(director.CrateBody).Distinct().Count() == 4,
                "Reload recreates exactly four distinct stable physical crate bodies.");
            LogCargoRecovery("after checkpoint reload", director);
            Check(scanOrder.All(id => director.Rerouted(id) && !director.Delivered(id)) && !RunState.Has(HarborCampaign.ManifestComplete),
                "Reload preserves both actually earned reroutes without instantly granting either physical arrival.");
            Check(RunState.MissionIndex == 21 && RunState.LakesideOutcome == EndingOutcome.Peace, "The in-flight checkpoint retains the original branch and cargo mission.");
            RequireCargoSeparation(director, "immediately after reload", true);
            Vector3[] restoredPositions = scanOrder.Select(id => director.CrateBody(id).position).ToArray();
            float deadline = Time.realtimeSinceStartup + 45;
            while (!scanOrder.All(director.Delivered) && Time.realtimeSinceStartup < deadline)
            {
                RequireCargoSeparation(director, "during recovered physical diversion", false);
                yield return null;
            }
            LogCargoRecovery("after recovered physical delivery wait", director);
            Check(scanOrder.All(director.Delivered) && RunState.Has(HarborCampaign.ManifestComplete) && RunState.MissionIndex == 22,
                "Both recovered in-flight crates physically arrive before the manifest advances.");
            for (int i = 0; i < scanOrder.Length; i++)
            {
                string id = scanOrder[i]; int slot = HarborCargoDirector.Ids.ToList().IndexOf(id);
                Vector3 bay = HarborLayout.World(HarborLayout.CargoBayLocal[slot]);
                Check(Vector3.Distance(director.CrateBody(id).position, bay) < .03f && Vector3.Distance(director.Crate(id).position, bay) < .03f
                    && Vector3.Distance(restoredPositions[i], director.CrateBody(id).position) > 1,
                    "Recovered " + id + " travels physically from its resumed route into its own stable bay.");
            }
            RequireCargoSeparation(director, "after recovered bay arrivals", true);
            Check(!RunState.Has(HarborCampaign.RelayComplete) && RunState.LakesideOutcome == EndingOutcome.Peace,
                "Cargo recovery grants neither a relay completion nor a different original outcome.");
            Debug.Log("[Harbor] PASS two actual in-flight reroutes → real checkpoint → four separate physical bodies → both actual bay arrivals");
        }

        static void LogCargoRecovery(string phase, HarborCargoDirector director)
        {
            foreach (string id in HarborCargoDirector.Ids)
            {
                var body = director.CrateBody(id); var crate = director.Crate(id);
                Debug.Log("[HarborCargoRecovery] phase=" + phase + " id=" + id + " bodyId=" + body.GetInstanceID() + " transformId=" + crate.GetInstanceID()
                    + " route=" + director.RouteStage(id) + " rerouted=" + director.Rerouted(id) + " delivered=" + director.Delivered(id)
                    + " rerouteFlag=" + RunState.Has(HarborCampaign.ReroutedFlag(id)) + " deliveryFlag=" + RunState.Has(HarborCampaign.DeliveredFlag(id))
                    + " body=" + body.position.ToString("F6") + " transform=" + crate.position.ToString("F6")
                    + " time=" + Time.time.ToString("F6") + " fixed=" + Time.fixedTime.ToString("F6") + " frame=" + Time.frameCount
                    + " state=" + GameManager.Instance.State + " manifest=" + RunState.Has(HarborCampaign.ManifestComplete));
            }
        }

        static void RequireCargoSeparation(HarborCargoDirector director, string phase, bool logEveryPair)
        {
            bool allSeparate = true; string collisions = "";
            for (int i = 0; i < HarborCargoDirector.Ids.Count; i++)
                for (int j = i + 1; j < HarborCargoDirector.Ids.Count; j++)
                {
                    string firstId = HarborCargoDirector.Ids[i], secondId = HarborCargoDirector.Ids[j];
                    var first = director.CrateBody(firstId); var second = director.CrateBody(secondId);
                    var firstBox = first.GetComponent<BoxCollider>(); var secondBox = second.GetComponent<BoxCollider>();
                    Check(firstBox && secondBox && firstBox.enabled && secondBox.enabled && !firstBox.isTrigger && !secondBox.isTrigger,
                        "Every stable crate retains its actual enabled physical BoxCollider.");
                    bool overlap = Physics.ComputePenetration(firstBox, first.position, first.rotation, secondBox, second.position, second.rotation, out var direction, out float depth);
                    if (logEveryPair || overlap)
                        Debug.Log("[HarborCargoPenetration] phase=" + phase + " first=" + firstId + " second=" + secondId + " overlap=" + overlap
                            + " depth=" + depth.ToString("F8") + " direction=" + direction.ToString("F6")
                            + " firstBody=" + first.position.ToString("F6") + " secondBody=" + second.position.ToString("F6")
                            + " firstTransform=" + first.transform.position.ToString("F6") + " secondTransform=" + second.transform.position.ToString("F6")
                            + " firstRoute=" + director.RouteStage(firstId) + " secondRoute=" + director.RouteStage(secondId));
                    if (overlap) { allSeparate = false; collisions += firstId + "/" + secondId + " depth=" + depth.ToString("F8") + "; "; }
                }
            Check(allSeparate, "No physical crate boxes may penetrate " + phase + ": " + collisions);
        }

        /// <summary>Observe a real blocked cargo spur; a clock cannot substitute for physical delivery.</summary>
        public static IEnumerator BlockedCargoSpurWaitsForPhysicalClearance()
        {
            yield return EnterFromIsland(EndingOutcome.Peace);
            yield return Pressure(false); // Earn the actual prerequisite through the existing normal-E driver.
            Take(Profession.Scientist);
            yield return Walk(HarborLayout.World(HarborLayout.ScannerLocal) + Vector3.left * 1.4f);
            var director = HarborCargoDirector.Instance;
            var scanner = Object.FindFirstObjectByType<HarborCargoScanner>();
            const string id = "A17";
            Check(!director.Rerouted(id) && !director.Delivered(id), "The physical spur fixture grants no cargo progress.");
            yield return Aim(scanner);
            yield return Wait(() => director.CurrentId == id && director.WindowRemaining > .2f, 32, "Actual A17 enters the moving arch.");
            yield return PressAt(scanner, true, false);
            Check(director.Rerouted(id) && !director.Delivered(id), "Actual E starts the real A17 diversion without granting arrival.");
            var body = director.CrateBody(id); var box = body.GetComponent<BoxCollider>();
            var obstacle = new GameObject("Harbor acceptance physical spur obstruction");
            obstacle.transform.position = HarborLayout.At(22, 1.05f, 25);
            var barrier = obstacle.AddComponent<BoxCollider>(); barrier.size = new Vector3(.4f, 2, 3);
            Physics.SyncTransforms(); // Register only the newly added fixture collider; never move or disable gameplay bodies.
            try
            {
                float deadline = Time.realtimeSinceStartup + 15, stableFixedSeconds = 0, lastFixed = Time.fixedTime;
                Vector3 previous = body.position;
                while (stableFixedSeconds < .25f && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                    if (Time.fixedTime <= lastFixed) continue;
                    bool hitsOwnedBarrier = body.SweepTest(Vector3.right, out var hit, .07f, QueryTriggerInteraction.Ignore) && hit.collider == barrier;
                    if (hitsOwnedBarrier && Vector3.Distance(previous, body.position) < .001f) stableFixedSeconds += Time.fixedTime - lastFixed;
                    else stableFixedSeconds = 0;
                    previous = body.position; lastFixed = Time.fixedTime;
                }
                Check(stableFixedSeconds >= .25f && director.RouteStage(id) == 1, "The actual body reaches the east spur and stops against the actual fixture collider.");
                Vector3 blocked = body.position;
                Check(Vector3.Distance(blocked, HarborLayout.World(HarborLayout.CargoBayLocal[0])) > 5, "The blocked crate is physically well short of its assigned bay.");
                Debug.Log("[HarborBlockedSpur] stopped body=" + blocked.ToString("F6") + " route=" + director.RouteStage(id) + " stableFixedSeconds=" + stableFixedSeconds.ToString("F6"));
                float observedUntil = Time.time + 8f; // Longer than the unblocked remaining spur and final bay travel.
                while (Time.time < observedUntil)
                {
                    Check(Vector3.Distance(blocked, body.position) < .001f && !director.Delivered(id) && !RunState.Has(HarborCampaign.ManifestComplete),
                        "An actually blocked rigidbody stays put and cannot gain delivery/manifest with elapsed time.");
                    Check(!Physics.ComputePenetration(box, body.position, body.rotation, barrier, barrier.transform.position, barrier.transform.rotation, out _, out _),
                        "The blocked physical crate cannot penetrate the real barrier.");
                    yield return null;
                }
                Debug.Log("[HarborBlockedSpur] held8scaledSeconds body=" + body.position.ToString("F6") + " delivered=" + director.Delivered(id));
                Object.Destroy(obstacle); obstacle = null; yield return null;
                yield return Wait(() => director.Delivered(id), 20, "Removing only the fixture obstacle lets the same real crate finish its physical route.");
                Vector3 bay = HarborLayout.World(HarborLayout.CargoBayLocal[0]);
                Check(Vector3.Distance(body.position, bay) < .03f && Vector3.Distance(director.Crate(id).position, bay) < .03f && Vector3.Distance(blocked, body.position) > 5,
                    "Both physical and displayed A17 reach the correct bay after real resumed movement.");
                Check(!director.Rerouted("A32") && !director.Delivered("A32") && !RunState.Has(HarborCampaign.ManifestComplete) && RunState.MissionIndex == 21,
                    "One resumed delivery cannot stand in for the second required lot or advance the cargo mission.");
            }
            finally { if (obstacle) Object.Destroy(obstacle); }
            Debug.Log("[Harbor] PASS actual blocked spur → no timed delivery → remove fixture obstruction → same body physically arrives");
        }

        /// <summary>Destroy the inactive free body during a real link, then rebuild the pair with normal input.</summary>
        public static IEnumerator InactiveFreeContainmentCancelsRelayAndRequiresNewE()
        {
            yield return EnterFromIsland(EndingOutcome.Peace);
            yield return Pressure(false);
            yield return Cargo(EndingOutcome.Peace, false); // Both required arrivals are earned; no manifest flag is prepared.
            var relay = HarborRelay.Instance;
            Check(PM.Agents.Count == 1 && PM.Active.IsFree && RunState.MissionIndex == 22, "Earned cargo recovery starts the actual relay approach with one free body.");
            var initialAgent = PM.Active; var initialBlob = initialAgent.Blob;
            yield return WalkPhysicalRelayAgent(initialAgent, initialBlob, HarborLayout.World(HarborLayout.RelayLocal[0]), 1, .08f);
            GameInput.SimSplit = true; yield return null; GameInput.SimSplit = false; yield return null;
            Check(PM.Agents.Count == 2, "Normal V creates the second real free body.");
            yield return Switch();
            yield return WalkPhysicalRelayAgent(PM.Active, PM.Active.Blob, HarborLayout.World(HarborLayout.RelayLocal[1]), 2, .12f);
            yield return Wait(() => relay.Ready, 3, "Both distinct actual free capsules settle fully on their separate pads.");
            yield return PressAt(Node(1), true);
            yield return Wait(() => relay.Running && relay.Progress > .25f, 1, "Actual E creates positive running handshake progress before containment.");
            var survivor = PM.Active;
            var contained = PM.Agents.Single(agent => agent != survivor);
            var containedBlob = contained.Blob;
            var guard = Object.FindFirstObjectByType<GuardAI>();
            int resets = RunState.Resets;
            Check(guard && contained.IsFree && contained.Host == null && containedBlob && !contained.CapturedPendingEject && !contained.InTransit,
                "The contained subject is the actual inactive FREE agent, not a host or a synthetic capture flag.");
            Debug.Log("[HarborFreeContainment] before inactiveId=" + contained.Id + " activeId=" + survivor.Id + " progress=" + relay.Progress.ToString("F6") + " inactive=" + contained.Position.ToString("F6"));
            LogPhysicalRelayState("before actual inactive containment");
            PM.ContainBlob(contained, guard); // Controlled guard callback; the real production removal/destruction path runs.
            yield return Wait(() => !relay.Running, 1, "Actual inactive free-body containment cancels the running handshake.");
            yield return null; yield return null;
            Check(PM.Agents.Count == 1 && !PM.Agents.Contains(contained) && !containedBlob && PM.Active == survivor && survivor.IsFree && !survivor.CapturedPendingEject,
                "The contained body is actually destroyed/removed while control and the other free body survive.");
            Check(relay.Progress == 0 && !RunState.Has(HarborCampaign.RelayComplete) && !relay.Ready && GameManager.Instance.IsPlaying && RunState.Resets == resets,
                "Old progress is discarded without granting the link or resetting the surviving body.");
            var survivorBlob = survivor.Blob;
            yield return EscapeRelayAttention(survivor, survivorBlob);
            LogPhysicalRelayState("before normal replacement split after physical escape");
            GameInput.SimSplit = true; yield return null; GameInput.SimSplit = false; yield return null;
            Check(PM.Agents.Count == 2 && PM.Agents.All(agent => agent != contained && agent.IsFree), "Normal V creates a genuinely new replacement body within the unchanged two-body limit.");
            LogPhysicalRelayState("after normal replacement split");
            var replacementAgent = PM.Agents.Single(agent => agent != survivor); var replacementBlob = replacementAgent.Blob;
            yield return WalkPhysicalRelayAgent(survivor, survivorBlob, HarborLayout.At(8, .15f, 43), 2);
            yield return WalkPhysicalRelayAgent(survivor, survivorBlob, HarborLayout.World(HarborLayout.RelayLocal[1]), 2);
            yield return Switch();
            Check(ReferenceEquals(PM.Active, replacementAgent) && PM.Active != contained, "Normal Tab selects the actual new registered body, not just a reused numeric ID.");
            yield return WalkPhysicalRelayAgent(replacementAgent, replacementBlob, HarborLayout.At(-8, .15f, 43), 2);
            yield return WalkPhysicalRelayAgent(replacementAgent, replacementBlob, HarborLayout.World(HarborLayout.RelayLocal[0]), 2);
            LogPhysicalRelayState("after normal replacement walk");
            float readyDeadline = Time.realtimeSinceStartup + 3f;
            while (!relay.Ready && Time.realtimeSinceStartup < readyDeadline) yield return null;
            LogPhysicalRelayState("after unchanged three-second readiness wait");
            Check(relay.Ready, "The replacement physically walks back to the empty pad. Actual status: " + relay.StatusLine);
            yield return new WaitForSeconds(1);
            Check(!relay.Running && relay.Progress == 0 && !RunState.Has(HarborCampaign.RelayComplete), "Reforming a valid pair cannot resume the cancelled handshake without fresh E.");
            yield return PressAt(Node(0), true);
            Check(relay.Running && relay.Progress < .2f, "Fresh normal E starts the replacement pair from zero progress.");
            yield return Wait(() => RunState.Has(HarborCampaign.RelayComplete), 5, "The fresh pair completes the unchanged three-second physical handshake.");
            Check(relay.Progress >= HarborRelay.HandshakeSeconds && PM.Agents.Count == 2 && PM.Agents.All(agent => agent.IsFree), "Only the complete fresh handshake earns the relay flag with both replacement-pair bodies free.");
            Debug.Log("[Harbor] PASS running E link → actual inactive FREE containment/removal → survivor retained → normal V/Tab/walk → fresh E link");
        }

        static void CheckPhysicalRelayAgent(AmoebaAgent expected, AmoebaController body, int count)
        {
            Check(PM && PM.Agents.Count == count && ReferenceEquals(PM.Active, expected) && PM.Agents.Contains(expected)
                && expected.IsFree && !expected.InTransit && !expected.CapturedPendingEject && body && expected.Blob == body && body.gameObject.activeInHierarchy,
                "A physical relay leg must keep its exact registered active agent/body and expected count; a guard containment cannot silently transfer this input to the survivor.");
        }
        static IEnumerator WalkPhysicalRelayAgent(AmoebaAgent expected, AmoebaController body, Vector3 target, int count, float tolerance = .12f, float deadline = float.PositiveInfinity)
        {
            var stack = new System.Collections.Generic.Stack<IEnumerator>(); stack.Push(Walk(target, tolerance));
            try
            {
                while (stack.Count > 0)
                {
                    CheckPhysicalRelayAgent(expected, body, count);
                    Check(Time.realtimeSinceStartup < deadline, "The bounded physical escape budget expired during actual movement.");
                    var current = stack.Peek();
                    if (!current.MoveNext()) { (current as IDisposable)?.Dispose(); stack.Pop(); continue; }
                    if (current.Current is IEnumerator nested) stack.Push(nested); else yield return current.Current;
                }
                CheckPhysicalRelayAgent(expected, body, count);
            }
            finally
            {
                while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
                GameInput.SimMove = Vector2.zero; GameInput.SimSprint = GameInput.SimJump = false;
            }
        }
        static bool RelaySplitHasWitness(AmoebaAgent agent)
        {
            var suspicion = SuspicionSystem.Instance;
            if (agent == null || !agent.Blob || !suspicion) return true; // Missing body/context is unsafe, never a visibility pass.
            Vector3 proposed = PossessionManager.FindFreeSpot(agent.Position, -agent.Blob.transform.right) + Vector3.up * .3f;
            foreach (var witness in HostCharacter.All)
                if (witness && witness.IsAwakeNpc && witness.ReportsPlayerViolations && suspicion.CanSee(witness, proposed, null, witness.Brain is GuardAI ? GuardAI.SightRange : suspicion.RangeFor(witness))) return true;
            return false;
        }
        static bool ActualRelayBodyHasWitness(AmoebaAgent agent)
        {
            var suspicion = SuspicionSystem.Instance;
            if (agent == null || !agent.Blob || !suspicion) return true;
            return HostCharacter.All.Any(witness => witness && witness.IsAwakeNpc && witness.ReportsPlayerViolations
                && suspicion.CanSee(witness, agent.Blob.Center, agent.Blob.transform, witness.Brain is GuardAI ? GuardAI.SightRange : suspicion.RangeFor(witness)));
        }
        static void LogEscapeGuardState(string phase, AmoebaAgent agent)
        {
            var suspicion = SuspicionSystem.Instance;
            Debug.Log("[HarborEscapeDiagnostic] phase=" + phase + " body=" + (agent == null ? "none" : agent.Position.ToString("F6"))
                + " suspicion=" + suspicion.Value.ToString("F6") + " level=" + suspicion.Level + " unobservedSeconds=" + suspicion.TimeSinceObserved.ToString("F6")
                + " actualBodySeen=" + ActualRelayBodyHasWitness(agent) + " proposedSplitSeen=" + RelaySplitHasWitness(agent));
            foreach (var host in HostCharacter.All)
                if (host && host.IsAwakeNpc && host.Brain is GuardAI guard && Mathf.Abs(host.transform.position.x - HarborLayout.Origin.x) < 80)
                    Debug.Log("[HarborEscapeDiagnostic] phase=" + phase + " guard=" + host.DisplayName + " state=" + guard.GState
                        + " position=" + host.transform.position.ToString("F6") + " distance=" + (agent == null ? "none" : Vector3.Distance(host.EyePosition, agent.Position).ToString("F6"))
                        + " seesBody=" + (agent != null && agent.Blob && suspicion.CanSee(host, agent.Blob.Center, agent.Blob.transform, GuardAI.SightRange))
                        + " destination=" + (host.Nav && host.Nav.enabled && host.Nav.isOnNavMesh && host.Nav.hasPath ? host.Nav.destination.ToString("F6") : "none"));
        }

        static IEnumerator EscapeRelayAttention(AmoebaAgent survivor, AmoebaController body)
        {
            float deadline = Time.realtimeSinceStartup + 60f;
            yield return WalkPhysicalRelayAgent(survivor, body, HarborLayout.At(8, .15f, 43), 1, .2f, deadline);
            yield return WalkPhysicalRelayAgent(survivor, body, HarborLayout.At(33, .15f, 43), 1, .2f, deadline);
            var suspicion = SuspicionSystem.Instance;
            bool Safe() => PM && PM.Agents.Count == 1 && ReferenceEquals(PM.Active, survivor) && PM.Agents.Contains(survivor) && body && survivor.Blob == body
                && survivor.IsFree && !survivor.CapturedPendingEject && !survivor.InTransit && suspicion.Value <= 5f && suspicion.Level != SuspicionLevel.Pursuing && !RelaySplitHasWitness(survivor);
            int circuits = 0;
            LogEscapeGuardState("arrived at NE before attention recovery", survivor);
            while (!Safe() && Time.realtimeSinceStartup < deadline)
            {
                CheckPhysicalRelayAgent(survivor, body, 1);
                bool visible = ActualRelayBodyHasWitness(survivor) || RelaySplitHasWitness(survivor);
                bool hunting = HostCharacter.All.Any(host => host && host.IsAwakeNpc && host.Brain is GuardAI guard && guard.GState == GuardState.Pursue);
                bool recentlyObserved = suspicion.TimeSinceObserved <= GuardAI.LoseTrackSeconds;
                if (visible || hunting || recentlyObserved)
                {
                    Check(circuits < 2, "Two actual escape circuits are exhausted while guard visibility/attention remains; no third circuit or hidden waiting is permitted.");
                    circuits++;
                    foreach (var target in new[] { HarborLayout.At(36.5f, .15f, 38.5f), HarborLayout.At(36.5f, .15f, 6.5f), HarborLayout.At(8.5f, .15f, 6.5f), HarborLayout.At(8.5f, .15f, 38.5f), HarborLayout.At(33, .15f, 43) })
                    {
                        yield return WalkPhysicalRelayAgent(survivor, body, target, 1, .2f, deadline);
                        LogEscapeGuardState("after actual perimeter leg circuit=" + circuits + " target=" + target, survivor);
                    }
                }
                else yield return null;
            }
            LogPhysicalRelayState("after real survivor escape and natural attention decay");
            Check(Safe(), "Normal escape must genuinely lose pursuit/attention and actual split visibility within60s; no suspicion or guard state is cleared by the fixture.");
            Debug.Log("[HarborFreeContainment] physicalEscape circuits=" + circuits + " suspicion=" + suspicion.Value.ToString("F6") + " splitWitness=" + RelaySplitHasWitness(survivor));
        }

        static void LogPhysicalRelayState(string phase)
        {
            var relay = HarborRelay.Instance; var suspicion = SuspicionSystem.Instance;
            if (!relay || !PM) { Debug.Log("[HarborPhysicalRelayDiagnostic] phase=" + phase + " missing relay/manager"); return; }
            Debug.Log("[HarborPhysicalRelayDiagnostic] phase=" + phase + " ready=" + relay.Ready + " running=" + relay.Running + " progress=" + relay.Progress.ToString("F6")
                + " status=" + relay.StatusLine + " suspicion=" + (suspicion ? suspicion.Value.ToString("F6") : "none") + " level=" + (suspicion ? suspicion.Level.ToString() : "none")
                + " time=" + Time.time.ToString("F6") + " frame=" + Time.frameCount + " activeId=" + (PM.Active == null ? "none" : PM.Active.Id.ToString()));
            foreach (var agent in PM.Agents)
            {
                var controller = agent.Blob ? agent.Blob.Controller : null;
                Debug.Log("[HarborPhysicalRelayDiagnostic] phase=" + phase + " id=" + agent.Id + " object=" + (agent.Blob ? agent.Blob.GetInstanceID().ToString() : "none")
                    + " position=" + agent.Position.ToString("F6") + " free=" + agent.IsFree + " transit=" + agent.InTransit + " capture=" + agent.CapturedPendingEject
                    + " host=" + (agent.Host ? agent.Host.DisplayName : "none") + " blobActive=" + (agent.Blob && agent.Blob.isActiveAndEnabled)
                    + " controller=" + (controller && controller.enabled) + " grounded=" + (controller && controller.isGrounded)
                    + " boundsMin=" + (controller ? controller.bounds.min.ToString("F6") : "none") + " boundsMax=" + (controller ? controller.bounds.max.ToString("F6") : "none"));
                for (int padIndex = 0; padIndex < 2 && controller; padIndex++)
                {
                    var bounds = padIndex == 0 ? relay.padA : relay.padB; bounds.Expand(HarborRelay.PadContainmentTolerance * 2);
                    Vector3 start = agent.Blob.Center, end = relay.sightPoints[padIndex].position;
                    var hits = Physics.RaycastAll(start, end - start, Vector3.Distance(start, end), ~0, QueryTriggerInteraction.Ignore);
                    string hitList = string.Join(";", hits.Select(hit => hit.collider.name + "#" + hit.collider.GetInstanceID() + ":layer=" + hit.collider.gameObject.layer + ":distance=" + hit.distance.ToString("F6") + ":own=" + hit.transform.IsChildOf(agent.Blob.transform)));
                    Debug.Log("[HarborPhysicalRelayDiagnostic] phase=" + phase + " id=" + agent.Id + " pad=" + padIndex
                        + " fullyContained=" + (bounds.Contains(controller.bounds.min) && bounds.Contains(controller.bounds.max))
                        + " padMin=" + bounds.min.ToString("F6") + " padMax=" + bounds.max.ToString("F6")
                        + " beamStart=" + start.ToString("F6") + " beamEnd=" + end.ToString("F6") + " hits=" + hitList);
                }
            }
        }

        static InteractContext Context() => new() { agent = PM.Active, host = PM.Active.Host, permissions = PM.Active.Host ? PM.Active.Host.Permissions : AccessPermission.None, position = PM.Active.Position };
        static void Take(Profession profession)
        {
            if (PM.Active.Host && PM.Active.Host.Profession == profession) return;
            var host = HostCharacter.All.Where(h => h && h.CanBePossessed && h.Profession == profession && h.transform.position.x > HarborLayout.Origin.x - 80 && h.transform.position.x < HarborLayout.Origin.x + 80)
                .OrderBy(h => Vector3.Distance(h.transform.position, PM.Active.Position)).FirstOrDefault();
            Check(host, "The authored harbor contains an ordinary " + profession + " role host.");
            PM.ForcePossessImmediate(host); Check(PM.Active.Host == host, "Declared role fixture owns its actual harbor host.");
        }
        static IEnumerator Walk(Vector3 target, float tolerance = .3f)
        {
            Check(NavMesh.SamplePosition(PM.Active.Position, out var a, 2, NavMesh.AllAreas) && NavMesh.SamplePosition(target, out _, 1, NavMesh.AllAreas), "The physical objective route has navigable ends: " + target);
            NavMesh.SamplePosition(target, out var b, 1, NavMesh.AllAreas); var path = new NavMeshPath();
            Check(NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete, "A complete physical route reaches " + target);
            float length = 0; for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            bool reached = false;
            yield return AutoTest.WalkTo(b.position, Mathf.Max(15, length / 1.7f + 10), value => reached = value, tolerance);
            GameInput.SimMove = Vector2.zero; GameInput.SimSprint = GameInput.SimJump = false;
            Check(reached && FlatDistance(PM.Active.Position, target) < tolerance + .15f, "Normal input reaches the actual objective: " + target + "; body=" + PM.Active.Position);
            yield return null; yield return null;
        }
        static IEnumerator Aim(IInteractable target)
        {
            for (float t = 0; t < .45f; t += Time.unscaledDeltaTime)
            {
                Vector3 d = target.InteractPoint - Camera.Position;
                Camera.SetYaw(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
                Camera.SetPitch(-Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg);
                yield return null;
            }
        }
        static IEnumerator PressAt(IInteractable target, bool allowed, bool settle = true, Action<float, float> pressReceipt = null)
        {
            if (settle) yield return Aim(target);
            var aimed = PM.ComputeTarget();
            Check(aimed.kind == TargetKind.Interact && ReferenceEquals(aimed.interactable, target) && aimed.allowed == allowed,
                "Real E must select the intended " + (allowed ? "available" : "denied") + " " + target.GetType().Name + "; got " + aimed.prompt);
            GameInput.SimInteract = true; float requestedAt = Time.time;
            yield return null; float acknowledgedAt = Time.time;
            GameInput.SimInteract = false; yield return null;
            pressReceipt?.Invoke(requestedAt, acknowledgedAt);
        }
        static IEnumerator Wait(Func<bool> condition, float seconds, string message)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Check(condition(), message);
        }
        static IEnumerator Ready()
        {
            yield return null; float deadline = Time.realtimeSinceStartup + 30;
            while ((!CityDirector.Instance || !CityDirector.Instance.Ready) && Time.realtimeSinceStartup < deadline) yield return null;
            Check(CityDirector.Instance && CityDirector.Instance.Ready, "Actual chapter navigation/spawns finish before acceptance input.");
            yield return null; yield return null;
            GameInput.Simulated = true; ClearInput();
        }
        static IEnumerator Reload()
        {
            var previous = GameManager.Instance;
            float deadline = Time.realtimeSinceStartup + 20;
            while (GameManager.Instance == previous && Time.realtimeSinceStartup < deadline) yield return null;
            Check(GameManager.Instance != previous, "The checkpoint actually reloads the scene rather than reusing old objects.");
            yield return Ready(); Time.timeScale = 1;
        }
        static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
        static void ClearInput() { GameInput.SimMove = Vector2.zero; GameInput.SimInteract = GameInput.SimLeave = GameInput.SimSplit = GameInput.SimSwitch = GameInput.SimJump = GameInput.SimSprint = false; }
        static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
