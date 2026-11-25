using Parasite.FX;
using Parasite.Stealth;
using UnityEngine;

namespace Parasite.Missions
{
    /// <summary>
    /// Generic mission interactable (newsstand, keycard, power box, data terminal…). Press E (optionally hold in range
    /// for <see cref="holdSeconds"/>) to set <see cref="flag"/>. Optional host/permission/prerequisite-flag gates.
    /// </summary>
    public class MissionProp : MonoBehaviour, IInteractable
    {
        public string flag = "prop_used";
        public string displayName = "Object";
        public string verb = "Use";
        public bool requireHost = true;
        public AccessPermission requiredPermission = AccessPermission.None;
        public string permissionReason = "";
        public string requiredFlag = "";
        public string requiredFlagReason = "";
        [Tooltip("Mission flag that must be active (current objective) for the prop to be usable; empty = always.")]
        public string activeWhenFlagMissing = "";
        public float holdSeconds = 0f;
        public float range = 2.3f;
        public string doneToast = "";
        public bool hideWhenDone;
        [Tooltip("When set, the prop (glow + reveal objects) stays hidden and unusable until this flag is set.")]
        public string visibleWhenFlag = "";
        public GameObject[] revealObjects = new GameObject[0];
        public Renderer glow;
        public Transform interactPoint;

        public bool Busy => busyT > 0f;
        public float Progress01 => holdSeconds <= 0f ? 0f : Mathf.Clamp01(1f - busyT / holdSeconds);
        public static MissionProp Current { get; private set; }

        float busyT;
        Possession.AmoebaAgent busyAgent;
        IControllable busyBody;
        Characters.HostCharacter busyHost;

        public Vector3 InteractPoint => interactPoint ? interactPoint.position : transform.position + Vector3.up * 1.1f;
        public float InteractRange => range;
        public bool IsAvailable => isActiveAndEnabled && !RunState.Has(flag) && !Busy && Revealed;
        public bool Revealed => string.IsNullOrEmpty(visibleWhenFlag) || RunState.Has(visibleWhenFlag);

        void OnEnable() => Interactables.Register(this);
        void OnDisable() { Interactables.Unregister(this); StopWork(); }

        void Start() => RefreshVisual();

        void RefreshVisual()
        {
            bool done = RunState.Has(flag);
            bool revealed = Revealed;
            foreach (var o in revealObjects) if (o && o.activeSelf != revealed) o.SetActive(revealed);
            if (glow && glow.enabled != revealed) glow.enabled = revealed;
            if (glow)
            {
                var m = glow.material;
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", done ? new Color(0.1f, 0.4f, 0.15f) : new Color(0.25f, 1f, 0.45f) * (1.2f + 0.6f * Mathf.Sin(Time.time * 3f)));
            }
            if (done && hideWhenDone) gameObject.SetActive(false);
        }

        public string GetPrompt(in InteractContext ctx) => $"{verb} — {displayName}";

        public bool CanInteract(in InteractContext ctx, out string deniedReason)
        {
            deniedReason = null;
            var pm = Possession.PossessionManager.Instance;
            if (ctx.agent == null || !pm || !pm.Agents.Contains(ctx.agent) || ctx.agent.InTransit)
            { deniedReason = "Your amoeba is not ready to operate this."; return false; }
            if (ctx.host)
            {
                if (ctx.agent.Host != ctx.host || ctx.host.Possessor != ctx.agent || ctx.host.State != HostState.Possessed)
                { deniedReason = "You must control an uncaptured host to operate this."; return false; }
            }
            else if (ctx.agent.Host != null || !ctx.agent.Blob || !ctx.agent.Blob.isActiveAndEnabled)
            { deniedReason = "Your amoeba is not ready to operate this."; return false; }
            if (requireHost && ctx.IsBlob) { deniedReason = "You need a body for this. Possess someone."; return false; }
            var permissions = ctx.host ? ctx.host.Permissions : AccessPermission.None;
            if (!AccessRules.Has(permissions, requiredPermission))
            { deniedReason = string.IsNullOrEmpty(permissionReason) ? "Your host isn't authorised." : permissionReason; return false; }
            if (!string.IsNullOrEmpty(requiredFlag) && !RunState.Has(requiredFlag))
            { deniedReason = string.IsNullOrEmpty(requiredFlagReason) ? "Not yet." : requiredFlagReason; return false; }
            if (!Revealed) { deniedReason = "Not yet — this control is unavailable."; return false; }
            return true;
        }

        public void Interact(in InteractContext ctx)
        {
            var gm = GameManager.Instance;
            if (!IsAvailable || (gm && !gm.IsPlaying)) return;
            if (!CanInteract(ctx, out var reason))
            {
                GameEvents.RaiseToast(reason, Palette.Danger);
                Sfx.Play(SfxId.Denied, InteractPoint);
                if (requiredPermission != AccessPermission.None && !ctx.IsBlob && !AccessRules.Has(ctx.permissions, requiredPermission))
                    SuspicionSystem.Instance?.ReportViolation(ViolationType.UnauthorizedAccess, InteractPoint, ctx.host);
                return;
            }
            if (!InStartReach(ctx.agent.Position)) { GameEvents.RaiseToast("Move closer to the control.", Palette.Warning); return; }
            if (holdSeconds <= 0f) { Complete(); return; }
            busyT = holdSeconds;
            busyAgent = ctx.agent;
            busyBody = ctx.agent.Body;
            busyHost = ctx.host;
            Current = this;
            Sfx.Play(SfxId.TerminalBeep, InteractPoint, 0.6f);
            GameEvents.RaiseToast($"{verb}… stay close", Palette.Info);
        }

        // Matches PossessionManager's planar targeting reach, including its vertical allowance.
        bool InStartReach(Vector3 position)
        {
            var delta = InteractPoint - position;
            if (Mathf.Abs(delta.y) > 2.6f) return false;
            delta.y = 0f;
            return delta.sqrMagnitude <= range * range;
        }

        // Retain the original holding tolerance so small controller pushes do not invalidate a started hold.
        bool InHoldReach(Vector3 position) => (position - InteractPoint).sqrMagnitude < (range + 1.2f) * (range + 1.2f);

        void StopWork(bool interrupted = false)
        {
            bool wasBusy = Busy;
            busyT = 0f; busyAgent = null; busyBody = null; busyHost = null;
            if (Current == this) Current = null;
            if (!interrupted || !wasBusy) return;
            GameEvents.RaiseToast($"{displayName}: interrupted", Palette.Warning);
            Sfx.Ui(SfxId.Denied, 0.4f);
        }

        void Update()
        {
            if (glow && !RunState.Has(flag) && Time.frameCount % 6 == 0) RefreshVisual();
            if (!Busy) return;
            var gm = GameManager.Instance;
            if ((gm && (gm.State == GameState.Resetting || gm.State == GameState.Ended)) || RunState.Has(flag))
            { StopWork(); return; }
            var pm = Possession.PossessionManager.Instance;
            bool sameBody = busyAgent != null && pm && pm.Agents.Contains(busyAgent) && !busyAgent.InTransit &&
                busyBody != null && !(busyBody is Object unityBody && !unityBody) && ReferenceEquals(busyAgent.Body, busyBody);
            if (!sameBody) { StopWork(interrupted: true); return; }
            var ctx = new InteractContext
            {
                agent = busyAgent, host = busyHost,
                permissions = busyHost ? busyHost.Permissions : AccessPermission.None, position = busyAgent.Position,
            };
            if (!InHoldReach(busyAgent.Position) || !CanInteract(ctx, out _)) { StopWork(interrupted: true); return; }
            // Switching selection is fine: the same authorized body may continue working while inactive.
            // Pause/story protection preserves a valid hold, but never advances its timer.
            if (gm && !gm.IsPlaying) return;
            busyT -= Time.deltaTime;
            if (busyT <= 0f) Complete();
        }

        void Complete()
        {
            StopWork();
            Sfx.Play(SfxId.TerminalBeep, InteractPoint);
            MissionManager.SetFlag(flag);
            if (!string.IsNullOrEmpty(doneToast)) GameEvents.RaiseToast(doneToast, Palette.Amoeba);
            RefreshVisual();
        }

        /// <summary>For tests.</summary>
        public void ForceComplete() => Complete();
    }
}
