using System.Collections.Generic;
using Parasite.Characters;
using Parasite.Possession;
using UnityEngine;

namespace Parasite.Missions
{
    public static class RailCampaign
    {
        public const int FirstMissionIndex = 23;
        public const string Started = "northline_started", ArrivalSeen = "northline_arrival_seen", CrossingPassed = "northline_crossed";
        public const string Validated = "northline_freight_validated", FreightArrived = "northline_freight_arrived";
        public const string PointA = "northline_point_a_right", PointB = "northline_point_b_right";
        public const string SignalReleased = "northline_manual_signal", Departed = "northline_departed";
        public static bool Active => RunState.Has(Started);
        public static bool Peace => RunState.LakesideOutcome == EndingOutcome.Peace;
        public static bool RequiredA => !Peace;
        public static bool RequiredB => Peace;
        public static string Consignment => Peace ? "NLM-07 medical cold store" : "NLR-07 relay carriage";
        static readonly RaycastHit[] sightHits = new RaycastHit[64];

        public static void AppendMissions(List<Mission> missions)
        {
            missions.Add(new Mission
            {
                title = "NOT UNDER THE WHEELS",
                briefing = "Northline is still running. Borrow MAINTENANCE and request a crossing at the yellow signal [E]. The tram must physically leave the crossing before both barriers lift. Walk that same body fully across during the eight-second green window. A late or captured body must request again. The interlock always holds the tram if anyone is still inside; there is no collision shortcut.",
                completeWhen = () => RunState.Has(CrossingPassed),
                objectives =
                {
                    new() { text = "As MAINTENANCE, request the interlocked crossing [E]", doneWhen = () => RunState.Has(CrossingPassed) || RailCrossing.Instance && RailCrossing.Instance.Requested,
                        waypoints = new[] { RailLayout.CrossingControl }, waypointLabel = "CROSSING REQUEST",
                        extra = () => RailCrossing.Instance ? "  (" + RailCrossing.Instance.StatusLine + ")" : "" },
                    new() { text = "During green, physically cross the marked lane from south to north", flag = CrossingPassed,
                        waypoints = new[] { RailLayout.At(0, .15f, -8) }, waypointLabel = "NORTH SIDE" },
                },
            });
            missions.Add(new Mission
            {
                title = Peace ? "THE MEDICINE MUST ARRIVE" : "A LINE OF YOUR OWN",
                briefing = "The sealed carriage is " + Consignment + ". A SCIENTIST validates its actual docked seal at the manifest desk [E]. Then MAINTENANCE throws two mechanical points: " +
                    (Peace ? "A LEFT, B RIGHT" : "A RIGHT, B LEFT") + ". Wait for both blades to finish moving, then dispatch [E]. A wrong route returns the same carriage to the loading dock without delivery credit. The right route must physically reach its marked bay; an obstruction cannot be timed away.",
                completeWhen = () => RunState.Has(FreightArrived),
                objectives =
                {
                    new() { text = "As a SCIENTIST, validate " + Consignment + " at the loading desk [E]", flag = Validated,
                        waypoints = new[] { RailLayout.ManifestControl }, waypointLabel = "MANIFEST DESK" },
                    new() { text = Peace ? "As MAINTENANCE, set A LEFT and B RIGHT, then dispatch [E]" : "As MAINTENANCE, set A RIGHT and B LEFT, then dispatch [E]",
                        doneWhen = () => RunState.Has(FreightArrived) || RailFreight.Instance && RailFreight.Instance.PointsCorrect && RailFreight.Instance.PointsSettled,
                        waypoints = RailLayout.PointControls, waypointLabel = "MECHANICAL POINTS" },
                    new() { text = "Watch the actual sealed carriage reach the required bay", flag = FreightArrived,
                        waypoints = new[] { RailLayout.DispatchControl, RailLayout.RequiredBay }, waypointLabel = "FREIGHT ROUTE",
                        extra = () => RailFreight.Instance ? "  (" + RailFreight.Instance.StatusLine + ")" : "" },
                },
            });
            missions.Add(new Mission
            {
                title = "THE LAST MANUAL SIGNAL",
                briefing = "Split [V] if needed. One MAINTENANCE host must stand on the west crank pad and hold the dead-man lever [E]. Switch [Tab] to the other body, release its host [Q] and crawl through the low service duct. At the east signal box, press [E] and keep the FREE body still for three seconds. Losing the holder, moving either operator, capture or blocked sight cancels the signal. Once released, the north door opens permanently: release the maintenance host, bring BOTH FREE amoebae fully into the departure carriage, lose pursuit and press [E] to leave.",
                completeWhen = () => RunState.Has(Departed),
                objectives =
                {
                    new() { text = "One MAINTENANCE host holds the west dead-man crank [E]; the other FREE body uses the low duct", doneWhen = () => RunState.Has(SignalReleased) || RailDeparture.Instance && RailDeparture.Instance.Holding,
                        waypoints = new[] { RailLayout.Crank, RailLayout.At(-3, .15f, 34) }, waypointLabel = "CRANK AND SERVICE DUCT" },
                    new() { text = "As the other FREE body, hold a clear stationary three-second signal at the east box [E]", flag = SignalReleased,
                        waypoints = new[] { RailLayout.Signal }, waypointLabel = "MANUAL SIGNAL",
                        extra = () => RailDeparture.Instance ? "  (" + RailDeparture.Instance.StatusLine + ")" : "" },
                    new() { text = "Release both hosts, bring both FREE bodies fully aboard, then depart [E]", flag = Departed,
                        waypoints = new[] { RailLayout.Departure }, waypointLabel = "NORTHLINE CARRIAGE" },
                },
            });
        }

        public static void OnFlag(string flag)
        {
            if (!Active || !GameManager.Instance) return;
            if (flag == CrossingPassed)
            {
                GameManager.Instance.SetCheckpoint(RailLayout.NorthCheckpoint, "Northline north crossing");
                MissionManager.Say(("NORTHLINE RADIO", "The crossing is yours. Read the sealed carriage at the manifest desk; the two mechanical points decide where it really goes."));
            }
            else if (flag == Validated)
                GameManager.Instance.SetCheckpoint(RailLayout.At(-31, .15f, 1), "Northline freight loading");
            else if (flag == FreightArrived)
            {
                GameManager.Instance.SetCheckpoint(RailLayout.SignalCheckpoint, "Northline manual signal");
                MissionManager.Say(("NORTHLINE RADIO", Peace ? "The cold store is in the medical bay. The last release still needs two of you, and nobody stays borrowed."
                    : "Your relay carriage is in its bay. Hold the manual crank with one host and send the other free through the service duct."));
            }
        }

        public static bool Registered(AmoebaAgent agent)
        {
            var pm = PossessionManager.Instance;
            if (!pm || agent == null || !pm.Agents.Contains(agent) || agent.InTransit || agent.CapturedPendingEject) return false;
            var body = agent.Body;
            // An interface reference can retain a destroyed Unity object. Reject it before invoking .Root/.transform.
            if (body == null || body is Object unityBody && !unityBody) return false;
            return body.Root && body.Root.gameObject.activeInHierarchy && (!(body is Behaviour behaviour) || behaviour.isActiveAndEnabled);
        }
        public static bool Free(AmoebaAgent agent) => Registered(agent) && agent.IsFree && agent.Blob && agent.Blob.isActiveAndEnabled
            && agent.Blob.Controller && agent.Blob.Controller.enabled && agent.Blob.gameObject.activeInHierarchy;
        public static bool BodyBounds(AmoebaAgent agent, out Bounds bounds)
        {
            bounds = default;
            if (!Registered(agent)) return false;
            var controller = agent.Host ? agent.Host.Controller : agent.Blob ? agent.Blob.Controller : null;
            if (!controller || !controller.enabled || !controller.gameObject.activeInHierarchy) return false;
            bounds = controller.bounds; return true;
        }
        public static bool FullyInside(AmoebaAgent agent, Bounds zone)
        {
            zone.Expand(.004f);
            return BodyBounds(agent, out var bounds) && zone.Contains(bounds.min) && zone.Contains(bounds.max);
        }
        public static bool NoPursuit => !Stealth.SuspicionSystem.Instance || Stealth.SuspicionSystem.Instance.Level != SuspicionLevel.Pursuing;
        public static bool Ready(in InteractContext ctx, Vector3 point, Transform target, out string reason)
        {
            reason = null;
            if (!Active || !GameManager.Instance || !GameManager.Instance.IsPlaying) { reason = "Continue Northline and resume play first."; return false; }
            if (!Registered(ctx.agent) || PossessionManager.Instance.Active != ctx.agent || ctx.host != ctx.agent.Host)
            { reason = "Use this control as the active registered body."; return false; }
            Vector3 delta = ctx.agent.Position - point;
            if (Mathf.Abs(delta.y) > 2.6f || new Vector2(delta.x, delta.z).magnitude > 2.3f)
            { reason = "Move the active body within reach of the control."; return false; }
            if (!ClearLine(ctx.agent.Body.EyePosition, point, ctx.agent.Body.Root, target)) { reason = "The control is physically obstructed."; return false; }
            return true;
        }
        public static bool ReadyHost(in InteractContext ctx, Profession role, Vector3 point, Transform target, out string reason)
        {
            if (!Ready(ctx, point, target, out reason)) return false;
            var permission = role == Profession.Maintenance ? AccessPermission.ServiceTunnel : AccessPermission.StaffTerminal;
            if (!ctx.host || !ctx.host.isActiveAndEnabled || !ctx.host.gameObject.activeInHierarchy || !ctx.host.Controller || !ctx.host.Controller.enabled || ctx.host.Possessor != ctx.agent || ctx.host.State != HostState.Possessed || ctx.host.Profession != role || !AccessRules.Has(ctx.host.Permissions, permission))
            { reason = "A possessed " + role.ToString().ToUpperInvariant() + " host with work credentials is required."; return false; }
            return true;
        }
        public static bool ClearLine(Vector3 start, Vector3 end, Transform own, Transform allowed)
        {
            Vector3 delta = end - start; float distance = delta.magnitude;
            if (distance < .001f) return true;
            int count = Physics.RaycastNonAlloc(start, delta / distance, sightHits, distance, ~0, QueryTriggerInteraction.Ignore);
            if (count >= sightHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                // A compound hit may report its Rigidbody root; ownership belongs to the actual hit collider.
                var hit = sightHits[i]; if (!hit.collider || own && hit.collider.transform.IsChildOf(own) || allowed && hit.collider.transform.IsChildOf(allowed)) continue;
                return false;
            }
            return true;
        }
    }
}
