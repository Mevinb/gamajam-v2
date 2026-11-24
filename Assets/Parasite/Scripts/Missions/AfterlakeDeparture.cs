using Parasite.Stealth;
using UnityEngine;

namespace Parasite.Missions
{
    /// <summary>Carry the chosen future onward, leaving the last borrowed host behind.</summary>
    public sealed class AfterlakeDeparture : MonoBehaviour, IInteractable
    {
        public Vector3 InteractPoint => transform.position + Vector3.up * 0.3f;
        public float InteractRange => 2.5f;
        public bool IsAvailable => isActiveAndEnabled && RunState.AfterlakeStarted && !RunState.Has(AfterlakeCampaign.Departed);
        public string GetPrompt(in InteractContext ctx) => "Board the ferry intake — free amoeba";
        public bool CanInteract(in InteractContext ctx, out string deniedReason)
        {
            deniedReason = null;
            if (!GameManager.Instance || !GameManager.Instance.IsPlaying || !IsAvailable) { deniedReason = "The ferry is unavailable."; return false; }
            if (!RunState.Has(AfterlakeCampaign.ChannelOpened)) { deniedReason = "Finish the shore radio transmission before departing."; return false; }
            if (!ctx.IsBlob) { deniedReason = "Release this host [Q]. Only a free amoeba fits the intake."; return false; }
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
