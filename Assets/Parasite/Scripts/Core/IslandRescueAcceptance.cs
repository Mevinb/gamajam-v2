using System;
using System.Collections;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Parasite.Characters;
using Parasite.Missions;
using Parasite.Possession;
using Parasite.Stealth;
using Parasite.World;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Parasite.Testing
{
    /// <summary>Shared Editor/player proof of moving people, physical signal custody and protected assembly.</summary>
    public static class IslandRescueAcceptance
    {
        static PossessionManager PM => PossessionManager.Instance;
        public static IEnumerator RunBranches()
        {
            yield return Branch(EndingOutcome.Peace);
            yield return Branch(EndingOutcome.Dominion);
        }
        public static IEnumerator RunRecovery()
        {
            yield return PeaceLeaderAndPartialRecovery();
            yield return DominionWrongReleaseAndCapture();
            yield return SignalCustodyAndCheckpoint();
            yield return AllyWitnessPolicy();
            yield return PeaceGuardExposure();
            yield return PeaceRegainedHostNeedsRegroup(false);
            yield return PeaceRegainedHostNeedsRegroup(true);
            yield return CourierRenewedPossessionCancelsRelease();
            yield return CourierImmediateCaptureEjectCancelsMark();
            yield return BoardingToleranceRequiresInset(0);
            yield return BoardingToleranceRequiresInset(2);
            yield return ParkedPassengersKeepGangwayClear();
        }

        public static IEnumerator Branch(EndingOutcome outcome)
        {
            yield return StartAtRefuge(outcome);
            var roster = IslandTravelers.Instance;
            Check(roster.Members.Count == 3 && roster.Members.Select(t => t.StableId).Distinct().Count() == 3, "Exactly three durable people spawn after the inner refuge.");
            Check(!IslandExtraction.ReadyToDepart(out _), "The launch cannot finish without rescued people or a mounted signal.");
            GameManager.Instance.FinishIsland();
            Check(GameManager.Instance.IsPlaying && !RunState.Has(IslandRoster.Departed), "Direct finalization obeys the same incomplete-assembly gate.");
            for (int i = 0; i < 3; i++)
            {
                var traveler = roster.Members[i];
                if (outcome == EndingOutcome.Peace)
                {
                    Check(traveler.Host.ProtectedMind && !traveler.Host.CanBePossessed, "Peace leaves the actual survivor's mind under its own control.");
                    if (i == 0)
                    {
                        UseHost("Island refuge engineer");
                        Check(!traveler.CanInteract(Context(), out _), "Maintenance access does not substitute for a trusted social or medical leader.");
                    }
                    UseHost("Island refuge volunteer");
                    yield return AskToFollow(traveler);
                    yield return LeadThroughCover(traveler);
                    yield return WaitUntil(() => traveler.Delivered, 8, "The actual survivor reaches its clinic slot.");
                    Check(traveler.Host.Possessor == null && Vector3.Distance(traveler.transform.position, IslandRoster.Clinic[i]) < 1.1f, "A physical, unoccupied clinic arrival commits this person.");
                    Debug.Log("[IslandRescue] DELIVERED " + traveler.StableId + " at " + traveler.transform.position + " slot=" + IslandRoster.Clinic[i]);
                }
                else
                {
                    PM.ForcePossessImmediate(traveler.Host);
                    Check(PM.Active.Host == traveler.Host, "An actual courier body is controlled before marking its own route.");
                    yield return PressAt(traveler, 180);
                    yield return Walk(IslandRoster.Handoffs[i]);
                    Check(!traveler.Delivered && traveler.Host.State == HostState.Possessed, "Walking a controlled courier to a lane alone is insufficient.");
                    var release = traveler.transform.position;
                    yield return Release();
                    Check(traveler.Host.State == HostState.Unconscious && traveler.Host.Possessor == null, "The real Q path releases the courier and starts normal recovery.");
                    yield return WaitUntil(() => traveler.Delivered, 16, "A released courier recovers and walks onward through its handoff lane.");
                    Check(traveler.Host.State == HostState.Normal && Vector3.Distance(traveler.transform.position, release) > 1, "Delivery requires recovered autonomous motion after release.");
                }
                Check(PM.Agents.Count <= 2 && RunState.LakesideOutcome == outcome, "Individual deliveries preserve the two-amoeba limit and immutable branch.");
            }
            yield return WaitUntil(() => RunState.Has(IslandRoster.Ready), 2, "All three people unlock the signal mission.");
            Check(RunState.MissionIndex == 19, "The real objective progression reaches mission20.");
            UseHost("Island supply marshal");
            yield return Walk(IslandCampaign.At(18, .15f, 16.3f));
            var pickup = UnityEngine.Object.FindFirstObjectByType<IslandSignalPickup>();
            yield return PressAt(pickup, 0);
            Check(IslandSignalTransport.Instance.HeldBy(Context()), "E picks up one physical signal case in the current human carrier.");
            Check(IslandSignalTransport.Instance.CaseTransform && IslandSignalTransport.Instance.CaseTransform.IsChildOf(PM.Active.Host.transform), "The actual case mesh travels with the carrier body.");
            var carrier = PM.Active.Host;
            yield return Walk(IslandCampaign.At(32, .15f, 35.3f));
            var mount = UnityEngine.Object.FindFirstObjectByType<IslandSignalMount>();
            yield return PressAt(mount, 0);
            Check(RunState.Has(IslandRoster.SignalMounted) && !IslandSignalTransport.Instance.Held, "E mounts the carried signal at its physical jetty socket.");
            Check(Vector3.Distance(IslandSignalTransport.Instance.CaseTransform.position, IslandCampaign.At(32, 1.15f, 37)) < .05f, "The actual case mesh leaves the hand and occupies the authored signal mount.");
            if (outcome == EndingOutcome.Dominion)
            {
                yield return WaitUntil(() => roster.Members.Any(t => t.Boarded), 50, "The first autonomous courier physically boards.");
                var boarded = roster.Members.Where(t => t.Boarded).Select(t => t.StableId).ToArray();
                Check(boarded.Length < 3, "The checkpoint fixture captures an actual partial passenger assembly.");
                yield return Reload("Partial courier boarding recovery");
                roster = IslandTravelers.Instance;
                Check(boarded.All(id => roster.Members.Single(t => t.StableId == id).Boarded) && RunState.Has(IslandRoster.SignalMounted), "Mounted signal and each individually boarded courier survive reload.");
                UseHost("Island rescue volunteer"); carrier = PM.Active.Host;
            }
            for (int i = 0; i < roster.Members.Count; i++)
            {
                var traveler = roster.Members[i];
                if (outcome == EndingOutcome.Peace)
                {
                    if (PM.Active.Host != carrier) PM.ForcePossessImmediate(carrier);
                    yield return AskToFollow(traveler);
                    yield return Walk(IslandCampaign.At(34, .15f, 38), traveler);
                    yield return Walk(IslandCampaign.At(34, .55f, 44.5f), traveler);
                }
                yield return WaitUntil(() => traveler.Boarded, 65, "A real passenger walks up the gangway to its deck slot.");
                Check(Vector3.Distance(traveler.transform.position, IslandRoster.Board[Array.IndexOf(IslandRoster.Ids, traveler.StableId)]) < 1.1f, "Boarding flags follow physical passenger arrival.");
                if (outcome == EndingOutcome.Peace && i == 0)
                {
                    yield return Reload("Partial survivor boarding recovery");
                    roster = IslandTravelers.Instance;
                    Check(roster.Members.Count(t => t.Boarded) == 1 && RunState.Has(IslandRoster.SignalMounted), "One physically boarded survivor and the mounted signal restore without completing the other two.");
                    UseHost("Island rescue volunteer"); carrier = PM.Active.Host;
                }
            }
            Check(!IslandExtraction.ReadyToDepart(out _), "An occupied human carrier cannot be abandoned at extraction.");
            GameManager.Instance.FinishIsland();
            Check(GameManager.Instance.IsPlaying, "Defensive finalization rejects the occupied active host.");
            if (PM.Active.Host == null) PM.ForcePossessImmediate(carrier);
            yield return Walk(IslandCampaign.At(34, .55f, 44));
            Check(PM.TrySplit(), "A second owned amoeba may cooperate during final assembly.");
            var occupied = PM.Active;
            Check(PM.SwitchActive(), "Control switches to the free half while the carrier stays occupied.");
            PM.Active.Blob.Place(IslandCampaign.At(34, .55f, 44));
            Check(!IslandExtraction.ReadyToDepart(out var denied) && denied.Contains("EVERY"), "An inactive occupied host also blocks extraction explicitly.");
            GameManager.Instance.FinishIsland();
            Check(GameManager.Instance.IsPlaying && !RunState.Has(IslandRoster.Departed), "Direct finalization cannot bypass the inactive-host gate.");
            Check(PM.SwitchActive() && PM.Active == occupied, "Return to the occupied half.");
            yield return Release();
            Check(carrier.Possessor == null && PM.Agents.All(a => a.Host == null), "Both ownership directions are clear after release.");
            yield return Walk(IslandCampaign.At(34, .55f, 44));
            var inactive = PM.Agents.First(a => a != PM.Active);
            inactive.InTransit = true;
            Check(!IslandExtraction.ReadyToDepart(out _), "An unfinished leap blocks finalization even when no host remains attached.");
            GameManager.Instance.FinishIsland();
            Check(GameManager.Instance.IsPlaying, "The direct entry point also rejects transit.");
            inactive.InTransit = false;
            inactive.Blob.Place(IslandCampaign.At(34, .52f, 42.8f));
            Check(!IslandExtraction.ReadyToDepart(out _), "The gangway before the actual bow is not counted as aboard the deck.");
            GameManager.Instance.FinishIsland();
            Check(GameManager.Instance.IsPlaying && !RunState.Has(IslandRoster.Departed), "Direct finalization also rejects an off-bow half on the gangway.");
            inactive.Blob.Place(IslandCampaign.At(30, .15f, 35));
            Check(!IslandExtraction.ReadyToDepart(out var gather) && gather.Contains("aboard"), "A free half still ashore cannot be silently collected.");
            Check(PM.SwitchActive(), "Switch to the half ashore.");
            yield return Walk(IslandCampaign.At(34, .55f, 44));
            Check(IslandExtraction.ReadyToDepart(out _), "Everyone is physically assembled with all owned bodies free and settled.");
            var launch = GameObject.Find("Island rescue launch");
            var departurePosition = launch.transform.position;
            var extraction = UnityEngine.Object.FindFirstObjectByType<IslandExtraction>();
            yield return PressAt(extraction, 180);
            Check(GameManager.Instance.State == GameState.Ending && !RunState.Has(IslandRoster.Departed), "The final E starts protected motion before publishing the summary.");
            yield return WaitUntil(() => GameManager.Instance.State == GameState.Ended, 8, "Protected departure motion and its finite narration window complete before the summary.");
            Check(GameManager.Instance.State == GameState.Ended && RunState.Has(IslandRoster.Departed), "The completed protected departure publishes the island summary.");
            Check(launch.transform.position.z > departurePosition.z + 3 && PM.Agents.All(a => a.Host == null), "The occupied deck actually moves with free passengers; no borrowed host remains.");
            Check(RunState.LakesideOutcome == outcome && IslandRoster.AllBoarded, "Departure retains branch and all durable people.");
            Debug.Log("[IslandRescue] PASS " + outcome + " actual people, signal custody, boarding and protected launch");
        }

        public static IEnumerator PeaceLeaderAndPartialRecovery()
        {
            yield return StartAtRefuge(EndingOutcome.Peace);
            var traveler = IslandTravelers.Instance.Members[0];
            UseHost("Island relief scientist");
            yield return AskToFollow(traveler);
            yield return LeadToFirstVisitedCover(traveler);
            var leader = PM.Active;
            var host = leader.Host;
            var guard = HostCharacter.All.Select(h => h ? h.Brain as GuardAI : null).First(g => g);
            host.Capture(); PM.OnHostCaptured(host, guard);
            Check(leader.Host == host && host.State == HostState.Captured, "Capture leaves the original body attached during its real3s eject window.");
            yield return WaitUntil(() => Vector3.Distance(traveler.transform.position, IslandRoster.Cover[1] + Vector3.left * 1.5f) < 1.3f, 8, "A captured leader cannot continue escorting; the person physically returns to visited cover.");
            Check(!traveler.Delivered, "Losing the leader never remotely delivers a person.");
            yield return Reload("Escort partial-cover recovery");
            Check(IslandTravelers.Instance.Members.Count == 3 && IslandRoster.SavedCover(IslandRoster.Ids[0]) >= 1, "Checkpoint restores one copy of each stable person and the visited cover.");
            traveler = IslandTravelers.Instance.Members[0];
            Check(Vector3.Distance(traveler.transform.position, IslandRoster.Cover[1] + Vector3.left * 1.5f) < 1.4f, "The undelivered person restarts on its authored visited cover, never beyond it.");
            UseHost("Island relief scientist");
            yield return AskToFollow(traveler);
            yield return LeadThroughCover(traveler);
            yield return WaitUntil(() => traveler.Delivered, 8, "A regrouped survivor can finish after recovery.");
            yield return Reload("Individual clinic arrival recovery");
            Check(IslandTravelers.Instance.Members.Count(t => t.Delivered) == 1 && !IslandRoster.AllDelivered, "Partial roster completion survives independently.");
            Check(Vector3.Distance(IslandTravelers.Instance.Members[0].transform.position, IslandRoster.Clinic[0]) < 1.4f, "The delivered person restores in its actual safe clinic slot.");
            Check(!IslandSignalTransport.Instance.Held && !RunState.Has(IslandRoster.Ready), "No duplicate item or premature signal mission appears on a partial reload.");
            Debug.Log("[IslandRescue] PASS captured leader, visited-cover retry and partial clinic checkpoint");
        }

        public static IEnumerator DominionWrongReleaseAndCapture()
        {
            yield return StartAtRefuge(EndingOutcome.Dominion);
            var traveler = IslandTravelers.Instance.Members[0];
            PM.ForcePossessImmediate(traveler.Host);
            yield return PressAt(traveler, 180);
            yield return Release();
            yield return WaitUntil(() => traveler.Host.State == HostState.Normal, 12, "A courier released away from its lane recovers normally.");
            yield return new WaitForSeconds(2);
            Check(!traveler.Delivered, "An early release cannot commit the marked route from anywhere.");
            PM.ForcePossessImmediate(traveler.Host);
            yield return PressAt(traveler, 180);
            var guard = HostCharacter.All.Select(h => h ? h.Brain as GuardAI : null).First(g => g);
            traveler.Host.Capture(); PM.OnHostCaptured(traveler.Host, guard);
            yield return new WaitForSeconds(.2f);
            Check(!traveler.Delivered && PM.Active.CapturedPendingEject, "Capture cancels the mark during the actual attached-host window.");
            yield return Reload("Courier capture retry");
            traveler = IslandTravelers.Instance.Members[0];
            Check(traveler.Host.CanBePossessed && !traveler.Delivered && IslandTravelers.Instance.Members.Count == 3, "Reload restores a fair unoccupied courier retry without duplicates.");
            PM.ForcePossessImmediate(traveler.Host);
            yield return PressAt(traveler, 180);
            yield return Walk(IslandRoster.Handoffs[0]);
            yield return Release();
            yield return WaitUntil(() => traveler.Delivered, 16, "The same route remains completable after a captured attempt.");
            Check(traveler.Host.ProtectedMind, "A delivered courier remains free to complete its own later boarding route.");
            Debug.Log("[IslandRescue] PASS wrong-lane release denial and captured courier retry");
        }

        public static IEnumerator SignalCustodyAndCheckpoint()
        {
            yield return StartAtRefuge(EndingOutcome.Peace);
            foreach (string id in IslandRoster.Ids) MissionManager.SetFlag(IslandRoster.Delivered(id));
            yield return WaitUntil(() => RunState.Has(IslandRoster.Ready), 2, "Fixture starts the independent physical signal objective after the roster prerequisite.");
            yield return Reload("Signal custody fixture");
            UseHost("Island supply marshal");
            var signal = IslandSignalTransport.Instance;
            Check(!signal.PickUp(Context()), "The public signal API rejects a remote pickup even in an authorized human.");
            yield return Walk(IslandCampaign.At(18, .15f, 16.3f));
            var pickup = UnityEngine.Object.FindFirstObjectByType<IslandSignalPickup>();
            yield return PressAt(pickup, 0);
            Check(signal.Held, "The real cache E establishes custody.");
            Check(!signal.Mount(Context()), "The public signal API rejects mounting away from its socket.");
            var host = PM.Active.Host;
            GameManager.Instance.SetPaused(true);
            yield return new WaitForSecondsRealtime(.3f);
            Check(signal.Held && !RunState.Has(IslandRoster.SignalMounted), "Pause preserves valid custody without completing transport.");
            GameManager.Instance.SetPaused(false);
            Check(PM.TrySplit() && PM.SwitchActive(), "The carrier can remain inactive while a cooperating half is selected.");
            yield return new WaitForSeconds(.2f);
            Check(signal.Held, "Selecting another owned body alone does not invalidate the original carrier.");
            Check(PM.SwitchActive(), "Return to the original carrier.");
            yield return Release();
            yield return null; yield return null;
            Check(!signal.Held && host.Possessor == null && !RunState.Has(IslandRoster.SignalMounted), "Releasing the carrier drops transient custody instead of remotely mounting.");
            Check(Vector3.Distance(signal.CaseTransform.position, IslandCampaign.At(18, .97f, 18)) < .05f, "The actual case mesh returns to its safe cache after release.");
            yield return Reload("Signal dropped on checkpoint");
            Check(!IslandSignalTransport.Instance.Held && UnityEngine.Object.FindObjectsByType<IslandSignalPickup>(FindObjectsSortMode.None).Length == 1, "Reload creates one recoverable cache and no serialized carrier reference.");
            Check(IslandTravelers.Instance.Members.All(t => t.Delivered) && !IslandRoster.AllBoarded, "The completed roster persists while passenger assembly remains incomplete.");
            UseHost("Island supply marshal");
            yield return Walk(IslandCampaign.At(18, .15f, 16.3f));
            yield return PressAt(UnityEngine.Object.FindFirstObjectByType<IslandSignalPickup>(), 0);
            var other = MissionSpawner.Find("Island rescue volunteer");
            PM.ForcePossessImmediate(other);
            yield return null; yield return null;
            Check(!IslandSignalTransport.Instance.Held, "Changing to another host cancels the original carrier even though this agent still owns a human.");
            yield return Reload("Signal host-swap recovery");
            UseHost("Island supply marshal");
            yield return Walk(IslandCampaign.At(18, .15f, 16.3f));
            yield return PressAt(UnityEngine.Object.FindFirstObjectByType<IslandSignalPickup>(), 0);
            host = PM.Active.Host;
            host.Capture(); PM.OnHostCaptured(host, HostCharacter.All.Select(h => h ? h.Brain as GuardAI : null).First(g => g));
            yield return null; yield return null;
            Check(!IslandSignalTransport.Instance.Held && !RunState.Has(IslandRoster.SignalMounted), "Captured custody cancels before the three-second auto-eject timer.");
            Debug.Log("[IslandRescue] PASS signal reach, pause, inactive owner, release/capture and checkpoint custody");
        }

        public static IEnumerator AllyWitnessPolicy()
        {
            yield return StartAtRefuge(EndingOutcome.Peace);
            foreach (var h in HostCharacter.All.ToArray())
                if (h && !h.GetComponent<IslandTraveler>() && h.transform.position.x > 560) PlaceNpc(h, IslandCampaign.At(46, .15f, 32));
            PM.Active.Blob.Place(IslandCampaign.At(14, .15f, -8));
            var ally = IslandTravelers.Instance.Members[0].Host;
            PlaceNpc(ally, IslandCampaign.At(14, .15f, -6.8f));
            ally.transform.rotation = Quaternion.Euler(0, 180, 0);
            var ss = SuspicionSystem.Instance;
            Calm(); yield return null;
            Check(ss.CanSee(ally, PM.Active.Blob.Center, PM.Active.Blob.transform, 16), "The ally retains normal physical vision; the exception concerns reporting.");
            Check(!ss.ReportViolation(ViolationType.WitnessedSplit, PM.Active.Blob.Center, PM.Active.Blob), "A rescued ally does not report the free amoeba it is helping.");
            yield return new WaitForSeconds(.6f);
            Check(ss.Value == 0, "The same ally is excluded from real continuous free-amoeba sightings as well.");
            var neutral = MissionSpawner.Find("Island refuge engineer");
            Check(neutral.ReportsPlayerViolations, "Ordinary island/city NPCs retain the default reporting policy.");
            PlaceNpc(neutral, IslandCampaign.At(14, .15f, -9.5f));
            neutral.transform.rotation = Quaternion.identity;
            Check(ss.ReportViolation(ViolationType.WitnessedSplit, PM.Active.Blob.Center, PM.Active.Blob), "An ordinary awake witness still reports the same actual visible violation.");
            Debug.Log("[IslandRescue] PASS narrow ally reporting exception with ordinary witness control");
        }

        public static IEnumerator PeaceGuardExposure()
        {
            yield return StartAtRefuge(EndingOutcome.Peace);
            var traveler = IslandTravelers.Instance.Members[0];
            UseHost("Island refuge volunteer");
            // Stationary sentries perform their ordinary +50-degree idle scan. Face this post between
            // Holding and the short westward lead so both stay in its unchanged physical vision cone.
            var guard = MissionSpawner.Spawn("Island exposure acceptance sentry", Profession.Guard, IslandCampaign.At(11, .15f, -13), -35, 1960);
            try
            {
                Check(SuspicionSystem.Instance.CanSee(guard, traveler.Host.ChestPosition, traveler.transform, 20), "The exposure test uses a real unobstructed sentry cone.");
                var holding = IslandRoster.Holding[0];
                var before = traveler.transform.position;
                yield return new WaitForSeconds(2.3f);
                Check(Vector3.Distance(before, traveler.transform.position) < .5f, "An unasked prisoner does not flee its holding point simply because the sentry is watching.");
                Check(IslandRoster.SavedCover(traveler.StableId) == -1, "No authored cover was physically visited before this first exposed escort.");
                LogExposureState("before ask", guard, traveler);
                yield return AskToFollow(traveler);
                // Move the human with real input before the two-second exposure window finishes. This short
                // westward lead stays far from every cover, so return-to-Holding must be earned by motion.
                var shortLead = IslandCampaign.At(6.2f, .15f, -9.5f);
                yield return Walk(shortLead, phase: "exposure fixture: physical lead before regroup");
                Check(FlatDistance(traveler.transform.position, holding) > 1.25f && IslandRoster.SavedCover(traveler.StableId) == -1,
                    "The real follower has left its initial holding pose without gaining any unvisited cover progress.");
                LogExposureState("after physical lead", guard, traveler);
                Check(SuspicionSystem.Instance.CanSee(guard, traveler.Host.ChestPosition, traveler.transform, 20), "The actual sentry still sees the displaced follower before exposure triggers.");
                float nextExposureLog = 0;
                yield return WaitUntil(() =>
                {
                    if (Time.realtimeSinceStartup >= nextExposureLog)
                    {
                        nextExposureLog = Time.realtimeSinceStartup + .15f;
                        LogExposureState("waiting for actual exposure", guard, traveler);
                    }
                    return traveler.GetPrompt(Context()).StartsWith("Regroup", StringComparison.Ordinal);
                }, 4, "Sustained actual guard sight breaks the moving escort and asks for regrouping.");
                LogExposureState("regroup observed", guard, traveler);
                var retreatFrom = traveler.transform.position;
                Check(!traveler.Delivered && IslandRoster.SavedCover(traveler.StableId) == -1, "Exposure cannot count as a rescue or invent a cover visit.");
                yield return WaitUntil(() => FlatDistance(traveler.transform.position, holding) < 1.4f, 10, "The exposed survivor physically returns to its actually visited initial Holding safe point.");
                Check(FlatDistance(retreatFrom, traveler.transform.position) > .4f, "The retreat requires real NPC displacement, rather than passing while still in its initial pose.");
                // Draw the same real sentry away through normal navigation; no follower warp or progress flag.
                ((GuardAI)guard.Brain).OrderInvestigate(IslandCampaign.At(40, .15f, -12));
                yield return WaitUntil(() => FlatDistance(guard.transform.position, traveler.transform.position) > 22, 10, "The real sentry walks out of the exposure range before regrouping.");
                yield return AskToFollow(traveler);
                Check(!traveler.GetPrompt(Context()).StartsWith("Regroup", StringComparison.Ordinal), "A fresh real aimed Regroup/E creates the renewed agreement.");
                yield return Walk(shortLead, traveler, "exposure fixture: renewed following after E");
                Check(FlatDistance(traveler.transform.position, holding) > 1.25f && IslandRoster.SavedCover(traveler.StableId) == -1,
                    "The regrouped survivor physically follows again without receiving any cover flag.");
                Debug.Log("[IslandRescue] PASS actual sentry exposure, physical Holding retreat and renewed aimed E following with no cover progress");
            }
            finally { if (guard) UnityEngine.Object.Destroy(guard.gameObject); }
        }

        static void LogExposureState(string phase, HostCharacter guard, IslandTraveler traveler)
        {
            // Read-only acceptance diagnostics: observe the real vision/exposure state without changing its clock or gates.
            const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            object Read(string name) => typeof(IslandTraveler).GetField(name, fields)?.GetValue(traveler);
            Vector3 eye = guard.EyePosition, chest = traveler.Host.ChestPosition, delta = chest - eye;
            Vector3 flat = new Vector3(delta.x, 0, delta.z);
            var ss = SuspicionSystem.Instance;
            Debug.Log("[IslandExposure] phase=" + phase + " frame=" + Time.frameCount + " real=" + Time.realtimeSinceStartup.ToString("F3")
                + " sim=" + Time.time.ToString("F3") + " dt=" + Time.deltaTime.ToString("F6") + " timeScale=" + Time.timeScale
                + " guardAt=" + guard.transform.position.ToString("F3") + " guardYaw=" + guard.transform.eulerAngles.y.ToString("F3")
                + " guardForward=" + guard.transform.forward.ToString("F3") + " guardState=" + guard.State + " guardAwake=" + guard.IsAwakeNpc
                + " guardAI=" + (guard.Brain is GuardAI ai ? ai.GState.ToString() : "not GuardAI") + " guardRoutine=" + guard.Brain.behaviour
                + " guardNavVelocity=" + (guard.Nav.enabled ? guard.Nav.velocity.ToString("F3") : "disabled")
                + " travelerAt=" + traveler.transform.position.ToString("F3") + " travelerState=" + traveler.Host.State
                + " range=" + delta.magnitude.ToString("F3") + " coneAngle=" + Vector3.Angle(guard.transform.forward, flat).ToString("F3")
                + " halfFov=" + (ss ? (ss.fovDegrees * .5f).ToString("F3") : "missing")
                + " actualCanSee=" + (ss && ss.CanSee(guard, chest, traveler.transform, 20))
                + " exposure=" + Read("exposure") + " scan=" + Read("scan") + " returning=" + Read("returning")
                + " hasLeader=" + (Read("leader") != null) + " hasLeaderHost=" + (Read("leaderHost") is HostCharacter)
                + " delivered=" + traveler.Delivered + " savedCover=" + IslandRoster.SavedCover(traveler.StableId)
                + " prompt=" + traveler.GetPrompt(Context()) + " actualRayHits=["
                + string.Join("; ", Physics.RaycastAll(eye, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore)
                    .OrderBy(hit => hit.distance).Select(hit => DescribeCollider(hit.collider) + " distance=" + hit.distance.ToString("F3")
                        + " self=" + hit.transform.IsChildOf(guard.transform) + " target=" + hit.transform.IsChildOf(traveler.transform))) + "]");
        }

        public static IEnumerator PeaceRegainedHostNeedsRegroup(bool captured)
        {
            yield return StartAtRefuge(EndingOutcome.Peace);
            var traveler = IslandTravelers.Instance.Members[0];
            UseHost("Island relief scientist");
            yield return AskToFollow(traveler);
            yield return LeadToFirstVisitedCover(traveler);
            var originalAgent = PM.Active;
            var originalHost = originalAgent.Host;
            int originalPossession = originalHost.PossessionGeneration;
            if (!captured)
            {
                Check(PM.TrySplit() && PM.SwitchActive(), "Select a second free half while the original leader host remains owned.");
                yield return new WaitForSeconds(.25f);
                Check(!traveler.GetPrompt(Context()).StartsWith("Regroup", StringComparison.Ordinal) && originalHost.PossessionGeneration == originalPossession,
                    "Inactive selection preserves the unchanged leader agreement.");
                Check(PM.SwitchActive() && PM.Active == originalAgent, "Return to the standing leader without changing its owner.");
                yield return Walk(IslandRoster.Cover[2], traveler);
            }
            if (captured)
            {
                int before = originalHost.CaptureGeneration;
                originalHost.Capture();
                PM.OnHostCaptured(originalHost, AnyGuard());
                PM.Eject(originalAgent); // No yield: both transitions happen before IslandTraveler.Update.
                Check(originalHost.CaptureGeneration == before + 1 && originalHost.State == HostState.Unconscious && originalAgent.Host == null,
                    "The real capture/eject path records a capture even when Captured is never sampled by Update.");
            }
            else yield return Release();
            yield return WaitUntil(() => originalHost.State == HostState.Normal, 12, "The exact former leader body recovers through its real unconscious timer.");
            PM.ForcePossessImmediate(originalHost);
            Check(PM.Active == originalAgent && originalAgent.Host == originalHost && originalHost.PossessionGeneration > originalPossession,
                "The same amoeba reacquires the same body in a new possession session, without reloading.");
            int cover = IslandRoster.SavedCover(traveler.StableId);
            var safe = IslandRoster.Cover[cover] + Vector3.left * 1.5f;
            yield return Walk(IslandCampaign.At(7, .15f, -6));
            yield return new WaitForSeconds(2);
            Check(traveler.GetPrompt(Context()).StartsWith("Regroup", StringComparison.Ordinal) && Vector3.Distance(traveler.transform.position, safe) < 1.4f,
                "Reacquiring the original body never silently restores the lost escort agreement; the person remains at visited cover.");
            Check(!traveler.Delivered, "Same-body reacquisition cannot finish the escort remotely.");
            yield return AskToFollow(traveler); // A new real aimed E is required to resume.
            yield return Walk(IslandCampaign.At(7, .15f, -6), traveler);
            Check(Vector3.Distance(traveler.transform.position, safe) > 2 && !traveler.GetPrompt(Context()).StartsWith("Regroup", StringComparison.Ordinal),
                "A fresh Regroup/E restores actual following after the ownership-loss latch.");
            Debug.Log("[IslandRescue] PASS " + (captured ? "capture/immediate-eject" : "release") + " then same-agent/same-host regain requires fresh E; inactive ownership preserved");
        }

        public static IEnumerator CourierRenewedPossessionCancelsRelease()
        {
            yield return StartAtRefuge(EndingOutcome.Dominion);
            var traveler = IslandTravelers.Instance.Members[0];
            PM.ForcePossessImmediate(traveler.Host);
            yield return PressAt(traveler, 180);
            yield return Walk(IslandRoster.Handoffs[0]);
            int markedPossession = traveler.Host.PossessionGeneration;
            yield return Release();
            yield return WaitUntil(() => traveler.Host.State == HostState.Normal, 12, "A valid handoff release reaches the real recovery frame.");
            Check(!traveler.Delivered && traveler.Host.CanBePossessed, "Recovery's normal notice delay leaves a real pre-delivery re-possession window.");
            PM.ForcePossessImmediate(traveler.Host);
            Check(traveler.Host.PossessionGeneration > markedPossession, "Renewed control starts a different possession session.");
            var wrongLane = IslandRoster.Handoffs[0] + new Vector3(-5, 0, -4);
            yield return Walk(wrongLane);
            yield return Release();
            yield return WaitUntil(() => traveler.Host.State == HostState.Normal, 12, "The courier released at the wrong lane recovers a second time.");
            yield return new WaitForSeconds(7);
            Check(!traveler.Delivered && FlatDistance(traveler.transform.position, wrongLane) < 1,
                "The old release authorization cannot survive renewed possession and carry a wrong-lane release through the former handoff.");
            PM.ForcePossessImmediate(traveler.Host);
            yield return PressAt(traveler, 180);
            yield return Walk(IslandRoster.Handoffs[0]);
            yield return Release();
            yield return WaitUntil(() => traveler.Delivered, 16, "A fresh mark, actual lane and voluntary release remain a fair successful retry.");
            Debug.Log("[IslandRescue] PASS renewed courier possession invalidates the previous release and accepts a fresh marked retry");
        }

        public static IEnumerator CourierImmediateCaptureEjectCancelsMark()
        {
            yield return StartAtRefuge(EndingOutcome.Dominion);
            var traveler = IslandTravelers.Instance.Members[0];
            PM.ForcePossessImmediate(traveler.Host);
            yield return PressAt(traveler, 180);
            yield return Walk(IslandRoster.Handoffs[0]);
            var original = traveler.transform.position;
            var agent = PM.Active;
            int before = traveler.Host.CaptureGeneration;
            traveler.Host.Capture();
            PM.OnHostCaptured(traveler.Host, AnyGuard());
            PM.Eject(agent); // Intentionally no yield from mark's captured state to its unconscious ejection.
            Check(traveler.Host.CaptureGeneration == before + 1 && traveler.Host.State == HostState.Unconscious && traveler.Host.Possessor == null && !agent.CapturedPendingEject,
                "Capture then immediate real ejection completes before the courier's next Update, leaving only Unconscious visible.");
            yield return WaitUntil(() => traveler.Host.State == HostState.Normal, 12, "The captured/ejected courier still recovers through ordinary host state semantics.");
            yield return new WaitForSeconds(3);
            Check(!traveler.Delivered && FlatDistance(traveler.transform.position, original) < .9f,
                "An unseen intermediate capture cancels the mark instead of authorizing autonomous delivery from the lane.");
            PM.ForcePossessImmediate(traveler.Host);
            yield return PressAt(traveler, 180);
            yield return Walk(IslandRoster.Handoffs[0]);
            yield return Release();
            yield return WaitUntil(() => traveler.Delivered, 16, "The same person can complete after a new legitimate agreement and release.");
            Debug.Log("[IslandRescue] PASS no-yield capture/eject cancels courier authorization and retains a fair retry");
        }

        public static IEnumerator BoardingToleranceRequiresInset(int slot)
        {
            Check(slot == 0 || slot == 2, "Exercise one of the two outer passenger slots.");
            yield return StartAtRefuge(EndingOutcome.Dominion);
            var traveler = IslandTravelers.Instance.Members[slot];
            MissionManager.SetFlag(IslandRoster.Delivered(traveler.StableId));
            MissionManager.SetFlag(IslandRoster.SignalMounted);
            var host = traveler.Host;
            var edge = IslandCampaign.At(slot == 0 ? 31.30f : 36.70f, .55f, 44.5f);
            var nav = host.Nav;
            bool automaticPosition = nav.updatePosition;
            try
            {
                Check(nav.Warp(IslandRoster.Board[slot]), "The real NPC has a valid nav agent on its boarding slot.");
                // Pin only the transform for this synchronous boundary tick: NavMesh voxel quantization must not
                // silently snap the legal deck-edge fixture inside the assembly inset before Drive examines it.
                nav.updatePosition = false;
                host.transform.position = edge;
                Physics.SyncTransforms();
                Check(!IslandRoster.DeckBounds.Contains(host.transform.position) && Vector3.Distance(host.transform.position, IslandRoster.Board[slot]) < .8f,
                    "The actual person occupies the reviewed .7m tolerance edge outside the inset, without a hidden NavMesh snap.");
                Check(Physics.Raycast(edge + Vector3.up, Vector3.down, out var footing, 2, Layers.EnvironmentMask) && footing.collider.name == "Rescue static deck",
                    "This edge is supported by the real ferry deck collider, rather than water or an artificial platform.");
                host.Brain.Tick(0); // Executes the real RoutineOverride/Drive before any automatic nav transform step.
                Check(!traveler.Boarded && !nav.isStopped && !IslandRoster.DeckBounds.Contains(host.transform.position),
                    "The outer arrival sphere cannot commit or stop the person outside the extraction bounds; navigation keeps approaching the slot.");
            }
            finally { nav.updatePosition = automaticPosition; }
            // Prove normal runtime navigation independently, starting on the real approach outside the bow inset.
            var approach = IslandCampaign.At(34, .55f, 42.8f);
            PlaceNpc(host, approach);
            Check(!IslandRoster.DeckBounds.Contains(host.transform.position), "The actual boarding walk starts on the gangway before the inset.");
            var start = host.transform.position;
            yield return WaitUntil(() => traveler.Boarded, 12, "The actual autonomous NPC walks from the gangway into its accepted deck slot.");
            Check(IslandRoster.DeckBounds.Contains(host.transform.position) && Vector3.Distance(start, host.transform.position) > 1,
                "Boarded is committed only after real motion enters the same inset used by defensive extraction.");
            Debug.Log("[IslandRescue] PASS outer slot " + slot + " tolerance edge rejects commit, keeps moving, and boards after physical inset entry");
        }

        public static IEnumerator ParkedPassengersKeepGangwayClear()
        {
            yield return StartAtRefuge(EndingOutcome.Dominion);
            UseHost("Island rescue volunteer");
            // Start the human at the exact center-aisle location observed in the blocked live run.
            yield return Walk(IslandCampaign.At(34, .55f, 44.335f), phase: "boarding clearance: human at entrance");
            var roster = IslandTravelers.Instance;
            for (int i = 0; i < roster.Members.Count; i++)
            {
                var person = roster.Members[i];
                MissionManager.SetFlag(IslandRoster.Delivered(person.StableId));
                // Isolate boarding from the earlier escort: actual unoccupied people start in a spaced queue
                // on the existing physical gangway. No Boarded flag is granted by this fixture setup.
                PlaceNpc(person.Host, IslandCampaign.At(34, .55f, 42.8f - i * 1.2f));
                Check(!person.Boarded && person.Host.State == HostState.Normal && person.Host.Possessor == null
                    && person.Host.Nav.enabled && person.Host.Nav.isOnNavMesh,
                    "An actual awake passenger waits on real gangway navigation before the boarding stage.");
                var route = new NavMeshPath();
                Check(NavMesh.CalculatePath(person.transform.position, IslandRoster.Board[i], NavMesh.AllAreas, route) && route.status == NavMeshPathStatus.PathComplete,
                    "The actual queue position has a complete path to its berth: " + person.StableId);
                Check(NavMesh.SamplePosition(IslandRoster.Board[i], out var sample, .5f, NavMesh.AllAreas), "The assigned berth has real navigation footing.");
                float vertical = Mathf.Abs(sample.position.y - IslandRoster.Board[i].y);
                float planarAllowance = Mathf.Sqrt(Mathf.Max(0, IslandRoster.BoardArrivalTolerance * IslandRoster.BoardArrivalTolerance - vertical * vertical));
                Check(person.Host.Nav.stoppingDistance + .02f < planarAllowance,
                    "The .35m arrival sphere is achievable with this actual stopping distance and deck vertical delta: " + person.StableId);
                Debug.Log("[IslandRescue] BERTH " + person.StableId + " target=" + IslandRoster.Board[i] + " sample=" + sample.position
                    + " stopping=" + person.Host.Nav.stoppingDistance + " vertical=" + vertical.ToString("F3") + " planarAllowance=" + planarAllowance.ToString("F3"));
            }
            MissionManager.SetFlag(IslandRoster.SignalMounted);
            float deadline = Time.realtimeSinceStartup + 18;
            while (!IslandRoster.AllBoarded && Time.realtimeSinceStartup < deadline)
            {
                for (int i = 0; i < roster.Members.Count; i++)
                {
                    var person = roster.Members[i];
                    if (!person.Boarded) continue;
                    Check(IslandRoster.DeckBounds.Contains(person.transform.position)
                        && Vector3.Distance(person.transform.position, IslandRoster.Board[i]) <= IslandRoster.BoardArrivalTolerance + .01f,
                        "The real passenger reaches the stricter assigned berth before its boarding flag commits: " + person.StableId);
                }
                Calm(); yield return null;
            }
            Check(IslandRoster.AllBoarded, "All three real passengers walk from the gangway to their clear parking berths.");
            for (int i = 0; i < roster.Members.Count; i++)
            {
                var person = roster.Members[i];
                Check(IslandRoster.DeckBounds.Contains(person.transform.position)
                    && Vector3.Distance(person.transform.position, IslandRoster.Board[i]) <= IslandRoster.BoardArrivalTolerance + .01f,
                    "Every boarding commit follows actual final berth arrival, including the final person: " + person.StableId);
                Check(Mathf.Abs(person.transform.position.x - IslandCampaign.At(34, 0, 0).x) > 1,
                    "A permanently boarded passenger does not occupy the gangway center aisle: " + person.StableId);
            }
            Check(!IslandExtraction.ReadyToDepart(out _), "Clearing the aisle does not bypass the occupied-human finalization gate.");
            yield return Walk(IslandCampaign.At(34, .15f, 38), phase: "boarding clearance: return down same gangway");
            yield return Walk(IslandCampaign.At(34, .55f, 44.335f), phase: "boarding clearance: reenter same gangway");
            Check(IslandRoster.AllBoarded && PM.Active.Host && PM.Active.Host.State == HostState.Possessed,
                "The actual controlled human can leave and reenter after parking, with people and ownership unchanged.");
            Debug.Log("[IslandRescue] PASS actual berth arrival and human gangway round trip after all three passengers park");
        }

        public static IEnumerator DeliveredClinicPassengerRequiresPhysicalSidestep()
        {
            yield return StartAtRefuge(EndingOutcome.Peace);
            UseHost("Island refuge volunteer");
            var morgan = IslandTravelers.Instance.Members.Single(t => t.StableId == "morgan");
            yield return AskToFollow(morgan);
            yield return LeadThroughCover(morgan);
            yield return WaitUntil(() => morgan.Delivered, 8, "Morgan physically reaches the unchanged clinic arrival sphere.");
            // This focused avoidance regression deliberately aligns a head-on contact. The full-branch route
            // retains its original waypoints; no NPC placement, body warp, collider change or rescue flag is added here.
            Stop();
            float settleUntil = Time.realtimeSinceStartup + 3, stableSince = Time.realtimeSinceStartup;
            Vector3 settlingPerson = morgan.transform.position;
            while (Time.realtimeSinceStartup < settleUntil)
            {
                if (FlatDistance(settlingPerson, morgan.transform.position) > .005f || morgan.Host.Nav.velocity.sqrMagnitude > .0025f)
                { settlingPerson = morgan.transform.position; stableSince = Time.realtimeSinceStartup; }
                if (Time.realtimeSinceStartup - stableSince >= .25f) break;
                Calm(); yield return null;
            }
            Check(Time.realtimeSinceStartup - stableSince >= .25f && morgan.Host.State == HostState.Normal && morgan.Delivered,
                "The actual delivered passenger settles before the deliberate centerline obstruction is arranged.");
            Vector3 north = morgan.transform.position + Vector3.forward * 1.6f; north.y = PM.Active.Position.y;
            yield return Walk(north, phase: "avoidance regression setup: safe north approach to settled Morgan");
            Check(PM.Active.Position.z - morgan.transform.position.z > .9f, "Lateral alignment starts safely north of the unchanged person capsule.");
            float alignBegan = Time.realtimeSinceStartup, alignUntil = alignBegan + 3, alignedSince = alignBegan;
            Vector3 alignStart = PM.Active.Position;
            Debug.Log("[IslandClinicAlign] BEGIN actual=" + alignStart.ToString("F6") + " person=" + morgan.transform.position.ToString("F6")
                + " personVelocity=" + morgan.Host.Nav.velocity.ToString("F6"));
            while (Time.realtimeSinceStartup < alignUntil)
            {
                float error = morgan.transform.position.x - PM.Active.Position.x;
                if (Mathf.Abs(error) >= .003f) alignedSince = Time.realtimeSinceStartup;
                PlayerCamera.Instance.SetYaw(0);
                GameInput.SimMove = new Vector2(Mathf.Clamp(error * 12, -1, 1), 0);
                GameInput.SimSprint = false; Calm();
                if (Time.realtimeSinceStartup - alignedSince >= .2f) break;
                yield return null;
            }
            Stop(); yield return null; yield return null;
            Debug.Log("[IslandClinicAlign] END actual=" + PM.Active.Position.ToString("F6") + " actualDelta=" + (PM.Active.Position - alignStart).ToString("F6")
                + " person=" + morgan.transform.position.ToString("F6") + " xError=" + (morgan.transform.position.x - PM.Active.Position.x).ToString("F6")
                + " northGap=" + (PM.Active.Position.z - morgan.transform.position.z).ToString("F6") + " elapsed=" + (Time.realtimeSinceStartup - alignBegan).ToString("F6"));
            Check(Time.realtimeSinceStartup - alignedSince >= .2f && Mathf.Abs(morgan.transform.position.x - PM.Active.Position.x) < .003f
                && PM.Active.Position.z - morgan.transform.position.z > .9f,
                "Ordinary lateral input aligns and settles the actor on Morgan's actual centerline while retaining safe separation.");
            float contactBegan = Time.realtimeSinceStartup, stationarySince = contactBegan;
            Vector3 contactStart = PM.Active.Position, personStart = morgan.transform.position;
            LogClinicContact("BEGIN", morgan.Host, contactStart, personStart, contactStart, contactBegan, stationarySince, Vector2.down);
            float contactUntil = Time.realtimeSinceStartup + 1.2f;
            Vector3 stationary = PM.Active.Position;
            Vector2 contactInput = Vector2.down;
            while (Time.realtimeSinceStartup < contactUntil)
            {
                if (contactUntil - Time.realtimeSinceStartup > .4f)
                { stationary = PM.Active.Position; stationarySince = Time.realtimeSinceStartup; }
                // Real lateral feedback prevents the known offset-induced slide while pressing into the actual capsule.
                contactInput = new Vector2(Mathf.Clamp((morgan.transform.position.x - PM.Active.Position.x) * 20, -.4f, .4f), -1);
                PlayerCamera.Instance.SetYaw(0); GameInput.SimMove = contactInput;
                GameInput.SimSprint = false; Calm(); yield return null;
            }
            Stop();
            var controller = PM.Active.Host.Controller;
            LogClinicContact("PRECHECK", morgan.Host, contactStart, personStart, stationary, contactBegan, stationarySince, contactInput);
            Check(FlatDistance(stationary, PM.Active.Position) < .05f
                && TryPersonBlocker(controller, Vector3.back, out var blocker) && blocker == morgan.Host,
                "Actual input is stopped by Morgan's real capsule before the walker attempts a sidestep.");
            int before = completedSidesteps;
            Vector3 crossing = morgan.transform.position + Vector3.back * 1.5f; crossing.y = PM.Active.Position.y;
            yield return Walk(crossing, phase: "avoidance regression: strict physical centerline crossing past settled Morgan");
            Check(completedSidesteps > before && FlatDistance(PM.Active.Position, crossing) < .45f,
                "The deliberately blocked centerline leg requires a real completed sidestep and its unchanged strict arrival sphere.");
            var originalTarget = IslandTravelers.Instance.Members.Single(t => t.StableId == "avery").transform.position + Vector3.back * 1.5f;
            yield return Walk(originalTarget, phase: "delivered clinic passenger: physical sidestep to original Avery approach");
            Check(completedSidesteps > before && FlatDistance(PM.Active.Position, originalTarget) < .45f,
                "Real lateral input recovers the blocked leg and reaches its original strict arrival target.");
            Check(morgan.Delivered && !morgan.Boarded && morgan.Host.State == HostState.Normal && morgan.Host.Possessor == null
                && !RunState.Has(IslandRoster.SignalMounted),
                "Avoidance preserves the actual passenger, ownership and mission prerequisites.");
            Debug.Log("[IslandRescue] PASS real delivered clinic capsule blocks input, bounded physical sidestep restores original route");
        }

        static void LogClinicContact(string phase, HostCharacter person, Vector3 bodyStart, Vector3 personStart, Vector3 stationary,
            float began, float stationarySince, Vector2 requestedInput)
        {
            var agent = PM.Active; var body = agent.Host; var controller = body.Controller;
            Vector3 actual = agent.Position, relative = person.transform.position - actual;
            BodyCapsule(controller, Vector3.zero, out var bottom, out var top, out float radius);
            bool classified = TryPersonBlocker(controller, Vector3.back, out var blocker);
            var text = new StringBuilder("[IslandClinicContact] ");
            text.Append("phase=").Append(phase).Append(" frame=").Append(Time.frameCount).Append(" elapsed=").Append((Time.realtimeSinceStartup - began).ToString("F6"))
                .Append(" dt=").Append(Time.deltaTime.ToString("F6")).Append(" unscaledDt=").Append(Time.unscaledDeltaTime.ToString("F6")).Append(" timeScale=").Append(Time.timeScale)
                .Append(" body=").Append(body.DisplayName).Append(" at=").Append(actual.ToString("F6")).Append(" rootY=").Append(actual.y.ToString("F6"))
                .Append(" bodyStart=").Append(bodyStart.ToString("F6")).Append(" bodyDelta=").Append((actual - bodyStart).ToString("F6"))
                .Append(" person=").Append(person.DisplayName).Append(" personAt=").Append(person.transform.position.ToString("F6"))
                .Append(" personDelta=").Append((person.transform.position - personStart).ToString("F6")).Append(" relativePerson=").Append(relative.ToString("F6"))
                .Append(" flatDistance=").Append(FlatDistance(actual, person.transform.position).ToString("F6")).Append(" relativeX=").Append(relative.x.ToString("F6"))
                .Append(" stationaryFrom=").Append(stationary.ToString("F6")).Append(" stationarySeconds=").Append((Time.realtimeSinceStartup - stationarySince).ToString("F6"))
                .Append(" stationaryDelta=").Append((actual - stationary).ToString("F6")).Append(" stationaryFlat=").Append(FlatDistance(stationary, actual).ToString("F6"))
                .Append(" requestedInput=").Append(requestedInput).Append(" yaw=").Append(PlayerCamera.Instance.Yaw).Append(" simulated=").Append(GameInput.Simulated).Append(" inputLocked=").Append(PM.InputLocked)
                .Append(" state=").Append(body.State).Append(" owner=").Append(body.Possessor == agent).Append(" personState=").Append(person.State)
                .Append(" personHitBox=").Append(person.HitBox.enabled).Append(" personController=").Append(person.Controller.enabled)
                .Append(" personNav=").Append(person.Nav.enabled).Append(" personNavVelocity=").Append(person.Nav.enabled ? person.Nav.velocity.ToString("F6") : "disabled")
                .Append(" controller={enabled=").Append(controller.enabled).Append(" grounded=").Append(controller.isGrounded).Append(" flags=").Append(controller.collisionFlags)
                .Append(" velocity=").Append(controller.velocity.ToString("F6")).Append(" center=").Append(controller.center.ToString("F6"))
                .Append(" radius=").Append(controller.radius.ToString("F6")).Append(" height=").Append(controller.height.ToString("F6")).Append(" skin=").Append(controller.skinWidth.ToString("F6"))
                .Append(" minimumMove=").Append(controller.minMoveDistance.ToString("F6")).Append(" detect=").Append(controller.detectCollisions).Append('}')
                .Append(" capsule={bottom=").Append(bottom.ToString("F6")).Append(" top=").Append(top.ToString("F6")).Append(" radius=").Append(radius.ToString("F6"))
                .Append(" lowestY=").Append((bottom.y - radius).ToString("F6")).Append('}')
                .Append(" classifiedBackwardPerson=").Append(classified).Append(" classifiedName=").Append(blocker ? blocker.DisplayName : "none");
            string Hits(Vector3 direction)
            {
                direction.y = 0;
                if (direction.sqrMagnitude < .0001f) return "zero direction";
                return string.Join("; ", Physics.CapsuleCastAll(bottom, top, radius, direction.normalized, .35f, ~0, QueryTriggerInteraction.Ignore)
                    .OrderBy(hit => hit.distance).Select((hit, index) => index + ":" + DescribeCollider(hit.collider)
                        + " self=" + hit.collider.transform.IsChildOf(controller.transform)
                        + " host=" + (hit.collider.GetComponentInParent<HostCharacter>() ? hit.collider.GetComponentInParent<HostCharacter>().DisplayName : "none")
                        + " distance=" + hit.distance.ToString("F6") + " point=" + hit.point.ToString("F6") + " normal=" + hit.normal.ToString("F6")));
            }
            text.Append(" orderedBackwardHits=[").Append(Hits(Vector3.back)).Append("] orderedPersonCenterlineHits=[").Append(Hits(relative)).Append(']');
            text.Append(" capsuleOverlaps=[").Append(string.Join("; ", Physics.OverlapCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore)
                .OrderBy(c => c.GetInstanceID()).Select(c => DescribeCollider(c) + " self=" + c.transform.IsChildOf(controller.transform)))).Append(']');
            text.Append(" groundSupport=[").Append(string.Join("; ", Physics.RaycastAll(actual + Vector3.up * .4f, Vector3.down, .8f, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore)
                .OrderBy(hit => hit.distance).Select(hit => DescribeCollider(hit.collider) + " point=" + hit.point.ToString("F6") + " normal=" + hit.normal.ToString("F6")
                    + " rootGap=" + (actual.y - hit.point.y).ToString("F6") + " capsuleGap=" + (bottom.y - radius - hit.point.y).ToString("F6")))).Append(']');
            Debug.Log(text.ToString());
        }

        static GuardAI AnyGuard() => HostCharacter.All.Select(h => h ? h.Brain as GuardAI : null).First(g => g);

        static IEnumerator StartAtRefuge(EndingOutcome outcome)
        {
            yield return IslandEntryAcceptance.EnterFromFerry(outcome);
            // Mission18 traversal is covered by scenario21. This fixture begins the independent moving-NPC slice at its completed prerequisite.
            PM.Active.Blob.Place(IslandCampaign.Refuge);
            MissionManager.SetFlag(IslandCampaign.InnerRefuge);
            yield return WaitUntil(() => IslandTravelers.Instance && IslandTravelers.Instance.Members.Count == 3, 3, "The actual chapter creates its roster after entry.");
            ParkPatrols(); Calm();
        }
        static void UseHost(string name)
        {
            var host = MissionSpawner.Find(name);
            Check(host && (host == PM.Active.Host || host.CanBePossessed), "A reachable ordinary role host is available: " + name);
            if (PM.Active.Host != host) PM.ForcePossessImmediate(host);
            Debug.Log("[IslandRescue] BODY " + host.DisplayName + " at " + host.transform.position + " state=" + host.State + " agent=" + PM.Active.Id);
        }
        static IEnumerator AskToFollow(IslandTraveler traveler)
        {
            yield return Walk(traveler.transform.position + Vector3.back * 1.5f, phase: "approach person " + traveler.StableId + " delivered=" + traveler.Delivered + " boarded=" + traveler.Boarded);
            yield return PressAt(traveler, 0);
        }
        static IEnumerator LeadToFirstVisitedCover(IslandTraveler traveler, [CallerMemberName] string caller = null)
        {
            var marker = IslandRoster.Cover[1];
            string flag = IslandRoster.CoverFlag(traveler.StableId, 1);
            Check(!RunState.Has(flag), "The cover prerequisite starts without a fixture-granted visit.");
            float closest = Vector3.Distance(traveler.transform.position, marker);
            // Keep the original marker leg. Continue west, below the wall, so the trailing person also
            // crosses its real1.8m visit sphere instead of parking behind a leader who stopped at its center.
            var targets = new[] { marker, marker + new Vector3(-3, 0, .3f) };
            for (int index = 0; index < targets.Length; index++)
            {
                string phase = caller + (index == 0 ? " original cover marker" : " lead through actual cover sphere");
                var leg = Walk(targets[index], traveler, phase);
                try
                {
                    while (leg.MoveNext())
                    {
                        closest = Mathf.Min(closest, Vector3.Distance(traveler.transform.position, marker));
                        yield return leg.Current; // The unchanged walker continues real simulated input.
                    }
                }
                finally { (leg as IDisposable)?.Dispose(); }
                closest = Mathf.Min(closest, Vector3.Distance(traveler.transform.position, marker));
                var leader = PM.Active.Host.transform;
                int slot = Array.IndexOf(IslandRoster.Ids, traveler.StableId);
                Vector3 formation = leader.position - leader.forward * 1.5f + leader.right * ((slot - 1) * .8f);
                Debug.Log("[IslandCoverVisit] phase=" + phase + " leader=" + leader.position.ToString("F3") + " forward=" + leader.forward.ToString("F3")
                    + " npc=" + traveler.transform.position.ToString("F3") + " formation=" + formation.ToString("F3")
                    + " npcDistance=" + Vector3.Distance(traveler.transform.position, marker).ToString("F3")
                    + " formationDistance=" + Vector3.Distance(formation, marker).ToString("F3") + " closest=" + closest.ToString("F3")
                    + " flag1=" + RunState.Has(flag) + " savedCover=" + IslandRoster.SavedCover(traveler.StableId));
            }
            float deadline = Time.realtimeSinceStartup + 5;
            while (!RunState.Has(flag) && Time.realtimeSinceStartup < deadline)
            {
                closest = Mathf.Min(closest, Vector3.Distance(traveler.transform.position, marker));
                Calm(); yield return null;
            }
            Check(closest < 1.8f && RunState.Has(flag) && IslandRoster.SavedCover(traveler.StableId) == 1,
                "The real follower enters the unchanged1.8m sphere, commits flag1 and gains no later-cover progress.");
        }
        static IEnumerator LeadThroughCover(IslandTraveler traveler)
        {
            for (int i = 1; i < IslandRoster.Cover.Length; i++) yield return Walk(IslandRoster.Cover[i], traveler, "lead " + traveler.StableId + " via cover " + i);
            yield return Walk(IslandRoster.Clinic[Array.IndexOf(IslandRoster.Ids, traveler.StableId)], traveler, "clinic arrival " + traveler.StableId);
        }
        static IEnumerator PressAt(IInteractable interactable, float yaw)
        {
            PlayerCamera.Instance.SetYaw(yaw);
            yield return new WaitForSeconds(.2f);
            var target = PM.ComputeTarget();
            Check(target.kind == TargetKind.Interact && target.interactable == interactable && target.allowed,
                "Actual aimed E selects the intended available target: " + interactable.GetType().Name + "; got " + target.prompt);
            GameInput.SimInteract = true; yield return null;
            GameInput.SimInteract = false; yield return null;
        }
        static IEnumerator Release()
        {
            GameInput.SimLeave = true; yield return null;
            GameInput.SimLeave = false; yield return null;
        }
        static int completedSidesteps;
        static IEnumerator Walk(Vector3 target, IslandTraveler follower = null, string phase = null, [CallerMemberName] string caller = null)
        {
            string leg = phase ?? caller ?? "walk";
            Check(NavMesh.SamplePosition(PM.Active.Position, out var a, 2, NavMesh.AllAreas) && NavMesh.SamplePosition(target, out _, 2, NavMesh.AllAreas), "Both ends of the human/assembly leg have actual navigation footing: " + leg);
            NavMesh.SamplePosition(target, out var b, 2, NavMesh.AllAreas);
            var path = new NavMeshPath();
            Check(NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete, "The authored moving-person route has a complete physical path to " + target + ": " + leg);
            float length = 0;
            for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            float budget = Mathf.Max(20, length / (follower ? 1.35f : 2.4f) * 1.8f + 8);
            float beganReal = Time.realtimeSinceStartup, beganSim = Time.time;
            float deadline = beganReal + budget;
            float nextProgress = beganReal + 8, progressReal = beganReal;
            Vector3 progressPosition = PM.Active.Position;
            int corner = 1;
            Vector3 aim = path.corners.Length > 1 ? path.corners[1] : target;
            bool wait = false;
            bool sidestepping = false;
            Vector3 sidestep = default;
            float sidestepDeadline = 0;
            int sidestepAttempts = 0;
            Debug.Log("[IslandRescueWalk] BEGIN phase=" + leg + " length=" + length.ToString("F2") + " budget=" + budget.ToString("F2")
                + " from=" + PM.Active.Position + " target=" + target + " samples=" + a.position + " -> " + b.position + " corners=" + Corners(path.corners));
            while (Time.realtimeSinceStartup < deadline)
            {
                var final = target - PM.Active.Position; final.y = 0;
                if (final.magnitude < .45f)
                {
                    Stop();
                    Debug.Log("[IslandRescueWalk] END phase=" + leg + " at=" + PM.Active.Position + " real=" + (Time.realtimeSinceStartup - beganReal).ToString("F2")
                        + " sim=" + (Time.time - beganSim).ToString("F2") + " targetError=" + final.magnitude.ToString("F3"));
                    yield break;
                }
                while (corner < path.corners.Length - 1 && FlatDistance(PM.Active.Position, path.corners[corner]) < .4f) corner++;
                aim = corner < path.corners.Length - 1 ? path.corners[corner] : target;
                float now = Time.realtimeSinceStartup;
                if (sidestepping)
                {
                    if (FlatDistance(PM.Active.Position, sidestep) < .15f)
                    {
                        completedSidesteps++; sidestepping = false;
                        progressPosition = PM.Active.Position; progressReal = now;
                        Debug.Log("[IslandRescueWalk] SIDESTEP_RECOVER phase=" + leg + " actual=" + PM.Active.Position.ToString("F3")
                            + " sidestep=" + sidestep.ToString("F3") + " resumeCorner=" + corner + " resumeAim=" + aim.ToString("F3")
                            + " originalTarget=" + target.ToString("F3") + " remainingBudget=" + (deadline - now).ToString("F2"));
                    }
                    else if (now >= sidestepDeadline)
                    {
                        Stop();
                        throw new InvalidOperationException("Physical NPC sidestep exceeded4s/original deadline: phase=" + leg + " actual=" + PM.Active.Position
                            + " sidestep=" + sidestep + " originalTarget=" + target);
                    }
                }
                var delta = (sidestepping ? sidestep : aim) - PM.Active.Position; delta.y = 0;
                wait = follower && FlatDistance(follower.transform.position, PM.Active.Position) > 3.2f;
                PlayerCamera.Instance.SetYaw(0);
                GameInput.SimMove = wait ? Vector2.zero : new Vector2(delta.normalized.x, delta.normalized.z);
                GameInput.SimSprint = false; Calm();
                Check(GameManager.Instance && GameManager.Instance.IsPlaying, "Walking keeps the playable chapter active: " + leg);
                if (now - progressReal >= 2)
                {
                    float moved = FlatDistance(progressPosition, PM.Active.Position);
                    if (!wait && moved < .1f)
                    {
                        Debug.Log("[IslandRescueWalk] STALL " + WalkState(leg, target, aim, path.corners, corner, follower, wait, beganReal, beganSim, budget, true));
                        var controller = PM.Active.Host ? PM.Active.Host.Controller : PM.Active.Blob ? PM.Active.Blob.Controller : null;
                        if (!sidestepping && sidestepAttempts < 3 && controller && controller.enabled
                            && (controller.collisionFlags & CollisionFlags.Sides) != 0
                            && TryPersonBlocker(controller, aim - PM.Active.Position, out var blocker))
                        {
                            sidestepAttempts++;
                            Debug.Log("[IslandRescueWalk] PERSON_BLOCKED phase=" + leg + " blocker=" + blocker.DisplayName
                                + " person=" + blocker.transform.position.ToString("F3") + " actual=" + PM.Active.Position.ToString("F3")
                                + " movedIn2s=" + moved.ToString("F3") + " attempt=" + sidestepAttempts + "/3");
                            if (TrySidestep(controller, aim, leg, out sidestep))
                            {
                                sidestepping = true; sidestepDeadline = Mathf.Min(deadline, now + 4);
                                Debug.Log("[IslandRescueWalk] SIDESTEP_BEGIN phase=" + leg + " blocker=" + blocker.DisplayName
                                    + " from=" + PM.Active.Position.ToString("F3") + " sidestep=" + sidestep.ToString("F3")
                                    + " originalAim=" + aim.ToString("F3") + " originalTarget=" + target.ToString("F3")
                                    + " deadlineUnchanged=" + deadline.ToString("F3"));
                            }
                        }
                    }
                    progressPosition = PM.Active.Position; progressReal = now;
                }
                if (now >= nextProgress)
                {
                    nextProgress = now + 8;
                    Debug.Log("[IslandRescueWalk] PROGRESS " + WalkState(leg, target, aim, path.corners, corner, follower, wait, beganReal, beganSim, budget, false));
                }
                yield return null;
            }
            string details = WalkState(leg, target, aim, path.corners, corner, follower, wait, beganReal, beganSim, budget, true);
            Stop(); Debug.LogError("[IslandRescueWalk] FAIL " + details);
            throw new InvalidOperationException("Physical island leg timed out: " + details);
        }
        static bool TryPersonBlocker(CharacterController controller, Vector3 direction, out HostCharacter person)
        {
            person = null; direction.y = 0;
            if (direction.sqrMagnitude < .0001f) return false;
            BodyCapsule(controller, Vector3.zero, out var bottom, out var top, out float radius);
            // The query uses the real controller radius, with no collider or character-size changes.
            foreach (var hit in Physics.CapsuleCastAll(bottom, top, radius, direction.normalized, .35f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            {
                if (!hit.collider || hit.collider.transform.IsChildOf(controller.transform)) continue;
                var host = hit.collider.GetComponentInParent<HostCharacter>();
                if (host && host != PM.Active.Host && (hit.collider is CapsuleCollider || hit.collider is CharacterController))
                { person = host; return true; }
                return false; // A nearer static blocker must never be mistaken for the person behind it.
            }
            return false;
        }
        static bool TrySidestep(CharacterController controller, Vector3 originalAim, string phase, out Vector3 destination)
        {
            destination = default;
            Vector3 from = PM.Active.Position, forward = originalAim - from; forward.y = 0; forward.Normalize();
            Vector3 lateral = new Vector3(forward.z, 0, -forward.x);
            bool found = false; float best = float.PositiveInfinity;
            foreach (float distance in new[] { 1f, 1.4f, 1.8f })
                foreach (int side in new[] { -1, 1 })
                {
                    Vector3 requested = from + lateral * (distance * side);
                    Vector3 candidate = requested, navigationPoint = requested;
                    string reason = null;
                    if (!NavMesh.SamplePosition(from, out var source, .2f, NavMesh.AllAreas)
                        || !NavMesh.SamplePosition(requested, out var sample, .18f, NavMesh.AllAreas)) reason = "no nearby navigation footing";
                    else
                    {
                        navigationPoint = sample.position;
                        candidate = new Vector3(sample.position.x, from.y, sample.position.z); // Execution uses horizontal input, not a NavMesh-surface-height root warp.
                        if (FlatDistance(requested, navigationPoint) > .18f || Mathf.Abs(navigationPoint.y - from.y) > .15f) reason = "sample changed floor or lateral destination";
                        else if (!SameFloorSupport(candidate, from.y)) reason = "no same-level physical floor below lateral destination";
                        else if (NavMesh.Raycast(source.position, navigationPoint, out _, NavMesh.AllAreas)) reason = "lateral navigation edge";
                        else
                        {
                            var path = new NavMeshPath();
                            Vector3 onward = originalAim - candidate; onward.y = 0;
                            Vector3 outgoing = candidate + Vector3.ClampMagnitude(onward, 1.4f);
                            if (!NavMesh.CalculatePath(source.position, navigationPoint, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                                reason = "incomplete lateral navigation path";
                            else if (!NavMesh.SamplePosition(outgoing, out var outgoingNav, .18f, NavMesh.AllAreas)
                                || FlatDistance(outgoing, outgoingNav.position) > .18f || Mathf.Abs(outgoingNav.position.y - from.y) > .15f)
                                reason = "outgoing segment has no same-floor navigation footing";
                            else if (NavMesh.Raycast(navigationPoint, outgoingNav.position, out _, NavMesh.AllAreas)) reason = "outgoing navigation edge";
                            else if (!SameFloorSupport(outgoing, from.y)) reason = "outgoing segment lacks same-level physical support";
                            else if (!ClearBodySweep(controller, from, candidate, out reason)) { }
                            else if (!ClearBodySweep(controller, candidate, outgoing, out reason)) { }
                        }
                    }
                    Debug.Log("[IslandRescueWalk] SIDESTEP_SAMPLE phase=" + phase + " side=" + side + " requested=" + requested.ToString("F3")
                        + " sampledNav=" + navigationPoint.ToString("F3") + " physicalTarget=" + candidate.ToString("F3") + " accepted=" + (reason == null) + " reason=" + (reason ?? "full body lateral/outgoing sweeps and complete same-floor navigation"));
                    if (reason != null) continue;
                    float score = FlatDistance(from, candidate) + FlatDistance(candidate, originalAim);
                    if (score < best) { destination = candidate; best = score; found = true; }
                }
            return found;
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
            if (hit.collider) { reason = "body sweep hits " + DescribeCollider(hit.collider) + " at " + hit.distance.ToString("F3"); return false; }
            var overlap = Physics.OverlapCapsule(bottom + travel, top + travel, radius, ~0, QueryTriggerInteraction.Ignore)
                .FirstOrDefault(c => c && !c.transform.IsChildOf(controller.transform));
            if (overlap) { reason = "destination body overlaps " + DescribeCollider(overlap); return false; }
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
        static string Corners(Vector3[] corners) => string.Join("; ", corners.Select((p, i) => i + ":" + p.ToString("F3")));
        static string WalkState(string phase, Vector3 target, Vector3 aim, Vector3[] corners, int corner, IslandTraveler follower, bool wait,
            float beganReal, float beganSim, float budget, bool collisions)
        {
            var agent = PM.Active; var host = agent.Host;
            var controller = host ? host.Controller : agent.Blob ? agent.Blob.Controller : null;
            var text = new StringBuilder();
            text.Append("phase=").Append(phase).Append(" outcome=").Append(RunState.LakesideOutcome).Append(" mission=").Append(RunState.MissionIndex + 1)
                .Append(" real=").Append((Time.realtimeSinceStartup - beganReal).ToString("F2")).Append(" sim=").Append((Time.time - beganSim).ToString("F2"))
                .Append(" budget=").Append(budget.ToString("F2")).Append(" dt=").Append(Time.deltaTime.ToString("F4")).Append(" unscaledDt=").Append(Time.unscaledDeltaTime.ToString("F4"))
                .Append(" timeScale=").Append(Time.timeScale).Append(" at=").Append(agent.Position.ToString("F3")).Append(" local=").Append((agent.Position - IslandCampaign.Origin).ToString("F3"))
                .Append(" target=").Append(target.ToString("F3")).Append(" corner=").Append(corner).Append('/').Append(corners.Length).Append(" aim=").Append(aim.ToString("F3"))
                .Append(" aimError=").Append(FlatDistance(agent.Position, aim).ToString("F3")).Append(" finalError=").Append(FlatDistance(agent.Position, target).ToString("F3"))
                .Append(" agent=").Append(agent.Id).Append(" transit=").Append(agent.InTransit).Append(" capturedPending=").Append(agent.CapturedPendingEject)
                .Append(" body=").Append(host ? host.DisplayName + " state=" + host.State + " reciprocalOwner=" + (host.Possessor == agent) : "free amoeba")
                .Append(" inputSimulated=").Append(GameInput.Simulated).Append(" inputLocked=").Append(PM.InputLocked).Append(" move=").Append(GameInput.SimMove).Append(" yaw=").Append(PlayerCamera.Instance.Yaw)
                .Append(" waitForFollower=").Append(wait);
            if (controller)
                text.Append(" controller={enabled=").Append(controller.enabled).Append(" ground=").Append(controller.isGrounded).Append(" flags=").Append(controller.collisionFlags)
                    .Append(" velocity=").Append(controller.velocity.ToString("F3")).Append(" radius=").Append(controller.radius).Append(" height=").Append(controller.height)
                    .Append(" center=").Append(controller.center).Append(" skin=").Append(controller.skinWidth).Append(" detect=").Append(controller.detectCollisions).Append('}');
            if (host)
                text.Append(" hostNavEnabled=").Append(host.Nav && host.Nav.enabled).Append(" hostHitboxEnabled=").Append(host.HitBox && host.HitBox.enabled);
            if (follower)
                text.Append(" follower={id=").Append(follower.StableId).Append(" at=").Append(follower.transform.position.ToString("F3"))
                    .Append(" distance=").Append(FlatDistance(follower.transform.position, agent.Position).ToString("F3")).Append(" delivered=").Append(follower.Delivered).Append(" boarded=").Append(follower.Boarded)
                    .Append(" state=").Append(follower.Host.State).Append(" nav=").Append(follower.Host.Nav.enabled).Append(" onMesh=").Append(follower.Host.Nav.enabled && follower.Host.Nav.isOnNavMesh).Append('}');
            text.Append(" roster=").Append(string.Join("; ", IslandTravelers.Instance.Members.Select(p => p.StableId + ":" + p.transform.position.ToString("F3") + " delivered=" + p.Delivered + " boarded=" + p.Boarded)));
            if (collisions && controller)
            {
                Vector3 center = controller.transform.TransformPoint(controller.center);
                Vector3 scale = controller.transform.lossyScale;
                float radius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                float halfLine = Mathf.Max(0, controller.height * Mathf.Abs(scale.y) * .5f - radius);
                Vector3 bottom = center - Vector3.up * halfLine, top = center + Vector3.up * halfLine;
                var direction = aim - agent.Position; direction.y = 0;
                var overlap = Physics.OverlapCapsule(bottom, top, radius + .05f, ~0, QueryTriggerInteraction.Ignore)
                    .Where(c => c && !c.transform.IsChildOf(controller.transform)).OrderBy(c => (c.ClosestPoint(center) - center).sqrMagnitude).Take(12);
                text.Append(" overlaps=[").Append(string.Join("; ", overlap.Select(DescribeCollider))).Append(']');
                var hits = direction.sqrMagnitude > .0001f ? Physics.CapsuleCastAll(bottom, top, Mathf.Max(.01f, radius - .015f), direction.normalized, 1.6f, ~0, QueryTriggerInteraction.Ignore)
                    .Where(h => h.collider && !h.collider.transform.IsChildOf(controller.transform)).OrderBy(h => h.distance).Take(12).ToArray() : Array.Empty<RaycastHit>();
                text.Append(" forwardHits=[").Append(string.Join("; ", hits.Select(h => DescribeCollider(h.collider) + " distance=" + h.distance.ToString("F3") + " normal=" + h.normal.ToString("F3")))).Append(']');
                text.Append(" nearbyPeople=[").Append(string.Join("; ", HostCharacter.All.Where(h => h && h != host && FlatDistance(h.transform.position, agent.Position) < 8)
                    .OrderBy(h => FlatDistance(h.transform.position, agent.Position)).Take(12).Select(h => h.DisplayName + " at=" + h.transform.position.ToString("F3") + " state=" + h.State
                        + " controller=" + h.Controller.enabled + " hitbox=" + h.HitBox.enabled + " nav=" + h.Nav.enabled))).Append(']');
                text.Append(" corners=[").Append(Corners(corners)).Append(']');
            }
            return text.ToString();
        }
        static string DescribeCollider(Collider collider)
        {
            string path = collider.name;
            for (var parent = collider.transform.parent; parent; parent = parent.parent) path = parent.name + "/" + path;
            return path + " type=" + collider.GetType().Name + " layer=" + collider.gameObject.layer + " bounds=" + collider.bounds.center.ToString("F3") + "/" + collider.bounds.size.ToString("F3");
        }
        static IEnumerator WaitUntil(Func<bool> predicate, float seconds, string message)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!predicate() && Time.realtimeSinceStartup < deadline) { Calm(); yield return null; }
            Check(predicate(), message);
        }
        static IEnumerator Reload(string reason)
        {
            int resets = RunState.Resets; var outcome = RunState.LakesideOutcome;
            var old = CityDirector.Instance;
            GameManager.Instance.ResetToCheckpoint(reason);
            yield return WaitUntil(() => CityDirector.Instance && CityDirector.Instance != old && CityDirector.Instance.Ready, 30, "The checkpoint reload finishes its real world/nav build.");
            yield return null; yield return null;
            GameInput.Simulated = true; Stop();
            GameInput.SimInteract = GameInput.SimLeave = GameInput.SimSplit = GameInput.SimSwitch = GameInput.SimJump = false;
            yield return WaitUntil(() => IslandTravelers.Instance && IslandTravelers.Instance.Members.Count == 3, 3, "The roster restores after reload.");
            Check(RunState.IslandStarted && RunState.LakesideOutcome == outcome && RunState.Resets == resets + 1 && PM.Agents.Count == 1 && PM.Active.IsFree,
                "Recovery preserves the branch/chapter and reunites one free starting body.");
            Check(Physics.Raycast(PM.Active.Position + Vector3.up, Vector3.down, 2, Layers.EnvironmentMask), "The authored recovery point has actual collider support.");
            ParkPatrols(); Calm();
        }
        static void ParkPatrols()
        {
            // Baseline traversal controls unrelated pursuit; the exposure fixture tests its own visible guard separately.
            foreach (var h in HostCharacter.All)
                if (h && h.Profession == Profession.Guard && h.transform.position.x > 560)
                { PlaceNpc(h, IslandCampaign.At(47, .15f, -33)); if (h.Brain is GuardAI guard) guard.Stun(600); }
        }
        static void PlaceNpc(HostCharacter host, Vector3 position)
        {
            if (host.Nav && host.Nav.enabled) host.Nav.Warp(position);
            else { bool enabled = host.Controller.enabled; host.Controller.enabled = false; host.transform.position = position; host.Controller.enabled = enabled; }
            if (host.Brain) { host.Brain.behaviour = NpcBehaviour.Stationary; host.Brain.SetHome(position, host.transform.rotation); }
            Physics.SyncTransforms();
        }
        static float FlatDistance(Vector3 a, Vector3 b) { a.y = b.y = 0; return Vector3.Distance(a, b); }
        static InteractContext Context() => new() { agent = PM.Active, host = PM.Active.Host, permissions = PM.Active.Host ? PM.Active.Host.Permissions : AccessPermission.None, position = PM.Active.Position };
        static void Stop() { GameInput.SimMove = Vector2.zero; GameInput.SimSprint = false; }
        static void Calm() { if (SuspicionSystem.Instance) { SuspicionSystem.Instance.SetValue(0); SuspicionSystem.Instance.OnSuspectGone(); } }
        static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
