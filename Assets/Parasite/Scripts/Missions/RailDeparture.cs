using System;
using Parasite.Characters;
using Parasite.Possession;
using UnityEngine;
using UnityEngine.AI;

namespace Parasite.Missions
{
    /// <summary>Inactive maintenance holder + distinct free service-duct operator; fresh E after any cancellation.</summary>
    public sealed class RailDeparture : MonoBehaviour
    {
        public static RailDeparture Instance { get; private set; }
        public const float SignalSeconds = 3f, Stillness = .04f, EmergencySeconds = 6f;
        public Transform ductGate, platformGate, receiver, carriage;
        Transform crankWheel; Quaternion crankRest; float crankAngle;
        Transform portal; Vector3 portalClosed; NavMeshObstacle portalObstacle;
        public Rigidbody Coach { get; private set; }
        public BoxCollider CoachDeck { get; private set; }
        Collider[] transitSupports = Array.Empty<Collider>(), coachColliders = Array.Empty<Collider>();
        public bool Configured { get; private set; }
        public bool DepartureAuthorized { get; private set; }
        public bool PermanentlyRetired { get; private set; }
        public bool Holding { get; private set; }
        public bool Signalling { get; private set; }
        public float Progress { get; private set; }
        public float DuctFraction { get; private set; }
        public float PlatformFraction { get; private set; }
        public float EmergencyRemaining { get; private set; }
        public string LastCancellation { get; private set; } = "";
        public string StatusLine => RunState.Has(RailCampaign.SignalReleased) ? "manual signal secured · release all hosts and board"
            : Signalling ? "free signal " + Progress.ToString("0.0") + " / 3.0 s" : Holding ? "maintenance holding · free operator uses service duct" : "dead-man crank needs MAINTENANCE";
        public event Action Completed;
        public event Action AvailabilityLost;
        AmoebaAgent holder, operatorAgent; HostCharacter heldHost; AmoebaController operatorBody;
        Vector3 holderPosition, operatorPosition, ductClosed, platformClosed;
        NavMeshObstacle ductObstacle, platformObstacle;
        readonly Collider[] occupants = new Collider[48];
        void Awake() => Instance = this;
        public void Configure(Transform lowGate, Transform northGate, Transform actualReceiver, Transform departureCarriage)
        {
            if (!lowGate || !lowGate.GetComponent<Collider>() || !northGate || !northGate.GetComponent<Collider>() || !actualReceiver || !departureCarriage)
                throw new ArgumentException("Northline signal requires physical duct/platform gates, receiver and departure carriage.");
            ductGate = lowGate; platformGate = northGate; receiver = actualReceiver; carriage = departureCarriage;
            ductClosed = ductGate.localPosition; platformClosed = platformGate.localPosition;
            ductObstacle = ductGate.GetComponent<NavMeshObstacle>(); platformObstacle = platformGate.GetComponent<NavMeshObstacle>();
            Holding = Signalling = false; holder = operatorAgent = null; heldHost = null; operatorBody = null; EmergencyRemaining = 0;
            bool earned = RunState.Has(RailCampaign.SignalReleased); Progress = earned ? SignalSeconds : 0; PlatformFraction = earned ? 1 : 0; DuctFraction = 0;
            DepartureAuthorized = false; Configured = true; ApplyGates();
        }
        public void SetCrankVisual(Transform wheel)
        { if (!wheel) throw new ArgumentException("The dead-man crank needs its visible authored handwheel."); crankWheel = wheel; crankRest = wheel.localRotation; crankAngle = 0; }
        public void SetPortal(Transform northPortal)
        {
            if (!northPortal || !northPortal.GetComponent<Collider>()) throw new ArgumentException("Northline departure requires its actual portal gate.");
            portal = northPortal; portalClosed = portal.localPosition; portalObstacle = portal.GetComponent<NavMeshObstacle>(); ApplyGates();
        }
        public void SetTransitPhysics(Rigidbody actualCoach, Collider[] supports, BoxCollider actualDeck)
        {
            if (!actualCoach || actualCoach.transform != carriage || !actualCoach.isKinematic || !actualCoach.detectCollisions || !actualDeck || actualDeck.attachedRigidbody != actualCoach || supports == null || supports.Length != 2)
                throw new ArgumentException("Northline departure needs its actual colliding kinematic coach and two explicit track footing colliders.");
            Coach = actualCoach; CoachDeck = actualDeck; transitSupports = (Collider[])supports.Clone(); coachColliders = Coach.GetComponentsInChildren<Collider>(true);
            if (coachColliders.Length == 0) throw new ArgumentException("The actual coach needs its compound collision shapes.");
        }
        public bool TransitHardwareReady
        {
            get
            {
                if (PermanentlyRetired || !Configured || !isActiveAndEnabled || !gameObject.activeInHierarchy || !Coach || !Coach.gameObject.activeInHierarchy || !Coach.isKinematic || !Coach.detectCollisions || !portal || !portal.gameObject.activeInHierarchy) return false;
                var portalCollider = portal.GetComponent<Collider>();
                if (!portalCollider || !portalCollider.enabled || portalCollider.isTrigger || PlatformFraction < .999f || !CoachDeck || !CoachDeck.enabled || CoachDeck.isTrigger) return false;
                if (transitSupports.Length != 2 || coachColliders.Length == 0) return false;
                foreach (var collider in coachColliders) if (!collider || !collider.enabled || collider.isTrigger || !collider.gameObject.activeInHierarchy) return false;
                Bounds envelope = coachColliders[0].bounds;
                for (int i = 1; i < coachColliders.Length; i++) envelope.Encapsulate(coachColliders[i].bounds);
                // The actual compound must remain a conventional3.2m-wide,14m-long northbound coach.
                if (Vector3.Dot(Coach.transform.forward, Vector3.forward) < .999f || Vector3.Dot(Coach.transform.up, Vector3.up) < .999f
                    || Mathf.Abs(envelope.size.x - RailLayout.CoachWidth) > .02f || Mathf.Abs(envelope.size.z - RailLayout.CoachLength) > .02f
                    || Mathf.Abs(CoachDeck.bounds.max.y - RailLayout.CoachDeckY) > .005f) return false;
                Bounds aperture = portalCollider.bounds;
                if (aperture.min.x > envelope.min.x - .02f || aperture.max.x < envelope.max.x + .02f || aperture.min.y < envelope.max.y + .02f) return false;
                foreach (var support in transitSupports)
                    if (!support || !support.enabled || support.isTrigger || !support.gameObject.activeInHierarchy || Mathf.Abs(support.bounds.max.y - .15f) > .005f) return false;
                return true;
            }
        }
        public bool ClearTransit(Vector3 delta)
        {
            if (!TransitHardwareReady) return false;
            float distance = delta.magnitude; if (distance <= .00001f) return true;
            foreach (var hit in Coach.SweepTestAll(delta / distance, distance + .005f, QueryTriggerInteraction.Ignore))
            {
                if (!hit.collider || hit.transform.IsChildOf(Coach.transform)) continue;
                bool support = false; foreach (var floor in transitSupports) if (hit.collider == floor) { support = true; break; }
                if (support) continue; // Only the two declared coplanar track slabs may support the moving deck.
                if (hit.distance <= distance + .005f && Vector3.Dot(hit.normal, delta / distance) < -.1f) return false;
            }
            return true;
        }
        public void CancelDepartureAuthorization() => DepartureAuthorized = false;
        public bool GateHardwareReady
        {
            get
            {
                if (!Configured || !ductGate || !platformGate || !receiver || !carriage || !ductGate.gameObject.activeInHierarchy || !platformGate.gameObject.activeInHierarchy) return false;
                var low = ductGate.GetComponent<Collider>(); var north = platformGate.GetComponent<Collider>();
                return low && low.enabled && !low.isTrigger && north && north.enabled && !north.isTrigger;
            }
        }
        bool HolderValid()
        {
            var pm = PossessionManager.Instance;
            return Holding && pm && pm.Agents.Count == 2 && RailCampaign.Registered(holder) && heldHost && holder.Host == heldHost && heldHost.Possessor == holder
                && heldHost.State == HostState.Possessed && heldHost.Profession == Profession.Maintenance && AccessRules.Has(heldHost.Permissions, AccessPermission.ServiceTunnel)
                && RailCampaign.FullyInside(holder, RailLayout.CrankPad) && (holder.Position - holderPosition).sqrMagnitude <= Stillness * Stillness;
        }
        public bool CanCrank(in InteractContext ctx, RailWorkControl control, out string reason)
        {
            reason = null;
            if (!GateHardwareReady || !isActiveAndEnabled || !RunState.Has(RailCampaign.FreightArrived) || RunState.Has(RailCampaign.SignalReleased)) { reason = "Deliver the sealed carriage first; the secured signal needs no more crank work."; return false; }
            if (!RailCampaign.ReadyHost(ctx, Profession.Maintenance, control.InteractPoint, control.transform, out reason)) return false;
            if (Holding && holder == ctx.agent) return true; // The original holder can deliberately cancel with E.
            if (Holding) { reason = "The original maintenance holder is still on the crank."; return false; }
            var pm = PossessionManager.Instance;
            if (!pm || pm.Agents.Count != 2 || pm.Agents[0] == pm.Agents[1] || pm.Agents[0].Id == pm.Agents[1].Id)
            { reason = "Split into two distinct bodies before holding the dead-man crank."; return false; }
            if (!RailCampaign.FullyInside(ctx.agent, RailLayout.CrankPad)) { reason = "Stand the full maintenance body on the marked west crank pad."; return false; }
            if (!RailCampaign.NoPursuit) { reason = "Lose pursuit before beginning the manual signal."; return false; }
            return true;
        }
        public bool TryCrank(in InteractContext ctx, RailWorkControl control, out string reason)
        {
            if (!CanCrank(ctx, control, out reason)) return false;
            if (Holding) { CancelHolder("The holder released the crank. Set both operators again, then use fresh E."); return true; }
            holder = ctx.agent; heldHost = ctx.host; holderPosition = holder.Position; Holding = true; LastCancellation = "";
            GameEvents.RaiseToast("Dead-man crank held. Tab to the other body, release its host, and use the low service duct. This host must stay still.", Palette.Info); return true;
        }
        public bool CanSignal(in InteractContext ctx, RailWorkControl control, out string reason)
        {
            reason = null;
            if (!GateHardwareReady || !isActiveAndEnabled || RunState.Has(RailCampaign.SignalReleased) || !RunState.Has(RailCampaign.FreightArrived)) { reason = "The signal is unavailable or already secured."; return false; }
            if (!RailCampaign.Ready(ctx, control.InteractPoint, control.transform, out reason)) return false;
            if (!HolderValid()) { reason = "A different maintenance body must keep the west dead-man crank held."; return false; }
            if (!RailCampaign.Free(ctx.agent) || ctx.agent == holder || ctx.agent.Id == holder.Id || !ctx.agent.Blob.Controller.isGrounded)
            { reason = "The distinct other operator must be a grounded FREE amoeba."; return false; }
            if (!RailCampaign.FullyInside(ctx.agent, RailLayout.SignalPad)) { reason = "Move the full free capsule into the east signal mark."; return false; }
            if (!RailCampaign.NoPursuit) { reason = "Lose pursuit before signalling."; return false; }
            if (!RailCampaign.ClearLine(ctx.agent.Blob.Center, receiver.position, ctx.agent.Blob.transform, control.transform))
            { reason = "The physical receiver sight line is blocked."; return false; }
            if (Signalling) { reason = "The signal is running. Keep both operators still."; return false; }
            return true;
        }
        public bool TrySignal(in InteractContext ctx, RailWorkControl control, out string reason)
        {
            if (!CanSignal(ctx, control, out reason)) return false;
            operatorAgent = ctx.agent; operatorBody = ctx.agent.Blob; operatorPosition = ctx.agent.Position;
            Progress = 0; Signalling = true; LastCancellation = "";
            GameEvents.RaiseToast("Manual signal started. Hold both operators still and the receiver clear for three seconds.", Palette.Info); return true;
        }
        public bool CanEmergencyReturn(in InteractContext ctx, RailWorkControl control, out string reason)
        {
            if (!GateHardwareReady || !isActiveAndEnabled || !RailCampaign.Ready(ctx, control.InteractPoint, control.transform, out reason)) { reason = "The inside service release is unavailable."; return false; }
            if (!RailCampaign.Free(ctx.agent) || ctx.agent.Position.x < RailLayout.Origin.x + 2.4f)
            { reason = "Use the inside release as a FREE body on the east side."; return false; }
            if (RunState.Has(RailCampaign.SignalReleased)) { reason = "The north platform door is already open."; return false; }
            if (EmergencyRemaining > 0) { reason = "The inside release is already open."; return false; }
            reason = null; return true;
        }
        public bool TryEmergencyReturn(in InteractContext ctx, RailWorkControl control, out string reason)
        {
            if (!CanEmergencyReturn(ctx, control, out reason)) return false;
            EmergencyRemaining = EmergencySeconds;
            GameEvents.RaiseToast("Inside service release: six seconds to return through the duct. No signal progress is granted.", Palette.Warning); return true;
        }
        public bool ReadyToDepart(out string reason)
        {
            reason = null; var pm = PossessionManager.Instance;
            if (!GateHardwareReady || !isActiveAndEnabled || !TransitHardwareReady || !RunState.Has(RailCampaign.SignalReleased) || !RunState.Has(RailCampaign.FreightArrived) || PlatformFraction < .999f)
            { reason = "The freight must arrive and the manual signal must physically release the north door."; return false; }
            if (!GameManager.Instance || !GameManager.Instance.IsPlaying || !pm || pm.Agents.Count != 2 || pm.Agents[0] == pm.Agents[1] || pm.Agents[0].Id == pm.Agents[1].Id)
            { reason = "Bring two distinct registered bodies aboard during play."; return false; }
            foreach (var agent in pm.Agents)
                if (!RailCampaign.Free(agent) || !agent.Blob.Controller.isGrounded || !RailCampaign.FullyInside(agent, RailLayout.Boarding))
                { reason = "Release every host and bring both full FREE capsules into the departure carriage."; return false; }
            if (!RailCampaign.NoPursuit) { reason = "Lose pursuit before departing."; return false; }
            return true;
        }
        public bool CanDepart(in InteractContext ctx, RailWorkControl control, out string reason)
        {
            if (RunState.Has(RailCampaign.Departed)) { reason = "Northline departure is already complete."; return false; }
            if (Completed == null) { reason = "The departure coordinator is unavailable."; return false; }
            if (!RailCampaign.Ready(ctx, control.InteractPoint, control.transform, out reason)) return false;
            if (!RailCampaign.Free(ctx.agent)) { reason = "Use the departure control as a FREE amoeba."; return false; }
            return ReadyToDepart(out reason);
        }
        public bool TryDepart(in InteractContext ctx, RailWorkControl control, out string reason)
        {
            if (!CanDepart(ctx, control, out reason)) return false;
            DepartureAuthorized = true; Completed?.Invoke();
            if (!DepartureAuthorized) { reason = "The coach could not begin its protected physical departure; position both free bodies and press E again."; return false; }
            return true;
        }
        void Update()
        {
            if (!Configured || !RailCampaign.Active || !GameManager.Instance || !GameManager.Instance.IsPlaying) return;
            if (!GateHardwareReady) { if (Holding || Signalling) CancelHolder("The physical service/signal gate is unavailable. No progress is granted."); EmergencyRemaining = 0; return; }
            float dt = Time.deltaTime;
            EmergencyRemaining = Mathf.Max(0, EmergencyRemaining - dt);
            if (Holding && !HolderValid()) CancelHolder("The maintenance holder moved, changed body or was captured. The signal needs a fresh crank and E attempt.");
            if (Signalling)
            {
                bool valid = HolderValid() && RailCampaign.Free(operatorAgent) && operatorBody && operatorAgent.Blob == operatorBody
                    && operatorBody.Controller.isGrounded && RailCampaign.FullyInside(operatorAgent, RailLayout.SignalPad)
                    && (operatorAgent.Position - operatorPosition).sqrMagnitude <= Stillness * Stillness && RailCampaign.NoPursuit
                    && RailCampaign.ClearLine(operatorBody.Center, receiver.position, operatorBody.transform, receiver.parent);
                if (!valid) CancelSignal("Signal interrupted: keep the actual holder and free operator still, unoccupied and clear of pursuit/obstruction. Press E again.");
                else
                {
                    Progress = Mathf.Min(SignalSeconds, Progress + dt);
                    if (Progress >= SignalSeconds)
                    {
                        Signalling = Holding = false; holder = operatorAgent = null; heldHost = null; operatorBody = null;
                        MissionManager.SetFlag(RailCampaign.SignalReleased);
                        GameEvents.RaiseToast("North signal secured. Release the maintenance host, bring both free bodies aboard and use the departure control.", Palette.Amoeba);
                    }
                }
            }
            if (crankWheel) { crankAngle = Mathf.MoveTowards(crankAngle, Holding ? 45 : 0, dt * 180); crankWheel.localRotation = crankRest * Quaternion.Euler(0, 0, crankAngle); }
            bool ductWanted = Holding || EmergencyRemaining > 0;
            if (ductWanted || !DuctOccupied()) DuctFraction = Mathf.MoveTowards(DuctFraction, ductWanted ? 1 : 0, dt / .6f);
            PlatformFraction = Mathf.MoveTowards(PlatformFraction, RunState.Has(RailCampaign.SignalReleased) ? 1 : 0, dt / 1.2f);
            ApplyGates();
        }
        bool DuctOccupied()
        {
            var zone = new Bounds(RailLayout.At(0, .75f, 34), new Vector3(5.5f, 1.5f, 2.1f));
            int count = Physics.OverlapBoxNonAlloc(zone.center, zone.extents, occupants, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            if (count >= occupants.Length) return true;
            for (int i = 0; i < count; i++) if (occupants[i] && (occupants[i].GetComponentInParent<HostCharacter>() || occupants[i].GetComponentInParent<AmoebaController>())) return true;
            return false;
        }
        void ApplyGates()
        {
            ductGate.localPosition = ductClosed + Vector3.up * (1.5f * Mathf.SmoothStep(0, 1, DuctFraction));
            platformGate.localPosition = platformClosed + Vector3.up * (4f * Mathf.SmoothStep(0, 1, PlatformFraction));
            if (ductObstacle) ductObstacle.enabled = DuctFraction < .99f;
            if (platformObstacle) platformObstacle.enabled = PlatformFraction < .99f;
            if (portal) { portal.localPosition = portalClosed + Vector3.up * (4f * Mathf.SmoothStep(0, 1, PlatformFraction)); if (portalObstacle) portalObstacle.enabled = PlatformFraction < .99f; }
        }
        void CancelSignal(string reason)
        { Signalling = false; operatorAgent = null; operatorBody = null; Progress = 0; LastCancellation = reason; GameEvents.RaiseToast(reason, Palette.Warning); }
        void CancelHolder(string reason)
        { Holding = false; holder = null; heldHost = null; CancelSignal(reason); }
        void OnDisable()
        {
            DepartureAuthorized = false; Holding = Signalling = false; holder = operatorAgent = null; heldHost = null; operatorBody = null;
            EmergencyRemaining = 0; if (!RunState.Has(RailCampaign.SignalReleased)) Progress = 0;
            AvailabilityLost?.Invoke();
        }
        void OnDestroy()
        {
            PermanentlyRetired = true; DepartureAuthorized = false;
            if (Instance == this) Instance = null;
            AvailabilityLost?.Invoke(); AvailabilityLost = null; Completed = null;
        }
    }
}
