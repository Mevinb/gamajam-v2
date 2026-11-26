using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Parasite.Characters;
using Parasite.Missions;
using Parasite.Possession;
using Parasite.UI;
using Parasite.World;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
namespace Parasite.Testing
{
    /// <summary>Shared physical/input cases. Focused entry prepares prior23; continuous entry requires its earned summary. Initial role ownership remains a declared chapter fixture.</summary>
    public static class RailAcceptance
    {
        static PossessionManager PM => PossessionManager.Instance;
        static PlayerCamera Camera => PlayerCamera.Instance;
        static void Check(bool yes, string reason) { if (!yes) throw new InvalidOperationException(reason); }
        static RailWorkControl Control(RailWork kind) => Object.FindObjectsByType<RailWorkControl>(FindObjectsSortMode.None).Single(c => c.kind == kind);
        static float Flat(Vector3 delta) => new Vector2(delta.x, delta.z).magnitude;
        static IEnumerator Wait(Func<bool> condition, float seconds, string reason, Action<bool, float> beforeCheck = null)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
            bool complete = condition();
            beforeCheck?.Invoke(complete, end); // Optional observation only; same condition calls/deadline and original Check outcome.
            Check(complete, reason);
        }
        static void Clear() { GameInput.SimMove = Vector2.zero; GameInput.SimSprint = GameInput.SimJump = GameInput.SimInteract = GameInput.SimLeave = GameInput.SimSplit = GameInput.SimSwitch = false; }
        static IEnumerator Split() { GameInput.SimSplit = true; yield return null; GameInput.SimSplit = false; yield return null; }
        static IEnumerator Switch() { var old = PM.Active; GameInput.SimSwitch = true; yield return null; GameInput.SimSwitch = false; yield return null; Check(PM.Active != old, "NormalTab selects the other registered body."); }
        static IEnumerator Ready()
        {
            yield return null; yield return Wait(() => CityDirector.Instance && CityDirector.Instance.Ready, 30, "Actual world/nav/spawn construction completes before input.");
            yield return null; yield return null; GameInput.Simulated = true; Clear();
        }
        static IEnumerator Reload() { var previous = GameManager.Instance; yield return Wait(() => GameManager.Instance != previous, 20, "Checkpoint actually replaces the manager."); yield return Ready(); Time.timeScale = 1; }
        static void Seed23(EndingOutcome outcome, string omitted = null)
        {
            if (omitted != "outcome") RunState.RecordLakesideOutcome(outcome);
            foreach (string flag in new[] { MissionFlags.Escaped, AfterlakeCampaign.Started, AfterlakeCampaign.Departed, IslandCampaign.Started, IslandCampaign.ArrivalSeen, IslandRoster.Departed,
                HarborCampaign.Started, HarborCampaign.ArrivalSeen, HarborCampaign.CanalOpen, HarborCampaign.ManifestComplete, HarborCampaign.RelayComplete }) if (flag != omitted) RunState.Flags.Add(flag);
            foreach (string id in HarborCampaign.RequiredCargoIds) { string flag = HarborCampaign.DeliveredFlag(id); if (flag != omitted) RunState.Flags.Add(flag); }
            RunState.MissionIndex = 23; RunState.HasCheckpoint = true; RunState.CheckpointPosition = HarborLayout.ManifestCheckpoint; RunState.CheckpointName = "Prepared completed harbor";
        }
        public static IEnumerator ContinuationRules()
        {
            yield return AutoTest.LoadCity(); Check(!GameManager.Instance.ContinueRail(), "An ordinary city cannot skip to Northline.");
            foreach (var outcome in new[] { EndingOutcome.Peace, EndingOutcome.Dominion })
            {
                RunState.NewGame(); Seed23(outcome);
                var missingFlags = new[] { "outcome", MissionFlags.Escaped, AfterlakeCampaign.Started, AfterlakeCampaign.Departed,
                    IslandCampaign.Started, IslandRoster.Departed, HarborCampaign.Started, HarborCampaign.CanalOpen,
                    HarborCampaign.ManifestComplete, HarborCampaign.RelayComplete }
                    .Concat(HarborCampaign.RequiredCargoIds.Select(HarborCampaign.DeliveredFlag)).ToArray();
                foreach (string missing in missingFlags)
                { RunState.NewGame(); Seed23(outcome, missing); Check(!RunState.BeginRail(), "Missing " + outcome + " prior prerequisite rejects continuation: " + missing); }
            }
            yield return Enter(EndingOutcome.Peace);
            Check(!GameManager.Instance.ContinueRail() && !RunState.BeginRail() && !GameManager.Instance.FinishRail(), "One-shot continuation cannot restart or directly complete Northline.");
            Check(!RunState.RecordLakesideOutcome(EndingOutcome.Dominion), "The first lake outcome remains immutable.");
        }
        public static IEnumerator Enter(EndingOutcome outcome, bool continueCurrentSummary = false)
        {
            if (!continueCurrentSummary)
            {
                yield return AutoTest.LoadCity(); RunState.NewGame(); Seed23(outcome); SceneManager.LoadScene(GameManager.CityScene); yield return Ready();
                Check(MissionManager.Instance.Missions.Count == 23 && !RunState.RailStarted, "Northline remains absent before explicit Continue.");
                yield return Split(); Check(PM.Agents.Count == 2 && PM.Agents.All(RailCampaign.Free), "Prepared prior summary retains two actual valid free bodies.");
                typeof(GameManager).GetProperty(nameof(GameManager.State), BindingFlags.Instance | BindingFlags.Public).GetSetMethod(true).Invoke(GameManager.Instance, new object[] { GameState.Ended });
                Time.timeScale = 0; GameEvents.RaiseEnding(new EndingData { outcome = outcome, route = RunState.Route, timeSeconds = RunState.PlayTime, captures = RunState.Captures });
            }
            else
            {
                Check(GameManager.Instance && GameManager.Instance.State == GameState.Ended && RunState.MissionIndex == 23
                    && MissionManager.Instance && MissionManager.Instance.Missions.Count == 23 && RunState.LakesideOutcome == outcome
                    && RunState.Has(MissionFlags.Escaped) && RunState.AfterlakeStarted && RunState.Has(AfterlakeCampaign.Departed)
                    && RunState.IslandStarted && RunState.Has(IslandRoster.Departed) && RunState.HarborStarted
                    && RunState.Has(HarborCampaign.CanalOpen) && RunState.Has(HarborCampaign.ManifestComplete)
                    && HarborCampaign.RequiredCargoDelivered && RunState.Has(HarborCampaign.RelayComplete)
                    && PM && PM.Agents.Count == 2 && PM.Agents.All(RailCampaign.Free) && !RunState.RailStarted
                    && !RunState.Has(RailCampaign.CrossingPassed) && !RunState.Has(RailCampaign.FreightArrived)
                    && !RunState.Has(RailCampaign.SignalReleased) && !RunState.Has(RailCampaign.Departed),
                    "Continuous Northline entry requires the actual earned M23 summary, complete branch cargo/relay and two valid free bodies, without seeded Northline progress.");
            }
            yield return new WaitForSecondsRealtime(.9f);
            var button = HUDController.Instance.GetComponentsInChildren<Button>(true).Single(b => b.name == "Btn_Continue · Northline");
            Check(button.gameObject.activeInHierarchy && button.IsInteractable(), (continueCurrentSummary ? "Actual earnedM23" : "Actual preparedM23") + " summary offers its Northline button.");
            var old = new HashSet<string>(RunState.Flags); var route = RunState.Route;
            float playTime = RunState.PlayTime, peakSuspicion = RunState.PeakSuspicion;
            int hosts = RunState.HostsPossessed, captures = RunState.Captures, resets = RunState.Resets;
            var previousManager = GameManager.Instance; var previousDirector = CityDirector.Instance;
            button.onClick.Invoke();
            yield return Wait(() => GameManager.Instance && GameManager.Instance != previousManager
                && CityDirector.Instance && CityDirector.Instance != previousDirector && CityDirector.Instance.Ready, 30,
                "The real summary Continue replaces the actual M23 manager/director with the Northline scene.");
            yield return Ready();
            yield return Wait(() => GameManager.Instance.IsPlaying && RunState.Has(RailCampaign.ArrivalSeen), RailArrival.PresentationSeconds + 3, "Finite protected arrival hands back control.");
            Check(RunState.RailStarted && RunState.MissionIndex == 23 && MissionManager.Instance.Missions.Count == 26 && old.IsSubsetOf(RunState.Flags), "Only missions24–26 append, preserving prior earned flags.");
            Check(RunState.Route == route && RunState.LakesideOutcome == outcome && RunState.PlayTime >= playTime && RunState.PeakSuspicion >= peakSuspicion
                && RunState.HostsPossessed == hosts && RunState.Captures == captures && RunState.Resets == resets,
                "M23→Northline Continue preserves first route/outcome/statistics without borrowing a role, capture or reset at this boundary.");
            Check(GameObject.Find(RailLayout.RootName) && !GameObject.Find(HarborLayout.RootName) && !GameObject.Find(IslandDistrict.RootName), "Only the current runtime annex exists.");
            Check(PM.Agents.Count == 1 && RailCampaign.Free(PM.Active) && Flat(PM.Active.Position - RailLayout.Spawn) < 1.5f
                && RunState.CurrentChapterSpawn == RailLayout.Spawn && !RunState.Has(RailCampaign.CrossingPassed) && !RunState.Has(RailCampaign.FreightArrived)
                && !RunState.Has(RailCampaign.SignalReleased) && !RunState.Has(RailCampaign.Departed), "No Northline completion is granted by Continue; the chapter starts with one valid free body at its real landing.");
            Debug.Log(continueCurrentSummary
                ? "[Northline] CONTINUOUS earnedM23 → realContinue, no prior flag/summary/role/split/seed/reset fixture at this boundary; later initial role ownership remains declared."
                : "[Northline] FIXTURE prior23 summary/flags and initialroleownership only; Northline progress uses actualinput/physics, no actorwarps/artificialcalm.");
        }
        public static IEnumerator RunChapter(EndingOutcome outcome, bool recovery, bool continueCurrentSummary = false)
        {
            yield return Enter(outcome, continueCurrentSummary); yield return Crossing(recovery); yield return Freight(recovery); yield return Signal(recovery); yield return Departure(recovery);
            Check(GameManager.Instance.State == GameState.Ended && RunState.MissionIndex == 26 && MissionManager.Instance.Missions.Count == 26 && RunState.LakesideOutcome == outcome
                && PM.Agents.Count == 2 && PM.Agents.All(RailCampaign.Free), "Only26 implemented missions finish with the immutable outcome and both real free bodies.");
            string headline = outcome == EndingOutcome.Peace ? "THE NORTHLINE OPENS." : "THE NEXT CITY HEARS.";
            Check(HUDController.Instance.GetComponentsInChildren<Text>(true).Any(t => t.gameObject.activeInHierarchy && t.text == headline), "Actual final summary shows the earned Northline branch.");
            Check(!HUDController.Instance.GetComponentsInChildren<Button>(true).Single(b => b.name == "Btn_Continue · Northline").gameObject.activeSelf, "Completed Northline cannot offer its old Continue action.");
            Debug.Log("[Northline] PASS " + outcome + (continueCurrentSummary ? " earnedM23" : " preparedM23") + " → realContinue → interlockedphysicalcrossing → realfreightarrival → host/freecrank+duct → bothfreeaboard → actualcoachendpoint");
        }
        static IEnumerator Crossing(bool recovery)
        {
            var crossing = RailCrossing.Instance; var control = Control(RailWork.Crossing);
            yield return Walk(RailLayout.CrossingControl + Vector3.back * 1.4f); yield return Press(control, false);
            Check(!crossing.Requested, "Free body cannot credential a crossing."); Take(Profession.Maintenance);
            yield return Walk(RailLayout.CrossingControl + Vector3.back * 1.4f); yield return Press(control, true);
            if (recovery)
            {
                GameManager.Instance.SetPaused(true); Vector3 position = crossing.tram.position; float warning = crossing.WarningElapsed, gate = crossing.GateFraction;
                yield return new WaitForSecondsRealtime(.6f); Check(Vector3.Distance(position, crossing.tram.position) < .001f && warning == crossing.WarningElapsed && gate == crossing.GateFraction, "Pause freezes tram physics, warning and gates.");
                GameManager.Instance.SetPaused(false); yield return Wait(() => crossing.WindowRemaining > 0, 25, "Actual car clears before green.");
                yield return Wait(() => crossing.ExpiredRequests == 1 && !crossing.Requested && crossing.GateFraction <= .001f, RailCrossing.WindowSeconds + 3, "Unused green expires and safely closes.");
                Check(!RunState.Has(RailCampaign.CrossingPassed), "Waiting south grants no crossing credit."); yield return Press(control, true);
            }
            yield return Wait(() => crossing.WindowRemaining > 0 && crossing.GateFraction >= .999f && crossing.CarClear, 25, "Fresh actual green and raised physical barriers.");
            var expected = PM.Active; var body = expected.Body; yield return Walk(RailLayout.At(0, .15f, -24)); yield return Walk(RailLayout.At(0, .15f, -8));
            yield return Wait(() => RunState.Has(RailCampaign.CrossingPassed), 1, "Full same requesting capsule crosses north.");
            Check(ReferenceEquals(expected, PM.Active) && ReferenceEquals(body, PM.Active.Body) && RunState.MissionIndex == 24 && crossing.CarClear, "No substituted body or clock can replace the crossing.");
        }
        static IEnumerator Freight(bool recovery)
        {
            // The grille's sight gap is narrower than the generic .25m arrival sphere. Reach the same
            // authored viewing point precisely; keep the original path budget and the real seal LOS gate.
            Take(Profession.Scientist); yield return Walk(RailLayout.ManifestControl + Vector3.back * 1.4f, .05f); yield return Press(Control(RailWork.Manifest), true);
            Check(RunState.Has(RailCampaign.Validated) && !RunState.Has(RailCampaign.FreightArrived), "Seal validation is not delivery.");
            Take(Profession.Maintenance); yield return Walk(RailLayout.DispatchControl + Vector3.back * 1.4f); var freight = RailFreight.Instance;
            if (recovery)
            {
                Check(!freight.PointsCorrect && !freight.ARight && !freight.BRight, "LEFT/LEFT is a real wrong route for either branch."); yield return Press(Control(RailWork.Dispatch), true);
                float observedStart = Time.realtimeSinceStartup, observedSimStart = Time.time, nextObservation = observedStart;
                var expectedCarriage = freight.carriage;
                bool Returned()
                {
                    bool complete = freight.FailedRoutes == 1 && !freight.Moving && freight.AtLoading;
                    if (Time.realtimeSinceStartup >= nextObservation)
                    {
                        LogWrongRouteFreight("PROGRESS", freight, expectedCarriage, complete, observedStart, observedSimStart, float.NaN);
                        nextObservation = Time.realtimeSinceStartup + 4f;
                    }
                    return complete;
                }
                yield return Wait(Returned, 70, "Wrong routing physically returns the same carriage to loading.",
                    (complete, actualDeadline) => LogWrongRouteFreight("FINAL_BEFORE_CHECK", freight, expectedCarriage, complete, observedStart, observedSimStart, actualDeadline));
                Check(!RunState.Has(RailCampaign.FreightArrived), "Wrong return earns no arrival.");
            }
            if (freight.ARight != RailCampaign.RequiredA) { yield return Walk(RailLayout.PointControls[0] + Vector3.back * 1.4f); yield return Press(Control(RailWork.PointA), true); yield return Wait(() => freight.PointsSettled, 2, "ActualA blade settles."); }
            if (freight.BRight != RailCampaign.RequiredB) { yield return Walk(RailLayout.PointControls[1] + Vector3.back * 1.4f); yield return Press(Control(RailWork.PointB), true); yield return Wait(() => freight.PointsSettled, 2, "ActualB blade settles."); }
            yield return Walk(RailLayout.DispatchControl + Vector3.back * 1.4f);
            if (recovery)
            {
                yield return Press(Control(RailWork.Dispatch), true); yield return Wait(() => Vector3.Distance(freight.carriage.position, RailLayout.FreightDock) > 3, 5, "Actual travel starts before recovery.");
                GameManager.Instance.ResetToCheckpoint("Northline in-flight acceptance"); yield return Reload(); freight = RailFreight.Instance;
                Check(RunState.Has(RailCampaign.Validated) && !RunState.Has(RailCampaign.FreightArrived) && freight.AtLoading && !freight.Moving && freight.PointsCorrect && freight.PointsSettled,
                    "Checkpoint keeps seal/points, restores one docked carriage and clears unearned transit.");
                Check(Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Count(b => b.name == "Northline actual sealed transfer cradle") == 1, "No duplicated physical carriage.");
                Take(Profession.Maintenance); yield return Walk(RailLayout.DispatchControl + Vector3.back * 1.4f);
            }
            GameObject blocker = null;
            try
            {
                if (recovery) { blocker = new GameObject("Northline acceptance freight wall"); blocker.transform.position = RailLayout.At(-15, .9f, 6); blocker.AddComponent<BoxCollider>().size = new Vector3(.3f, 1.5f, 3); Physics.SyncTransforms(); }
                yield return Press(Control(RailWork.Dispatch), true);
                if (recovery)
                {
                    var body = freight.carriage; var wall = blocker.GetComponent<Collider>();
                    yield return Wait(() => body.position.x > RailLayout.Origin.x - 19 && body.SweepTest(Vector3.right, out var hit, .09f, QueryTriggerInteraction.Ignore) && hit.collider == wall, 8, "Actual carriage physically stops at owned fixture wall.");
                    Vector3 stopped = body.position; float end = Time.time + 24;
                    while (Time.time < end) { Check(Vector3.Distance(stopped, body.position) < .001f && !RunState.Has(RailCampaign.FreightArrived), "Blocked carriage never tunnels or gains timed arrival."); yield return null; }
                    Object.Destroy(blocker); blocker = null; yield return null;
                }
                yield return Wait(() => RunState.Has(RailCampaign.FreightArrived), 40, "Real carriage reaches correct branch bay.");
                Check(Vector3.Distance(freight.carriage.position, RailLayout.RequiredBay) < .03f && Vector3.Distance(freight.carriage.transform.position, RailLayout.RequiredBay) < .03f && RunState.MissionIndex == 25, "Actual body/displayed bay arrival precedes mission26.");
            }
            finally { if (blocker) Object.Destroy(blocker); }
        }
        // Bounded wrong-route observation only. Never call MovePhysical, change travel, or modify scene colliders.
        static void LogWrongRouteFreight(string phase, RailFreight freight, Rigidbody expectedCarriage, bool originalPredicate, float beganReal, float beganSim, float actualWaitDeadline)
        {
            try
            {
                string PathOf(Transform value)
                {
                    if (!value) return "unavailable";
                    string path = value.name; for (var parent = value.parent; parent; parent = parent.parent) path = parent.name + "/" + path;
                    return path + "#" + value.GetInstanceID();
                }
                object Field(string name) => freight ? typeof(RailFreight).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(freight) : null;
                var body = freight ? freight.carriage : null;
                var shape = freight ? freight.carriageCollider : null;
                var route = Field("route") is List<Vector3> points ? points.ToArray() : Array.Empty<Vector3>();
                int leg = Field("leg") is int legValue ? legValue : -1;
                object validDispatch = Field("validDispatch");
                bool legValid = body && leg >= 0 && leg < route.Length;
                Vector3 privateTarget = legValid ? route[leg] : Vector3.zero;
                bool reachesPrivateLeg = legValid && Vector3.Distance(body.position, privateTarget) <= .015f && Vector3.Distance(body.transform.position, privateTarget) <= .015f;
                int movementLeg = reachesPrivateLeg && leg < route.Length - 1 ? leg + 1 : leg;
                bool terminalLeg = reachesPrivateLeg && leg == route.Length - 1;
                Vector3 movementTarget = body && movementLeg >= 0 && movementLeg < route.Length ? route[movementLeg] : Vector3.zero;
                bool physical = freight && freight.Physical;
                bool movementGuard = physical && freight.Moving && RailCampaign.Active && GameManager.Instance && GameManager.Instance.IsPlaying;
                Vector3 destination = body && legValid && !terminalLeg ? Vector3.MoveTowards(body.position, movementTarget, RailFreight.Speed * Time.fixedDeltaTime) : body ? body.position : Vector3.zero;
                Vector3 delta = body ? destination - body.position : Vector3.zero;
                float distance = delta.magnitude;
                Vector3 direction = distance > .00001f ? delta / distance : Vector3.zero;
                // Same actual Rigidbody query and .005m look-ahead as RailFreight.MovePhysical, read-only.
                var hits = body && distance > .00001f ? body.SweepTestAll(direction, distance + .005f, QueryTriggerInteraction.Ignore).OrderBy(hit => hit.distance).ToArray() : Array.Empty<RaycastHit>();
                bool Self(RaycastHit hit) => body && hit.transform && hit.transform.IsChildOf(body.transform);
                bool Blocks(RaycastHit hit) => hit.collider && !Self(hit) && hit.distance <= distance + .005f && Vector3.Dot(hit.normal, direction) < -.1f;
                string Hit(RaycastHit hit, int index) => index + ":collider=" + PathOf(hit.collider ? hit.collider.transform : null)
                    + " type=" + (hit.collider ? hit.collider.GetType().Name : "unavailable") + " layer=" + (hit.collider ? hit.collider.gameObject.layer.ToString() : "unavailable")
                    + " hitTransform=" + PathOf(hit.transform) + " attachedBody=" + PathOf(hit.rigidbody ? hit.rigidbody.transform : null)
                    + " bounds=" + (hit.collider ? hit.collider.bounds.ToString("F5") : "unavailable")
                    + " distance=" + hit.distance.ToString("F6") + " point=" + hit.point.ToString("F5") + " normal=" + hit.normal.ToString("F5")
                    + " normalDotDirection=" + Vector3.Dot(hit.normal, direction).ToString("F6") + " selfByProduction=" + Self(hit)
                    + " colliderBelowCarriage=" + (body && hit.collider && hit.collider.transform.IsChildOf(body.transform)) + " blocksByProduction=" + Blocks(hit);
                var agent = PM ? PM.Active : null;
                var text = new System.Text.StringBuilder();
                text.Append("[NorthlineFreightWrongRoute] phase=").Append(phase).Append(" originalPredicate=").Append(originalPredicate)
                    .Append(" elapsedReal=").Append((Time.realtimeSinceStartup - beganReal).ToString("F5")).Append(" elapsedSim=").Append((Time.time - beganSim).ToString("F5"))
                    .Append(" frame=").Append(Time.frameCount).Append(" realNow=").Append(Time.realtimeSinceStartup.ToString("F5"))
                    .Append(" time=").Append(Time.time.ToString("F5")).Append(" fixedTime=").Append(Time.fixedTime.ToString("F5"))
                    .Append(" fixedDt=").Append(Time.fixedDeltaTime.ToString("F6")).Append(" timeScale=").Append(Time.timeScale)
                    .Append(" originalBudget=70 actualWaitDeadline=").Append(float.IsNaN(actualWaitDeadline) ? "recorded at final callback" : actualWaitDeadline.ToString("F5"))
                    .Append(" manager=").Append(GameManager.Instance ? GameManager.Instance.State.ToString() : "unavailable").Append(" railActive=").Append(RailCampaign.Active)
                    .Append(" mission=").Append(RunState.MissionIndex).Append(" validated=").Append(RunState.Has(RailCampaign.Validated)).Append(" arrivedFlag=").Append(RunState.Has(RailCampaign.FreightArrived))
                    .Append(" coordinator=").Append(freight ? PathOf(freight.transform) : "unavailable").Append(" coordinatorEnabled=").Append(freight && freight.isActiveAndEnabled)
                    .Append(" body=").Append(body ? PathOf(body.transform) : "unavailable").Append(" expectedBody=").Append(expectedCarriage ? PathOf(expectedCarriage.transform) : "unavailable")
                    .Append(" exactBodyReference=").Append(object.ReferenceEquals(body, expectedCarriage)).Append(" physical=").Append(physical)
                    .Append(" moving=").Append(freight && freight.Moving).Append(" returning=").Append(freight && freight.Returning).Append(" atLoading=").Append(freight && freight.AtLoading)
                    .Append(" failedRoutes=").Append(freight ? freight.FailedRoutes.ToString() : "unavailable").Append(" validDispatch=").Append(validDispatch ?? "unavailable")
                    .Append(" ARight=").Append(freight && freight.ARight).Append(" BRight=").Append(freight && freight.BRight).Append(" pointsSettled=").Append(freight && freight.PointsSettled)
                    .Append(" rbAt=").Append(body ? body.position.ToString("F5") : "unavailable").Append(" transformAt=").Append(body ? body.transform.position.ToString("F5") : "unavailable")
                    .Append(" rbLocal=").Append(body ? (body.position - RailLayout.Origin).ToString("F5") : "unavailable")
                    .Append(" velocity=").Append(body ? body.linearVelocity.ToString("F5") : "unavailable").Append(" angularVelocity=").Append(body ? body.angularVelocity.ToString("F5") : "unavailable")
                    .Append(" interpolation=").Append(body ? body.interpolation.ToString() : "unavailable").Append(" kinematic=").Append(body && body.isKinematic).Append(" detectCollisions=").Append(body && body.detectCollisions)
                    .Append(" boxCenter=").Append(shape ? shape.bounds.center.ToString("F5") : "unavailable").Append(" boxHalfExtents=").Append(shape ? shape.bounds.extents.ToString("F5") : "unavailable")
                    .Append(" boxLocalCenter=").Append(shape ? shape.center.ToString("F5") : "unavailable").Append(" boxLocalSize=").Append(shape ? shape.size.ToString("F5") : "unavailable")
                    .Append(" privateLeg=").Append(leg).Append('/').Append(route.Length).Append(" privateTarget=").Append(privateTarget.ToString("F5"))
                    .Append(" privateReachedByProduction=").Append(reachesPrivateLeg).Append(" nextMovementLeg=").Append(movementLeg).Append(" terminalLeg=").Append(terminalLeg)
                    .Append(" movementGuard=").Append(movementGuard).Append(" nextMovementTarget=").Append(movementTarget.ToString("F5"))
                    .Append(" nextDestination=").Append(destination.ToString("F5")).Append(" exactStepDirection=").Append(direction.ToString("F6"))
                    .Append(" exactStepDistance=").Append(distance.ToString("F6")).Append(" sweepDistanceWithLookAhead=").Append((distance + .005f).ToString("F6"))
                    .Append(" route=[").Append(string.Join("; ", route.Select((point, index) => index + ":" + point.ToString("F4") + " local=" + (point - RailLayout.Origin).ToString("F4")))).Append(']')
                    .Append(" activeAgent=").Append(agent == null ? "unavailable" : agent.Id.ToString()).Append(" activeBody=").Append(agent?.Body == null ? "unavailable" : PathOf(agent.Body.Root))
                    .Append(" activeAt=").Append(agent == null ? "unavailable" : agent.Position.ToString("F5"))
                    .Append(" sweepCount=").Append(hits.Length).Append(" blockingCount=").Append(hits.Count(Blocks)).Append(" sortedExactProductionSweep=[")
                    .Append(string.Join("; ", hits.Select(Hit))).Append(']');
                Debug.Log(text.ToString());
            }
            catch (Exception error) { Debug.Log("[NorthlineFreightWrongRoute] DIAGNOSTIC_ERROR type=" + error.GetType().Name + " message=" + error.Message); }
        }

        static IEnumerator Signal(bool recovery)
        {
            Take(Profession.Maintenance); yield return Walk(RailLayout.Crank, .08f); var departure = RailDeparture.Instance;
            yield return Press(Control(RailWork.Crank), false); Check(!departure.Holding, "One body cannot start a two-operator crank.");
            yield return Split(); yield return Press(Control(RailWork.Crank), true); Check(departure.Holding, "NormalE starts actual maintenance hold.");
            yield return Switch(); Check(PM.Active.IsFree, "Other split body is really free."); yield return Wait(() => departure.DuctFraction >= .999f, 2, "Real low gate opens.");
            yield return Walk(RailLayout.At(-4, .15f, 34)); foreach (var point in RailLayout.CulvertRoute) yield return DirectFreeWalk(point); yield return Walk(RailLayout.Signal, .08f);
            var control = Control(RailWork.Signal);
            if (recovery)
            {
                yield return Press(control, true); yield return Wait(() => departure.Progress > .2f, 1, "Actual signal work begins."); GameManager.Instance.SetPaused(true); float progress = departure.Progress;
                yield return new WaitForSecondsRealtime(.6f); Check(progress == departure.Progress && departure.Signalling && departure.Holding, "Pause freezes operators and signal."); GameManager.Instance.SetPaused(false);
                Camera.SetYaw(0); GameInput.SimMove = Vector2.left; yield return new WaitForSeconds(.12f); Clear(); yield return Wait(() => !departure.Signalling, 1, "Real movement cancels.");
                Check(departure.Progress == 0 && !RunState.Has(RailCampaign.SignalReleased), "Cancelled progress is discarded."); yield return Walk(RailLayout.Signal, .08f);
                yield return Press(control, true); yield return Wait(() => departure.Progress > .2f, 1, "FreshE creates new attempt.");
                var blocker = new GameObject("Northline acceptance receiver obstruction"); blocker.transform.position = Vector3.Lerp(PM.Active.Blob.Center, departure.receiver.position, .75f); blocker.AddComponent<BoxCollider>().size = Vector3.one * .06f; Physics.SyncTransforms();
                try { yield return Wait(() => !departure.Signalling, 1, "Physical receiver obstruction cancels the work."); Check(departure.Progress == 0 && !RunState.Has(RailCampaign.SignalReleased), "Obstructed line earns no signal."); }
                finally { Object.Destroy(blocker); }
                yield return null;
            }
            yield return Press(control, true); Check(departure.Signalling && departure.Progress < .2f, "Fresh normalE starts zero progress.");
            yield return Wait(() => RunState.Has(RailCampaign.SignalReleased) && departure.PlatformFraction >= .999f, 6, "Three actual seconds release the real platform and portal.");
            Check(!RunState.Has(RailCampaign.Departed), "Open signal does not depart a coach.");
        }
        static IEnumerator PrepareBoardingForDeparture()
        {
            var departure = RailDeparture.Instance; yield return Board(0); yield return Press(Control(RailWork.Departure), false);
            Check(!departure.DepartureAuthorized && !RunState.Has(RailCampaign.Departed), "An occupied second host denies departure."); yield return Switch(); Check(PM.Active.Host, "Actual maintenance host remains before release.");
            GameInput.SimLeave = true; yield return null; GameInput.SimLeave = false; yield return null; Check(PM.Active.IsFree, "NormalQ releases final host.");
            yield return Walk(RailLayout.At(-4, .15f, 49)); yield return Board(1); yield return Wait(() => departure.ReadyToDepart(out _), 2, "Two actual free capsules are grounded on the .75m coach deck and fully aboard.");
            Check(!GameManager.Instance.FinishRail(), "Even valid boarding requires a fresh authorizedE.");
        }
        static IEnumerator Departure(bool recovery)
        {
            yield return PrepareBoardingForDeparture(); var departure = RailDeparture.Instance;
            var positions = PM.Agents.Select(a => a.Position).ToArray(); Vector3 start = departure.Coach.position; GameObject barrier = null;

            try
            {
                if (recovery)
                {
                    barrier = new GameObject("Northline acceptance blocked departure"); barrier.transform.position = RailLayout.At(8, 1.6f, 56.2f); barrier.AddComponent<BoxCollider>().size = new Vector3(4.2f, 3.4f, .2f); Physics.SyncTransforms();
                    yield return Press(Control(RailWork.Departure), true); yield return Wait(() => GameManager.Instance.IsPlaying && !departure.DepartureAuthorized, 5, "Real obstructed departure rolls back and needs freshE.");
                    Check(!RunState.Has(RailCampaign.Departed) && RunState.MissionIndex == 25 && Vector3.Distance(departure.Coach.position, start) < .005f, "Unmoved/interrupted coach gains no mission26 credit.");
                    for (int i = 0; i < PM.Agents.Count; i++) Check(PM.Agents[i].Blob.Controller.enabled && Vector3.Distance(PM.Agents[i].Position, positions[i]) < .03f, "Exact captured boarder pose/control restores after interruption.");
                    Object.Destroy(barrier); barrier = null; yield return null; yield return Wait(() => departure.ReadyToDepart(out _), 2, "Restored boarders settle for freshE.");
                }
                yield return Press(Control(RailWork.Departure), true); Check(GameManager.Instance.State == GameState.Ending && !RunState.Has(RailCampaign.Departed), "E begins protected actual movement without early credit.");
                GameInput.SimInteract = true; yield return null; GameInput.SimInteract = false; yield return null;
                Check(!RunState.Has(RailCampaign.Departed) && Vector3.Distance(departure.Coach.position, start) < RailResolution.TravelMetres - .1f, "Skip cannot teleport or credit an unmoved coach.");
                yield return Wait(() => GameManager.Instance.State == GameState.Ended, RailResolution.MaximumSeconds + 2, "Only actual endpoint publishes finite summary.");
                Check(Vector3.Distance(departure.Coach.position, start + Vector3.forward * RailResolution.TravelMetres) < .005f, "Physical coach travels full8m.");
                for (int i = 0; i < PM.Agents.Count; i++) Check(PM.Agents[i].Blob.Controller.enabled && Vector3.Distance(PM.Agents[i].Position, positions[i] + Vector3.forward * RailResolution.TravelMetres) < .015f, "Real free boarder travels with coach and restores control at endpoint.");
            }
            finally { if (barrier) Object.Destroy(barrier); }
        }
        static IEnumerator EarnLifecycleBoarding()
        {
            yield return Enter(EndingOutcome.Peace); yield return Crossing(false); yield return Freight(false); yield return Signal(false);
            yield return PrepareBoardingForDeparture();
        }
        public static IEnumerator ActualCoordinatorDisableCancelsTrip()
        {
            yield return EarnLifecycleBoarding(); var owner = GameManager.Instance; var departure = RailDeparture.Instance;
            var coach = departure.Coach; Vector3 start = coach.position; var agents = PM.Agents.ToArray(); var bodies = agents.Select(a => a.Blob).ToArray(); var positions = agents.Select(a => a.Position).ToArray();
            yield return Press(Control(RailWork.Departure), true);
            yield return Wait(() => RailResolution.Instance && RailResolution.Instance.Running && RailResolution.Instance.Travelled > .25f && RailResolution.Instance.Travelled < RailResolution.TravelMetres - .5f, 2, "The real coach has moved partway before actual owner disable.");
            departure.enabled = false;
            yield return Wait(() => owner == GameManager.Instance && owner.IsPlaying && !departure.DepartureAuthorized && !RailResolution.Instance, 2,
                "Actual component disable cancels the owned trip, rolls back and resumes its captured manager instead of freezing Ending.");
            Check(!RunState.Has(RailCampaign.Departed) && RunState.MissionIndex == 25 && Vector3.Distance(coach.position, start) < .005f, "Disabled coordinator cannot publish an interrupted/unmoved coach.");
            for (int i = 0; i < agents.Length; i++) Check(PM.Agents.Contains(agents[i]) && agents[i].Blob == bodies[i] && bodies[i].Controller.enabled && Vector3.Distance(agents[i].Position, positions[i]) < .03f,
                "The same actual boarder body/pose/control returns after coordinator retirement.");
            yield return Press(Control(RailWork.Departure), false);
            departure.enabled = true; yield return null;
            yield return Wait(() => departure.ReadyToDepart(out _), 2, "Re-enabled hardware and actual deck footing permit a fresh E attempt.");
            yield return CompleteFreshBoardingTrip();
            Debug.Log("[Northline] PASS actual mid-motion coordinator disable → owned rollback/no credit → real re-enable → fresh E/full endpoint");
        }
        public static IEnumerator ActualCoordinatorDestroyUsesCheckpointRecovery()
        {
            yield return EarnLifecycleBoarding(); var oldManager = GameManager.Instance; var departure = RailDeparture.Instance; var coach = departure.Coach;
            var oldAgents = PM.Agents.ToArray(); Vector3 start = coach.position;
            yield return Press(Control(RailWork.Departure), true);
            yield return Wait(() => RailResolution.Instance && RailResolution.Instance.Running && RailResolution.Instance.Travelled > .25f && RailResolution.Instance.Travelled < RailResolution.TravelMetres - .5f, 2,
                "Actual moving departure exists before destroying only its coordinator component.");
            Object.Destroy(departure); yield return null;
            yield return Wait(() => oldManager != GameManager.Instance || oldManager.State == GameState.Resetting, 2,
                "Permanent coordinator destruction requires a normal checkpoint even if OnDisable first completed a recoverable rollback.");
            Check(!departure && !RunState.Has(RailCampaign.Departed) && RunState.MissionIndex == 25, "The real coordinator component is destroyed and cannot publish credit.");
            if (oldManager == GameManager.Instance)
                Check(coach && Vector3.Distance(coach.position, start) < .005f && oldAgents.All(RailCampaign.Free),
                    "Component destruction restores the actual coach/boarders before normal recovery; it does not destroy their objects.");
            yield return Wait(() => GameManager.Instance != oldManager, 20, "Normal checkpoint reconstructs the missing coordinator."); yield return Ready();
            Check(GameManager.Instance.IsPlaying && RailDeparture.Instance && !RailDeparture.Instance.PermanentlyRetired && PM.Agents.Count == 1 && RailCampaign.Free(PM.Active)
                && oldAgents.All(a => !PM.Agents.Contains(a)) && RunState.Has(RailCampaign.FreightArrived) && RunState.Has(RailCampaign.SignalReleased) && !RunState.Has(RailCampaign.Departed),
                "A new actual coordinator/body restores earned freight and signal without an unearned departure.");
            yield return Split(); yield return Walk(RailLayout.At(-4, .15f, 49)); yield return Board(0); yield return Switch(); yield return Walk(RailLayout.At(-4, .15f, 49)); yield return Board(1);
            yield return CompleteFreshBoardingTrip();
            Debug.Log("[Northline] PASS actual coordinator-component destruction → owned rollback → ordinary checkpoint/new coordinator → real boarding/fresh E/endpoint");
        }
        public static IEnumerator ActualCoachRebindCannotMoveReplacement()
        {
            yield return EarnLifecycleBoarding(); var oldManager = GameManager.Instance; var departure = RailDeparture.Instance; var originalCoach = departure.Coach;
            Vector3 originalStart = originalCoach.position; GameObject replacement = null;
            yield return Press(Control(RailWork.Departure), true);
            yield return Wait(() => RailResolution.Instance && RailResolution.Instance.Running && RailResolution.Instance.Travelled > .25f && RailResolution.Instance.Travelled < RailResolution.TravelMetres - .5f, 2,
                "Real owned coach and body snapshots exist before the actual external binding fixture.");
            try
            {
                replacement = new GameObject("Northline acceptance replacement coach"); replacement.transform.position = RailLayout.At(25, 0, 45); replacement.transform.rotation = Quaternion.Euler(0, 20, 0);
                var replacementBody = replacement.AddComponent<Rigidbody>(); replacementBody.isKinematic = true; replacementBody.detectCollisions = true; replacementBody.useGravity = false; replacementBody.interpolation = RigidbodyInterpolation.Extrapolate;
                var deck = new GameObject("Replacement actual physical deck"); deck.transform.SetParent(replacement.transform, false);
                var deckCollider = deck.AddComponent<BoxCollider>(); deckCollider.center = new Vector3(0, .67f, 0); deckCollider.size = new Vector3(3.2f, .16f, 14);
                Vector3 replacementStart = replacementBody.position; Quaternion replacementRotation = replacementBody.rotation;
                departure.carriage = replacement.transform;
                departure.SetTransitPhysics(replacementBody, new[] { GameObject.Find("Northline yard physical footing").GetComponent<Collider>(), GameObject.Find("Northline exit track physical footing").GetComponent<Collider>() }, deckCollider);
                yield return Wait(() => oldManager != GameManager.Instance || oldManager.State == GameState.Resetting, 2,
                    "Actual binding replacement cancels publication and uses normal chapter recovery.");
                Check(!RunState.Has(RailCampaign.Departed) && RunState.MissionIndex == 25, "A different coach cannot inherit old departure credit.");
                Check(replacementBody && Vector3.Distance(replacementBody.position, replacementStart) < .001f && Quaternion.Angle(replacementBody.rotation, replacementRotation) < .01f
                    && replacementBody.interpolation == RigidbodyInterpolation.Extrapolate,
                    "Rollback never moves, rotates or rewrites interpolation on the unrelated replacement coach.");
                if (oldManager == GameManager.Instance) Check(originalCoach && Vector3.Distance(originalCoach.position, originalStart) < .005f,
                    "Only the exact captured original coach is restored to its starting pose.");
                yield return Wait(() => GameManager.Instance != oldManager, 20, "Normal checkpoint reconstructs stable coach ownership."); yield return Ready();
                Check(GameManager.Instance.IsPlaying && RailDeparture.Instance && PM.Agents.Count == 1 && RailCampaign.Free(PM.Active) && RunState.Has(RailCampaign.SignalReleased)
                    && !RunState.Has(RailCampaign.Departed), "Recovery creates a real new owned coach/body and keeps only earned prior progress.");
            }
            finally { if (replacement) Object.Destroy(replacement); }
            Debug.Log("[Northline] PASS actual in-flight coach rebind → no oldcredit/no replacement mutation → original-only rollback → normal checkpoint");
        }
        public static IEnumerator ActualCoachDestroyUsesCheckpointRecovery()
        {
            yield return EarnLifecycleBoarding(); var oldManager = GameManager.Instance; var oldPossession = PM; var oldAgents = PM.Agents.ToArray(); var oldCoach = RailDeparture.Instance.Coach;
            yield return Press(Control(RailWork.Departure), true);
            yield return Wait(() => RailResolution.Instance && RailResolution.Instance.Running && RailResolution.Instance.Travelled > .25f && RailResolution.Instance.Travelled < RailResolution.TravelMetres - .5f, 2,
                "Actual moving coach and parented real blobs exist before the destruction fixture.");
            Object.Destroy(oldCoach.gameObject); yield return null;
            yield return Wait(() => oldManager != GameManager.Instance || oldManager.State == GameState.Resetting, 2,
                "Destroyed coach/boarders trigger normal checkpoint recovery from actual usable bodies, not the stale agent list count.");
            Check(!RunState.Has(RailCampaign.Departed) && RunState.MissionIndex == 25 && !oldCoach, "Actual coach destruction cannot publish departure credit.");
            if (oldPossession && oldPossession == PM) Check(!oldPossession.Agents.Any(RailCampaign.Free), "Destroyed parented blob references are not usable bodies even if list entries remain.");
            yield return Wait(() => GameManager.Instance != oldManager, 20, "Normal checkpoint recovery reloads the real scene."); yield return Ready();
            Check(GameManager.Instance.IsPlaying && PM.Agents.Count == 1 && RailCampaign.Free(PM.Active) && oldAgents.All(a => !PM.Agents.Contains(a))
                && RunState.Has(RailCampaign.FreightArrived) && RunState.Has(RailCampaign.SignalReleased) && !RunState.Has(RailCampaign.Departed) && RunState.MissionIndex == 25,
                "Checkpoint restores one real new free body and earned freight/signal, without stale agents or unearned departure.");
            yield return Split(); Check(PM.Agents.Count == 2 && PM.Agents.All(RailCampaign.Free), "Normal V creates the replacement pair after actual body loss.");
            yield return Walk(RailLayout.At(-4, .15f, 49)); yield return Board(0); yield return Switch(); yield return Walk(RailLayout.At(-4, .15f, 49)); yield return Board(1);
            yield return CompleteFreshBoardingTrip();
            Debug.Log("[Northline] PASS actual moving-coach destruction/real child-body loss → normal checkpoint/new registered bodies → real boarding/fresh E/full endpoint");
        }
        static IEnumerator CompleteFreshBoardingTrip()
        {
            var departure = RailDeparture.Instance; yield return Wait(() => departure.ReadyToDepart(out _), 2, "Both actual free boarders settle for a fresh physical trip.");
            Vector3 start = departure.Coach.position; yield return Press(Control(RailWork.Departure), true);
            Check(GameManager.Instance.State == GameState.Ending && !RunState.Has(RailCampaign.Departed), "Fresh E authorizes movement, never immediate departure credit.");
            yield return Wait(() => GameManager.Instance.State == GameState.Ended, RailResolution.MaximumSeconds + 2, "Only the real coach endpoint publishes the recovered trip.");
            Check(RunState.Has(RailCampaign.Departed) && RunState.MissionIndex == 26 && Vector3.Distance(departure.Coach.position, start + Vector3.forward * RailResolution.TravelMetres) < .005f
                && PM.Agents.Count == 2 && PM.Agents.All(RailCampaign.Free), "Recovered trip actually reaches its endpoint with both original-control free bodies.");
        }
        static IEnumerator Board(int stop)
        {
            yield return Walk(RailLayout.BoardingRampFoot, .12f);
            // Human navigation intentionally excludes the parked vehicle. The two real free capsules use the physical ramp/deck.
            yield return DirectFreeWalk(RailLayout.BoardingRampLanding);
            yield return DirectFreeWalk(RailLayout.BoardingStops[stop]);
            yield return Wait(() => PM.Active.Blob.Controller.isGrounded, 1, "The real boarder settles on the raised coach deck.");
            Check(PM.Active.Blob.Controller.bounds.min.y >= RailLayout.CoachDeckY - .02f && RailCampaign.FullyInside(PM.Active, RailLayout.Boarding),
                "Boarding credit requires actual .75m deck footing, not the .15m yard below the train.");
        }
        static void Take(Profession role)
        {
            if (PM.Active.Host && PM.Active.Host.Profession == role) return;
            var host = HostCharacter.All.Where(h => h && h.CanBePossessed && h.Profession == role && Mathf.Abs(h.transform.position.x - RailLayout.Origin.x) < 50).OrderBy(h => Vector3.Distance(h.transform.position, PM.Active.Position)).FirstOrDefault();
            Check(host, "Actual ordinary Northline role host exists."); PM.ForcePossessImmediate(host); Check(PM.Active.Host == host, "Declared initial-role fixture owns the real host; no objective granted.");
        }
        static IEnumerator Press(RailWorkControl control, bool allowed)
        {
            for (float t = 0; t < .45f; t += Time.unscaledDeltaTime) { Vector3 d = control.InteractPoint - Camera.Position; Camera.SetYaw(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg); Camera.SetPitch(-Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg); yield return null; }
            var target = PM.ComputeTarget();
            if (control.kind == RailWork.Manifest) LogManifestSight(control, allowed, target.kind.ToString(), target.allowed, target.prompt, ReferenceEquals(target.interactable, control));
            if (control.kind == RailWork.Departure) LogDepartureSight(control, allowed, target.kind.ToString(), target.allowed, target.prompt, ReferenceEquals(target.interactable, control));
            Check(target.kind == TargetKind.Interact && ReferenceEquals(target.interactable, control) && target.allowed == allowed, "RealE targets intended " + control.kind + "; got " + target.prompt);
            GameInput.SimInteract = true; yield return null; GameInput.SimInteract = false; yield return null;
        }
        // One observation per Manifest Press after aiming and before the unchanged assertion/E; no validation or movement change.
        static void LogManifestSight(RailWorkControl control, bool expectedAllowed, string targetKind, bool targetAllowed, string prompt, bool selectedControl)
        {
            try
            {
                var agent = PM ? PM.Active : null; var host = agent?.Host; var body = agent?.Body;
                var freight = RailFreight.Instance; var carriage = freight ? freight.carriage : null;
                var own = host ? host.transform : null; var permitted = carriage ? carriage.transform : null;
                Vector3 eye = host ? host.EyePosition : Vector3.zero, seal = carriage ? carriage.position + Vector3.up * 1.1f : Vector3.zero;
                Vector3 delta = seal - eye; float distance = delta.magnitude;
                var context = new InteractContext { agent = agent, host = host, position = agent != null ? agent.Position : Vector3.zero,
                    permissions = host ? host.Permissions : AccessPermission.None };
                string reason = "No RailFreight instance.";
                bool realAllowed = freight && freight.CanValidate(context, control, out reason);
                bool clearLine = own && permitted && RailCampaign.ClearLine(eye, seal, own, permitted);
                var hits = own && permitted && distance > .001f
                    ? Physics.RaycastAll(eye, delta / distance, distance, ~0, QueryTriggerInteraction.Ignore)
                        .OrderBy(h => h.distance).ThenBy(h => h.collider ? h.collider.GetInstanceID() : 0).ToArray()
                    : Array.Empty<RaycastHit>();
                bool Own(RaycastHit hit) => own && hit.transform && hit.transform.IsChildOf(own);
                bool Permitted(RaycastHit hit) => permitted && hit.transform && hit.transform.IsChildOf(permitted);
                string PathOf(Transform transform)
                {
                    if (!transform) return "unavailable";
                    string path = transform.name; for (var p = transform.parent; p; p = p.parent) path = p.name + "/" + path;
                    return path + "#" + transform.GetInstanceID();
                }
                var text = new System.Text.StringBuilder();
                text.Append("[NorthlineManifestLOS] ONCE expectedAllowed=").Append(expectedAllowed).Append(" selectedControl=").Append(selectedControl)
                    .Append(" targetKind=").Append(targetKind).Append(" targetAllowed=").Append(targetAllowed).Append(" prompt=").Append(prompt)
                    .Append(" actualCanValidate=").Append(realAllowed).Append(" reason=").Append(reason).Append(" clearLine=").Append(clearLine)
                    .Append(" actor=").Append(agent == null ? "null" : agent.Id.ToString()).Append(" actualBody=").Append(body == null ? "null" : PathOf(body.Root))
                    .Append(" bodyPosition=").Append(agent != null ? agent.Position.ToString("F6") : "unavailable")
                    .Append(" host=").Append(host ? host.DisplayName : "unavailable").Append(" profession=").Append(host ? host.Profession.ToString() : "unavailable")
                    .Append(" permissions=").Append(host ? host.Permissions.ToString() : "unavailable").Append(" hostState=").Append(host ? host.State.ToString() : "unavailable")
                    .Append(" reciprocalOwner=").Append(host && host.Possessor == agent).Append(" eye=").Append(eye.ToString("F6"))
                    .Append(" control=").Append(PathOf(control.transform)).Append(" controlPosition=").Append(control.transform.position.ToString("F6"))
                    .Append(" controlPoint=").Append(control.InteractPoint.ToString("F6")).Append(" carriage=").Append(PathOf(permitted))
                    .Append(" carriageBodyPosition=").Append(carriage ? carriage.position.ToString("F6") : "unavailable")
                    .Append(" carriageTransformPosition=").Append(permitted ? permitted.position.ToString("F6") : "unavailable")
                    .Append(" carriageBounds=").Append(freight && freight.carriageCollider ? freight.carriageCollider.bounds.ToString("F6") : "unavailable")
                    .Append(" physical=").Append(freight && freight.Physical).Append(" atLoading=").Append(freight && freight.AtLoading)
                    .Append(" moving=").Append(freight && freight.Moving).Append(" seal=").Append(seal.ToString("F6"))
                    .Append(" eyeSealDistance=").Append(distance.ToString("F6")).Append(" maximumDistance=11")
                    .Append(" rayMask=-1 queryTriggers=Ignore hitCount=").Append(hits.Length).Append(" nonAllocCapacity=64")
                    .Append(" nonAllocMaySaturate=").Append(hits.Length >= 64).Append(" sortedFullRayHits=[")
                    .Append(string.Join("; ", hits.Select((hit, i) => i + ":" + (hit.collider ? WalkColliderDescription(hit.collider) + " colliderId=" + hit.collider.GetInstanceID() : "missing collider")
                        + " hitTransform=" + PathOf(hit.transform) + " colliderTransform=" + PathOf(hit.collider ? hit.collider.transform : null)
                        + " distance=" + hit.distance.ToString("F6") + " point=" + hit.point.ToString("F6")
                        + " ownIgnored=" + Own(hit) + " carriageIgnored=" + Permitted(hit)
                        + " blocksSamePredicate=" + (hit.collider && !Own(hit) && !Permitted(hit))))).Append(']');
                Debug.Log(text.ToString());
            }
            catch (Exception error) { Debug.Log("[NorthlineManifestLOS] DIAGNOSTIC_ERROR type=" + error.GetType().Name + " message=" + error.Message); }
        }
        // One observation per Departure Press only; retain the actual ClearLine predicate and the unchanged assertion/E.
        static void LogDepartureSight(RailWorkControl control, bool expectedAllowed, string targetKind, bool targetAllowed, string prompt, bool selectedControl)
        {
            try
            {
                var agent = PM ? PM.Active : null; var body = agent?.Body; var host = agent?.Host;
                var own = body == null ? null : body.Root; var permitted = control.transform;
                var departure = RailDeparture.Instance; var coach = departure ? departure.Coach : null; var coachRoot = coach ? coach.transform : null;
                Vector3 eye = body == null ? Vector3.zero : body.EyePosition, point = control.InteractPoint;
                Vector3 delta = point - eye; float distance = delta.magnitude;
                var context = new InteractContext { agent = agent, host = host, position = agent != null ? agent.Position : Vector3.zero,
                    permissions = host ? host.Permissions : AccessPermission.None };
                string readyReason = "No departure coordinator.", departReason = readyReason, controlReason;
                bool ready = departure && departure.ReadyToDepart(out readyReason);
                bool canDepart = departure && departure.CanDepart(context, control, out departReason);
                bool controlReady = RailCampaign.Ready(context, point, permitted, out controlReason);
                bool clearLine = own && RailCampaign.ClearLine(eye, point, own, permitted);
                var hits = own && distance > .001f
                    ? Physics.RaycastAll(eye, delta / distance, distance, ~0, QueryTriggerInteraction.Ignore)
                        .OrderBy(h => h.distance).ThenBy(h => h.collider ? h.collider.GetInstanceID() : 0).ToArray()
                    : Array.Empty<RaycastHit>();
                string PathOf(Transform transform)
                {
                    if (!transform) return "unavailable";
                    string path = transform.name; for (var p = transform.parent; p; p = p.parent) path = p.name + "/" + path;
                    return path + "#" + transform.GetInstanceID();
                }
                bool Below(Transform node, Transform parent) => node && parent && node.IsChildOf(parent);
                string Hit(RaycastHit hit, int index)
                {
                    var collider = hit.collider; var colliderTransform = collider ? collider.transform : null;
                    var attached = collider ? collider.attachedRigidbody : null; var reported = hit.rigidbody;
                    bool ownHit = Below(hit.transform, own), controlHit = Below(hit.transform, permitted);
                    bool ownCollider = Below(colliderTransform, own), controlCollider = Below(colliderTransform, permitted);
                    return index + ":" + (collider ? WalkColliderDescription(collider) + " colliderId=" + collider.GetInstanceID() : "missing collider")
                        + " hitTransform=" + PathOf(hit.transform) + " colliderTransform=" + PathOf(colliderTransform)
                        + " hitRigidbody=" + PathOf(reported ? reported.transform : null)
                        + " colliderAttachedRigidbody=" + PathOf(attached ? attached.transform : null)
                        + " distance=" + hit.distance.ToString("F6") + " point=" + hit.point.ToString("F6") + " normal=" + hit.normal.ToString("F6")
                        + " ownByHitTransform=" + ownHit + " controlByHitTransform=" + controlHit
                        + " ownByColliderTransform=" + ownCollider + " controlByColliderTransform=" + controlCollider
                        + " coachByHitTransform=" + Below(hit.transform, coachRoot) + " coachByColliderTransform=" + Below(colliderTransform, coachRoot)
                        + " blocksActualPredicate=" + (collider && !ownHit && !controlHit)
                        + " blocksColliderHierarchyAlternative=" + (collider && !ownCollider && !controlCollider);
                }
                var text = new System.Text.StringBuilder();
                text.Append("[NorthlineDepartureLOS] ONCE expectedAllowed=").Append(expectedAllowed).Append(" selectedControl=").Append(selectedControl)
                    .Append(" targetKind=").Append(targetKind).Append(" targetAllowed=").Append(targetAllowed).Append(" prompt=").Append(prompt)
                    .Append(" actualCanDepart=").Append(canDepart).Append(" departReason=").Append(departReason)
                    .Append(" boardingReady=").Append(ready).Append(" boardingReason=").Append(readyReason)
                    .Append(" controlReady=").Append(controlReady).Append(" controlReason=").Append(controlReason).Append(" actualClearLine=").Append(clearLine)
                    .Append(" agent=").Append(agent == null ? "null" : agent.Id.ToString()).Append(" bodyType=").Append(body == null ? "null" : body.GetType().FullName)
                    .Append(" body=").Append(PathOf(own)).Append(" bodyPosition=").Append(agent != null ? agent.Position.ToString("F6") : "unavailable")
                    .Append(" host=").Append(host ? host.DisplayName : "FREE").Append(" activeFree=").Append(RailCampaign.Free(agent))
                    .Append(" transit=").Append(agent != null && agent.InTransit).Append(" capturedPending=").Append(agent != null && agent.CapturedPendingEject)
                    .Append(" eye=").Append(eye.ToString("F6")).Append(" control=").Append(PathOf(permitted))
                    .Append(" controlPosition=").Append(permitted.position.ToString("F6")).Append(" controlLocalPosition=").Append(permitted.localPosition.ToString("F6"))
                    .Append(" controlPoint=").Append(point.ToString("F6")).Append(" coach=").Append(PathOf(coachRoot))
                    .Append(" coachPosition=").Append(coach ? coach.position.ToString("F6") : "unavailable")
                    .Append(" eyeInCoach=").Append(coachRoot ? coachRoot.InverseTransformPoint(eye).ToString("F6") : "unavailable")
                    .Append(" pointInCoach=").Append(coachRoot ? coachRoot.InverseTransformPoint(point).ToString("F6") : "unavailable")
                    .Append(" boardingBounds=").Append(RailLayout.Boarding.ToString("F6")).Append(" eyeControlDistance=").Append(distance.ToString("F6"))
                    .Append(" bodies=[").Append(PM ? string.Join("; ", PM.Agents.Select(a => "id=" + a.Id + " free=" + RailCampaign.Free(a)
                        + " at=" + a.Position.ToString("F6") + " fullAboard=" + RailCampaign.FullyInside(a, RailLayout.Boarding)
                        + " grounded=" + (a.Blob && a.Blob.Controller && a.Blob.Controller.isGrounded))) : "no manager")
                    .Append("] rayMask=-1 queryTriggers=Ignore hitCount=").Append(hits.Length).Append(" nonAllocCapacity=64")
                    .Append(" nonAllocMaySaturate=").Append(hits.Length >= 64).Append(" sortedFullRayHits=[")
                    .Append(string.Join("; ", hits.Select(Hit))).Append(']');
                Debug.Log(text.ToString());
            }
            catch (Exception error) { Debug.Log("[NorthlineDepartureLOS] DIAGNOSTIC_ERROR type=" + error.GetType().Name + " message=" + error.Message); }
        }
        static void Same(AmoebaAgent agent, IControllable body, int count) => Check(PM && PM.Agents.Count == count && ReferenceEquals(PM.Active, agent) && PM.Agents.Contains(agent) && ReferenceEquals(agent.Body, body)
            && !agent.InTransit && !agent.CapturedPendingEject && body != null && body.Root, "Exact agent/body/count must survive each input frame; no silent capture transfer.");
        static IEnumerator Walk(Vector3 target, float tolerance = .25f, [System.Runtime.CompilerServices.CallerMemberName] string caller = null)
        {
            var agent = PM.Active; var body = agent.Body; int count = PM.Agents.Count;
            Check(NavMesh.SamplePosition(agent.Position, out var from, 2, NavMesh.AllAreas) && NavMesh.SamplePosition(target, out _, 1, NavMesh.AllAreas), "Actual walking endpoints are navigable.");
            NavMesh.SamplePosition(target, out var end, 1, NavMesh.AllAreas); var path = new NavMeshPath(); Check(NavMesh.CalculatePath(from.position, end.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete, "Complete NavMesh path reaches original objective.");
            float length = 0; for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]); bool reached = false;
            float budget = Mathf.Max(15, length / 1.7f + 10), beganReal = Time.realtimeSinceStartup, beganSim = Time.time, nextLog = beganReal + 4;
            float originalDeadline = beganReal + budget, stalledSince = -1, completedReal = float.PositiveInfinity;
            int sidestepAttempts = 0;
            bool callbackCalled = false, enumerationCompleted = false;
            var driver = AutoTest.WalkTo(end.position, budget, v => { callbackCalled = true; reached = v; }, tolerance);
            var originalCorners = path.corners;
            var stack = new Stack<IEnumerator>(); stack.Push(driver);
            void DisposeDriver() { while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose(); Clear(); }
            LogWalkDiagnostic("BEGIN", caller, target, from.position, end.position, originalCorners, length, budget, tolerance,
                beganReal, beganSim, agent, body, count, driver, callbackCalled, reached, enumerationCompleted);
            try
            {
                try
                {
                    while (stack.Count > 0 && Time.realtimeSinceStartup < originalDeadline)
                    {
                        Same(agent, body, count);
                        float now = Time.realtimeSinceStartup;
                        if (now >= nextLog)
                        {
                            nextLog = now + 4;
                            LogWalkDiagnostic("PROGRESS", caller, target, from.position, end.position, originalCorners, length, budget, tolerance,
                                beganReal, beganSim, agent, body, count, driver, callbackCalled, reached, enumerationCompleted);
                        }
                        var controller = WalkController(body);
                        Vector3 intendedMove = Quaternion.Euler(0, Camera.Yaw, 0) * new Vector3(GameInput.SimMove.x, 0, GameInput.SimMove.y);
                        bool stationaryContact = controller && controller.enabled && controller.isGrounded
                            && (controller.collisionFlags & CollisionFlags.Sides) != 0 && Flat(controller.velocity) < .15f
                            && intendedMove.sqrMagnitude > .0001f;
                        // Continuous real stalled time triggers before WalkTo's existing .8s hop; partial progress resets it.
                        if (!stationaryContact) stalledSince = -1;
                        else if (stalledSince < 0) stalledSince = now;
                        if (stalledSince >= 0 && now - stalledSince >= .4f && sidestepAttempts < 3
                            && TryPersonBlocker(controller, intendedMove, out var blocker, out var blockerCollider))
                        {
                            float stalledFor = now - stalledSince; sidestepAttempts++; stalledSince = now;
                            LogWalkDiagnostic("NPC_STALL", caller, target, from.position, end.position, originalCorners, length, budget, tolerance,
                                beganReal, beganSim, agent, body, count, driver, callbackCalled, reached, enumerationCompleted);
                            Check(NavMesh.SamplePosition(agent.Position, out var currentNav, 2, NavMesh.AllAreas)
                                && NavMesh.CalculatePath(currentNav.position, end.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete,
                                "The blocked original leg still has a complete actual path before planning a physical bypass.");
                            Debug.Log("[NorthlineWalk] PERSON_BLOCKED caller=" + caller + " blocker=" + blocker.DisplayName
                                + " collider=" + WalkColliderDescription(blockerCollider) + " actual=" + agent.Position.ToString("F3")
                                + " continuousStallSeconds=" + stalledFor.ToString("F3") + " attempt=" + sidestepAttempts + "/3 originalTarget=" + target.ToString("F3")
                                + " originalDeadline=" + originalDeadline.ToString("F3"));
                            if (TrySidestep(controller, intendedMove, end.position, blockerCollider, caller, out var sidestep, out var passpoint))
                            {
                                DisposeDriver(); // The old iterator cannot steer back at its stale corner during the bypass.
                                float sidestepDeadline = Mathf.Min(originalDeadline, Time.realtimeSinceStartup + 4);
                                Debug.Log("[NorthlineWalk] SIDESTEP_BEGIN caller=" + caller + " from=" + agent.Position.ToString("F3")
                                    + " lateral=" + sidestep.ToString("F3") + " passpoint=" + passpoint.ToString("F3") + " originalTarget=" + target.ToString("F3")
                                    + " actionDeadline=" + sidestepDeadline.ToString("F3") + " originalDeadline=" + originalDeadline.ToString("F3"));
                                try
                                {
                                    foreach (var point in new[] { sidestep, passpoint })
                                    {
                                        Check(controller && controller.enabled && controller.gameObject.activeInHierarchy,
                                            "The actual bypass controller remains enabled.");
                                        var actualLegPath = new NavMeshPath();
                                        Check(NavMesh.SamplePosition(agent.Position, out var actualNav, .2f, NavMesh.AllAreas)
                                            && Flat(agent.Position - actualNav.position) <= .18f && Mathf.Abs(agent.Position.y - actualNav.position.y) <= .15f
                                            && NavMesh.SamplePosition(point, out var pointNav, .18f, NavMesh.AllAreas)
                                            && Flat(point - pointNav.position) <= .18f && Mathf.Abs(point.y - pointNav.position.y) <= .15f
                                            && SameFloorSupport(agent.Position, agent.Position.y) && SameFloorSupport(point, agent.Position.y)
                                            && !NavMesh.Raycast(actualNav.position, pointNav.position, out _, NavMesh.AllAreas)
                                            && NavMesh.CalculatePath(actualNav.position, pointNav.position, NavMesh.AllAreas, actualLegPath)
                                            && actualLegPath.status == NavMeshPathStatus.PathComplete,
                                            "Each bypass leg retains complete same-floor navigation and physical support from its actual arrival.");
                                        Check(ClearBodySweep(controller, agent.Position, point, out var actualBlockage),
                                            "The next bypass leg is clear from the actual arrival, including its full endpoint capsule: " + actualBlockage);
                                        while (Flat(agent.Position - point) >= .15f && Time.realtimeSinceStartup < sidestepDeadline)
                                        {
                                            Same(agent, body, count);
                                            Vector3 movement = point - agent.Position; movement.y = 0;
                                            Camera.SetYaw(0); GameInput.SimMove = new Vector2(movement.normalized.x, movement.normalized.z);
                                            GameInput.SimSprint = GameInput.SimJump = false;
                                            yield return null;
                                        }
                                        Same(agent, body, count);
                                        Check(Flat(agent.Position - point) < .15f && Time.realtimeSinceStartup < sidestepDeadline,
                                            "Both real NPC bypass points must be reached inside one4s action and the original leg deadline.");
                                        Debug.Log("[NorthlineWalk] BYPASS_POINT_REACHED caller=" + caller + " point=" + point.ToString("F3")
                                            + " actual=" + agent.Position.ToString("F3") + " actionDeadline=" + sidestepDeadline.ToString("F3")
                                            + " originalDeadline=" + originalDeadline.ToString("F3"));
                                    }
                                }
                                finally { Clear(); }
                                Same(agent, body, count);
                                Check(NavMesh.SamplePosition(agent.Position, out var resumedNav, 2, NavMesh.AllAreas)
                                    && NavMesh.CalculatePath(resumedNav.position, end.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete,
                                    "After actual forward-pass arrival the same original destination remains physically navigable.");
                                Check(TryFreshRejoin(controller, agent.Position, end.position, out var actualRejoin, out var rejoinBlockage),
                                    "The actual pass arrival has a full-body-clear original-target rejoin: " + rejoinBlockage);
                                float remaining = originalDeadline - Time.realtimeSinceStartup;
                                Check(remaining > 0, "A bypass cannot extend the original real-time walking budget.");
                                callbackCalled = reached = false;
                                driver = AutoTest.WalkTo(end.position, remaining, v => { callbackCalled = true; reached = v; }, tolerance);
                                stack.Push(driver); stalledSince = -1;
                                Debug.Log("[NorthlineWalk] SIDESTEP_REPLAN caller=" + caller + " actual=" + agent.Position.ToString("F3")
                                    + " originalTarget=" + target.ToString("F3") + " originalSampledEnd=" + end.position.ToString("F3")
                                    + " actualValidatedRejoin=" + actualRejoin.ToString("F3")
                                    + " remainingOriginalBudget=" + remaining.ToString("F3") + " originalDeadline=" + originalDeadline.ToString("F3"));
                                yield return null;
                                continue;
                            }
                        }
                        if (Time.realtimeSinceStartup >= originalDeadline) break;
                        var current = stack.Peek();
                        if (!current.MoveNext()) { (current as IDisposable)?.Dispose(); stack.Pop(); continue; }
                        if (current.Current is IEnumerator nested) stack.Push(nested); else yield return current.Current;
                    }
                    Same(agent, body, count); enumerationCompleted = stack.Count == 0; completedReal = Time.realtimeSinceStartup;
                }
                finally
                {
                    // Capture the live input/collision context before the unchanged final Check or outer Clear.
                    LogWalkDiagnostic("END_OR_FAIL", caller, target, from.position, end.position, originalCorners, length, budget, tolerance,
                        beganReal, beganSim, agent, body, count, driver, callbackCalled, reached, enumerationCompleted);
                }
                Check(reached && Flat(agent.Position - target) < tolerance + .15f, "Normal unwarped movement reaches original target.");
                Check(enumerationCompleted && completedReal < originalDeadline, "Normal movement and any physical NPC sidestep share one original real-time deadline.");
            }
            finally { DisposeDriver(); }
            yield return null; yield return null;
        }
        static CharacterController WalkController(IControllable body)
            => body is HostCharacter host && host ? host.Controller : body is AmoebaController blob && blob ? blob.Controller : null;
        static bool TryPersonBlocker(CharacterController controller, Vector3 direction, out HostCharacter person, out Collider blocker)
        {
            person = null; blocker = null; direction.y = 0;
            if (direction.sqrMagnitude < .0001f) return false;
            BodyCapsule(controller, Vector3.zero, out var bottom, out var top, out float radius);
            // The query uses the real controller radius, with no collider or character-size changes.
            foreach (var hit in Physics.CapsuleCastAll(bottom, top, radius, direction.normalized, .35f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            {
                if (!hit.collider || hit.collider.transform.IsChildOf(controller.transform)) continue;
                var host = hit.collider.GetComponentInParent<HostCharacter>();
                if (host && host != PM.Active.Host && (hit.collider is CapsuleCollider || hit.collider is CharacterController))
                { person = host; blocker = hit.collider; return true; }
                return false; // A nearer static blocker must never be mistaken for the person behind it.
            }
            return false;
        }
        static bool TrySidestep(CharacterController controller, Vector3 intendedMove, Vector3 originalEnd, Collider blocker,
            string phase, out Vector3 destination, out Vector3 passDestination)
        {
            destination = passDestination = default;
            if (!blocker || !blocker.enabled || !blocker.gameObject.activeInHierarchy || blocker.isTrigger) return false;
            Vector3 from = PM.Active.Position, forward = intendedMove; forward.y = 0;
            if (forward.sqrMagnitude < .0001f) return false;
            forward.Normalize(); Vector3 lateral = new Vector3(forward.z, 0, -forward.x);
            bool found = false; float best = float.PositiveInfinity;
            foreach (float distance in new[] { 1f, 1.4f, 1.8f })
                foreach (int side in new[] { -1, 1 })
                {
                    Vector3 requested = from + lateral * (distance * side);
                    Vector3 candidate = requested, navigationPoint = requested, passpoint = default, rejoin = default;
                    string reason = null; float advance = 0;
                    if (!NavMesh.SamplePosition(from, out var source, .2f, NavMesh.AllAreas)
                        || !NavMesh.SamplePosition(requested, out var sample, .18f, NavMesh.AllAreas)) reason = "no nearby navigation footing";
                    else
                    {
                        navigationPoint = sample.position; candidate = new Vector3(sample.position.x, from.y, sample.position.z);
                        if (Flat(requested - navigationPoint) > .18f || Mathf.Abs(navigationPoint.y - from.y) > .15f) reason = "sample changed floor or lateral destination";
                        else if (!SameFloorSupport(candidate, from.y)) reason = "no same-level physical floor below lateral destination";
                        else if (NavMesh.Raycast(source.position, navigationPoint, out _, NavMesh.AllAreas)) reason = "lateral navigation edge";
                        else
                        {
                            var path = new NavMeshPath();
                            if (!NavMesh.CalculatePath(source.position, navigationPoint, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                                reason = "incomplete lateral navigation path";
                            else
                            {
                                BodyCapsule(controller, candidate - from, out var bottom, out var top, out float radius);
                                Bounds obstacle = blocker.bounds;
                                float far = Vector3.Dot(obstacle.center - (bottom + top) * .5f, forward)
                                    + Mathf.Abs(forward.x) * obstacle.extents.x + Mathf.Abs(forward.z) * obstacle.extents.z;
                                // The actual scaled capsule passes the NPC's full projected collider, plus .25m clearance.
                                advance = Mathf.Max(2f, far + radius + .25f); passpoint = candidate + forward * advance;
                                if (!NavMesh.SamplePosition(passpoint, out var passNav, .18f, NavMesh.AllAreas)
                                    || Flat(passpoint - passNav.position) > .18f || Mathf.Abs(passNav.position.y - from.y) > .15f)
                                    reason = "forward pass point has no same-floor navigation footing";
                                else if (!SameFloorSupport(passpoint, from.y)) reason = "forward pass lacks same-level physical support";
                                else if (NavMesh.Raycast(navigationPoint, passNav.position, out _, NavMesh.AllAreas)) reason = "forward pass navigation edge";
                                else if (!NavMesh.CalculatePath(navigationPoint, passNav.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                                    reason = "incomplete forward pass navigation path";
                                else if (!ClearBodySweep(controller, from, candidate, out reason)) { }
                                else if (!ClearBodySweep(controller, candidate, passpoint, out reason)) { }
                                else
                                {
                                    // The old corner can now be behind the NPC; plan from this pass point to the same original end.
                                    if (!TryFreshRejoin(controller, passpoint, originalEnd, out rejoin, out reason)) { }
                                }
                            }
                        }
                    }
                    Debug.Log("[NorthlineWalk] SIDESTEP_SAMPLE phase=" + phase + " side=" + side + " requested=" + requested.ToString("F3")
                        + " sampledNav=" + navigationPoint.ToString("F3") + " lateral=" + candidate.ToString("F3") + " passpoint=" + passpoint.ToString("F3")
                        + " advance=" + advance.ToString("F3") + " freshRejoin=" + rejoin.ToString("F3") + " accepted=" + (reason == null)
                        + " reason=" + (reason ?? "both full-body bypass sweeps/endpoints and fresh original-target rejoin are clear with same-floor navigation/support"));
                    if (reason != null) continue;
                    float score = Flat(from - candidate) + advance + Flat(passpoint - originalEnd);
                    if (score < best) { destination = candidate; passDestination = passpoint; best = score; found = true; }
                }
            return found;
        }
        static bool TryFreshRejoin(CharacterController controller, Vector3 from, Vector3 originalEnd, out Vector3 rejoin, out string reason)
        {
            rejoin = from; reason = null;
            if (!NavMesh.SamplePosition(from, out var source, .2f, NavMesh.AllAreas)
                || Flat(from - source.position) > .18f || Mathf.Abs(from.y - source.position.y) > .15f)
            { reason = "rejoin source has no same-floor navigation footing"; return false; }
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(source.position, originalEnd, NavMesh.AllAreas, path)
                || path.status != NavMeshPathStatus.PathComplete || path.corners.Length == 0)
            { reason = "no complete original-target rejoin path"; return false; }
            var corners = path.corners; int next = 0;
            while (next < corners.Length - 1 && Flat(corners[next] - from) < .5f) next++;
            Vector3 onward = corners[next] - from; onward.y = 0; rejoin = from + Vector3.ClampMagnitude(onward, 1.4f);
            if (!NavMesh.SamplePosition(rejoin, out var navigation, .18f, NavMesh.AllAreas)
                || Flat(rejoin - navigation.position) > .18f || Mathf.Abs(navigation.position.y - from.y) > .15f)
            { reason = "fresh rejoin has no same-floor navigation footing"; return false; }
            if (NavMesh.Raycast(source.position, navigation.position, out _, NavMesh.AllAreas)) { reason = "fresh rejoin navigation edge"; return false; }
            if (!SameFloorSupport(rejoin, from.y)) { reason = "fresh rejoin lacks same-level physical support"; return false; }
            return ClearBodySweep(controller, from, rejoin, out reason);
        }
        static bool SameFloorSupport(Vector3 position, float rootY)
            => Physics.Raycast(position + Vector3.up * .4f, Vector3.down, out var support, .8f, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore)
                && Mathf.Abs(support.point.y - rootY) <= .15f;
        static bool ClearBodySweep(CharacterController controller, Vector3 from, Vector3 to, out string reason)
        {
            reason = null;
            BodyCapsule(controller, from - PM.Active.Position, out var bottom, out var top, out float radius);
            radius += .005f; // A small planning margin outside the unchanged full controller capsule.
            Vector3 travel = to - from;
            var hit = Physics.CapsuleCastAll(bottom, top, radius, travel.normalized, travel.magnitude, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => h.collider && !h.collider.transform.IsChildOf(controller.transform)).OrderBy(h => h.distance).FirstOrDefault();
            if (hit.collider) { reason = "body sweep hits " + WalkColliderDescription(hit.collider) + " at " + hit.distance.ToString("F3"); return false; }
            var overlap = Physics.OverlapCapsule(bottom + travel, top + travel, radius, ~0, QueryTriggerInteraction.Ignore)
                .FirstOrDefault(c => c && !c.transform.IsChildOf(controller.transform));
            if (overlap) { reason = "destination body overlaps " + WalkColliderDescription(overlap); return false; }
            return true;
        }
        static void BodyCapsule(CharacterController controller, Vector3 offset, out Vector3 bottom, out Vector3 top, out float radius)
        {
            Vector3 center = controller.transform.TransformPoint(controller.center) + offset;
            Vector3 scale = controller.transform.lossyScale;
            radius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float halfLine = Mathf.Max(0, controller.height * Mathf.Abs(scale.y) * .5f - radius);
            bottom = center - Vector3.up * halfLine; top = center + Vector3.up * halfLine;
        }
        static string WalkColliderDescription(Collider collider)
        {
            string path = collider.name;
            for (var parent = collider.transform.parent; parent; parent = parent.parent) path = parent.name + "/" + path;
            return path + " type=" + collider.GetType().Name + " layer=" + collider.gameObject.layer
                + " bounds=" + collider.bounds.center.ToString("F3") + "/" + collider.bounds.size.ToString("F3");
        }
        // Observation only. Read the real WalkTo iterator and controller geometry; casts never change movement or collision.
        static void LogWalkDiagnostic(string phase, string caller, Vector3 target, Vector3 from, Vector3 end, Vector3[] originalCorners,
            float length, float budget, float tolerance, float beganReal, float beganSim, AmoebaAgent agent, IControllable originalBody,
            int count, IEnumerator driver, bool callbackCalled, bool reached, bool enumerationCompleted)
        {
            try
            {
                object Local(string name) => driver.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(f => f.Name == name || f.Name.StartsWith("<" + name + ">", StringComparison.Ordinal))?.GetValue(driver);
                Transform Root(IControllable candidate) => candidate == null || candidate is Object unity && !unity ? null : candidate.Root;
                string Corners(Vector3[] points) => points == null ? "unavailable" : string.Join("; ", points.Select((p, i) => i + ":" + p.ToString("F3")));
                string ColliderPath(Collider c)
                {
                    if (!c) return "destroyed";
                    string name = c.name; for (var p = c.transform.parent; p; p = p.parent) name = p.name + "/" + name;
                    return name + "#" + c.GetInstanceID() + " type=" + c.GetType().Name + " layer=" + c.gameObject.layer
                        + " bounds=" + c.bounds.center.ToString("F3") + "/" + c.bounds.size.ToString("F3");
                }
                var currentAgent = PM ? PM.Active : null; var currentBody = currentAgent?.Body;
                var root = Root(currentBody); var originalRoot = Root(originalBody);
                Vector3 actual = root ? root.position : Vector3.zero;
                var controller = currentBody is HostCharacter host && host ? host.Controller
                    : currentBody is AmoebaController blob && blob ? blob.Controller : null;
                var liveCorners = Local("corners") as Vector3[]; int corner = Local("ci") is int index ? index : -1;
                bool liveAim = liveCorners != null && corner >= 0;
                Vector3 intended = liveAim && corner < liveCorners.Length ? liveCorners[corner]
                    : liveAim ? end : originalCorners.Length > 1 ? originalCorners[1] : end;
                Vector3 direction = Quaternion.Euler(0, Camera ? Camera.Yaw : 0, 0) * new Vector3(GameInput.SimMove.x, 0, GameInput.SimMove.y);
                string castDirection = "actual simulated movement";
                if (direction.sqrMagnitude < .000001f) { direction = intended - actual; direction.y = 0; castDirection = "intended aim (zero current input)"; }
                var text = new System.Text.StringBuilder();
                text.Append("[NorthlineWalk] ").Append(phase).Append(" caller=").Append(caller).Append(" target=").Append(target.ToString("F3"))
                    .Append(" targetLocal=").Append((target - RailLayout.Origin).ToString("F3")).Append(" originalFrom=").Append(from.ToString("F3"))
                    .Append(" originalEnd=").Append(end.ToString("F3")).Append(" originalLength=").Append(length.ToString("F3"))
                    .Append(" originalBudget=").Append(budget.ToString("F3")).Append(" tolerance=").Append(tolerance.ToString("F3"))
                    .Append(" real=").Append((Time.realtimeSinceStartup - beganReal).ToString("F3")).Append(" sim=").Append((Time.time - beganSim).ToString("F3"))
                    .Append(" dt=").Append(Time.deltaTime.ToString("F5")).Append(" timeScale=").Append(Time.timeScale)
                    .Append(" callbackCalled=").Append(callbackCalled).Append(" reachedCallback=").Append(reached).Append(" enumerationCompleted=").Append(enumerationCompleted)
                    .Append(" targetDistance=").Append(root ? Flat(actual - target).ToString("F4") : "unavailable")
                    .Append(" sampledEndDistance=").Append(root ? Flat(actual - end).ToString("F4") : "unavailable")
                    .Append(" expectedAgent=").Append(agent == null ? "null" : agent.Id.ToString()).Append(" activeAgent=").Append(currentAgent == null ? "null" : currentAgent.Id.ToString())
                    .Append(" exactAgent=").Append(ReferenceEquals(currentAgent, agent)).Append(" exactBody=").Append(ReferenceEquals(currentBody, originalBody))
                    .Append(" expectedCount=").Append(count).Append(" actualCount=").Append(PM ? PM.Agents.Count : -1)
                    .Append(" body=").Append(root ? root.name : "unavailable").Append(" originalBody=").Append(originalRoot ? originalRoot.name : "unavailable")
                    .Append(" at=").Append(actual.ToString("F3")).Append(" local=").Append((actual - RailLayout.Origin).ToString("F3"))
                    .Append(" originalBodyAt=").Append(originalRoot ? originalRoot.position.ToString("F3") : "unavailable")
                    .Append(" state=").Append(GameManager.Instance ? GameManager.Instance.State.ToString() : "no manager")
                    .Append(" transit=").Append(currentAgent != null && currentAgent.InTransit).Append(" capturedPending=").Append(currentAgent != null && currentAgent.CapturedPendingEject)
                    .Append(" inputLocked=").Append(PM && PM.InputLocked).Append(" simulated=").Append(GameInput.Simulated)
                    .Append(" move=").Append(GameInput.SimMove.ToString("F3")).Append(" sprint=").Append(GameInput.SimSprint).Append(" jump=").Append(GameInput.SimJump)
                    .Append(" yaw=").Append(Camera ? Camera.Yaw.ToString("F3") : "unavailable").Append(" driverT=").Append(Local("t"))
                    .Append(" driverCorner=").Append(corner).Append('/').Append(liveCorners == null ? -1 : liveCorners.Length)
                    .Append(" intended=").Append(intended.ToString("F3")).Append(" aimSource=").Append(liveAim ? "live WalkTo iterator" : "original path before iterator initialized")
                    .Append(" originalCorners=[").Append(Corners(originalCorners)).Append("] liveCorners=[").Append(Corners(liveCorners)).Append(']');
                if (controller)
                {
                    Vector3 scale = controller.transform.lossyScale, center = controller.transform.TransformPoint(controller.center);
                    float radius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                    float halfLine = Mathf.Max(0, controller.height * Mathf.Abs(scale.y) * .5f - radius);
                    Vector3 bottom = center - Vector3.up * halfLine, top = center + Vector3.up * halfLine;
                    var hits = direction.sqrMagnitude > .000001f ? Physics.CapsuleCastAll(bottom, top, radius, direction.normalized, 1.6f, ~0, QueryTriggerInteraction.Ignore)
                        .OrderBy(h => h.distance).ThenBy(h => h.collider ? h.collider.GetInstanceID() : 0).ToArray() : Array.Empty<RaycastHit>();
                    var overlaps = Physics.OverlapCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore).OrderBy(c => c.GetInstanceID()).ToArray();
                    text.Append(" controller={enabled=").Append(controller.enabled).Append(" grounded=").Append(controller.isGrounded).Append(" flags=").Append(controller.collisionFlags)
                        .Append(" velocity=").Append(controller.velocity.ToString("F4")).Append(" radius=").Append(controller.radius).Append(" height=").Append(controller.height)
                        .Append(" center=").Append(controller.center.ToString("F3")).Append(" lossyScale=").Append(scale.ToString("F3"))
                        .Append(" skin=").Append(controller.skinWidth).Append(" detectCollisions=").Append(controller.detectCollisions)
                        .Append(" bottom=").Append(bottom.ToString("F3")).Append(" top=").Append(top.ToString("F3")).Append(" actualRadius=").Append(radius).Append('}')
                        .Append(" castDirectionSource=").Append(castDirection).Append(" castDirection=").Append(direction.normalized.ToString("F3"))
                        .Append(" forwardHitCount=").Append(hits.Length).Append(" sortedForwardHitsIncludingSelf=[")
                        .Append(string.Join("; ", hits.Take(16).Select(h => ColliderPath(h.collider) + " self=" + (h.collider && h.collider.transform.IsChildOf(controller.transform))
                            + " distance=" + h.distance.ToString("F5") + " point=" + h.point.ToString("F3") + " normal=" + h.normal.ToString("F3"))))
                        .Append("] overlapCount=").Append(overlaps.Length).Append(" overlapsIncludingSelf=[")
                        .Append(string.Join("; ", overlaps.Take(16).Select(c => ColliderPath(c) + " self=" + c.transform.IsChildOf(controller.transform)))).Append(']');
                }
                Debug.Log(text.ToString());
            }
            catch (Exception error) { Debug.Log("[NorthlineWalk] DIAGNOSTIC_ERROR phase=" + phase + " caller=" + caller + " type=" + error.GetType().Name + " message=" + error.Message); }
        }
        static IEnumerator DirectFreeWalk(Vector3 target)
        {
            var agent = PM.Active; var body = agent.Body; int count = PM.Agents.Count; float deadline = Time.realtimeSinceStartup + 5; Check(RailCampaign.Free(agent), "Only actual free capsule uses low non-human-nav duct.");
            try { while (Flat(agent.Position - target) > .1f && Time.realtimeSinceStartup < deadline) { Same(agent, body, count); var d = target - agent.Position; d.y = 0; d.Normalize(); Camera.SetYaw(0); GameInput.SimMove = new Vector2(d.x, d.z); GameInput.SimSprint = GameInput.SimJump = false; yield return null; } Same(agent, body, count); Check(Flat(agent.Position - target) <= .1f, "Normal physical free-body input traverses low opening."); }
            finally { Clear(); }
        }
    }
}
