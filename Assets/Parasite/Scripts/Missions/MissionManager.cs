using System.Collections;
using Parasite.FX;
using Parasite.Stealth;
using Parasite.World;
using UnityEngine;

namespace Parasite.Missions
{
    /// <summary>Single mission: get into Vale Biotech by one of three routes and confront Dr. Vale.</summary>
    public class MissionManager : MonoBehaviour
    {
        public static MissionManager Instance { get; private set; }
        public string Objective { get; private set; } = "";
        public bool EndingStarted { get; private set; }

        public const string ObjectiveStart =
            "Find Dr. Vale inside VALE BIOTECH (north block).\n" +
            "Three ways in: a GUARD's badge opens the Main Gate · a SCIENTIST can use the Staff Terminal · " +
            "a MAINTENANCE worker can open the Service Tunnel.";
        public const string ObjectiveInside = "You're in. Find Dr. Vale's office at the back of the lab.";

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start() => SetObjective(RunState.LabEntered ? ObjectiveInside : ObjectiveStart);

        void SetObjective(string text)
        {
            Objective = text;
            GameEvents.RaiseObjective(text);
        }

        public void RegisterRouteUsed(EntryRoute route)
        {
            if (RunState.Route == EntryRoute.None) RunState.Route = route;
        }

        void Update()
        {
            if (EndingStarted || RunState.LabEntered) return;
            var pm = Possession.PossessionManager.Instance;
            if (pm == null || pm.ActiveBody == null) return;
            if (LabInteriorZone.InAny(pm.ActiveBody.Root.position)) OnEnteredLab(pm.ActiveBody.Root.position);
        }

        void OnEnteredLab(Vector3 pos)
        {
            RunState.LabEntered = true;
            SetObjective(ObjectiveInside);
            GameEvents.RaiseToast("Inside Vale Biotech", Palette.Amoeba);
            GameManager.Instance?.SetCheckpoint(pos, "Vale Biotech Lab");
        }

        public void BeginEnding(Transform vale)
        {
            if (EndingStarted) return;
            EndingStarted = true;
            if (RunState.Route == EntryRoute.None) RunState.Route = EntryRoute.MainGate;
            GameManager.Instance?.BeginEnding();
            StartCoroutine(EndingSequence(vale));
        }

        static readonly (string who, string line)[] Lines =
        {
            ("DR. VALE", "There you are. Specimen 7. You took longer than I predicted."),
            ("DR. VALE", "Did you really think a containment door fails by accident? I opened it."),
            ("DR. VALE", "The guard rotation, the broken drain grate, the unlocked tunnel… I left you a path."),
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

    /// <summary>On Dr. Vale. Any form (blob or host) can confront him.</summary>
    public class DrValeInteraction : MonoBehaviour, IInteractable
    {
        public float range = 3.6f;
        public Vector3 InteractPoint => transform.position + Vector3.up * 1.5f;
        public float InteractRange => range;
        public bool IsAvailable => MissionManager.Instance != null && !MissionManager.Instance.EndingStarted;
        void OnEnable() => Interactables.Register(this);
        void OnDisable() => Interactables.Unregister(this);
        public string GetPrompt(in InteractContext ctx) => "Confront Dr. Vale";
        public bool CanInteract(in InteractContext ctx, out string deniedReason) { deniedReason = null; return true; }
        public void Interact(in InteractContext ctx) => MissionManager.Instance?.BeginEnding(transform);
    }
}
