using Parasite.Characters;
using Parasite.FX;
using Parasite.Possession;
using Parasite.Stealth;
using UnityEngine;

namespace Parasite.Missions
{
    /// <summary>
    /// One BLACKOUT transformer isolator: a human host holds [E] for <see cref="BlackoutRules.HoldSeconds"/>. The grid
    /// refuses a body that already pulled another isolator in the current window. Non-maintenance bodies tampering
    /// with it in view of a witness raise suspicion.
    /// </summary>
    public class BlackoutSwitch : MonoBehaviour, IInteractable
    {
        public static BlackoutSwitch Current { get; private set; }
        public string switchName = "TRANSFORMER";
        public Renderer lamp;
        public Transform interactPoint;
        public float range = 2.3f;

        public bool Cut { get; private set; }
        public bool Busy => busyAgent != null;
        public float Progress01 => Busy ? Mathf.Clamp01(holdT / BlackoutRules.HoldSeconds) : 0f;
        public Vector3 InteractPoint => interactPoint ? interactPoint.position : transform.position + Vector3.up * 1.1f;
        public float InteractRange => range;
        public bool IsAvailable => isActiveAndEnabled && Grid && Grid.Live && !Cut && !Busy;

        AmoebaAgent busyAgent;
        HostCharacter busyHost;
        float holdT;
        Material lampMat;
        static BlackoutGrid Grid => BlackoutGrid.Instance;

        void OnEnable() => Interactables.Register(this);
        void OnDisable() { Interactables.Unregister(this); if (Current == this) Current = null; busyAgent = null; }

        public string GetPrompt(in InteractContext ctx) => $"Pull isolator — {switchName}";

        public bool CanInteract(in InteractContext ctx, out string deniedReason)
        {
            deniedReason = null;
            if (!Grid || !Grid.Live) { deniedReason = "Not yet — follow the current mission."; return false; }
            if (ctx.IsBlob || ctx.host.State != HostState.Possessed) { deniedReason = "Only human hands can pull an isolator. Possess someone."; return false; }
            if (Grid.UsedBy(ctx.host)) { deniedReason = "This body's palm print is already logged on the grid — use a DIFFERENT body."; return false; }
            return true;
        }

        public void Interact(in InteractContext ctx)
        {
            if (!CanInteract(ctx, out var why))
            {
                GameEvents.RaiseToast(why, Palette.Warning);
                Sfx.Play(SfxId.Denied, InteractPoint);
                return;
            }
            busyAgent = ctx.agent; busyHost = ctx.host; holdT = 0f; Current = this;
            if (busyHost.Profession != Profession.Maintenance)
                SuspicionSystem.Instance?.ReportViolation(ViolationType.UnauthorizedAccess, InteractPoint, busyHost);
            Sfx.Play(SfxId.TerminalBeep, InteractPoint, 0.6f);
            GameEvents.RaiseToast($"Pulling the {switchName} isolator… stay close", Palette.Info);
        }

        void Update()
        {
            RefreshLamp();
            if (!Busy) return;
            var gm = GameManager.Instance;
            if (gm && !gm.IsPlaying) return;
            var pm = PossessionManager.Instance;
            bool here = pm != null && pm.Agents.Contains(busyAgent) && !busyAgent.InTransit && busyHost && busyAgent.Host == busyHost &&
                        busyHost.State == HostState.Possessed && Grid && Grid.Live && !Cut &&
                        (busyAgent.Position - InteractPoint).sqrMagnitude < (range + 1f) * (range + 1f);
            if (!here)
            {
                busyAgent = null; busyHost = null; if (Current == this) Current = null;
                GameEvents.RaiseToast($"{switchName}: interrupted", Palette.Warning);
                Sfx.Ui(SfxId.Denied, 0.4f);
                return;
            }
            holdT += Time.deltaTime;
            if (holdT < BlackoutRules.HoldSeconds) return;
            var by = busyHost;
            busyAgent = null; busyHost = null; if (Current == this) Current = null;
            Cut = true;
            Sfx.Play(SfxId.Release, InteractPoint, 0.9f);
            if (Grid) Grid.OnCut(this, by);
        }

        /// <summary>The grid rerouted: the isolator is live again.</summary>
        public void Rearm() { Cut = false; busyAgent = null; busyHost = null; if (Current == this) Current = null; }

        /// <summary>Restored after a checkpoint reload once the district blackout is complete.</summary>
        public void SetCut() { Cut = true; busyAgent = null; busyHost = null; }

        void RefreshLamp()
        {
            if (!lamp) return;
            if (!lampMat) { lampMat = lamp.material; lampMat.EnableKeyword("_EMISSION"); }
            Color c = Cut ? new Color(0.02f, 0.02f, 0.02f)
                : Busy ? Palette.Warning * 2f
                : Grid && Grid.Live ? Palette.Amoeba * (1.2f + 0.8f * Mathf.Sin(Time.time * 4f)) : Palette.Info * 0.4f;
            lampMat.SetColor("_EmissionColor", c);
        }

        void OnDestroy() { if (lampMat) Destroy(lampMat); }
    }
}
