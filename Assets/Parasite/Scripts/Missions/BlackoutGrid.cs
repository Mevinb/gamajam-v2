using System.Collections.Generic;
using Parasite.Characters;
using Parasite.FX;
using Parasite.World;
using UnityEngine;

namespace Parasite.Missions
{
    /// <summary>
    /// BLACKOUT coordinator: the first isolator pulled opens a reroute window; all three must be dark before it closes
    /// or every transformer re-arms (and the palm-print log clears). Each outage kills the street lights it feeds and
    /// sends the nearest guards to look. Completion persists (<see cref="doneFlag"/>) and is restored after reloads.
    /// </summary>
    public class BlackoutGrid : MonoBehaviour
    {
        public static BlackoutGrid Instance { get; private set; }
        public string requiredFlag = MissionFlags.ProtocolEpsilonBreached, doneFlag = MissionFlags.DistrictBlackout;
        public float windowSeconds = BlackoutRules.WindowSeconds;
        public BlackoutSwitch[] switches = new BlackoutSwitch[0];
        [Header("Night-shift pedestrians (spawned when the mission starts, so a third body is always near)")]
        public string[] extraNames = new string[0];
        public Profession[] extraProfessions = new Profession[0];
        public Vector3[] extraPositions = new Vector3[0];
        public int[] extraSeeds = new int[0];

        public bool Live => RunState.Has(requiredFlag) && !RunState.Has(doneFlag);
        public bool WindowOpen => windowStart >= 0f;
        public float Remaining => BlackoutRules.Remaining(windowStart, Time.time, windowSeconds);
        public int Reroutes { get; private set; }
        public int CutCount { get { int n = 0; foreach (var s in switches) if (s && s.Cut) n++; return n; } }
        /// <summary>Street/wall lights currently switched off by the outage.</summary>
        public int DarkLights { get { darkened.RemoveWhere(l => !l); return darkened.Count; } }

        readonly HashSet<int> usedBodies = new();
        readonly Dictionary<BlackoutSwitch, List<Light>> fed = new();
        readonly List<Light> allLights = new();
        readonly HashSet<Light> darkened = new();
        float windowStart = -1f;
        bool started;

        static readonly (string who, string line)[] BriefLines =
        {
            ("DR. PETROV (radio)", "Vale has the city grid triangulating every transmitter near the plaza. The antenna won't last a minute."),
            ("DR. PETROV (radio)", "Three transformers feed that grid. Kill all three inside one reroute — and every isolator logs a palm print."),
        };
        static readonly (string who, string line)[] FaultLine = { ("CITY GRID", "Fault detected. Rerouting plaza circuits in thirty-five seconds.") };
        static readonly (string who, string line)[] RerouteLine = { ("CITY GRID", "Reroute complete. All plaza circuits restored.") };
        static readonly (string who, string line)[] DoneLines =
        {
            ("CITY GRID", "Plaza district offline. Emergency power only."),
            ("DR. PETROV (radio)", "The antenna's on its own battery now. They're blind — go!"),
        };

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (!l || l.type == LightType.Directional || l.GetComponentInParent<HostCharacter>()) continue;
                foreach (var s in switches)
                {
                    if (!s) continue;
                    Vector3 d = l.transform.position - s.transform.position; d.y = 0f;
                    if (d.magnitude > BlackoutRules.LightRadius) continue;
                    if (!fed.TryGetValue(s, out var list)) fed[s] = list = new List<Light>();
                    list.Add(l);
                    if (!allLights.Contains(l)) allLights.Add(l);
                }
            }
            if (RunState.Has(doneFlag))
                foreach (var s in switches) if (s) { s.SetCut(); SetLights(s, false); }
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm && !gm.IsPlaying) return;
            if (!started && Live) Begin();
            if (WindowOpen && BlackoutRules.Expired(windowStart, Time.time, windowSeconds)) Reroute();
        }

        void Begin()
        {
            started = true;
            for (int i = 0; i < extraNames.Length && i < extraPositions.Length; i++)
            {
                if (MissionSpawner.Find(extraNames[i])) continue;
                var prof = i < extraProfessions.Length ? extraProfessions[i] : Profession.Civilian;
                MissionSpawner.Spawn(extraNames[i], prof, extraPositions[i], 180f, i < extraSeeds.Length ? extraSeeds[i] : 400 + i,
                    prof == Profession.Maintenance ? NpcBehaviour.Stationary : NpcBehaviour.Wander, null, 2.5f);
            }
            MissionManager.Say(BriefLines);
        }

        public bool UsedBy(HostCharacter h) => h && !BlackoutRules.CanOperate(h.GetInstanceID(), usedBodies);

        public void OnCut(BlackoutSwitch s, HostCharacter by)
        {
            if (by) usedBodies.Add(by.GetInstanceID());
            bool first = !WindowOpen;
            if (first) windowStart = Time.time;
            SetLights(s, false);
            SendResponders(s.InteractPoint);
            if (BlackoutRules.Complete(CutCount))
            {
                windowStart = -1f;
                usedBodies.Clear();
                Sfx.Ui(SfxId.TerminalBeep, 0.9f);
                MissionManager.SetFlag(doneFlag);
                MissionManager.Say(DoneLines);
                GameEvents.RaiseToast("DISTRICT BLACKOUT — the Plaza Master Antenna runs on emergency battery. Vale can't trace it.", Palette.Amoeba);
                return;
            }
            if (first) { MissionManager.Say(FaultLine); Sfx.Ui(SfxId.AlarmSiren, 0.5f); }
            GameEvents.RaiseToast($"{s.switchName} is dark — {BlackoutRules.Required - CutCount} to go. Grid reroute in {Remaining:0} s!", Palette.Warning);
        }

        void Reroute()
        {
            windowStart = -1f;
            usedBodies.Clear();
            Reroutes++;
            foreach (var s in switches) if (s) { s.Rearm(); SetLights(s, true); }
            Sfx.Ui(SfxId.Denied, 0.7f);
            MissionManager.Say(RerouteLine);
            GameEvents.RaiseToast("The grid rerouted — all three transformers are live again. Pre-position your bodies and try again.", Palette.Danger);
        }

        // Lamp lights are budgeted per frame by the graphics LampLights manager (which owns Light.enabled), so a dedicated
        // light object is switched off by deactivating its GameObject; anything else just has the component disabled.
        void SetLights(BlackoutSwitch s, bool on)
        {
            if (!fed.TryGetValue(s, out var list)) return;
            foreach (var l in list)
            {
                if (!l) continue;
                bool dedicated = l.GetComponents<Component>().Length <= 2 && l.transform.childCount == 0;
                if (dedicated) l.gameObject.SetActive(on); else l.enabled = on;
                if (on) darkened.Remove(l); else darkened.Add(l);
            }
        }

        static void SendResponders(Vector3 point)
        {
            var guards = new List<HostCharacter>();
            foreach (var h in HostCharacter.All)
                if (h && h.IsAwakeNpc && h.Brain is GuardAI) guards.Add(h);
            guards.Sort((a, b) => (a.transform.position - point).sqrMagnitude.CompareTo((b.transform.position - point).sqrMagnitude));
            for (int i = 0; i < guards.Count && i < BlackoutRules.RespondingGuards; i++)
                ((GuardAI)guards[i].Brain).OrderInvestigate(point);
        }

        /// <summary>Live objective-panel line while the reroute window is open.</summary>
        public string StatusLine() => WindowOpen ? $"GRID REROUTE IN {Remaining:0} s · {CutCount}/{BlackoutRules.Required} dark — every box needs a different body" : "";
    }
}
