using System;
using System.Collections.Generic;
using UnityEngine;

namespace Parasite.Missions
{
    /// <summary>Four stable, physical sealed cargo bodies. Scanning changes a route, never directly completes a delivery.</summary>
    public sealed class HarborCargoDirector : MonoBehaviour
    {
        public static HarborCargoDirector Instance { get; private set; }
        static readonly string[] StableIds = { "A17", "A32", "R09", "R21" };
        public static IReadOnlyList<string> Ids => StableIds;
        public const float BrakeSeconds = 5f;
        public Transform[] crates = Array.Empty<Transform>();
        public Vector3[] loopPoints = Array.Empty<Vector3>(), baySlots = Array.Empty<Vector3>();
        public Bounds scanBounds;
        public float speed = HarborLayout.CargoSpeed;
        public bool Configured { get; private set; }
        public float BrakeRemaining { get; private set; }
        public int PhysicalCrateCount => bodies.Count;
        public string CurrentId { get { var crate = InArch(); return crate == null ? "" : crate.id; } }
        public string NextId { get { var crate = NextCrate(out _); return crate == null ? "" : crate.id; } }
        public float NextWindowIn { get { NextCrate(out float seconds); return seconds; } }
        public float WindowRemaining
        {
            get
            {
                var crate = InArch(); if (crate == null) return 0;
                var direction = LoopDirection(crate.distance);
                Vector3 centre = crate.collider.bounds.center; float exit = float.PositiveInfinity;
                for (int axis = 0; axis < 3; axis++)
                    if (Mathf.Abs(direction[axis]) > .0001f)
                    { float edge = direction[axis] > 0 ? scanBounds.max[axis] : scanBounds.min[axis]; exit = Mathf.Min(exit, (edge - centre[axis]) / direction[axis]); }
                return BrakeRemaining + Mathf.Max(0, exit) / Mathf.Max(.1f, speed);
            }
        }
        public string StatusLine => !Configured ? "conveyor unavailable" :
            (CurrentId.Length > 0 ? "arch " + CurrentId + " · window " + WindowRemaining.ToString("0.0") + " s" : "arch empty") +
            (NextId.Length > 0 ? " · next " + NextId + " in " + NextWindowIn.ToString("0.0") + " s" : " · no waiting lot") +
            (BrakeRemaining > 0 ? " · brake " + BrakeRemaining.ToString("0.0") + " s" : "");
        sealed class Cargo
        {
            public string id; public int slot, route = -1; public Transform transform; public Rigidbody body; public Collider collider;
            public float distance; public Vector3[] diversion; public bool delivered;
        }
        readonly List<Cargo> bodies = new(); float[] segmentLengths; float perimeter, archEntry;
        void Awake() => Instance = this;
        public void Configure(Transform[] crateTransforms, Vector3[] points, Bounds arch, Vector3[] slots)
        {
            if (crateTransforms == null || crateTransforms.Length != 4 || points == null || points.Length < 4 || slots == null || slots.Length != 4)
                throw new ArgumentException("Harbor cargo requires four stable crate bodies, a closed loop and four bay slots.");
            var unique = new HashSet<Transform>();
            for (int i = 0; i < 4; i++)
            {
                var value = crateTransforms[i]; var body = value ? value.GetComponent<Rigidbody>() : null; var collider = value ? value.GetComponent<Collider>() : null;
                if (!value || !unique.Add(value) || !body || !body.isKinematic || !body.detectCollisions || !collider || collider.isTrigger)
                    throw new ArgumentException("Each sealed cargo ID requires its own physical collider and colliding kinematic Rigidbody.");
            }
            crates = (Transform[])crateTransforms.Clone(); loopPoints = (Vector3[])points.Clone(); baySlots = (Vector3[])slots.Clone(); scanBounds = arch;
            bodies.Clear(); segmentLengths = new float[loopPoints.Length]; perimeter = 0;
            for (int i = 0; i < loopPoints.Length; i++) { segmentLengths[i] = Vector3.Distance(loopPoints[i], loopPoints[(i + 1) % loopPoints.Length]); perimeter += segmentLengths[i]; }
            if (perimeter < 1 || arch.size.x <= 0 || arch.size.y <= 0 || arch.size.z <= 0) throw new ArgumentException("The conveyor loop and arch must have physical extent.");
            // The authored arch lies on the first straight. Its exact world bounds determine the live centre scan window.
            Vector3 firstDirection = (loopPoints[1] - loopPoints[0]).normalized;
            float centreAlong = Vector3.Dot(scanBounds.center - loopPoints[0], firstDirection);
            float halfAlong = Mathf.Abs(firstDirection.x) * scanBounds.extents.x + Mathf.Abs(firstDirection.y) * scanBounds.extents.y + Mathf.Abs(firstDirection.z) * scanBounds.extents.z;
            archEntry = Mathf.Clamp(centreAlong - halfAlong, 0, segmentLengths[0]);
            for (int i = 0; i < 4; i++)
            {
                var cargo = new Cargo { id = StableIds[i], slot = i, transform = crates[i], body = crates[i].GetComponent<Rigidbody>(), collider = crates[i].GetComponent<Collider>(), distance = perimeter * i / 4f };
                cargo.delivered = RunState.Has(HarborCampaign.DeliveredFlag(cargo.id));
                Vector3 position;
                if (cargo.delivered) { cargo.route = 3; position = baySlots[i]; }
                else if (RunState.Has(HarborCampaign.ReroutedFlag(cargo.id)))
                {
                    // Sorting is earned; transient travel restarts at this lot's separate, fenced bay approach.
                    // Keep the final physical leg and its existing sweep/arrival checks; do not grant delivery on reload.
                    position = baySlots[i] - Vector3.forward * 3f;
                    SetDiversion(cargo); cargo.route = cargo.diversion.Length - 1;
                }
                else position = Point(cargo.distance);
                cargo.body.position = position; cargo.transform.position = position; bodies.Add(cargo);
            }
            BrakeRemaining = 0; Configured = true;
        }
        void Start()
        {
            if (!Configured && crates.Length == 4) Configure(crates, loopPoints.Length > 0 ? loopPoints : World(HarborLayout.CargoLoopLocal),
                scanBounds.size.sqrMagnitude > 0 ? scanBounds : HarborLayout.ScanBounds, baySlots.Length > 0 ? baySlots : World(HarborLayout.CargoBayLocal));
        }
        static Vector3[] World(Vector3[] local) { var result = new Vector3[local.Length]; for (int i = 0; i < local.Length; i++) result[i] = HarborLayout.World(local[i]); return result; }
        public Transform Crate(string id) => Find(id)?.transform;
        public Rigidbody CrateBody(string id) => Find(id)?.body;
        public int RouteStage(string id) => Find(id)?.route ?? -1;
        public bool Rerouted(string id) => Find(id) != null && RunState.Has(HarborCampaign.ReroutedFlag(id));
        public bool Delivered(string id) => Find(id) != null && RunState.Has(HarborCampaign.DeliveredFlag(id));
        Cargo Find(string id) => bodies.Find(value => value.id == id);
        bool Physical(Cargo cargo) => cargo.transform && cargo.transform.gameObject.activeInHierarchy && cargo.body && cargo.body.isKinematic && cargo.body.detectCollisions && cargo.collider && cargo.collider.enabled && !cargo.collider.isTrigger;
        Cargo InArch()
        {
            if (!Configured) return null;
            foreach (var cargo in bodies) if (cargo.route < 0 && !cargo.delivered && Physical(cargo) && scanBounds.Contains(cargo.collider.bounds.center)) return cargo;
            return null;
        }
        Cargo NextCrate(out float seconds)
        {
            seconds = float.PositiveInfinity; Cargo best = null; if (!Configured) return null;
            var current = InArch();
            foreach (var cargo in bodies)
            {
                if (cargo == current || cargo.route >= 0 || cargo.delivered || !Physical(cargo)) continue;
                float forward = Mathf.Repeat(archEntry - cargo.distance, perimeter); float time = forward / Mathf.Max(.1f, speed) + BrakeRemaining;
                if (time < seconds) { seconds = time; best = cargo; }
            }
            if (best == null) seconds = 0; return best;
        }
        public bool CanScan(in InteractContext ctx, out string reason)
        {
            reason = null;
            if (!Configured || !RunState.Has(HarborCampaign.CanalOpen)) { reason = "Balance the lock pressure first."; return false; }
            if (RunState.Has(HarborCampaign.ManifestComplete)) { reason = "The required manifest is already physically delivered."; return false; }
            if (!HarborCampaign.ReadyHost(ctx, Profession.Scientist, HarborLayout.World(HarborLayout.ScannerLocal) + Vector3.up * 1.1f, 2.3f, out reason)) return false;
            var cargo = InArch(); if (cargo == null) { reason = "The arch is empty. " + StatusLine; return false; }
            bool required = false; foreach (var id in HarborCampaign.RequiredCargoIds) if (id == cargo.id) required = true;
            if (!required) { reason = "Wrong lot " + cargo.id + ". This manifest requires " + HarborCampaign.RequiredCargoIds[0] + " and " + HarborCampaign.RequiredCargoIds[1] + "."; return false; }
            return true;
        }
        public bool TryScan(in InteractContext ctx, out string reason)
        {
            if (!CanScan(ctx, out reason)) return false;
            var cargo = InArch(); SetDiversion(cargo); MissionManager.SetFlag(HarborCampaign.ReroutedFlag(cargo.id));
            GameEvents.RaiseToast(cargo.id + " validated. Watch the sealed crate travel to its marked bay; delivery is not complete yet.", Palette.Info); return true;
        }
        void SetDiversion(Cargo cargo)
        {
            Vector3 branch = HarborLayout.At(14, .55f, 25), bay = baySlots[cargo.slot];
            cargo.diversion = new[] { branch, new Vector3(bay.x, branch.y, branch.z), bay }; cargo.route = 0;
        }
        public bool CanBrake(in InteractContext ctx, out string reason)
        {
            reason = null;
            if (!Configured || !RunState.Has(HarborCampaign.CanalOpen) || RunState.Has(HarborCampaign.ManifestComplete)) { reason = "The cargo brake is unavailable until the lock opens, or after delivery completes."; return false; }
            if (!HarborCampaign.ReadyHost(ctx, Profession.Maintenance, HarborLayout.World(HarborLayout.BrakeLocal) + Vector3.up * 1.1f, 2.3f, out reason)) return false;
            if (BrakeRemaining > 0) { reason = "The belt is already braked for " + BrakeRemaining.ToString("0.0") + " more seconds."; return false; }
            return true;
        }
        public bool TryBrake(in InteractContext ctx, out string reason)
        { if (!CanBrake(ctx, out reason)) return false; BrakeRemaining = BrakeSeconds; GameEvents.RaiseToast("Conveyor braked for five seconds. Switch to a scientist, or time the next moving window without the brake.", Palette.Info); return true; }
        void FixedUpdate()
        {
            if (!Configured || !HarborCampaign.Active || !RunState.Has(HarborCampaign.CanalOpen) || !GameManager.Instance || !GameManager.Instance.IsPlaying) return;
            float dt = Time.fixedDeltaTime; bool braked = BrakeRemaining > 0; BrakeRemaining = Mathf.Max(0, BrakeRemaining - dt);
            foreach (var cargo in bodies)
            {
                if (!Physical(cargo) || cargo.delivered) continue;
                if (cargo.route < 0)
                {
                    if (!braked)
                    {
                        float next = Mathf.Repeat(cargo.distance + Mathf.Max(.1f, speed) * dt, perimeter);
                        if (MovePhysical(cargo, Point(next))) cargo.distance = next;
                    }
                    continue;
                }
                Vector3 target = cargo.diversion[cargo.route];
                if ((cargo.body.position - target).sqrMagnitude <= .0004f && (cargo.transform.position - target).sqrMagnitude <= .0004f)
                {
                    if (cargo.route < cargo.diversion.Length - 1) cargo.route++;
                    else
                    {
                        cargo.delivered = true; cargo.route = 3; MissionManager.SetFlag(HarborCampaign.DeliveredFlag(cargo.id));
                        GameEvents.RaiseToast(cargo.id + " physically arrived in its bay.", Palette.Amoeba);
                        if (HarborCampaign.RequiredCargoDelivered) MissionManager.SetFlag(HarborCampaign.ManifestComplete);
                        continue;
                    }
                }
                MovePhysical(cargo, Vector3.MoveTowards(cargo.body.position, cargo.diversion[cargo.route], Mathf.Max(.1f, speed) * dt));
            }
        }
        bool MovePhysical(Cargo cargo, Vector3 destination)
        {
            Vector3 delta = destination - cargo.body.position; float distance = delta.magnitude;
            if (distance < .00001f) return true;
            // Kinematic motion alone does not resolve a wall collision. Refuse a blocked sweep rather than drive sealed cargo through it.
            Vector3 direction = delta / distance;
            foreach (var hit in cargo.body.SweepTestAll(direction, distance + .005f, QueryTriggerInteraction.Ignore))
            {
                if (!hit.collider || hit.transform.IsChildOf(cargo.transform)) continue;
                if (hit.distance <= distance + .005f && Vector3.Dot(hit.normal, direction) < -.1f) return false;
            }
            cargo.body.MovePosition(destination); return true;
        }
        Vector3 Point(float distance)
        {
            for (int i = 0; i < segmentLengths.Length; i++)
            { if (distance <= segmentLengths[i]) return Vector3.Lerp(loopPoints[i], loopPoints[(i + 1) % loopPoints.Length], segmentLengths[i] > .0001f ? distance / segmentLengths[i] : 0); distance -= segmentLengths[i]; }
            return loopPoints[0];
        }
        Vector3 LoopDirection(float distance)
        { for (int i = 0; i < segmentLengths.Length; i++) { if (distance < segmentLengths[i]) return (loopPoints[(i + 1) % loopPoints.Length] - loopPoints[i]).normalized; distance -= segmentLengths[i]; } return (loopPoints[1] - loopPoints[0]).normalized; }
        void OnDestroy() { if (Instance == this) Instance = null; bodies.Clear(); }
    }
}
