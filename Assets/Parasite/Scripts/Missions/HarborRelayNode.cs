using UnityEngine;
namespace Parasite.Missions
{
    public sealed class HarborRelayNode : MonoBehaviour, IInteractable
    {
        public HarborRelay relay; public Renderer lamp; public float range = 2.3f; MaterialPropertyBlock block;
        public Vector3 InteractPoint => transform.position + Vector3.up * .8f;
        public float InteractRange => range;
        public bool IsAvailable => isActiveAndEnabled && relay && !RunState.Has(HarborCampaign.RelayComplete);
        public void Configure(HarborRelay owner, Renderer statusLamp = null) { relay = owner; lamp = statusLamp; }
        public string GetPrompt(in InteractContext ctx) => "Link harbor relay — two FREE bodies · " + (relay ? relay.StatusLine : "unavailable");
        public bool CanInteract(in InteractContext ctx, out string reason)
        {
            if (!IsAvailable) { reason = "The harbor relay is unavailable or already linked."; return false; }
            var possession = Possession.PossessionManager.Instance;
            if (!possession || ctx.agent == null || possession.Active != ctx.agent || !possession.Agents.Contains(ctx.agent))
            { reason = "Use this node as the active registered amoeba."; return false; }
            Vector3 delta = ctx.agent.Position - InteractPoint;
            if (Mathf.Abs(delta.y) > 2.6f || new Vector2(delta.x, delta.z).magnitude > InteractRange)
            { reason = "Move the active free body within reach of this relay node."; return false; }
            return relay.CanBegin(ctx, out reason);
        }
        public void Interact(in InteractContext ctx)
        { if (!CanInteract(ctx, out var reason) || !relay.TryBegin(ctx, out reason)) GameEvents.RaiseToast(reason, Palette.Warning); }
        void OnEnable() => Interactables.Register(this);
        void OnDisable() => Interactables.Unregister(this);
        void Update()
        {
            if (!lamp) return; block ??= new MaterialPropertyBlock(); lamp.GetPropertyBlock(block);
            Color color = RunState.Has(HarborCampaign.RelayComplete) ? Palette.Amoeba : relay && relay.Running ? Palette.Info : Palette.Warning;
            block.SetColor("_EmissionColor", color * 1.5f); lamp.SetPropertyBlock(block);
        }
    }
}
