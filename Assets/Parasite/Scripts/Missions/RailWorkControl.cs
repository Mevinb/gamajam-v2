using UnityEngine;

namespace Parasite.Missions
{
    public enum RailWork { Crossing, CrossingReturn, Manifest, PointA, PointB, Dispatch, Crank, Signal, EmergencyReturn, Departure }

    public sealed class RailWorkControl : MonoBehaviour, IInteractable
    {
        public RailWork kind;
        public Vector3 InteractPoint => transform.position + Vector3.up * (kind == RailWork.Signal || kind == RailWork.Departure || kind == RailWork.EmergencyReturn ? .8f : 1.1f);
        public float InteractRange => 2.3f;
        public bool IsAvailable
        {
            get
            {
                if (!isActiveAndEnabled || !RailCampaign.Active || RunState.Has(RailCampaign.Departed)) return false;
                switch (kind)
                {
                    case RailWork.Crossing: case RailWork.CrossingReturn: return !RunState.Has(RailCampaign.CrossingPassed);
                    case RailWork.Manifest: return !RunState.Has(RailCampaign.Validated);
                    case RailWork.PointA: case RailWork.PointB: case RailWork.Dispatch: return !RunState.Has(RailCampaign.FreightArrived);
                    case RailWork.Crank: case RailWork.Signal: case RailWork.EmergencyReturn: return !RunState.Has(RailCampaign.SignalReleased);
                    case RailWork.Departure: return RunState.Has(RailCampaign.SignalReleased);
                    default: return false;
                }
            }
        }
        public string GetPrompt(in InteractContext ctx)
        {
            switch (kind)
            {
                case RailWork.CrossingReturn: return "Request safe return SOUTH — MAINTENANCE · no mission credit";
                case RailWork.Crossing: return "Request crossing — MAINTENANCE · " + (RailCrossing.Instance ? RailCrossing.Instance.StatusLine : "unavailable");
                case RailWork.Manifest: return "Validate docked seal — SCIENTIST · " + RailCampaign.Consignment;
                case RailWork.PointA: case RailWork.PointB: return "Throw point " + (kind == RailWork.PointA ? "A" : "B") + " — MAINTENANCE · " + (RailFreight.Instance ? RailFreight.Instance.StatusLine : "unavailable");
                case RailWork.Dispatch: return "Dispatch sealed carriage — MAINTENANCE · " + (RailFreight.Instance ? RailFreight.Instance.StatusLine : "unavailable");
                case RailWork.Crank: return "Hold/release dead-man crank — MAINTENANCE · " + (RailDeparture.Instance ? RailDeparture.Instance.StatusLine : "unavailable");
                case RailWork.EmergencyReturn: return "Inside service release — FREE body · six-second return";
                case RailWork.Signal: return "Send manual signal — other FREE body · " + (RailDeparture.Instance ? RailDeparture.Instance.StatusLine : "unavailable");
                case RailWork.Departure: return "Depart Northline — BOTH FREE bodies fully aboard";
                default: return "Northline control unavailable";
            }
        }
        public bool CanInteract(in InteractContext ctx, out string reason)
        {
            reason = "Northline control is unavailable.";
            if (!IsAvailable) return false;
            switch (kind)
            {
                case RailWork.Crossing: case RailWork.CrossingReturn: return RailCrossing.Instance && RailCrossing.Instance.CanRequest(ctx, this, out reason);
                case RailWork.Manifest: return RailFreight.Instance && RailFreight.Instance.CanValidate(ctx, this, out reason);
                case RailWork.PointA: case RailWork.PointB: return RailFreight.Instance && RailFreight.Instance.CanThrow(ctx, this, kind == RailWork.PointA ? 0 : 1, out reason);
                case RailWork.Dispatch: return RailFreight.Instance && RailFreight.Instance.CanDispatch(ctx, this, out reason);
                case RailWork.Crank: return RailDeparture.Instance && RailDeparture.Instance.CanCrank(ctx, this, out reason);
                case RailWork.EmergencyReturn: return RailDeparture.Instance && RailDeparture.Instance.CanEmergencyReturn(ctx, this, out reason);
                case RailWork.Signal: return RailDeparture.Instance && RailDeparture.Instance.CanSignal(ctx, this, out reason);
                case RailWork.Departure: return RailDeparture.Instance && RailDeparture.Instance.CanDepart(ctx, this, out reason);
                default: return false;
            }
        }
        public void Interact(in InteractContext ctx)
        {
            if (!CanInteract(ctx, out string reason)) { GameEvents.RaiseToast(reason, Palette.Warning); return; }
            bool success;
            switch (kind)
            {
                case RailWork.Crossing: case RailWork.CrossingReturn: success = RailCrossing.Instance.TryRequest(ctx, this, out reason); break;
                case RailWork.Manifest: success = RailFreight.Instance.TryValidate(ctx, this, out reason); break;
                case RailWork.PointA: case RailWork.PointB: success = RailFreight.Instance.TryThrow(ctx, this, kind == RailWork.PointA ? 0 : 1, out reason); break;
                case RailWork.Dispatch: success = RailFreight.Instance.TryDispatch(ctx, this, out reason); break;
                case RailWork.Crank: success = RailDeparture.Instance.TryCrank(ctx, this, out reason); break;
                case RailWork.EmergencyReturn: success = RailDeparture.Instance.TryEmergencyReturn(ctx, this, out reason); break;
                case RailWork.Signal: success = RailDeparture.Instance.TrySignal(ctx, this, out reason); break;
                case RailWork.Departure: success = RailDeparture.Instance.TryDepart(ctx, this, out reason); break;
                default: success = false; reason = "Unknown Northline control."; break;
            }
            if (!success) GameEvents.RaiseToast(reason, Palette.Warning);
        }
        void OnEnable() => Interactables.Register(this);
        void OnDisable() => Interactables.Unregister(this);
    }
}
