using System;
using Parasite.Possession;
using Parasite.Stealth;
using UnityEngine;

namespace Parasite.Missions
{
    /// <summary>A fresh three-second link between two distinct, grounded free bodies; every condition is rechecked continuously.</summary>
    public sealed class HarborRelay : MonoBehaviour
    {
        public static HarborRelay Instance { get; private set; }
        public const float HandshakeSeconds = 3f, MovementTolerance = .025f, PadContainmentTolerance = .002f;
        public Bounds padA, padB; public Transform[] sightPoints = Array.Empty<Transform>();
        public bool Configured { get; private set; }
        public bool Running { get; private set; }
        public float Progress { get; private set; }
        public string LastCancellation { get; private set; } = "";
        public bool Ready => Conditions(out _, out _, out _);
        public string StatusLine => RunState.Has(HarborCampaign.RelayComplete) ? "relay linked" : Running ? "hold still · " + Progress.ToString("0.0") + " / 3.0 s" :
            Conditions(out _, out _, out string why) ? "two free signals ready · press E" : why;
        public event Action Completed;
        AmoebaAgent first, second; Vector3 firstPosition, secondPosition;
        readonly RaycastHit[] hits = new RaycastHit[32];
        void Awake() => Instance = this;
        public void Configure(Bounds firstPad, Bounds secondPad, Transform[] emitterSightPoints)
        {
            if (firstPad.size.x <= 0 || firstPad.size.y <= 0 || firstPad.size.z <= 0 || secondPad.size.x <= 0 || secondPad.size.y <= 0 || secondPad.size.z <= 0
                || firstPad.Intersects(secondPad) || emitterSightPoints == null || emitterSightPoints.Length != 2 || !emitterSightPoints[0] || !emitterSightPoints[1])
                throw new ArgumentException("Harbor relay requires two separate physical pad volumes and two actual emitter sight points.");
            padA = firstPad; padB = secondPad; sightPoints = (Transform[])emitterSightPoints.Clone();
            Configured = true; Running = false; Progress = RunState.Has(HarborCampaign.RelayComplete) ? HandshakeSeconds : 0; first = second = null;
        }
        void Start() { if (!Configured && sightPoints.Length == 2) Configure(padA, padB, sightPoints); }
        public bool CanBegin(in InteractContext ctx, out string reason)
        {
            reason = null;
            if (RunState.Has(HarborCampaign.RelayComplete)) { reason = "The relay is already linked."; return false; }
            if (Running) { reason = "The handshake is running. Keep both free bodies still."; return false; }
            if (!GameManager.Instance || !GameManager.Instance.IsPlaying) { reason = "Resume play before starting the relay."; return false; }
            if (!Conditions(out var a, out var b, out reason)) return false;
            if (ctx.agent == null || ctx.host || (ctx.agent != a && ctx.agent != b)) { reason = "Use a relay node as one of the two free amoebae on the pads."; return false; }
            Vector3 use = ctx.agent == a ? padA.center : padB.center; Vector3 d = ctx.agent.Position - use;
            if (Mathf.Abs(d.y) > 2.6f || new Vector2(d.x, d.z).magnitude > 2.3f) { reason = "Move the free body within reach of its relay node."; return false; }
            return true;
        }
        public bool TryBegin(in InteractContext ctx, out string reason)
        {
            if (!CanBegin(ctx, out reason)) return false;
            if (!Conditions(out first, out second, out reason)) return false;
            firstPosition = first.Position; secondPosition = second.Position; Progress = 0; LastCancellation = ""; Running = true;
            GameEvents.RaiseToast("Two free signals detected. Keep both bodies still with clear beams for three seconds.", Palette.Info); return true;
        }
        bool Conditions(out AmoebaAgent a, out AmoebaAgent b, out string reason)
        {
            a = b = null; reason = null;
            if (!Configured || !HarborCampaign.Active) { reason = "The harbor relay is unavailable."; return false; }
            if (!RunState.Has(HarborCampaign.ManifestComplete) || !HarborCampaign.RequiredCargoDelivered) { reason = "Both required cargo lots must physically arrive first."; return false; }
            var pm = PossessionManager.Instance;
            if (!pm || pm.Agents.Count != 2 || pm.Agents[0] == pm.Agents[1] || pm.Agents[0] == null || pm.Agents[1] == null || pm.Agents[0].Id == pm.Agents[1].Id)
            { reason = "Two distinct free amoebae are required. Split with V; the two-body limit remains."; return false; }
            foreach (var agent in pm.Agents)
            {
                if (!Free(agent)) { reason = "Release both hosts and wait for captures or possession leaps to finish."; return false; }
                if (FullyInside(agent, padA)) { if (a != null) { reason = "Place one free amoeba on each pad."; return false; } a = agent; }
                else if (FullyInside(agent, padB)) { if (b != null) { reason = "Place one free amoeba on each pad."; return false; } b = agent; }
                else { reason = "Both grounded free bodies must fit fully on separate pads."; return false; }
            }
            if (a == null || b == null) { reason = "Place one free amoeba on each pad."; return false; }
            if (SuspicionSystem.Instance && SuspicionSystem.Instance.Level == SuspicionLevel.Pursuing) { reason = "Lose pursuit before linking the harbor relay."; return false; }
            if (!LineClear(a, 0) || !LineClear(b, 1)) { reason = "An emitter beam is obstructed. Clear both physical sight lines."; return false; }
            return true;
        }
        static bool Free(AmoebaAgent agent) => agent != null && agent.IsFree && !agent.CapturedPendingEject && agent.Blob && agent.Blob.isActiveAndEnabled
            && agent.Blob.gameObject.activeInHierarchy && agent.Blob.Controller && agent.Blob.Controller.enabled;
        static bool FullyInside(AmoebaAgent agent, Bounds pad)
        {
            var controller = agent.Blob.Controller; var body = controller.bounds;
            // Two millimetres cover floating-point/contact rounding at the coplanar floor, not a partial-body pad overlap.
            pad.Expand(PadContainmentTolerance * 2);
            return controller.isGrounded && pad.Contains(body.min) && pad.Contains(body.max);
        }
        bool LineClear(AmoebaAgent agent, int index)
        {
            if (index >= sightPoints.Length || !sightPoints[index]) return false;
            var emitter = sightPoints[index]; var allowedCollider = emitter.GetComponent<Collider>();
            if (!allowedCollider) allowedCollider = emitter.GetComponentInParent<Collider>();
            Vector3 start = agent.Blob.Center, d = emitter.position - start; float distance = d.magnitude;
            if (distance < .01f) return true;
            int count = Physics.RaycastNonAlloc(start, d / distance, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            if (count >= hits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i]; if (!hit.collider || hit.transform.IsChildOf(agent.Blob.transform)) continue;
                if (hit.collider == allowedCollider || hit.transform.IsChildOf(emitter)) continue;
                return false;
            }
            return true;
        }
        void Update()
        {
            if (!Running || !GameManager.Instance || !GameManager.Instance.IsPlaying) return;
            if (!Conditions(out var a, out var b, out string why)) { Cancel(why); return; }
            if (a != first || b != second || (a.Position - firstPosition).sqrMagnitude > MovementTolerance * MovementTolerance || (b.Position - secondPosition).sqrMagnitude > MovementTolerance * MovementTolerance)
            { Cancel("A body moved or changed pads. Settle both free bodies and press E to retry."); return; }
            Progress = Mathf.Min(HandshakeSeconds, Progress + Time.deltaTime);
            if (Progress < HandshakeSeconds) return;
            // Ready deliberately remains a physical condition after the flag, so GameManager can revalidate the completion event.
            Running = false; MissionManager.SetFlag(HarborCampaign.RelayComplete); Completed?.Invoke();
        }
        void Cancel(string reason)
        { Running = false; Progress = 0; first = second = null; LastCancellation = reason; GameEvents.RaiseToast("Relay interrupted. " + reason, Palette.Warning); }
        void OnDisable() { Running = false; if (!RunState.Has(HarborCampaign.RelayComplete)) Progress = 0; first = second = null; }
        void OnDestroy() { Completed = null; if (Instance == this) Instance = null; }
    }
}
