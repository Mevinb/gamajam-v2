using Parasite.FX;
using Parasite.Possession;
using Parasite.Stealth;
using UnityEngine;

namespace Parasite.Missions
{
    /// <summary>Interruptible mission work: preserve the operator, cooperative control and safe broadcast conditions.</summary>
    public class CampaignOperation : MonoBehaviour, IInteractable
    {
        public static CampaignOperation Current { get; private set; }
        public string flag, displayName, requiredFlag, controlId, doneToast;
        public Profession profession;
        public bool anyHost, requireCalm;
        public float seconds = 7f, range = 2.4f;
        public Renderer screen;
        public Vector3 InteractPoint => transform.position + Vector3.up * 1.2f;
        public float InteractRange => range;
        public bool Busy => operatorAgent != null;
        public float Progress01 => Mathf.Clamp01(elapsed / Mathf.Max(0.1f, seconds));
        public bool IsAvailable => isActiveAndEnabled && !Busy && !RunState.Has(flag);
        AmoebaAgent operatorAgent;
        Characters.HostCharacter operatorHost;
        float elapsed;
        Material screenMaterial;

        public string GetPrompt(in InteractContext ctx) => $"{displayName} — {seconds:0}s";
        public bool CanInteract(in InteractContext ctx, out string deniedReason)
        {
            deniedReason = null;
            if (!RunState.Has(requiredFlag)) { deniedReason = "Not yet — complete the current mission first."; return false; }
            if (!ctx.host || ctx.host.State != HostState.Possessed || (!anyHost && ctx.host.Profession != profession))
            { deniedReason = anyHost ? "You need a human host to operate this terminal." : $"This operation needs a {profession} host."; return false; }
            if (!string.IsNullOrEmpty(controlId))
            {
                var control = CooperativeConsole.Find(controlId);
                if (!control || !control.HeldByOther(ctx.agent))
                { deniedReason = "Your OTHER amoeba must hold the marked support console. Keep that body there and [Tab] back."; return false; }
            }
            var ss = SuspicionSystem.Instance;
            if (requireCalm && ss && ss.Level == SuspicionLevel.Pursuing)
            { deniedReason = "Lose the pursuit before exposing your contact's uplink."; return false; }
            return true;
        }
        public void Interact(in InteractContext ctx)
        {
            if (!IsAvailable) { GameEvents.RaiseToast("Operation already in progress or complete.", Palette.Warning); return; }
            if (!CanInteract(ctx, out var why)) { GameEvents.RaiseToast(why, Palette.Warning); return; }
            operatorAgent = ctx.agent; operatorHost = ctx.host; elapsed = 0f; Current = this;
            Sfx.Play(SfxId.TerminalBeep, InteractPoint);
            string instruction = string.IsNullOrEmpty(controlId) ? "Stay at the terminal and avoid pursuit." : "Both bodies must remain in position.";
            GameEvents.RaiseToast($"{displayName}… {instruction}", Palette.Info);
        }
        void OnEnable() => Interactables.Register(this);
        void OnDisable() { Interactables.Unregister(this); StopWork(); }
        void StopWork() { operatorAgent = null; operatorHost = null; elapsed = 0f; if (Current == this) Current = null; }
        void Update()
        {
            if (screen)
            {
                if (!screenMaterial) { screenMaterial = screen.material; screenMaterial.EnableKeyword("_EMISSION"); }
                screenMaterial.SetColor("_EmissionColor", RunState.Has(flag) ? Palette.Amoeba * 1.5f : Busy ? Palette.Warning * 1.7f : Palette.Info * 0.8f);
            }
            if (!Busy) return;
            var pm = PossessionManager.Instance;
            var ctx = new InteractContext { agent = operatorAgent, host = operatorHost, permissions = operatorHost ? operatorHost.Permissions : AccessPermission.None, position = operatorAgent.Position };
            if (!pm || !pm.Agents.Contains(operatorAgent) || operatorAgent.InTransit || !operatorHost || operatorAgent.Host != operatorHost ||
                Vector3.Distance(operatorAgent.Position, transform.position) > range || !CanInteract(ctx, out _))
            {
                StopWork();
                string instruction = string.IsNullOrEmpty(controlId) ? "Return to the terminal, lose any pursuit and try again." : "Restore both positions and try again.";
                GameEvents.RaiseToast($"{displayName}: interrupted. {instruction}", Palette.Warning);
                return;
            }
            elapsed += Time.deltaTime;
            if (elapsed < seconds) return;
            StopWork();
            MissionManager.SetFlag(flag);
            Sfx.Play(SfxId.TerminalBeep, InteractPoint);
            GameEvents.RaiseToast(doneToast, Palette.Amoeba);
        }
        void OnDestroy() { if (screenMaterial) Destroy(screenMaterial); }
    }
}
