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
    /// Five-mission campaign: escape → find the creator → get credentials (3 routes) → steal the research
    /// (incl. a two-person-rule door that needs the split) → confront Dr. Vale. Flags persist across checkpoint reloads.
    /// </summary>
    public class MissionManager : MonoBehaviour
    {
        public static MissionManager Instance { get; private set; }
        public string Objective { get; private set; } = "";
        public bool EndingStarted { get; private set; }
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
            Advance(announce: true);
            Refresh();
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
            if (EndingStarted) return;
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
            };
            return list;
        }

        // ------------------------------------------------------------ ending
        public void BeginEnding(Transform vale)
        {
            if (EndingStarted) return;
            EndingStarted = true;
            RunState.Flags.Add(MissionFlags.Confronted);
            if (RunState.Route == EntryRoute.None) RunState.Route = EntryRoute.MainGate;
            Waypoints.Clear();
            SetObjective("");
            GameManager.Instance?.BeginEnding();
            StartCoroutine(EndingSequence(vale));
        }

        static readonly (string who, string line)[] Lines =
        {
            ("DR. VALE", "There you are. Specimen 7. You took longer than I predicted."),
            ("DR. VALE", "Did you really think a containment door fails by accident? I opened it."),
            ("DR. VALE", "The guard rotation, the broken drain grate, the unlocked tunnel… I left you a path."),
            ("DR. VALE", "And the files you just stole? I wrote them for you to find."),
            ("DR. VALE", "I needed to know if you could plan. Choose. Hide. Divide yourself and still be one."),
            ("DR. VALE", "You can. Which means the experiment is over—and the real work begins."),
            ("DR. VALE", "Welcome home."),
        };

        IEnumerator EndingSequence(Transform vale)
        {
            Sfx.Play(SfxId.EndingSting, vale ? vale.position : Vector3.zero);
            yield return new WaitForSeconds(0.4f);
            foreach (var (who, line) in Lines)
            {
                GameEvents.RaiseSubtitle(who, line);
                float t = 0, dur = 1.6f + line.Length * 0.045f;
                while (t < dur)
                {
                    t += Time.deltaTime;
                    if (t > 0.5f && (GameInput.InteractPressed || GameInput.JumpPressed)) break;
                    yield return null;
                }
            }
            GameEvents.RaiseSubtitle("", "");
            var data = new EndingData
            {
                route = RunState.Route,
                timeSeconds = RunState.PlayTime,
                hostsPossessed = RunState.HostsPossessed,
                peakSuspicion = RunState.PeakSuspicion,
                captures = RunState.Captures,
            };
            GameManager.Instance?.FinishEnding(data);
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
