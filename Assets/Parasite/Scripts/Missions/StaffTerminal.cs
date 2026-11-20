using Parasite.FX;
using Parasite.Stealth;
using UnityEngine;

namespace Parasite.Missions
{
    /// <summary>Scientist-only terminal that unlocks and opens the linked staff door.</summary>
    public class StaffTerminal : MonoBehaviour, IInteractable
    {
        public string terminalName = "Staff Terminal";
        public AccessDoor linkedDoor;
        public Renderer screen;
        public float range = 2.4f;
        public bool Used { get; private set; }

        public Vector3 InteractPoint => transform.position + Vector3.up * 1.2f;
        public float InteractRange => range;
        public bool IsAvailable => !Used && isActiveAndEnabled;

        void OnEnable() => Interactables.Register(this);
        void OnDisable() => Interactables.Unregister(this);

        void Start() => SetScreen(new Color(1f, 0.25f, 0.15f) * 2.5f);

        void SetScreen(Color c)
        {
            if (!screen) return;
            var m = screen.material;
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c);
        }

        public string GetPrompt(in InteractContext ctx) => $"Log in — {terminalName}";

        public bool CanInteract(in InteractContext ctx, out string deniedReason)
        {
            deniedReason = null;
            if (ctx.IsBlob) { deniedReason = "No fingers. Possess a scientist to log in."; return false; }
            if (!AccessRules.Has(ctx.permissions, AccessPermission.StaffTerminal))
            { deniedReason = "LOGIN FAILED — staff credentials required (Scientist)."; return false; }
            return true;
        }

        public void Interact(in InteractContext ctx)
        {
            if (!CanInteract(ctx, out var reason))
            {
                GameEvents.RaiseToast(reason, Palette.Danger);
                Sfx.Play(SfxId.Denied, InteractPoint);
                if (!ctx.IsBlob) SuspicionSystem.Instance?.ReportViolation(ViolationType.UnauthorizedAccess, InteractPoint, ctx.host);
                return;
            }
            Used = true;
            Sfx.Play(SfxId.TerminalBeep, InteractPoint);
            SetScreen(new Color(0.2f, 1f, 0.3f) * 2.5f);
            GameEvents.RaiseToast($"Credentials accepted — {(linkedDoor ? linkedDoor.doorName : "door")} unlocked", Palette.Amoeba);
            if (linkedDoor) { linkedDoor.Unlock(); linkedDoor.Open(); }
            MissionManager.Instance?.RegisterRouteUsed(EntryRoute.StaffTerminal);
        }
    }
}
