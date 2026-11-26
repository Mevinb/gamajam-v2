using Parasite.Stealth;
using UnityEngine;

namespace Parasite.Missions
{
    /// <summary>Carry the chosen future onward only after every borrowed host has been released.</summary>
    public sealed class AfterlakeDeparture : MonoBehaviour, IInteractable
    {
        public Vector3 InteractPoint => transform.position + Vector3.up * 0.3f;
        public float InteractRange => 2.5f;
        public bool IsAvailable => isActiveAndEnabled && RunState.AfterlakeStarted && !RunState.Has(AfterlakeCampaign.Departed);
        public string GetPrompt(in InteractContext ctx) => "Board the ferry intake — all amoebae free";

        /// <summary>Shared departure/finalization guard: an inactive host cannot be abandoned still possessed.</summary>
        public static bool AllAmoebaeFree(out string deniedReason)
        {
            deniedReason = null;
            var pm = Possession.PossessionManager.Instance;
            if (!pm || pm.Active == null || pm.Agents.Count == 0)
            { deniedReason = "No amoeba is ready to board the ferry."; return false; }
            foreach (var agent in pm.Agents)
            {
                if (agent == null || agent.InTransit)
                { deniedReason = "Wait for possession leaps to finish before boarding."; return false; }
                if (agent.Host)
                { deniedReason = "Release EVERY host before boarding: [Tab] to switch amoebae, then [Q] to leave the host."; return false; }
                if (!agent.IsFree)
                { deniedReason = "An amoeba is missing. Use Restart checkpoint in the pause menu before boarding."; return false; }
            }
            return true;
        }
        public bool CanInteract(in InteractContext ctx, out string deniedReason)
        {
            deniedReason = null;
            if (!GameManager.Instance || !GameManager.Instance.IsPlaying || !IsAvailable) { deniedReason = "The ferry is unavailable."; return false; }
            if (!RunState.Has(AfterlakeCampaign.ChannelOpened)) { deniedReason = "Finish the shore radio transmission before departing."; return false; }
            if (!ctx.IsBlob) { deniedReason = "Release this host [Q]. If another host is occupied, switch with [Tab] and release it too."; return false; }
            if (!AllAmoebaeFree(out deniedReason)) return false;
            if (SuspicionSystem.Instance && SuspicionSystem.Instance.Level == SuspicionLevel.Pursuing) { deniedReason = "Lose the patrol before leading it to the ferry."; return false; }
            return true;
        }
        public void Interact(in InteractContext ctx)
        {
            if (!CanInteract(ctx, out var why)) { GameEvents.RaiseToast(why, Palette.Warning); return; }
            GameManager.Instance.FinishAfterlake();
        }
        void OnEnable() => Interactables.Register(this);
        void OnDisable() => Interactables.Unregister(this);
    }
}
