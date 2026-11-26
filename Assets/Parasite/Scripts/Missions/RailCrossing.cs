using Parasite.Possession;
using UnityEngine;
using UnityEngine.AI;

namespace Parasite.Missions
{
    /// <summary>Actual moving tram, fail-safe gates and a fresh same-body crossing window. Never moves a player.</summary>
    public sealed class RailCrossing : MonoBehaviour
    {
        public static RailCrossing Instance { get; private set; }
        public const float WarningSeconds = 2f, WindowSeconds = 8f, TramSpeed = 4.2f;
        public Rigidbody tram; public BoxCollider tramCollider; public Transform[] gates;
        public bool Configured { get; private set; }
        public bool Requested { get; private set; }
        public float WarningElapsed { get; private set; }
        public float WindowRemaining { get; private set; }
        public float GateFraction { get; private set; }
        public int ExpiredRequests { get; private set; }
        public bool TramPhysical => tram && tram.gameObject.activeInHierarchy && tram.isKinematic && tram.detectCollisions
            && tramCollider && tramCollider.enabled && !tramCollider.isTrigger && tramCollider.gameObject.activeInHierarchy;
        public bool GatesPhysical
        {
            get
            {
                if (gates == null || gateColliders == null || gates.Length != 2 || gateColliders.Length != 2) return false;
                for (int i = 0; i < 2; i++) if (!gates[i] || !gates[i].gameObject.activeInHierarchy || !gateColliders[i] || !gateColliders[i].enabled || gateColliders[i].isTrigger) return false;
                return true;
            }
        }
        public bool HardwareReady => Configured && TramPhysical && GatesPhysical;
        public bool CarClear => HardwareReady && !tramCollider.bounds.Intersects(RailLayout.Crossing);
        public string StatusLine => RunState.Has(RailCampaign.CrossingPassed) ? "crossing secured · tram held clear" : WindowRemaining > 0 ? "GREEN · " + WindowRemaining.ToString("0.0") + " s"
            : Requested ? CarClear ? "tram clear · barriers releasing" : "WAIT · actual tram clearing" : "tram running · request MAINTENANCE crossing";
        Vector3[] closed; NavMeshObstacle[] obstacles; BoxCollider[] gateColliders; AmoebaAgent requester; IControllable requestedBody;
        int direction = 1; bool closing, returningSouth; readonly Collider[] occupants = new Collider[64];
        void Awake() => Instance = this;
        public void Configure(Rigidbody actualTram, Transform[] physicalGates)
        {
            if (!actualTram || !actualTram.isKinematic || !actualTram.detectCollisions || !actualTram.GetComponent<BoxCollider>() || physicalGates == null || physicalGates.Length != 2)
                throw new System.ArgumentException("Northline crossing requires a colliding kinematic tram and two physical gates.");
            tram = actualTram; tramCollider = tram.GetComponent<BoxCollider>(); gates = physicalGates;
            closed = new Vector3[2]; obstacles = new NavMeshObstacle[2]; gateColliders = new BoxCollider[2];
            for (int i = 0; i < 2; i++)
            {
                if (!gates[i] || !gates[i].GetComponent<BoxCollider>()) throw new System.ArgumentException("Crossing gates retain real colliders.");
                closed[i] = gates[i].localPosition; obstacles[i] = gates[i].GetComponent<NavMeshObstacle>(); gateColliders[i] = gates[i].GetComponent<BoxCollider>();
            }
            bool earned = RunState.Has(RailCampaign.CrossingPassed);
            GateFraction = earned ? 1 : 0; Requested = false; closing = false; WarningElapsed = WindowRemaining = 0;
            Configured = true; if (!HardwareReady) throw new System.ArgumentException("Northline crossing hardware must be active and physical at configuration."); ApplyGates();
        }
        public bool CanRequest(in InteractContext ctx, RailWorkControl control, out string reason)
        {
            reason = null;
            if (!HardwareReady || !isActiveAndEnabled || !control || RunState.Has(RailCampaign.CrossingPassed)) { reason = "The crossing is unavailable or already secured."; return false; }
            if (Requested || GateFraction > .001f || closing) { reason = "Let the current crossing finish safely before requesting again."; return false; }
            if (!RailCampaign.ReadyHost(ctx, Profession.Maintenance, control.InteractPoint, control.transform, out reason)) return false;
            bool southbound = control.kind == RailWork.CrossingReturn;
            if (!RailCampaign.BodyBounds(ctx.agent, out var bounds) || (southbound ? bounds.min.z <= RailLayout.Crossing.max.z : bounds.max.z >= RailLayout.Crossing.min.z))
            { reason = southbound ? "Use the return request from the marked north side." : "Request from the marked south side."; return false; }
            if (!RailCampaign.NoPursuit) { reason = "Lose pursuit before requesting the crossing."; return false; }
            return true;
        }
        public bool TryRequest(in InteractContext ctx, RailWorkControl control, out string reason)
        {
            if (!CanRequest(ctx, control, out reason)) return false;
            returningSouth = control.kind == RailWork.CrossingReturn; requester = ctx.agent; requestedBody = ctx.agent.Body; Requested = true; WarningElapsed = WindowRemaining = 0;
            GameEvents.RaiseToast("Crossing requested. Wait for the tram to clear and both barriers to rise; then cross that same body on green.", Palette.Info); return true;
        }
        bool Occupied()
        {
            var zone = RailLayout.Crossing;
            // Include the actual entire closing sweep, including both outside faces of each barrier.
            // A capsule standing just beyond the lane still blocks a gate descending onto its head.
            if (gateColliders != null)
                for (int i = 0; i < gateColliders.Length; i++)
                {
                    if (!gateColliders[i] || !gates[i]) return true; // Unknown gate extent cannot be declared clear.
                    Bounds current = gateColliders[i].bounds, atClosed = current;
                    Vector3 localOffset = closed[i] - gates[i].localPosition;
                    atClosed.center += gates[i].parent ? gates[i].parent.TransformVector(localOffset) : localOffset;
                    zone.Encapsulate(current); zone.Encapsulate(atClosed);
                }
            zone.Expand(.04f); // Two centimetres cover contact/skin rounding, never grant extra crossing credit.
            int count = Physics.OverlapBoxNonAlloc(zone.center, zone.extents, occupants, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            if (count >= occupants.Length) return true;
            for (int i = 0; i < count; i++)
                if (occupants[i] && (occupants[i].GetComponentInParent<Characters.HostCharacter>() || occupants[i].GetComponentInParent<AmoebaController>())) return true;
            return false;
        }
        void FixedUpdate()
        {
            if (!HardwareReady || !RailCampaign.Active || !GameManager.Instance || !GameManager.Instance.IsPlaying || RunState.Has(RailCampaign.CrossingPassed)) return;
            // A late pedestrian always keeps the tram held. A request holds the car as soon as its actual collider is clear.
            if (Occupied() || Requested && CarClear || GateFraction > .001f) return;
            Vector3 position = tram.position;
            float local = position.x - RailLayout.Origin.x;
            if (local >= 32) direction = -1; else if (local <= -32) direction = 1;
            Vector3 next = position + Vector3.right * (direction * TramSpeed * Time.fixedDeltaTime);
            RailFreight.MovePhysical(tram, next);
        }
        void Update()
        {
            if (!Configured || !RailCampaign.Active || !GameManager.Instance || !GameManager.Instance.IsPlaying) return;
            float dt = Time.deltaTime;
            if (!HardwareReady)
            {
                if (Requested) Cancel("Crossing hardware is not physically available. No green or crossing credit can be granted.");
                if (GatesPhysical && !Occupied()) GateFraction = Mathf.MoveTowards(GateFraction, 0, dt / .8f);
                ApplyGates(); return;
            }
            if (RunState.Has(RailCampaign.CrossingPassed)) { GateFraction = 1; ApplyGates(); return; }
            bool sameBody = RailCampaign.Registered(requester) && requester.Body == requestedBody;
            if (Requested && !sameBody) Cancel("Crossing cancelled: the requesting body changed or was captured.");
            if (Requested && WindowRemaining <= 0 && GateFraction <= 0)
            {
                WarningElapsed = CarClear && !Occupied() ? WarningElapsed + dt : 0;
                if (WarningElapsed >= WarningSeconds) GateFraction = .001f;
            }
            if (Requested && GateFraction > 0 && WindowRemaining <= 0)
            {
                if (!CarClear) { Cancel("The tram has not cleared the interlock."); }
                else
                {
                    GateFraction = Mathf.MoveTowards(GateFraction, 1, dt / .8f);
                    if (GateFraction >= 1) WindowRemaining = WindowSeconds;
                }
            }
            else if (Requested && WindowRemaining > 0)
            {
                WindowRemaining = Mathf.Max(0, WindowRemaining - dt);
                if (sameBody && CarClear && GateFraction >= .999f && RailCampaign.BodyBounds(requester, out var body) && (returningSouth ? body.max.z < RailLayout.Crossing.min.z : body.min.z > RailLayout.Crossing.max.z))
                {
                    if (!returningSouth) { MissionManager.SetFlag(RailCampaign.CrossingPassed); Requested = false; requester = null; requestedBody = null; }
                    else Cancel("Safe return completed without northbound crossing credit. Request a fresh south-side green to continue.");
                }
                else if (WindowRemaining <= 0) { ExpiredRequests++; Cancel("Crossing window expired. Clear the lane, then request another green."); }
            }
            if (closing && !Occupied())
            {
                GateFraction = Mathf.MoveTowards(GateFraction, 0, dt / .8f);
                if (GateFraction <= 0) closing = false;
            }
            ApplyGates();
        }
        void Cancel(string reason)
        { Requested = false; requester = null; requestedBody = null; WindowRemaining = WarningElapsed = 0; closing = GateFraction > 0; GameEvents.RaiseToast(reason, Palette.Warning); }
        void ApplyGates()
        {
            if (gates == null) return;
            for (int i = 0; i < gates.Length; i++)
            { if (!gates[i]) continue; gates[i].localPosition = closed[i] + Vector3.up * (3.8f * Mathf.SmoothStep(0, 1, GateFraction)); if (obstacles[i]) obstacles[i].enabled = GateFraction < .99f; }
        }
        void OnEnable() { if (Configured && !RunState.Has(RailCampaign.CrossingPassed)) closing = GateFraction > 0; }
        void OnDisable() { Requested = false; requester = null; requestedBody = null; WindowRemaining = WarningElapsed = 0; }
        void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
