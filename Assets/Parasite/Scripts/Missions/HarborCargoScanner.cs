using UnityEngine;
namespace Parasite.Missions
{
    public sealed class HarborCargoScanner : MonoBehaviour, IInteractable
    {
        public HarborCargoDirector director; public Renderer lamp; public float range = 2.3f; MaterialPropertyBlock block;
        public Vector3 InteractPoint => transform.position + Vector3.up * 1.1f;
        public float InteractRange => range;
        public bool IsAvailable => isActiveAndEnabled && director && !RunState.Has(HarborCampaign.ManifestComplete);
        public void Configure(HarborCargoDirector owner, Renderer statusLamp = null) { director = owner; lamp = statusLamp; }
        public string GetPrompt(in InteractContext ctx) => "Scan sealed lot — SCIENTIST · need " + HarborCampaign.RequiredCargoIds[0] + "/" + HarborCampaign.RequiredCargoIds[1] + " · " + (director ? director.StatusLine : "arch unavailable");
        public bool CanInteract(in InteractContext ctx, out string reason)
        { if (!IsAvailable) { reason = "The cargo scanner is unavailable or the manifest is complete."; return false; } return director.CanScan(ctx, out reason); }
        public void Interact(in InteractContext ctx)
        { if (!CanInteract(ctx, out var reason) || !director.TryScan(ctx, out reason)) GameEvents.RaiseToast(reason, Palette.Warning); }
        void OnEnable() => Interactables.Register(this);
        void OnDisable() => Interactables.Unregister(this);
        void Update()
        {
            if (!lamp) return; block ??= new MaterialPropertyBlock(); lamp.GetPropertyBlock(block);
            block.SetColor("_EmissionColor", (RunState.Has(HarborCampaign.ManifestComplete) ? Palette.Amoeba : director && director.CurrentId.Length > 0 ? Palette.Info : Palette.Warning) * 1.4f); lamp.SetPropertyBlock(block);
        }
    }
}
