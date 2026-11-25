using UnityEngine;
namespace Parasite.Missions
{
    public sealed class IslandSignalMount:MonoBehaviour,IInteractable
    {
        public Vector3 InteractPoint=>transform.position+Vector3.up;
        public float InteractRange=>2.3f;
        public bool IsAvailable=>isActiveAndEnabled&&!RunState.Has(IslandRoster.SignalMounted);
        public string GetPrompt(in InteractContext ctx)=>"Mount the carried rescue signal";
        public bool CanInteract(in InteractContext ctx,out string reason)
        {reason=null;if(!IslandSignalTransport.ReadyHuman(ctx)||!IslandSignalTransport.Instance||!IslandSignalTransport.Instance.HeldBy(ctx)){reason="Bring the physical signal case in its current carrier host.";return false;}return true;}
        public void Interact(in InteractContext ctx){if(!CanInteract(ctx,out var why)){GameEvents.RaiseToast(why,Palette.Warning);return;}if(IslandSignalTransport.Instance.Mount(ctx))GameEvents.RaiseToast("Signal mounted. Bring everyone aboard, then release every host and gather your free halves on the deck.",Palette.Info);}
        void OnEnable()=>Interactables.Register(this);
        void OnDisable()=>Interactables.Unregister(this);
    }
}
