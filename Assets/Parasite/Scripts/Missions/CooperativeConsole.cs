using System.Collections.Generic;
using Parasite.FX;
using Parasite.Possession;
using UnityEngine;

namespace Parasite.Missions
{
    /// <summary>A second possessed body must remain physically at this control during a paired operation.</summary>
    public class CooperativeConsole : MonoBehaviour, IInteractable
    {
        public static readonly List<CooperativeConsole> All = new();
        public string controlId, displayName, requiredFlag, retireWhenFlag;
        public Profession profession;
        public Renderer lamp;
        public float range = 2.4f;
        AmoebaAgent operatorAgent;
        Characters.HostCharacter operatorHost;
        Material lampMaterial;

        public Vector3 InteractPoint => transform.position + Vector3.up * 1.2f;
        public float InteractRange => range;
        public bool IsAvailable => isActiveAndEnabled && !RunState.Has(retireWhenFlag);
        public bool Held
        {
            get
            {
                var pm = PossessionManager.Instance;
                return isActiveAndEnabled && pm && operatorAgent != null && pm.Agents.Contains(operatorAgent) &&
                    !operatorAgent.InTransit && operatorHost && operatorAgent.Host == operatorHost &&
                    operatorHost.State == HostState.Possessed && operatorHost.Profession == profession &&
                    Vector3.Distance(operatorAgent.Position, transform.position) <= range;
            }
        }
        public bool HeldByOther(AmoebaAgent agent) => Held && operatorAgent != agent;
        public static CooperativeConsole Find(string id) => All.Find(c => c && c.controlId == id);
        public string GetPrompt(in InteractContext ctx) => $"Hold {displayName} — {profession} (leave this body here, [Tab])";
        public bool CanInteract(in InteractContext ctx, out string deniedReason)
        {
            deniedReason = null;
            if (!RunState.Has(requiredFlag)) { deniedReason = "Follow the current mission first."; return false; }
            if (!ctx.host || ctx.host.State != HostState.Possessed || ctx.host.Profession != profession)
            { deniedReason = $"A {profession} host must hold this control."; return false; }
            return IsAvailable;
        }
        public void Interact(in InteractContext ctx)
        {
            if (!CanInteract(ctx, out var why)) { GameEvents.RaiseToast(why, Palette.Warning); return; }
            operatorAgent = ctx.agent; operatorHost = ctx.host;
            Sfx.Play(SfxId.TerminalBeep, InteractPoint);
            GameEvents.RaiseToast($"{displayName} held. Leave this body at the console and [Tab] to your other amoeba.", Palette.Info);
        }
        void OnEnable() { All.Add(this); Interactables.Register(this); }
        void OnDisable() { All.Remove(this); Interactables.Unregister(this); operatorAgent = null; }
        void Update()
        {
            if (!lamp) return;
            if (!lampMaterial) { lampMaterial = lamp.material; lampMaterial.EnableKeyword("_EMISSION"); }
            lampMaterial.SetColor("_EmissionColor", Held ? Palette.Amoeba * 2f : Palette.Warning * 0.7f);
        }
        void OnDestroy() { if (lampMaterial) Destroy(lampMaterial); }
    }
}
