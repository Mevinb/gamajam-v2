using UnityEngine;
namespace Parasite.Missions
{
    public sealed class IslandSignalPickup:MonoBehaviour,IInteractable
    {
        public Vector3 InteractPoint=>transform.position+Vector3.up*.82f;
        public float InteractRange=>2.3f;
        public bool IsAvailable=>isActiveAndEnabled&&RunState.Has(IslandRoster.Ready)&&!RunState.Has(IslandRoster.SignalMounted)&&IslandSignalTransport.Instance&&!IslandSignalTransport.Instance.Held;
        public string GetPrompt(in InteractContext ctx)=>"Take the physical signal case — human host";
        public bool CanInteract(in InteractContext ctx,out string reason)
        {reason=null;if(!IsAvailable){reason="Finish the three survivor/courier routes first.";return false;}if(!IslandSignalTransport.ReadyHuman(ctx)){reason="A human host must carry this case.";return false;}return true;}
        public void Interact(in InteractContext ctx){if(!CanInteract(ctx,out var why)){GameEvents.RaiseToast(why,Palette.Warning);return;}if(IslandSignalTransport.Instance.PickUp(ctx))GameEvents.RaiseToast("Carry the case to the northern flare mount. Releasing or changing this host returns it to the safe cache.",Palette.Info);}
        void OnEnable()=>Interactables.Register(this);
        void OnDisable()=>Interactables.Unregister(this);
    }
}
