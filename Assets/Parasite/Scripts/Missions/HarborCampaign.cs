using System.Collections.Generic;
using Parasite.Possession;
using UnityEngine;

namespace Parasite.Missions
{
    /// <summary>Three physical harbor tasks following the island's explicit continuation; the lake outcome is read-only.</summary>
    public static class HarborCampaign
    {
        public const int FirstMissionIndex = 20;
        public const string Started = "harbor_started", ArrivalSeen = "harbor_arrival_seen", CanalOpen = "harbor_canal_open";
        public const string ManifestComplete = "harbor_manifest_complete", RelayComplete = "harbor_relay_complete";
        static readonly string[] PeaceManifest = { "A17", "A32" }, DominionManifest = { "R09", "R21" };
        public static bool Active => RunState.Has(Started);
        public static bool Peace => RunState.LakesideOutcome == EndingOutcome.Peace;
        public static IReadOnlyList<string> RequiredCargoIds => Peace ? PeaceManifest : DominionManifest;
        public static string ValveFlag(int slot) => "harbor_valve_" + (slot == 0 ? "a" : slot == 1 ? "b" : "c");
        public static string ReroutedFlag(string id) => "harbor_cargo_rerouted_" + id.ToLowerInvariant();
        public static string DeliveredFlag(string id) => "harbor_cargo_delivered_" + id.ToLowerInvariant();
        public static bool RequiredCargoDelivered
        {
            get { foreach (var id in RequiredCargoIds) if (!RunState.Has(DeliveredFlag(id))) return false; return true; }
        }
        public static void AppendMissions(List<Mission> missions)
        {
            missions.Add(new Mission
            {
                title = "A MEASURE OF PRESSURE",
                briefing = "The harbor lock is sealed. Borrow MAINTENANCE and balance three physical valves: A adds 3, B adds 2, C bleeds 1. The gauge must stay at 4 for two seconds. Going above 4 releases pressure and resets all three handles; no one is hurt. Use the bleed before adding the final pump.",
                completeWhen = () => RunState.Has(CanalOpen),
                objectives =
                {
                    new() { text = "As MAINTENANCE, set A (+3), B (+2) and bleed C (-1) without exceeding 4 [E]", doneWhen = () => RunState.Has(CanalOpen) || HarborPressureWorks.Instance && HarborPressureWorks.Instance.ValveMask == 7,
                        waypoints = new[] { HarborLayout.World(HarborLayout.ValveLocal[0]), HarborLayout.World(HarborLayout.ValveLocal[1]), HarborLayout.World(HarborLayout.ValveLocal[2]) }, waypointLabel = "PRESSURE VALVES",
                        extra = () => HarborPressureWorks.Instance ? "  (" + HarborPressureWorks.Instance.StatusLine + ")" : "" },
                    new() { text = "Let the balanced gauge hold 4 for two seconds; the physical lock then lifts", flag = CanalOpen,
                        waypoints = new[] { HarborLayout.World(HarborLayout.GaugeLocal) }, waypointLabel = "LOCK GAUGE" },
                },
            });
            var cargo = new Mission
            {
                title = Peace ? "COLD CHAIN" : "SIGNAL IN TRANSIT",
                briefing = Peace
                    ? "Independent responders need sealed antidote lots A17 and A32. A SCIENTIST must manually scan each correct crate while it moves through the arch. The other lots are relay equipment. MAINTENANCE may stop the belt for five seconds, but one scientist can time both scans. Do not enter the fenced conveyor trench."
                    : "The next shore needs relay lots R09 and R21. A SCIENTIST must manually scan each correct crate while it moves through the arch. The A lots belong to the antidote shipment. MAINTENANCE may stop the belt for five seconds, but one scientist can time both scans. Do not enter the fenced conveyor trench.",
                completeWhen = () => RunState.Has(ManifestComplete),
            };
            foreach (var id in RequiredCargoIds)
            {
                string stableId = id;
                cargo.objectives.Add(new Objective { text = "As a SCIENTIST, scan " + stableId + " in the moving arch [E], then wait for its physical bay arrival", flag = DeliveredFlag(stableId),
                    waypoints = new[] { HarborLayout.World(HarborLayout.ScannerLocal) }, waypointLabel = "CARGO SCANNER",
                    extra = () => HarborCargoDirector.Instance ? "  (" + HarborCargoDirector.Instance.StatusLine + ")" : "" });
            }
            missions.Add(cargo);
            missions.Add(new Mission
            {
                title = Peace ? "AN OPEN FREQUENCY" : "TWO MINDS, ONE SIGNAL",
                briefing = "The manifest must be physically delivered before the relay can link. Split [V] if needed, release BOTH hosts, and place two distinct FREE amoebae fully on the two pads. [Tab] between them. Lose pursuit and keep both emitter sight lines clear. Press [E] on either relay node, then keep both bodies still for three seconds. Movement, capture, possession or a blocked beam cancels the attempt; pause freezes it.",
                objectives =
                {
                    new() { text = "Place one FREE amoeba fully on each pad, with clear emitter sight lines and no pursuit", doneWhen = () => RunState.Has(RelayComplete) || HarborRelay.Instance && HarborRelay.Instance.Ready,
                        waypoints = new[] { HarborLayout.World(HarborLayout.RelayLocal[0]), HarborLayout.World(HarborLayout.RelayLocal[1]) }, waypointLabel = "TWO FREE PADS" },
                    new() { text = "At either relay node, press [E] and keep both free bodies still for the three-second handshake", flag = RelayComplete,
                        waypoints = new[] { HarborLayout.World(HarborLayout.RelayLocal[0]), HarborLayout.World(HarborLayout.RelayLocal[1]) }, waypointLabel = "RELAY HANDSHAKE",
                        extra = () => HarborRelay.Instance ? "  (" + HarborRelay.Instance.StatusLine + ")" : "" },
                },
            });
        }
        public static void OnFlag(string flag)
        {
            if (!Active) return;
            if (flag == CanalOpen)
            {
                if (GameManager.Instance) GameManager.Instance.SetCheckpoint(HarborLayout.LockCheckpoint, "Harbor lock approach");
                MissionManager.Say(("HARBOR RADIO", "Pressure stable. The lock is lifting. The scanner accepts a scientist's credentials; read the moving lot number before you press."));
            }
            if (flag == ManifestComplete)
            {
                if (GameManager.Instance) GameManager.Instance.SetCheckpoint(HarborLayout.ManifestCheckpoint, "Harbor relay approach");
                MissionManager.Say(("HARBOR RADIO", Peace ? "Both antidote lots are physically in the response bays. Leave every borrowed body behind. Two free signals can open this channel."
                    : "Both relay lots are physically in the transfer bays. Release every host. The next link needs two free signals, not two borrowed faces."));
            }
        }
        public static bool ReadyHost(in InteractContext ctx, Profession profession, Vector3 usePoint, float range, out string reason)
        {
            reason = null; var pm = PossessionManager.Instance;
            if (!Active || !GameManager.Instance || !GameManager.Instance.IsPlaying) { reason = "Continue the harbor chapter and resume play first."; return false; }
            if (!pm || ctx.agent == null || pm.Active != ctx.agent || !pm.Agents.Contains(ctx.agent) || ctx.agent.InTransit || ctx.agent.CapturedPendingEject || !ctx.host
                || !ctx.host.isActiveAndEnabled || !ctx.host.gameObject.activeInHierarchy
                || ctx.agent.Host != ctx.host || ctx.host.Possessor != ctx.agent || ctx.host.State != HostState.Possessed || ctx.host.Profession != profession)
            { reason = "A possessed " + profession.ToString().ToUpperInvariant() + " host must work this control."; return false; }
            var permission = profession == Profession.Maintenance ? AccessPermission.ServiceTunnel : AccessPermission.StaffTerminal;
            if (!AccessRules.Has(ctx.host.Permissions, permission)) { reason = "This host lacks the required work credentials."; return false; }
            Vector3 d = ctx.agent.Position - usePoint;
            if (Mathf.Abs(d.y) > 2.6f || new Vector2(d.x, d.z).magnitude > range) { reason = "Move this host within reach of the control."; return false; }
            return true;
        }
    }
}
