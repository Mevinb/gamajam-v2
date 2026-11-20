using Parasite.FX;
using Parasite.Stealth;
using UnityEngine;
using UnityEngine.AI;

namespace Parasite.Missions
{
    /// <summary>
    /// A door/gate/hatch that requires a host permission. The moving part is <see cref="panel"/>.
    /// If <see cref="terminalControlled"/> it cannot be opened directly; a StaffTerminal unlocks it.
    /// </summary>
    public class AccessDoor : MonoBehaviour, IInteractable
    {
        public string doorName = "Door";
        public AccessPermission required = AccessPermission.None;
        public EntryRoute route = EntryRoute.None;
        public bool terminalControlled;
        public Transform panel;
        public Vector3 openLocalOffset = new(0f, 0f, 0f);
        public Vector3 openLocalEuler = new(0f, 0f, 0f);
        public float openSeconds = 0.9f;
        public bool startsOpen;
        public Transform interactPoint;
        public float range = 2.6f;

        public bool IsOpen { get; private set; }
        public bool Unlocked { get; private set; }

        Vector3 closedPos, openPos;
        Quaternion closedRot, openRot;
        float t;
        NavMeshObstacle obstacle;

        public Vector3 InteractPoint => interactPoint ? interactPoint.position : transform.position + Vector3.up * 1.2f;
        public float InteractRange => range;
        public bool IsAvailable => !IsOpen && isActiveAndEnabled;

        void Awake()
        {
            if (!panel) panel = transform;
            closedPos = panel.localPosition; closedRot = panel.localRotation;
            openPos = closedPos + openLocalOffset; openRot = closedRot * Quaternion.Euler(openLocalEuler);
            obstacle = panel.GetComponent<NavMeshObstacle>();
            if (!obstacle && panel.GetComponent<Collider>())
            {
                obstacle = panel.gameObject.AddComponent<NavMeshObstacle>();
                obstacle.carving = true;
                var b = panel.GetComponent<Collider>().bounds;
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.center = panel.InverseTransformPoint(b.center);
                var ls = panel.lossyScale;
                obstacle.size = new Vector3(b.size.x / Mathf.Max(0.001f, Mathf.Abs(ls.x)), b.size.y / Mathf.Max(0.001f, Mathf.Abs(ls.y)), b.size.z / Mathf.Max(0.001f, Mathf.Abs(ls.z)));
            }
            if (startsOpen) { IsOpen = true; t = 1f; Apply(); }
        }

        void OnEnable() => Interactables.Register(this);
        void OnDisable() => Interactables.Unregister(this);

        void Update()
        {
            float target = IsOpen ? 1f : 0f;
            if (!Mathf.Approximately(t, target)) { t = Mathf.MoveTowards(t, target, Time.deltaTime / Mathf.Max(0.05f, openSeconds)); Apply(); }
        }

        void Apply()
        {
            float e = t * t * (3f - 2f * t);
            panel.localPosition = Vector3.Lerp(closedPos, openPos, e);
            panel.localRotation = Quaternion.Slerp(closedRot, openRot, e);
            if (obstacle) obstacle.enabled = t < 0.5f;
        }

        public string GetPrompt(in InteractContext ctx)
        {
            if (terminalControlled && !Unlocked) return $"{doorName} — locked (staff terminal)";
            if (required == AccessPermission.None) return $"Open {doorName}";
            return $"Use access — {doorName}";
        }

        public bool CanInteract(in InteractContext ctx, out string deniedReason)
        {
            deniedReason = null;
            if (ctx.IsBlob) { deniedReason = "You can't work a door as a blob. Possess someone."; return false; }
            if (terminalControlled && !Unlocked) { deniedReason = "Locked. Unlock it from the Staff Terminal."; return false; }
            if (!AccessRules.Has(ctx.permissions, required))
            {
                deniedReason = required switch
                {
                    AccessPermission.SecurityGate => "ACCESS DENIED — security badge required (Guard).",
                    AccessPermission.ServiceTunnel => "ACCESS DENIED — maintenance keys required.",
                    AccessPermission.StaffTerminal => "ACCESS DENIED — staff credentials required (Scientist).",
                    _ => "ACCESS DENIED.",
                };
                return false;
            }
            return true;
        }

        public void Interact(in InteractContext ctx)
        {
            if (!CanInteract(ctx, out var reason))
            {
                GameEvents.RaiseToast(reason, Palette.Danger);
                Sfx.Play(SfxId.Denied, InteractPoint);
                // Only a host tripping a real lock is suspicious; blob pawing at a door is not a violation.
                if (!ctx.IsBlob && !(terminalControlled && !Unlocked))
                    SuspicionSystem.Instance?.ReportViolation(ViolationType.UnauthorizedAccess, InteractPoint, ctx.host);
                return;
            }
            Open();
            if (route != EntryRoute.None) MissionManager.Instance?.RegisterRouteUsed(route);
        }

        public void Unlock() { Unlocked = true; }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            Sfx.Play(SfxId.DoorOpen, InteractPoint);
            GameEvents.RaiseToast($"{doorName} opened", Palette.Info);
        }

        public void Close() { IsOpen = false; }

        /// <summary>For tests/autoplay.</summary>
        public void ForceOpenImmediate() { IsOpen = true; t = 1f; Apply(); }
    }
}
