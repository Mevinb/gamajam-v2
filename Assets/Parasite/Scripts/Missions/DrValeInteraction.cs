using UnityEngine;

namespace Parasite.Missions
{
    /// <summary>On Dr. Vale. Any form (blob or host) can confront him once the research is stolen and his office is open.</summary>
    public class DrValeInteraction : MonoBehaviour, IInteractable
    {
        public float range = 3.6f;
        public Vector3 InteractPoint => transform.position + Vector3.up * 1.5f;
        public float InteractRange => range;
        public bool IsAvailable => MissionManager.Instance != null && !MissionManager.Instance.EndingStarted;
        void OnEnable() => Interactables.Register(this);
        void OnDisable() => Interactables.Unregister(this);
        public string GetPrompt(in InteractContext ctx) => "Confront Dr. Vale";

        public bool CanInteract(in InteractContext ctx, out string deniedReason)
        {
            deniedReason = null;
            if (RunState.Has(MissionFlags.OfficeOpen) || RunState.Has(MissionFlags.Research)) return true;
            deniedReason = "Not yet — first steal his research from Lab A and Lab B.";
            return false;
        }

        public void Interact(in InteractContext ctx)
        {
            if (!CanInteract(ctx, out var r)) { GameEvents.RaiseToast(r, Palette.Warning); return; }
            MissionManager.Instance?.BeginEnding(transform);
        }
    }
}
