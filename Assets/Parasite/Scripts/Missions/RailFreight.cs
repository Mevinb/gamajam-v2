using System.Collections.Generic;
using UnityEngine;

namespace Parasite.Missions
{
    /// <summary>Actual mechanical point movement and one stable sealed bogie. Time alone cannot grant bay arrival.</summary>
    public sealed class RailFreight : MonoBehaviour
    {
        public static RailFreight Instance { get; private set; }
        public const float Speed = 3.2f, PointSeconds = 1f;
        public Rigidbody carriage; public BoxCollider carriageCollider; public Transform[] blades;
        public bool Configured { get; private set; }
        public bool ARight { get; private set; }
        public bool BRight { get; private set; }
        public bool Moving { get; private set; }
        public bool Returning { get; private set; }
        public bool PointsCorrect => ARight == RailCampaign.RequiredA && BRight == RailCampaign.RequiredB;
        public bool PointsSettled => Configured && blades != null && blades.Length == 2 && blades[0] && blades[1]
            && Mathf.Abs(aAngle - (ARight ? 25 : -25)) < .01f && Mathf.Abs(bAngle - (BRight ? 25 : -25)) < .01f
            && Quaternion.Angle(blades[0].localRotation, rest[0] * Quaternion.Euler(0, ARight ? 25 : -25, 0)) < .02f
            && Quaternion.Angle(blades[1].localRotation, rest[1] * Quaternion.Euler(0, BRight ? 25 : -25, 0)) < .02f;
        public bool Physical => Configured && carriage && carriage.gameObject.activeInHierarchy && carriage.isKinematic && carriage.detectCollisions
            && carriageCollider && carriageCollider.enabled && !carriageCollider.isTrigger && blades != null && blades.Length == 2 && blades[0] && blades[1];
        public bool AtLoading => Physical && !Moving && Vector3.Distance(carriage.position, RailLayout.FreightDock) < .025f
            && Vector3.Distance(carriage.transform.position, RailLayout.FreightDock) < .025f;
        public int FailedRoutes { get; private set; }
        public string StatusLine => RunState.Has(RailCampaign.FreightArrived) ? RailCampaign.Consignment + " physically delivered"
            : Moving ? Returning ? "wrong route · carriage returning to loading" : "sealed carriage moving · arrival not yet earned"
            : "A " + (ARight ? "RIGHT" : "LEFT") + " · B " + (BRight ? "RIGHT" : "LEFT") + (PointsSettled ? " · points settled" : " · blades moving");
        float aAngle, bAngle; Quaternion[] rest; readonly List<Vector3> route = new(); int leg; bool validDispatch;
        void Awake() => Instance = this;
        public void Configure(Rigidbody actualCarriage, Transform[] actualBlades)
        {
            if (!actualCarriage || !actualCarriage.isKinematic || !actualCarriage.detectCollisions || !actualCarriage.GetComponent<BoxCollider>() || actualBlades == null || actualBlades.Length != 2 || !actualBlades[0] || !actualBlades[1])
                throw new System.ArgumentException("Northline freight needs a colliding kinematic carriage and two actual point blades.");
            carriage = actualCarriage; carriageCollider = carriage.GetComponent<BoxCollider>(); blades = actualBlades;
            rest = new[] { blades[0].localRotation, blades[1].localRotation };
            ARight = RunState.Has(RailCampaign.PointA); BRight = RunState.Has(RailCampaign.PointB);
            aAngle = ARight ? 25 : -25; bAngle = BRight ? 25 : -25;
            // Only earned arrival is restored at a bay. A checkpoint abandons transient travel at the dock.
            Vector3 restored = RunState.Has(RailCampaign.FreightArrived) ? RailLayout.RequiredBay : RailLayout.FreightDock;
            carriage.position = restored; carriage.transform.position = restored; Moving = Returning = false; route.Clear(); leg = 0;
            Configured = true; ApplyBlades();
        }
        public bool CanValidate(in InteractContext ctx, RailWorkControl control, out string reason)
        {
            reason = null;
            if (!Configured || !isActiveAndEnabled || !RunState.Has(RailCampaign.CrossingPassed)) { reason = "Secure the actual crossing first."; return false; }
            if (RunState.Has(RailCampaign.Validated)) { reason = "This sealed consignment is already validated."; return false; }
            if (!AtLoading) { reason = "The actual sealed carriage must be stationary at the loading dock."; return false; }
            if (!RailCampaign.ReadyHost(ctx, Profession.Scientist, control.InteractPoint, control.transform, out reason)) return false;
            Vector3 seal = carriage.position + Vector3.up * 1.1f;
            if (Vector3.Distance(ctx.host.EyePosition, seal) > 11 || !RailCampaign.ClearLine(ctx.host.EyePosition, seal, ctx.host.transform, carriage.transform))
            { reason = "The real carriage seal must be visible from the manifest desk."; return false; }
            return true;
        }
        public bool TryValidate(in InteractContext ctx, RailWorkControl control, out string reason)
        {
            if (!CanValidate(ctx, control, out reason)) return false;
            MissionManager.SetFlag(RailCampaign.Validated);
            GameEvents.RaiseToast(RailCampaign.Consignment + " seal verified. A maintenance operator must set both physical points and dispatch.", Palette.Info); return true;
        }
        public bool CanThrow(in InteractContext ctx, RailWorkControl control, int index, out string reason)
        {
            reason = null;
            if (!Physical || !isActiveAndEnabled || index < 0 || index > 1 || !RunState.Has(RailCampaign.CrossingPassed) || RunState.Has(RailCampaign.FreightArrived))
            { reason = "These points are unavailable or the consignment has arrived."; return false; }
            if (Moving || !PointsSettled) { reason = "Points are interlocked while blades or a carriage are moving."; return false; }
            return RailCampaign.ReadyHost(ctx, Profession.Maintenance, control.InteractPoint, control.transform, out reason);
        }
        public bool TryThrow(in InteractContext ctx, RailWorkControl control, int index, out string reason)
        {
            if (!CanThrow(ctx, control, index, out reason)) return false;
            if (index == 0) ARight = !ARight; else BRight = !BRight;
            bool value = index == 0 ? ARight : BRight; string flag = index == 0 ? RailCampaign.PointA : RailCampaign.PointB;
            if (value) MissionManager.SetFlag(flag); else RunState.Flags.Remove(flag);
            GameEvents.RaiseToast("Point " + (index == 0 ? "A" : "B") + (value ? " throwing RIGHT" : " throwing LEFT") + ". Wait for the physical blade.", Palette.Info); return true;
        }
        public bool CanDispatch(in InteractContext ctx, RailWorkControl control, out string reason)
        {
            reason = null;
            if (!Configured || !isActiveAndEnabled || !RunState.Has(RailCampaign.Validated) || RunState.Has(RailCampaign.FreightArrived))
            { reason = "A scientist must validate the sealed docked carriage before dispatch."; return false; }
            if (!AtLoading || !PointsSettled) { reason = "Wait for the actual carriage at loading and both point blades to settle."; return false; }
            return RailCampaign.ReadyHost(ctx, Profession.Maintenance, control.InteractPoint, control.transform, out reason);
        }
        public bool TryDispatch(in InteractContext ctx, RailWorkControl control, out string reason)
        {
            if (!CanDispatch(ctx, control, out reason)) return false;
            validDispatch = PointsCorrect; Returning = false; Moving = true; leg = 0; route.Clear();
            float firstZ = ARight ? 18 : 8, finalZ = BRight ? 8 : 18;
            route.Add(RailLayout.At(-7, .15f, 6)); route.Add(RailLayout.At(-7, .15f, firstZ));
            route.Add(RailLayout.At(11, .15f, firstZ)); route.Add(RailLayout.At(11, .15f, finalZ)); route.Add(RailLayout.At(28, .15f, finalZ));
            if (!validDispatch)
            {
                // A wrong routing has an observable full journey and return, never an instant flag or reposition.
                route.Add(RailLayout.At(28, .15f, RailLayout.FreightReturnZ)); route.Add(RailLayout.At(-22, .15f, RailLayout.FreightReturnZ)); route.Add(RailLayout.FreightDock);
            }
            GameEvents.RaiseToast("Carriage dispatched on the selected points. Watch the physical route.", Palette.Info); return true;
        }
        void Update()
        {
            if (!Physical || !GameManager.Instance || !GameManager.Instance.IsPlaying) return;
            aAngle = Mathf.MoveTowards(aAngle, ARight ? 25 : -25, Time.deltaTime * 50 / PointSeconds);
            bAngle = Mathf.MoveTowards(bAngle, BRight ? 25 : -25, Time.deltaTime * 50 / PointSeconds); ApplyBlades();
        }
        void FixedUpdate()
        {
            if (!Physical || !Moving || !RailCampaign.Active || !GameManager.Instance || !GameManager.Instance.IsPlaying) return;
            Vector3 target = route[leg];
            if (Vector3.Distance(carriage.position, target) <= .015f && Vector3.Distance(carriage.transform.position, target) <= .015f)
            {
                if (!validDispatch && leg == 4 && !Returning)
                { Returning = true; FailedRoutes++; GameEvents.RaiseToast("That was the wrong routing for " + RailCampaign.Consignment + ". The same carriage is returning; correct both points after it docks.", Palette.Warning); }
                if (leg < route.Count - 1) { leg++; target = route[leg]; }
                else
                {
                    Moving = false;
                    if (validDispatch && PointsCorrect && RunState.Has(RailCampaign.Validated) && Vector3.Distance(carriage.position, RailLayout.RequiredBay) <= .025f)
                        MissionManager.SetFlag(RailCampaign.FreightArrived);
                    else { Returning = false; GameEvents.RaiseToast("Carriage back at loading. Its seal remains validated; set the correct physical points and dispatch again.", Palette.Info); }
                    return;
                }
            }
            MovePhysical(carriage, Vector3.MoveTowards(carriage.position, target, Speed * Time.fixedDeltaTime));
        }
        void ApplyBlades()
        { if (blades == null) return; blades[0].localRotation = rest[0] * Quaternion.Euler(0, aAngle, 0); blades[1].localRotation = rest[1] * Quaternion.Euler(0, bAngle, 0); }
        public static bool MovePhysical(Rigidbody body, Vector3 destination)
        {
            if (!body || !body.isKinematic || !body.detectCollisions || !body.gameObject.activeInHierarchy) return false;
            var collider = body.GetComponent<Collider>(); if (!collider || !collider.enabled || collider.isTrigger) return false;
            Vector3 delta = destination - body.position; float distance = delta.magnitude;
            if (distance <= .00001f) return true;
            foreach (var hit in body.SweepTestAll(delta / distance, distance + .005f, QueryTriggerInteraction.Ignore))
                if (hit.collider && !hit.transform.IsChildOf(body.transform) && hit.distance <= distance + .005f && Vector3.Dot(hit.normal, delta / distance) < -.1f) return false;
            body.MovePosition(destination); return true;
        }
        void OnDestroy() { Moving = false; route.Clear(); if (Instance == this) Instance = null; }
    }
}
