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
        public Renderer glow;
        public Transform interactPoint;

        public bool Busy => busyT > 0f;
        public float Progress01 => holdSeconds <= 0f ? 0f : Mathf.Clamp01(1f - busyT / holdSeconds);
        public static MissionProp Current { get; private set; }

        float busyT;
        Possession.AmoebaAgent busyAgent;
        Vector3 busyPos;

        public Vector3 InteractPoint => interactPoint ? interactPoint.position : transform.position + Vector3.up * 1.1f;
        public float InteractRange => range;
        public bool IsAvailable => isActiveAndEnabled && !RunState.Has(flag) && !Busy;

        void OnEnable() => Interactables.Register(this);
        void OnDisable() { Interactables.Unregister(this); if (Current == this) Current = null; }

        void Start() => RefreshVisual();

        void RefreshVisual()
        {
            bool done = RunState.Has(flag);
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
            if (requireHost && ctx.IsBlob) { deniedReason = "You need a body for this. Possess someone."; return false; }
            if (!AccessRules.Has(ctx.permissions, requiredPermission))
            { deniedReason = string.IsNullOrEmpty(permissionReason) ? "Your host isn't authorised." : permissionReason; return false; }
            if (!string.IsNullOrEmpty(requiredFlag) && !RunState.Has(requiredFlag))
            { deniedReason = string.IsNullOrEmpty(requiredFlagReason) ? "Not yet." : requiredFlagReason; return false; }
            return true;
        }

        public void Interact(in InteractContext ctx)
        {
            if (!CanInteract(ctx, out var reason))
            {
                GameEvents.RaiseToast(reason, Palette.Danger);
                Sfx.Play(SfxId.Denied, InteractPoint);
                if (requiredPermission != AccessPermission.None && !ctx.IsBlob && !AccessRules.Has(ctx.permissions, requiredPermission))
                    SuspicionSystem.Instance?.ReportViolation(ViolationType.UnauthorizedAccess, InteractPoint, ctx.host);
                return;
            }
            if (holdSeconds <= 0f) { Complete(); return; }
            busyT = holdSeconds;
            busyAgent = ctx.agent;
            busyPos = ctx.position;
            Current = this;
            Sfx.Play(SfxId.TerminalBeep, InteractPoint, 0.6f);
            GameEvents.RaiseToast($"{verb}… stay close", Palette.Info);
        }

        void Update()
        {
            if (glow && !RunState.Has(flag) && Time.frameCount % 6 == 0) RefreshVisual();
            if (busyT <= 0f) return;
            var pm = Possession.PossessionManager.Instance;
            bool stillHere = busyAgent != null && pm != null && pm.Agents.Contains(busyAgent) && !busyAgent.InTransit &&
                             (busyAgent.Position - InteractPoint).sqrMagnitude < (range + 1.2f) * (range + 1.2f) &&
                             (!requireHost || busyAgent.Host != null);
            if (!stillHere)
            {
                busyT = 0f; Current = null;
                GameEvents.RaiseToast($"{displayName}: interrupted", Palette.Warning);
                Sfx.Ui(SfxId.Denied, 0.4f);
                return;
            }
            busyT -= Time.deltaTime;
            if (busyT <= 0f) Complete();
        }

        void Complete()
        {
            busyT = 0f; if (Current == this) Current = null;
            Sfx.Play(SfxId.TerminalBeep, InteractPoint);
            MissionManager.SetFlag(flag);
            if (!string.IsNullOrEmpty(doneToast)) GameEvents.RaiseToast(doneToast, Palette.Amoeba);
            RefreshVisual();
        }

        /// <summary>For tests.</summary>
        public void ForceComplete() => Complete();
    }
}
