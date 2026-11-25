using UnityEngine;
namespace Parasite.Missions
{
    public sealed class HarborBrake : MonoBehaviour, IInteractable
    {
        public HarborCargoDirector director; public Renderer lamp; public float range = 2.3f; MaterialPropertyBlock block;
        public Vector3 InteractPoint => transform.position + Vector3.up * 1.1f;
        public float InteractRange => range;
        public bool IsAvailable => isActiveAndEnabled && director && !RunState.Has(HarborCampaign.ManifestComplete);
        public void Configure(HarborCargoDirector owner, Renderer statusLamp = null) { director = owner; lamp = statusLamp; }
        public string GetPrompt(in InteractContext ctx) => director && director.BrakeRemaining > 0 ? "Belt brake engaged · " + director.BrakeRemaining.ToString("0.0") + " s" : "Brake belt for five seconds — MAINTENANCE (optional)";
        public bool CanInteract(in InteractContext ctx, out string reason)
        { if (!IsAvailable) { reason = "The cargo brake is unavailable."; return false; } return director.CanBrake(ctx, out reason); }
        public void Interact(in InteractContext ctx)
        { if (!CanInteract(ctx, out var reason) || !director.TryBrake(ctx, out reason)) GameEvents.RaiseToast(reason, Palette.Warning); }
        void OnEnable() => Interactables.Register(this);
        void OnDisable() => Interactables.Unregister(this);
        void Update()
        { if (!lamp) return; block ??= new MaterialPropertyBlock(); lamp.GetPropertyBlock(block); block.SetColor("_EmissionColor", (director && director.BrakeRemaining > 0 ? Palette.Warning : Palette.Info) * 1.3f); lamp.SetPropertyBlock(block); }
    }
}
