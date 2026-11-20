using Parasite.FX;
using Parasite.Stealth;
using UnityEngine;

namespace Parasite.Missions
{
    /// <summary>The final exit only accepts the exposed, free amoeba after evidence reaches the outside world.</summary>
    public class CampaignExtraction : MonoBehaviour, IInteractable
    {
        public Vector3 InteractPoint => transform.position + Vector3.up * 0.35f;
        public float InteractRange => 2.3f;
        public bool IsAvailable => isActiveAndEnabled && MissionManager.Instance && !MissionManager.Instance.EndingStarted;
        public string GetPrompt(in InteractContext ctx) => "Escape through the storm drain — free amoeba";
        public bool CanInteract(in InteractContext ctx, out string deniedReason)
        {
            deniedReason = null;
            if (!RunState.Has(MissionFlags.EvidenceBroadcast)) { deniedReason = "The evidence must reach the journalist before you disappear."; return false; }
            if (!ctx.IsBlob) { deniedReason = "Leave your host [Q]. Only a free amoeba fits through the drain."; return false; }
            var ss = SuspicionSystem.Instance;
            if (ss && ss.Level == SuspicionLevel.Pursuing) { deniedReason = "Break line of sight and lose the pursuit before escaping."; return false; }
            return true;
        }
        public void Interact(in InteractContext ctx)
        {
            if (!CanInteract(ctx, out var why)) { GameEvents.RaiseToast(why, Palette.Warning); return; }
            MissionManager.Instance.CompleteEscape(transform);
        }
        void OnEnable() => Interactables.Register(this);
        void OnDisable() => Interactables.Unregister(this);
    }
}
