using UnityEngine;
namespace Parasite.Missions
{
    public sealed class HarborValve : MonoBehaviour, IInteractable
    {
        public HarborPressureWorks works; public int slot; public Transform handle; public Renderer lamp; public float range = 2.3f;
        Quaternion rest; bool configured; MaterialPropertyBlock block;
        public Vector3 InteractPoint => transform.position + Vector3.up * 1.1f;
        public float InteractRange => range;
        public bool IsAvailable => isActiveAndEnabled && works && !RunState.Has(HarborCampaign.CanalOpen);
        public void Configure(HarborPressureWorks owner, int index, Transform wheel, Renderer statusLamp)
        { bool sameHandle = configured && handle == wheel; works = owner; slot = index; handle = wheel; lamp = statusLamp;
            if (!sameHandle) rest = handle ? handle.localRotation : Quaternion.identity; configured = true; Apply(); }
        void Start() { if (!configured) Configure(works ? works : HarborPressureWorks.Instance, slot, handle, lamp); }
        public string GetPrompt(in InteractContext ctx) => "Valve " + (slot == 0 ? "A (+3)" : slot == 1 ? "B (+2)" : "C bleed (-1)") + " — " + (works && works.IsValveOpen(slot) ? "close" : "open") + " · max 4";
        public bool CanInteract(in InteractContext ctx, out string reason)
        { if (!IsAvailable) { reason = "The lock is secured or this valve is unavailable."; return false; } return works.CanToggle(ctx, slot, out reason); }
        public void Interact(in InteractContext ctx)
        { if (!CanInteract(ctx, out var reason) || !works.TryToggle(ctx, slot, out reason)) { GameEvents.RaiseToast(reason, Palette.Warning); return; } }
        void OnEnable() => Interactables.Register(this);
        void OnDisable() => Interactables.Unregister(this);
        void Update() => Apply();
        void Apply()
        {
            bool open = works && works.IsValveOpen(slot);
            if (handle) handle.localRotation = rest * Quaternion.Euler(0, 0, open ? 90 : 0);
            if (lamp) { block ??= new MaterialPropertyBlock(); lamp.GetPropertyBlock(block); block.SetColor("_EmissionColor", (open ? Palette.Amoeba : Palette.Warning) * 1.3f); lamp.SetPropertyBlock(block); }
        }
    }
}
