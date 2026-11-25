using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Parasite.FX;
using Parasite.Stealth;
using Parasite.World;
using UnityEngine;

namespace Parasite.Missions
{
    public static class MissionFlags
    {
        public const string AlleyExit = "alley_exit", FirstHost = "first_host", TriedSplit = "tried_split";
        public const string NewsRead = "news_read", Eavesdropped = "eavesdropped";
        public const string Keycard = "keycard", Power = "tunnel_power";
        public const string GateOpen = "gate_open", StaffOpen = "staff_open", TunnelOpen = "tunnel_open";
        public const string LabEntered = "lab_entered", DataA = "data_a", DataB = "data_b", Research = "research_done";
        public const string OfficeOpen = "office_open", Confronted = "confronted";
        public const string SignedArchive = "signed_archive", ContainmentOffline = "containment_offline";
        public const string EvidenceBroadcast = "evidence_broadcast", Escaped = "escaped_with_evidence";
        public const string SubVaultDiversion = "subvault_diversion", BlackSiteArchive = "blacksite_archive";
        public const string BackupGenerator = "backup_generator", GridOverride = "grid_override";
        public const string ProtocolEpsilonBreached = "epsilon_breached", MasterBroadcast = "master_broadcast";
        // THE TAIL (city, before CREDENTIALS): leg checkpoints persist so a reload resumes Petrov at the last one.
        public const string TailLeg1 = "tail_leg1", TailLeg2 = "tail_leg2", TailDone = "tail_done", DeadDrop = "dead_drop";
        // HUNTER PROTOCOL (lab, after CREATOR).
        public const string HunterGate = "hunter_gate", HuntersDown = "hunters_down";
        // BLACKOUT (district, before THE FINAL CONVERGENCE).
        public const string DistrictBlackout = "district_blackout";
    }

    public class Objective
    {
        public string text;
        public string flag;                 // done when this flag is set (unless doneWhen is given)
        public Func<bool> doneWhen;
        public Func<string> extra;          // optional dynamic suffix, e.g. "(keycard ✓)"
        public string[] waypointFlags = Array.Empty<string>(); // props to point at (by their flag)
        public Vector3[] waypoints = Array.Empty<Vector3>();
        public string waypointLabel = "";
        public Func<Vector3?> liveWaypoint;  // moving or conditional target (e.g. Petrov during THE TAIL)
        public bool optional;
        public bool IsDone => doneWhen != null ? doneWhen() : RunState.Has(flag);
    }

    public class Mission
    {
        public string title, briefing;
        public List<Objective> objectives = new();
        public Func<bool> completeWhen;
        public bool IsComplete => completeWhen != null ? completeWhen() : objectives.Where(o => !o.optional).All(o => o.IsDone);
    }

    /// <summary>
    /// Expanded campaign: infiltrate via three routes, meet Vale, steal signed evidence, break containment,
    /// expose the experiment and escape. Completed flags persist; cooperative controls must be held again after a reload.
    /// </summary>
    public class MissionManager : MonoBehaviour
    {
        public static MissionManager Instance { get; private set; }
        public string Objective { get; private set; } = "";
        public bool EndingStarted { get; private set; }
        public bool DialoguePlaying { get; private set; }
        public List<Mission> Missions { get; private set; }
        public Mission Current => Missions != null && RunState.MissionIndex < Missions.Count ? Missions[RunState.MissionIndex] : null;
        public readonly List<(Vector3 pos, string label)> Waypoints = new();
        public static event Action<string, string> Banner;   // title, subtitle

        readonly Dictionary<string, Transform> propsByFlag = new();
        float refreshT;
        string lastObjective = "";

        void Awake() { Instance = this; Banner = null; Missions = BuildCampaign(); }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            foreach (var p in FindObjectsByType<MissionProp>(FindObjectsSortMode.None)) propsByFlag[p.flag] = p.interactPoint ? p.interactPoint : p.transform;
            foreach (var z in FindObjectsByType<EavesdropZone>(FindObjectsSortMode.None)) propsByFlag[z.flag] = z.transform;
            foreach (var r in FindObjectsByType<DualAuthReader>(FindObjectsSortMode.None)) propsByFlag[r.flag + ":" + r.readerName] = r.transform;
            foreach (var t in FindObjectsByType<StaffTerminal>(FindObjectsSortMode.None)) propsByFlag["terminal"] = t.transform;
            foreach (var d in FindObjectsByType<AccessDoor>(FindObjectsSortMode.None))
                if (!string.IsNullOrEmpty(d.openedFlag)) propsByFlag["door:" + d.openedFlag] = d.interactPoint ? d.interactPoint : d.transform;
            foreach (var v in FindObjectsByType<DrValeInteraction>(FindObjectsSortMode.None)) propsByFlag["vale"] = v.transform;
            RestoreCampaignState();
            Advance(announce: true);
            if (RunState.AfterlakeStarted && !RunState.IslandStarted) AfterlakeCampaign.AnnounceArrival();
            Refresh();
        }

        /// <summary>Show the current objective after a protected introduction without changing campaign progress.</summary>
        public void ReannounceCurrentMission()
        {
            Refresh();
            if (Current != null)
                Banner?.Invoke($"MISSION {RunState.MissionIndex + 1}/{Missions.Count} · {Current.title}", Current.briefing);
        }

        // ------------------------------------------------------------ flags
        public static void SetFlag(string flag)
        {
            if (string.IsNullOrEmpty(flag) || !RunState.Flags.Add(flag)) return;
            if (Instance) { Instance.OnFlag(flag); Instance.Advance(true); Instance.Refresh(); }
        }

        void OnFlag(string flag)
        {
            if ((flag == MissionFlags.DataA || flag == MissionFlags.DataB) && RunState.Has(MissionFlags.DataA) && RunState.Has(MissionFlags.DataB))
                SetFlag(MissionFlags.Research);
            if (flag == MissionFlags.GridOverride && !RunState.Has(MissionFlags.ProtocolEpsilonBreached))
                SetFlag(MissionFlags.ProtocolEpsilonBreached);
            if (flag == MissionFlags.ContainmentOffline || flag == MissionFlags.ProtocolEpsilonBreached || flag == MissionFlags.GridOverride)
                RestoreCampaignState();
            if (flag == MissionFlags.Confronted || flag == MissionFlags.SignedArchive || flag == MissionFlags.ContainmentOffline ||
                flag == MissionFlags.EvidenceBroadcast || flag == MissionFlags.BlackSiteArchive || flag == MissionFlags.GridOverride ||
                flag == MissionFlags.ProtocolEpsilonBreached || flag == MissionFlags.MasterBroadcast ||
                flag == MissionFlags.DeadDrop || flag == MissionFlags.HuntersDown || flag == MissionFlags.DistrictBlackout)
            {
                var pm = Possession.PossessionManager.Instance;
                if (GameManager.Instance && pm && pm.ActiveBody != null)
                    GameManager.Instance.SetCheckpoint(pm.ActiveBody.Root.position, "After " + flag.Replace('_', ' '));
            }
            if (RunState.HarborStarted) HarborCampaign.OnFlag(flag);
            else if (RunState.IslandStarted) IslandCampaign.OnFlag(flag);
            else if (RunState.AfterlakeStarted) AfterlakeCampaign.OnFlag(flag);
            if (flag == MissionFlags.Eavesdropped) GameEvents.RaiseToast("Overheard: Dr. Vale works nights at VALE BIOTECH. Three ways in.", Palette.Amoeba);
            if (flag == MissionFlags.DeadDrop)
            {
                Say(DeadDropNote);
                GameEvents.RaiseToast("Petrov's dead drop: Vale freed you on purpose, and his signed orders exist. Vale Biotech is north, past Campus Road.", Palette.Amoeba);
            }
            if (flag == MissionFlags.HuntersDown) GameEvents.RaiseToast("Protocol Hound offline: scanners dark, hunters recalled. Now the coolant and the signed archive.", Palette.Amoeba);
            if (flag == MissionFlags.EvidenceBroadcast) GameEvents.RaiseToast("Independent News: 'Kill-switch and human trials are locked in the subterranean Black Site Vault!'", Palette.Warning);
            if (flag == MissionFlags.BlackSiteArchive) GameEvents.RaiseToast("Black Site Archive acquired! Vale Biotech initiates Containment Protocol Epsilon!", Palette.Warning);
            if (flag == MissionFlags.ProtocolEpsilonBreached) GameEvents.RaiseToast("Protocol Epsilon collapsed. Campus lockdown dropped. Reach the Plaza Master Antenna!", Palette.Amoeba);
            if (flag == MissionFlags.MasterBroadcast) GameEvents.RaiseToast("Master broadcast transmitting worldwide! Eject from your host [Q] and extract through the storm drain.", Palette.Amoeba);
        }

        public void RegisterRouteUsed(EntryRoute route)
        {
            if (RunState.Route == EntryRoute.None) RunState.Route = route;
        }

        // ------------------------------------------------------------ progression
        void Advance(bool announce)
        {
            int guard = 0;
            bool advanced = false;
            while (Current != null && Current.IsComplete && guard++ < Missions.Count)
            {
                if (announce && !advanced) GameEvents.RaiseToast($"MISSION COMPLETE — {Current.title}", Palette.Amoeba);
                RunState.MissionIndex++;
                advanced = true;
            }
            if ((advanced || announce) && Current != null)
            {
                Banner?.Invoke($"MISSION {RunState.MissionIndex + 1}/{Missions.Count} · {Current.title}", Current.briefing);
                if (advanced) Sfx.Ui(SfxId.TerminalBeep, 0.8f);
            }
        }

        void Update()
        {
            if (EndingStarted || DialoguePlaying) return;
            var pm = Possession.PossessionManager.Instance;
            var body = pm != null ? pm.ActiveBody : null;
            if (body != null)
            {
                var p = body.Root.position;
                if (!RunState.Has(MissionFlags.AlleyExit) && p.z > CityLayout.AlleyMouth.z - 2f) SetFlag(MissionFlags.AlleyExit);
                if (!RunState.Has(MissionFlags.FirstHost) && RunState.HostsPossessed > 0) SetFlag(MissionFlags.FirstHost);
                if (!RunState.Has(MissionFlags.TriedSplit) && pm.Agents.Count > 1) SetFlag(MissionFlags.TriedSplit);
                if (!RunState.LabEntered && LabInteriorZone.InAny(p)) OnEnteredLab(p);
            }
            refreshT -= Time.deltaTime;
            if (refreshT <= 0f) { refreshT = 0.25f; Refresh(); }
        }

        void OnEnteredLab(Vector3 pos)
        {
            RunState.LabEntered = true;
            GameEvents.RaiseToast("Inside Vale Biotech", Palette.Amoeba);
            GameManager.Instance?.SetCheckpoint(pos, "Vale Biotech Lab");
            SetFlag(MissionFlags.LabEntered);
        }

        void Refresh()
        {
            Waypoints.Clear();
            var m = Current;
            if (m == null) { SetObjective(""); return; }
            var sb = new StringBuilder();
            sb.Append("<b>MISSION ").Append(RunState.MissionIndex + 1).Append('/').Append(Missions.Count).Append(" · ").Append(m.title).Append("</b>");
            foreach (var o in m.objectives)
            {
                bool done = o.IsDone;
                sb.Append('\n');
                string extra = o.extra != null ? o.extra() : "";
                if (done) sb.Append("<color=#6f9a80>  + ").Append(o.text).Append("</color>");
                else sb.Append(o.optional ? "<color=#a8c4b4>  · " : "  › ").Append(o.text).Append(extra).Append(o.optional ? "</color>" : "");
                if (done) continue;
                foreach (var w in o.waypoints) Waypoints.Add((w, o.waypointLabel));
                foreach (var f in o.waypointFlags)
                    if (propsByFlag.TryGetValue(f, out var t) && t) Waypoints.Add((t.position, o.waypointLabel));
                var live = o.liveWaypoint != null ? o.liveWaypoint() : null;
                if (live.HasValue) Waypoints.Add((live.Value, o.waypointLabel));
            }
            var prop = MissionProp.Current;
            if (prop) sb.Append($"\n<color=#ffd45a>  {prop.verb}… {prop.Progress01 * 100f:0}%</color>");
            var work = CampaignOperation.Current;
            if (work) sb.Append($"\n<color=#ffd45a>  {work.displayName}… {work.Progress01 * 100f:0}%</color>");
            var ez = EavesdropZone.Active;
            if (ez && ez.Listening) sb.Append($"\n<color=#ffd45a>  Listening… {ez.Progress01 * 100f:0}%</color>");
            var dr = DualAuthReader.ArmedAny();
            if (dr) sb.Append($"\n<color=#ffd45a>  {dr.readerName} armed — {dr.ArmedRemaining:0.0}s</color>");
            AppendMechanicStatus(sb);
            SetObjective(sb.ToString());
        }

        /// <summary>Live lines for the new mechanics: tail distance/alertness, bio-scan meter, blackout reroute timer.</summary>
        static void AppendMechanicStatus(StringBuilder sb)
        {
            var tail = TailTarget.Instance;
            if (tail && tail.Engaged)
            {
                string line = tail.StatusLine();
                bool hot = tail.Glancing || tail.Band == TailBand.TooClose || tail.Band == TailBand.Lost || tail.Alertness >= TailRules.WaryAt;
                if (line.Length > 0) sb.Append(hot ? "\n<color=#ff8a5a>  " : "\n<color=#ffd45a>  ").Append(line).Append("</color>");
            }
            var squad = HunterSquad.Instance;
            if (squad && squad.Active)
            {
                var burned = BioScanTracker.WornBurnedBody();
                if (burned) sb.Append($"\n<color=#ff5a4a>  BODY BURNED — hunters know {burned.DisplayName}: leave it [Q] or hop bodies</color>");
                else
                {
                    var read = BioScanTracker.HighestPossessed(out float meter);
                    if (read && meter > 0.02f) sb.Append($"\n<color=#ff8a5a>  BIO-SCAN {meter * 100f:0}% on {read.DisplayName} — break line of sight</color>");
                }
            }
            var grid = BlackoutGrid.Instance;
            if (grid && grid.WindowOpen) sb.Append("\n<color=#ff8a5a>  ").Append(grid.StatusLine()).Append("</color>");
            var isolator = BlackoutSwitch.Current;
            if (isolator) sb.Append($"\n<color=#ffd45a>  Pulling {isolator.switchName}… {isolator.Progress01 * 100f:0}%</color>");
        }

        static readonly (string who, string line)[] DeadDropNote =
        {
            ("PETROV'S NOTE", "Vale opened Specimen 7's cell himself. I watched him do it."),
            ("PETROV'S NOTE", "His signed orders are in the office archive. It takes two people to open, and he trusts no one."),
            ("PETROV'S NOTE", "To the press: when you can, broadcast from the Plaza uplink. They will come for me first."),
        };

        static bool SwitchCut(int i)
        {
            var g = BlackoutGrid.Instance;
            return g && i < g.switches.Length && g.switches[i] && g.switches[i].Cut;
        }

        void SetObjective(string text)
        {
            Objective = text;
            if (text == lastObjective) return;
            lastObjective = text;
            GameEvents.RaiseObjective(text);
        }

        // ------------------------------------------------------------ campaign
        static List<Mission> BuildCampaign()
        {
            bool Has(string f) => RunState.Has(f);
            var list = new List<Mission>
            {
                new()
                {
                    title = "OUT OF THE DRAIN",
                    briefing = "Specimen 7 is loose in the city. Learn to move unseen — then find a body to hide in.",
                    completeWhen = () => Has(MissionFlags.FirstHost) || Has(MissionFlags.LabEntered),
                    objectives =
                    {
                        new() { text = "Slip out of the storm-drain alley", flag = MissionFlags.AlleyExit, waypoints = new[] { CityLayout.AlleyMouth }, waypointLabel = "ALLEY EXIT" },
                        new() { text = "Take a host — aim at a pedestrian within 5 m and press [E]", flag = MissionFlags.FirstHost },
                        new() { text = "Optional: split in two with [V], switch with [Tab]", flag = MissionFlags.TriedSplit, optional = true },
                    },
                },
                new()
                {
                    title = "WORD ON THE STREET",
                    briefing = "Your creator is somewhere in this city. Blend in and find out where.",
                    completeWhen = () => (Has(MissionFlags.NewsRead) && Has(MissionFlags.Eavesdropped)) || Has(MissionFlags.LabEntered),
                    objectives =
                    {
                        new() { text = "Read the headlines at the City Plaza newsstand", flag = MissionFlags.NewsRead, waypointFlags = new[] { MissionFlags.NewsRead }, waypointLabel = "NEWSSTAND" },
                        new() { text = "Eavesdrop on Dr. Hana Petrov at the café — stay close, as a host", flag = MissionFlags.Eavesdropped, waypointFlags = new[] { MissionFlags.Eavesdropped }, waypointLabel = "EAVESDROP" },
                    },
                },
                new()
                {
                    title = "THE TAIL",
                    briefing = "Petrov hung up in a hurry. Follow her to wherever she's going: close enough to keep her in sight, far enough that she never makes you.",
                    completeWhen = () => Has(MissionFlags.DeadDrop) || Has(MissionFlags.LabEntered),
                    objectives =
                    {
                        new() { text = "Tail Dr. Petrov — stay 4–20 m behind, freeze when she glances back, never let her see a free amoeba",
                            doneWhen = () => Has(MissionFlags.TailDone) || Has(MissionFlags.DeadDrop),
                            liveWaypoint = () => TailTarget.Instance && TailTarget.Instance.Target ? TailTarget.Instance.TargetPosition : (Vector3?)null,
                            waypointLabel = "DR. PETROV" },
                        new() { text = "Take what she left at the dead drop [E]", flag = MissionFlags.DeadDrop,
                            liveWaypoint = () => Has(MissionFlags.TailDone) ? MissionLayout.DeadDrop : (Vector3?)null, waypointLabel = "DEAD DROP" },
                    },
                },
                new()
                {
                    title = "CREDENTIALS",
                    briefing = "Vale Biotech is locked down. Pick a way in — each needs the right kind of body.",
                    completeWhen = () => Has(MissionFlags.LabEntered),
                    objectives =
                    {
                        new() { text = "A · Possess a GUARD and badge through the Main Gate", flag = MissionFlags.GateOpen, optional = true,
                                waypointFlags = new[] { "door:" + MissionFlags.GateOpen }, waypointLabel = "MAIN GATE" },
                        new() { text = "B · As a SCIENTIST, take the keycard left at the café, then use the Staff Terminal", flag = MissionFlags.StaffOpen, optional = true,
                                waypointFlags = new[] { MissionFlags.Keycard, "terminal" }, waypointLabel = "STAFF ACCESS",
                                extra = () => Has(MissionFlags.Keycard) ? "  (keycard OK)" : "" },
                        new() { text = "C · As MAINTENANCE, restore power at the east-alley substation, then open the Service Tunnel", flag = MissionFlags.TunnelOpen, optional = true,
                                waypointFlags = new[] { MissionFlags.Power, "door:" + MissionFlags.TunnelOpen }, waypointLabel = "SERVICE TUNNEL",
                                extra = () => Has(MissionFlags.Power) ? "  (power OK)" : "" },
                        new() { text = "Get inside the lab", flag = MissionFlags.LabEntered },
                    },
                },
                new()
                {
                    title = "VALE'S RESEARCH",
                    briefing = "Before you face him, learn what he made you for. His office obeys a two-person rule.",
                    completeWhen = () => Has(MissionFlags.OfficeOpen),
                    objectives =
                    {
                        new() { text = "Download the Specimen 7 files — Lab A terminal", flag = MissionFlags.DataA, waypointFlags = new[] { MissionFlags.DataA }, waypointLabel = "LAB A" },
                        new() { text = "Download the Specimen 7 files — Lab B terminal", flag = MissionFlags.DataB, waypointFlags = new[] { MissionFlags.DataB }, waypointLabel = "LAB B" },
                        new() { text = "Office door needs TWO staff: split [V], possess a second staff member, badge both readers within 8 s [Tab]",
                                flag = MissionFlags.OfficeOpen,
                                waypointFlags = new[] { MissionFlags.OfficeOpen + ":Reader A", MissionFlags.OfficeOpen + ":Reader B" }, waypointLabel = "READER" },
                    },
                },
                new()
                {
                    title = "CREATOR",
                    briefing = "He's waiting for you.",
                    completeWhen = () => Has(MissionFlags.Confronted),
                    objectives =
                    {
                        new() { text = "Confront Dr. Vale in his office", flag = MissionFlags.Confronted, waypointFlags = new[] { "vale" }, waypointLabel = "DR. VALE" },
                    },
                },
                new()
                {
                    title = "HUNTER PROTOCOL",
                    briefing = "Vale unleashed Protocol Hound: hunters whose bio-scanners read any body you wear, even one you left parked. Reach the security office and kill the lockdown.",
                    completeWhen = () => Has(MissionFlags.HuntersDown),
                    objectives =
                    {
                        new() { text = "Slip through the security office's scanner arch as a FREE amoeba — it burns any possessed body on the spot",
                            doneWhen = () => Has(MissionFlags.HunterGate) || Has(MissionFlags.HuntersDown),
                            waypoints = new[] { MissionLayout.ScanGate }, waypointLabel = "SCANNER ARCH" },
                        new() { text = "Possess the duty officer and hold the Protocol Hound breaker (4 s)", flag = MissionFlags.HuntersDown,
                            waypoints = new[] { MissionLayout.HoundBreaker }, waypointLabel = "LOCKDOWN BREAKER" },
                        new() { text = "Keep worn bodies out of the scanner cones; a full scan burns that body — leave it [Q] or hop to break the trail",
                            optional = true, doneWhen = () => Has(MissionFlags.HuntersDown) },
                    },
                },
                new()
                {
                    title = "THE PART HE HID",
                    briefing = "The stolen files were bait. Vale's signed archive proves who ordered the experiments. Split between coolant and archive; neither body can leave during the copy.",
                    objectives =
                    {
                        new() { text = "As MAINTENANCE, hold the service-room coolant bypass [E]; leave that body there and [Tab]",
                            doneWhen = () => Has(MissionFlags.SignedArchive) || (CooperativeConsole.Find("coolant") && CooperativeConsole.Find("coolant").Held),
                            waypoints = new[] { MissionLayout.CoolantBypass }, waypointLabel = "COOLANT BYPASS" },
                        new() { text = "As your OTHER amoeba in a SCIENTIST, copy Vale's signed archive in his office (8 s)", flag = MissionFlags.SignedArchive,
                            waypoints = new[] { MissionLayout.SignedArchive }, waypointLabel = "SIGNED ARCHIVE" },
                    },
                },
                new()
                {
                    title = "BREAK THE LEASH",
                    briefing = "A city containment relay can track and seal every escape. Hold a false dispatch with a guard while your other maintenance body overloads the relay. Cameras and campus locks will go dark.",
                    objectives =
                    {
                        new() { text = "As a GUARD, hold the lobby dispatch override [E]; keep this host at the console and [Tab]",
                            doneWhen = () => Has(MissionFlags.ContainmentOffline) || (CooperativeConsole.Find("dispatch") && CooperativeConsole.Find("dispatch").Held),
                            waypoints = new[] { MissionLayout.DispatchOverride }, waypointLabel = "DISPATCH OVERRIDE" },
                        new() { text = "As the OTHER amoeba in MAINTENANCE, overload the east service-alley containment relay (7 s)", flag = MissionFlags.ContainmentOffline,
                            waypoints = new[] { MissionLayout.ContainmentRelay }, waypointLabel = "CONTAINMENT RELAY" },
                    },
                },
                new()
                {
                    title = "THE SHADOW UPLINK",
                    briefing = "Broadcast preliminary findings to independent journalist; journalist alerts Specimen 7 that Vale's primary neural network kill-switch and unredacted human trial archives are locked in the subterranean Black Site Vault.",
                    completeWhen = () => Has(MissionFlags.EvidenceBroadcast),
                    objectives =
                    {
                        new() { text = "As any host, broadcast preliminary evidence at the City Plaza uplink (10 s); lose any pursuit first", flag = MissionFlags.EvidenceBroadcast,
                            waypoints = new[] { MissionLayout.JournalistUplink }, waypointLabel = "JOURNALIST UPLINK" },
                    },
                },
                new()
                {
                    title = "THE BLACK SITE ARCHIVE",
                    briefing = "Dual-amoeba cooperative hack: amoeba 1 as GUARD triggers the Sub-Vault Evacuation Alarm at the Lobby Security Console to draw sentries away; amoeba 2 as SCIENTIST breaches the vault in Lab A and downloads the Black Site Mainframe for 10 s.",
                    completeWhen = () => Has(MissionFlags.BlackSiteArchive),
                    objectives =
                    {
                        new() { text = "As a GUARD, trigger the Sub-Vault Evacuation Alarm at the Lobby Security Console [E]; keep this host at the console and [Tab]",
                            doneWhen = () => Has(MissionFlags.BlackSiteArchive) || Has(MissionFlags.SubVaultDiversion) || (CooperativeConsole.Find(MissionFlags.SubVaultDiversion) && CooperativeConsole.Find(MissionFlags.SubVaultDiversion).Held),
                            waypoints = new[] { MissionLayout.SubVaultDiversion }, waypointLabel = "LOBBY SECURITY CONSOLE" },
                        new() { text = "As your OTHER amoeba in a SCIENTIST, breach the vault in Lab A and download the Black Site Mainframe (10 s)",
                            flag = MissionFlags.BlackSiteArchive,
                            waypoints = new[] { MissionLayout.BlackSiteVault }, waypointLabel = "BLACK SITE VAULT" },
                    },
                },
                new()
                {
                    title = "CONTAINMENT PROTOCOL EPSILON",
                    briefing = "Vale Biotech triggers emergency facility lockdown with red beacons and laser perimeter grids: amoeba 1 as MAINTENANCE overloads the East Service Alley Auxiliary Backup Generator; amoeba 2 as GUARD/SCIENTIST overrides the Forecourt Security Grid Pylon to drop campus lockdown.",
                    completeWhen = () => Has(MissionFlags.ProtocolEpsilonBreached) || Has(MissionFlags.GridOverride),
                    objectives =
                    {
                        new() { text = "As MAINTENANCE, overload the East Service Alley Auxiliary Backup Generator [E]; keep this host at the console and [Tab]",
                            doneWhen = () => Has(MissionFlags.ProtocolEpsilonBreached) || Has(MissionFlags.GridOverride) || Has(MissionFlags.BackupGenerator) || (CooperativeConsole.Find(MissionFlags.BackupGenerator) && CooperativeConsole.Find(MissionFlags.BackupGenerator).Held),
                            waypoints = new[] { MissionLayout.BackupGenerator }, waypointLabel = "AUXILIARY GENERATOR" },
                        new() { text = "As your OTHER amoeba in a GUARD or SCIENTIST, override the Forecourt Security Grid Pylon (8 s)",
                            flag = MissionFlags.GridOverride,
                            waypoints = new[] { MissionLayout.GridOverride }, waypointLabel = "SECURITY GRID PYLON" },
                    },
                },
                new()
                {
                    title = "BLACKOUT",
                    briefing = "Vale's grid can triangulate the Plaza Master Antenna. Black out the three plaza transformers inside one 35-second reroute. Each isolator logs a palm print, so every box needs a different body: park your other amoeba first.",
                    completeWhen = () => Has(MissionFlags.DistrictBlackout),
                    objectives =
                    {
                        new() { text = "Cut transformer P-1 by the west plaza [hold E]", doneWhen = () => Has(MissionFlags.DistrictBlackout) || SwitchCut(0),
                            waypoints = new[] { MissionLayout.Transformers[0].pos }, waypointLabel = "TRANSFORMER P-1" },
                        new() { text = "Cut transformer P-2 at the Market St corner", doneWhen = () => Has(MissionFlags.DistrictBlackout) || SwitchCut(1),
                            waypoints = new[] { MissionLayout.Transformers[1].pos }, waypointLabel = "TRANSFORMER P-2" },
                        new() { text = "Cut transformer P-3 on Campus Road — with a third, different body", doneWhen = () => Has(MissionFlags.DistrictBlackout) || SwitchCut(2),
                            waypoints = new[] { MissionLayout.Transformers[2].pos }, waypointLabel = "TRANSFORMER P-3" },
                    },
                },
                new()
                {
                    title = "THE FINAL CONVERGENCE",
                    briefing = "District-wide high alert: broadcast the unredacted Black Site Archive at the Plaza Master Antenna for 12 s, eject to free amoeba [Q], and extract through the South Alley Storm Drain.",
                    completeWhen = () => Has(MissionFlags.Escaped),
                    objectives =
                    {
                        new() { text = "As any host, broadcast the unredacted Black Site Archive at the Plaza Master Antenna (12 s); lose any pursuit first",
                            flag = MissionFlags.MasterBroadcast,
                            waypoints = new[] { MissionLayout.MasterUplink }, waypointLabel = "MASTER ANTENNA" },
                        new() { text = "After the broadcast, leave your host [Q] and escape through the south-alley storm drain [E]",
                            flag = MissionFlags.Escaped,
                            waypoints = new[] { MissionLayout.Extraction }, waypointLabel = "STORM-DRAIN EXTRACTION" },
                    },
                },
            };
            if (RunState.AfterlakeStarted) AfterlakeCampaign.AppendMissions(list);
            if (RunState.IslandStarted) IslandCampaign.AppendMissions(list);
            if (RunState.HarborStarted) HarborCampaign.AppendMissions(list);
            return list;
        }

        // Restore persistent world changes after checkpoint reload; transient paired controls deliberately reset.
        void RestoreCampaignState()
        {
            bool escapedLab = RunState.Has(MissionFlags.ContainmentOffline) || RunState.Has(MissionFlags.ProtocolEpsilonBreached) || RunState.Has(MissionFlags.GridOverride);
            foreach (var d in FindObjectsByType<AccessDoor>(FindObjectsSortMode.None))
            {
                bool office = d.doorName == "Dr. Vale's Office" && RunState.Has(MissionFlags.OfficeOpen);
                if (office || (!string.IsNullOrEmpty(d.openedFlag) && RunState.Has(d.openedFlag)) || (escapedLab && d.route != EntryRoute.None))
                { d.Unlock(); d.Open(); }
            }
            if (!escapedLab) return;
            foreach (var c in Stealth.SecurityCamera.All) if (c) c.DisableFor(float.PositiveInfinity);
            if (Stealth.SuspicionSystem.Instance)
            { Stealth.SuspicionSystem.Instance.OnSuspectGone(); Stealth.SuspicionSystem.Instance.SetValue(0f); }
        }

        // ------------------------------------------------------------ ending
        /// <summary>Ctrl+M demo jump. Interrupts Vale safely; an existing ending remains untouched and unchosen.</summary>
        public bool TryPresentationEscape()
        {
            var gm = GameManager.Instance;
            var pm = Possession.PossessionManager.Instance;
            if (!gm || RunState.AfterlakeStarted) return false;
            // Repeated requests must never stop the escape coroutine, replace the lake or reset its outcome.
            if (EndingStarted || UI.LakesideEpilogue.Instance || gm.State == GameState.Ended || RunState.Has(MissionFlags.Escaped)) return true;
            if (DialoguePlaying)
            {
                // Only RevealSequence can be active here: EscapeSequence is protected by EndingStarted above.
                StopAllCoroutines();
                DialoguePlaying = false;
                GameEvents.RaiseSubtitle("", "");
                gm.ResumeAfterDialogue();
            }
            if (!gm.IsPlaying || !pm || pm.Active == null || pm.Active.InTransit || !pm.Active.Blob ||
                Missions == null || Missions.Count == 0) return false;
            if (pm.Active.Host && !pm.LeaveHost()) return false;
            foreach (var mission in Missions)
                foreach (var objective in mission.objectives)
                    if (!string.IsNullOrEmpty(objective.flag) && objective.flag != MissionFlags.Escaped) RunState.Flags.Add(objective.flag);
            RunState.Flags.Add(MissionFlags.Research);
            RunState.Flags.Add(MissionFlags.ContainmentOffline);
            RunState.Flags.Add(MissionFlags.EvidenceBroadcast);
            RunState.Flags.Add(MissionFlags.SubVaultDiversion);
            RunState.Flags.Add(MissionFlags.BlackSiteArchive);
            RunState.Flags.Add(MissionFlags.BackupGenerator);
            RunState.Flags.Add(MissionFlags.GridOverride);
            RunState.Flags.Add(MissionFlags.ProtocolEpsilonBreached);
            RunState.Flags.Add(MissionFlags.MasterBroadcast);
            RunState.Flags.Add(MissionFlags.TailDone);
            RunState.Flags.Add(MissionFlags.DeadDrop);
            RunState.Flags.Add(MissionFlags.HunterGate);
            RunState.Flags.Add(MissionFlags.HuntersDown);
            RunState.Flags.Add(MissionFlags.DistrictBlackout);
            RunState.LabEntered = true;
            RunState.MissionIndex = Missions.Count - 1;
            RestoreCampaignState();
            var extraction = FindFirstObjectByType<CampaignExtraction>();
            Vector3 position = extraction ? extraction.transform.position : MissionLayout.Extraction;
            pm.Active.Blob.Place(position + Vector3.up * 0.15f);
            pm.Active.Blob.ClearControlInput();
            var camera = Possession.PlayerCamera.Instance;
            if (camera) { camera.SetYaw(0); camera.SetTarget(pm.Active.Blob.CameraTarget, pm.Active.Blob.CameraDistance, snap: true); }
            GameEvents.RaiseToast("Presentation shortcut — final escape. Your lakeside choice remains yours.", Palette.Info);
            CompleteEscape(extraction ? extraction.transform : pm.Active.Blob.transform);
            return EndingStarted;
        }

        public void BeginConfrontation(Transform vale)
        {
            if (EndingStarted || DialoguePlaying || RunState.Has(MissionFlags.Confronted)) return;
            DialoguePlaying = true;
            if (GameManager.Instance) GameManager.Instance.BeginEnding();
            StartCoroutine(RevealSequence(vale));
        }

        static readonly (string who, string line)[] RevealLines =
        {
            ("DR. VALE", "There you are. Specimen 7. You took longer than I predicted."),
            ("DR. VALE", "Did you really think a containment door fails by accident? I opened it."),
            ("DR. VALE", "The files you stole were bait. I needed to know if you could plan. Hide. Divide yourself and still be one."),
            ("DR. VALE", "The real orders are in my signed archive. Its coolant interlock takes two operators. You cannot carry it out alone."),
            ("DR. VALE", "And even if you do, the containment relay will trace you. My guards control its dispatch lock."),
            ("DR. VALE", "But first, meet my hunters. Their scanners can smell you inside any body you steal."),
            ("SPECIMEN 7", "Two bodies. One choice. This time, I choose where the experiment ends."),
        };

        IEnumerator RevealSequence(Transform vale)
        {
            Sfx.Play(SfxId.EndingSting, vale ? vale.position : Vector3.zero);
            yield return Dialogue(RevealLines);
            DialoguePlaying = false;
            if (GameManager.Instance) GameManager.Instance.ResumeAfterDialogue();
            SetFlag(MissionFlags.Confronted);
            GameEvents.RaiseToast("Vale's hunters are in the building. Their bio-scanners read possessed bodies — reach the security office breaker.", Palette.Amoeba);
        }

        public void CompleteEscape(Transform drain)
        {
            if (RunState.AfterlakeStarted || EndingStarted || !RunState.Has(MissionFlags.MasterBroadcast) || !RunState.Has(MissionFlags.ContainmentOffline)) return;
            EndingStarted = true;
            Waypoints.Clear(); SetObjective("");
            if (GameManager.Instance) GameManager.Instance.BeginEnding();
            StartCoroutine(EscapeSequence(drain));
        }

        IEnumerator EscapeSequence(Transform drain)
        {
            Sfx.Play(SfxId.EndingSting, drain ? drain.position : Vector3.zero);
            yield return Dialogue(new[]
            {
                ("INDEPENDENT NEWS", "The unredacted Black Site files and neural kill-switch are broadcasting across every frequency."),
                ("DR. PETROV", "Protocol Epsilon breached. The entire district knows what Vale did."),
                ("SPECIMEN 7", "He opened the first door. We chose the last one."),
            });
            // GameManager commits Escaped only after the lakeside player's explicit final choice.
            // A story interruption before resolution must still leave the extraction mission resumable.
            var data = new EndingData
            {
                route = RunState.Route,
                timeSeconds = RunState.PlayTime,
                hostsPossessed = RunState.HostsPossessed,
                peakSuspicion = RunState.PeakSuspicion,
                captures = RunState.Captures,
            };
            if (GameManager.Instance) GameManager.Instance.FinishEnding(data);
        }

        static IEnumerator Dialogue((string who, string line)[] lines)
        {
            yield return new WaitForSecondsRealtime(0.4f);
            foreach (var (who, line) in lines)
            {
                GameEvents.RaiseSubtitle(who, line);
                float t = 0, dur = 1.6f + line.Length * 0.045f;
                while (t < dur)
                {
                    t += Time.unscaledDeltaTime;
                    if (t > 0.5f && (GameInput.InteractPressed || GameInput.JumpPressed)) break;
                    yield return null;
                }
            }
            GameEvents.RaiseSubtitle("", "");
        }

        // ------------------------------------------------------------ gameplay dialogue
        Coroutine ambient;

        /// <summary>Non-blocking gameplay dialogue (scaled time); replaces any ambient lines, never protected story dialogue.</summary>
        public static void Say(params (string who, string line)[] lines)
        {
            var mm = Instance;
            if (!mm || mm.EndingStarted || mm.DialoguePlaying || lines == null || lines.Length == 0) return;
            if (mm.ambient != null) mm.StopCoroutine(mm.ambient);
            mm.ambient = mm.StartCoroutine(mm.AmbientLines(lines));
        }

        /// <summary>A one-line bark that never interrupts a running ambient sequence.</summary>
        public static void Bark(string who, string line)
        {
            var mm = Instance;
            if (!mm || mm.ambient != null) return;
            Say((who, line));
        }

        IEnumerator AmbientLines((string who, string line)[] lines)
        {
            foreach (var (who, line) in lines)
            {
                if (EndingStarted || DialoguePlaying) { ambient = null; yield break; }
                GameEvents.RaiseSubtitle(who, line);
                yield return new WaitForSeconds(1.6f + line.Length * 0.045f);
            }
            if (!EndingStarted && !DialoguePlaying) GameEvents.RaiseSubtitle("", "");
            ambient = null;
        }

        public static string RouteName(EntryRoute r) => r switch
        {
            EntryRoute.MainGate => "Main Gate (security badge)",
            EntryRoute.StaffTerminal => "Staff Terminal (scientist credentials)",
            EntryRoute.ServiceTunnel => "Service Tunnel (maintenance keys)",
            _ => "Unknown",
        };
    }
}
