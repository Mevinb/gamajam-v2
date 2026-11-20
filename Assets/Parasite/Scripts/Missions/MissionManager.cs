using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Parasite.FX;
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
    /// Eight-mission campaign: infiltrate via three routes, meet Vale, steal signed evidence, break containment,
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
            if (flag == MissionFlags.ContainmentOffline) RestoreCampaignState();
            if (flag == MissionFlags.Confronted || flag == MissionFlags.SignedArchive || flag == MissionFlags.ContainmentOffline || flag == MissionFlags.EvidenceBroadcast)
            {
                var pm = Possession.PossessionManager.Instance;
                if (GameManager.Instance && pm && pm.ActiveBody != null)
                    GameManager.Instance.SetCheckpoint(pm.ActiveBody.Root.position, "After " + flag.Replace('_', ' '));
            }
            if (flag == MissionFlags.Eavesdropped) GameEvents.RaiseToast("Overheard: Dr. Vale works nights at VALE BIOTECH. Three ways in.", Palette.Amoeba);
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
            while (Current != null && Current.IsComplete && guard++ < 10)
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
            }
            var prop = MissionProp.Current;
            if (prop) sb.Append($"\n<color=#ffd45a>  {prop.verb}… {prop.Progress01 * 100f:0}%</color>");
            var work = CampaignOperation.Current;
            if (work) sb.Append($"\n<color=#ffd45a>  {work.displayName}… {work.Progress01 * 100f:0}%</color>");
            var ez = EavesdropZone.Active;
            if (ez && ez.Listening) sb.Append($"\n<color=#ffd45a>  Listening… {ez.Progress01 * 100f:0}%</color>");
            var dr = DualAuthReader.ArmedAny();
            if (dr) sb.Append($"\n<color=#ffd45a>  {dr.readerName} armed — {dr.ArmedRemaining:0.0}s</color>");
            SetObjective(sb.ToString());
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
                    title = "OUTSIDE THE EXPERIMENT",
                    briefing = "The relay is dead. Get the signed evidence to the independent journalist's Plaza uplink, then abandon your disguise and vanish through the storm drain where it began.",
                    objectives =
                    {
                        new() { text = "As any host, broadcast the evidence at the City Plaza uplink (10 s); lose any pursuit first", flag = MissionFlags.EvidenceBroadcast,
                            waypoints = new[] { MissionLayout.JournalistUplink }, waypointLabel = "JOURNALIST UPLINK" },
                        new() { text = "After the broadcast, leave your host [Q] and escape through the south-alley storm drain [E]", flag = MissionFlags.Escaped,
                            waypoints = new[] { MissionLayout.Extraction }, waypointLabel = "STORM-DRAIN EXTRACTION" },
                    },
                },
            };
            return list;
        }

        // Restore persistent world changes after checkpoint reload; transient paired controls deliberately reset.
        void RestoreCampaignState()
        {
            bool escapedLab = RunState.Has(MissionFlags.ContainmentOffline);
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
        /// <summary>Ctrl+M demo jump. Reuses the real drain story and still requires an explicit lakeside choice.</summary>
        public bool TryPresentationEscape()
        {
            var gm = GameManager.Instance;
            var pm = Possession.PossessionManager.Instance;
            if (!gm || !gm.IsPlaying || EndingStarted || DialoguePlaying || UI.LakesideEpilogue.Instance ||
                !pm || pm.Active == null || pm.Active.InTransit || !pm.Active.Blob) return false;
            if (pm.Active.Host && !pm.LeaveHost()) return false;
            foreach (var mission in Missions)
                foreach (var objective in mission.objectives)
                    if (!string.IsNullOrEmpty(objective.flag) && objective.flag != MissionFlags.Escaped) RunState.Flags.Add(objective.flag);
            RunState.Flags.Add(MissionFlags.Research);
            RunState.Flags.Add(MissionFlags.ContainmentOffline);
            RunState.Flags.Add(MissionFlags.EvidenceBroadcast);
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
            ("SPECIMEN 7", "Two bodies. One choice. This time, I choose where the experiment ends."),
        };

        IEnumerator RevealSequence(Transform vale)
        {
            Sfx.Play(SfxId.EndingSting, vale ? vale.position : Vector3.zero);
            yield return Dialogue(RevealLines);
            DialoguePlaying = false;
            if (GameManager.Instance) GameManager.Instance.ResumeAfterDialogue();
            SetFlag(MissionFlags.Confronted);
            GameEvents.RaiseToast("Vale's reveal is only the beginning. Keep both amoebas: maintenance at coolant, scientist at the signed archive.", Palette.Amoeba);
        }

        public void CompleteEscape(Transform drain)
        {
            if (EndingStarted || !RunState.Has(MissionFlags.EvidenceBroadcast) || !RunState.Has(MissionFlags.ContainmentOffline)) return;
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
                ("INDEPENDENT NEWS", "The signed orders are public. Vale Biotech cannot call this a lab accident anymore."),
                ("DR. PETROV", "The containment relay is gone. Whatever you are… keep going."),
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

        public static string RouteName(EntryRoute r) => r switch
        {
            EntryRoute.MainGate => "Main Gate (security badge)",
            EntryRoute.StaffTerminal => "Staff Terminal (scientist credentials)",
            EntryRoute.ServiceTunnel => "Service Tunnel (maintenance keys)",
            _ => "Unknown",
        };
    }
}
