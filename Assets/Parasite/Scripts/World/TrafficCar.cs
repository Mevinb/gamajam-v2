using System.Collections.Generic;
using UnityEngine;

namespace Parasite.World
{
    public class TrafficCar : MonoBehaviour
    {
        public const float CruiseSpeed = 9f;
        public float Speed { get; private set; }
        public float Distance { get; private set; }
        public float IntersectionEnterTime { get; private set; } = -999f;
        public TrafficSystem.Lane Lane => lane;
        TrafficSystem.Lane lane;
        TrafficSystem system;
        Transform[] wheels;
        Renderer[] renderers;
        BoxCollider box;
        bool shown = true;
        float cruise;
        static readonly RaycastHit[] hits = new RaycastHit[32];

        public void Init(TrafficSystem sys, TrafficSystem.Lane l, float startDistance)
        {
            system = sys; lane = l; Distance = startDistance;
            cruise = CruiseSpeed * Random.Range(0.85f, 1.1f);
            Speed = cruise;
            Place();
        }

        void Place()
        {
            lane.Sample(Distance, out var p, out var d);
            transform.SetPositionAndRotation(p, Quaternion.LookRotation(d));
        }

        float CarAheadGap()
        {
            float best = float.MaxValue;
            foreach (var c in lane.cars)
            {
                if (c == this) continue;
                float gap = c.Distance - Distance;
                if (gap < 0f) gap += lane.Length;
                if (gap < best) best = gap;
            }
            return best;
        }

        public bool IsInIntersection(out TrafficSystem.Intersection inter)
        {
            foreach (var it in TrafficSystem.Intersections)
            {
                if (it.bounds.Contains(transform.position))
                {
                    inter = it;
                    return true;
                }
            }
            inter = default;
            return false;
        }

        public bool ApproachingIntersection(out TrafficSystem.Intersection inter, out float distToStopLine)
        {
            inter = default;
            distToStopLine = float.MaxValue;
            Ray ray = new Ray(transform.position, transform.forward);
            foreach (var it in TrafficSystem.Intersections)
            {
                if (it.bounds.Contains(transform.position)) continue;
                if (it.bounds.IntersectRay(ray, out float hitDist))
                {
                    if (hitDist > 0f && hitDist < 20f && hitDist < distToStopLine)
                    {
                        distToStopLine = hitDist;
                        inter = it;
                    }
                }
            }
            return distToStopLine < 20f;
        }

        public bool HasRightOfWayOver(TrafficCar other, float myDistToStop = float.MaxValue, float otherDistToStop = float.MaxValue)
        {
            if (!other) return true;

            bool myIn = IsInIntersection(out _);
            bool otherIn = other.IsInIntersection(out _);

            // Car already inside intersection conflict zone has right of way
            if (otherIn && !myIn) return false;
            if (myIn && !otherIn) return true;

            // Both inside: earlier entry time proceeds first
            if (myIn && otherIn)
            {
                if (Mathf.Abs(IntersectionEnterTime - other.IntersectionEnterTime) > 0.05f)
                    return IntersectionEnterTime < other.IntersectionEnterTime;
            }

            // Priority by street
            int myPrio = lane != null ? lane.priority : 1;
            int otherPrio = other.lane != null ? other.lane.priority : 1;
            if (myPrio != otherPrio) return myPrio > otherPrio;

            // Equal priority: closer car has right of way
            if (myDistToStop < float.MaxValue && otherDistToStop < float.MaxValue)
            {
                if (Mathf.Abs(myDistToStop - otherDistToStop) > 0.6f)
                    return myDistToStop < otherDistToStop;
            }

            // Deterministic tie-breaker
            return GetInstanceID() < other.GetInstanceID();
        }

        /// <summary>People, the amoeba, or another car immediately in front of the bumper.</summary>
        bool Blocked(out float dist, float lookAhead)
        {
            dist = 0f;
            Vector3 o = transform.position + Vector3.up * 0.8f + transform.forward * 2.5f;
            int n = Physics.SphereCastNonAlloc(o, 1.0f, transform.forward, hits, lookAhead, ~0, QueryTriggerInteraction.Collide);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                int l = c.gameObject.layer;
                bool person = l == Layers.Characters || l == Layers.Amoeba;
                var other = person ? null : c.GetComponentInParent<TrafficCar>();
                if (!person && (!other || other == this)) continue;

                if (other != null)
                {
                    // If car is on the same lane, follow distance is handled by CarAheadGap(), but brake if very close
                    if (other.lane == lane && hits[i].distance > 4f) continue;
                    // If crossing car, don't emergency brake from afar if we have right of way
                    if (other.lane != lane && hits[i].distance > 3f && HasRightOfWayOver(other)) continue;
                }

                if (hits[i].distance < best) best = hits[i].distance;
            }
            if (best == float.MaxValue) return false;
            dist = best;
            return true;
        }

        void Update()
        {
            if (GameManager.Instance && !GameManager.Instance.IsPlaying) return;
            Tick(Time.deltaTime);
        }

        // Keep the displacement bounded by the measured clearance as well as braking the speed. A frame hitch must
        // never let the kinematic car step through a person while its speed is still catching up to the stop target.
        void Tick(float dt)
        {
            if (dt <= 0f || lane == null) return;
            bool inside = IsInIntersection(out _);
            if (inside && IntersectionEnterTime < 0f) IntersectionEnterTime = Time.time;
            else if (!inside) IntersectionEnterTime = -999f;

            float target = cruise;
            float safeTravel = float.MaxValue;
            float gap = CarAheadGap();
            if (gap < 16f) target = Mathf.Min(target, Mathf.Max(0f, (gap - 7f) * 1.2f));
            safeTravel = Mathf.Min(safeTravel, Mathf.Max(0f, gap - 7f));

            // Intersection approach right-of-way logic:
            if (ApproachingIntersection(out var appInter, out float distToStop))
            {
                bool yield = false;
                if (system)
                {
                    foreach (var l in system.Lanes)
                    {
                        if (!TrafficSystem.LanesConflict(lane, l)) continue;
                        foreach (var other in l.cars)
                        {
                            if (!other || other == this) continue;
                            // Crossing car already inside the intersection conflict zone
                            if (appInter.bounds.Contains(other.transform.position))
                            {
                                yield = true;
                                break;
                            }
                            // Crossing car approaching the same intersection with right of way
                            if (other.ApproachingIntersection(out var otherInter, out float otherDist) && otherInter.bounds == appInter.bounds)
                            {
                                if (!HasRightOfWayOver(other, distToStop, otherDist))
                                {
                                    yield = true;
                                    break;
                                }
                            }
                        }
                        if (yield) break;
                    }
                }
                if (yield)
                {
                    // Stop the bumper, rather than the car's centre, behind the intersection boundary.
                    float clearance = Mathf.Max(0f, distToStop - box.size.z * 0.5f - 0.6f);
                    float stopTarget = clearance * 1.4f;
                    target = Mathf.Min(target, stopTarget);
                    safeTravel = Mathf.Min(safeTravel, clearance);
                }
            }

            // Pedestrian / amoeba emergency braking and close proximity bumper safety
            if (Blocked(out float pd, Mathf.Max(9f, Speed * dt + 2f)))
            {
                target = Mathf.Min(target, Mathf.Max(0f, (pd - 1.5f) * 1.4f));
                safeTravel = Mathf.Min(safeTravel, Mathf.Max(0f, pd - 0.5f));
            }

            Speed = Mathf.MoveTowards(Speed, target, (target < Speed ? 14f : 4f) * dt);
            float travel = Mathf.Min(Speed * dt, safeTravel);
            // Clamp the reported/wheel speed too, so a blocked car does not spin its tyres while standing still.
            Speed = travel / dt;
            Distance += travel;
            if (Distance >= lane.Length) Distance -= lane.Length;
            Vector3 prev = transform.position;
            lane.Sample(Distance, out var p, out var d);
            transform.position = p;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 1f - Mathf.Exp(-6f * dt));
            if ((p - prev).sqrMagnitude > 25f) transform.rotation = Quaternion.LookRotation(d); // wrapped around
            bool visible = Distance > 7f && Distance < lane.Length - 7f;
            if (visible != shown) { shown = visible; foreach (var r in renderers) r.enabled = visible; box.enabled = visible; }
            float spin = Speed * dt / 0.34f * Mathf.Rad2Deg;
            if (wheels != null) foreach (var w in wheels) w.Rotate(spin, 0f, 0f, Space.Self);
        }

        // ------------------------------------------------------------------ procedural body
        public static TrafficCar Build(Transform parent, Color paint, int seed)
        {
            var rnd = new System.Random(seed);
            var go = new GameObject("TrafficCar");
            go.transform.SetParent(parent, false);
            var car = go.AddComponent<TrafficCar>();
            var body = new GameObject("Body").transform;
            body.SetParent(go.transform, false);
            bool van = rnd.NextDouble() < 0.2, taxi = !van && rnd.NextDouble() < 0.2;
            if (taxi) paint = new Color(0.95f, 0.72f, 0.05f);
            var paintM = MatFactory.Lit(paint, 0.82f, 0.45f);
            var glass = MatFactory.Lit(new Color(0.04f, 0.05f, 0.06f), 0.95f, 0.2f);
            var trim = MatFactory.Lit(new Color(0.03f, 0.03f, 0.03f), 0.4f);
            var chrome = MatFactory.Lit(new Color(0.8f, 0.8f, 0.82f), 0.9f, 1f);
            var tyre = MatFactory.Lit(new Color(0.03f, 0.03f, 0.03f), 0.15f);
            var head = MatFactory.Emissive(new Color(1f, 0.95f, 0.8f), new Color(1f, 0.92f, 0.75f) * 2.2f);
            var tail = MatFactory.Emissive(new Color(0.5f, 0.02f, 0.02f), new Color(1f, 0.05f, 0.03f) * 1.6f);
            float L = van ? 4.9f : 4.4f, W = 1.85f;
            Prim.Make(PrimitiveType.Cube, body, new Vector3(0, 0.62f, 0), new Vector3(W, 0.62f, L), paintM);
            Prim.Make(PrimitiveType.Cube, body, new Vector3(0, 0.36f, 0), new Vector3(W + 0.02f, 0.16f, L - 0.2f), trim);
            if (van)
            {
                Prim.Make(PrimitiveType.Cube, body, new Vector3(0, 1.38f, -0.35f), new Vector3(W - 0.05f, 0.95f, L - 1.2f), paintM);
                Prim.Make(PrimitiveType.Cube, body, new Vector3(0, 1.38f, L * 0.5f - 0.95f), new Vector3(W - 0.15f, 0.6f, 0.08f), glass, false, null, Quaternion.Euler(-12, 0, 0));
            }
            else
            {
                Prim.Make(PrimitiveType.Cube, body, new Vector3(0, 1.17f, -0.25f), new Vector3(W - 0.18f, 0.52f, 2.2f), glass);
                Prim.Make(PrimitiveType.Cube, body, new Vector3(0, 1.45f, -0.3f), new Vector3(W - 0.24f, 0.06f, 1.8f), paintM);
                Prim.Make(PrimitiveType.Cube, body, new Vector3(0, 1.1f, 0.95f), new Vector3(W - 0.2f, 0.06f, 0.75f), glass, false, null, Quaternion.Euler(-28, 0, 0));
            }
            if (taxi)
            {
                Prim.Make(PrimitiveType.Cube, body, new Vector3(0, 1.58f, -0.3f), new Vector3(0.6f, 0.18f, 0.25f), MatFactory.Emissive(Color.white, new Color(1f, 0.9f, 0.5f) * 1.5f));
            }
            foreach (float x in new[] { -0.62f, 0.62f })
            {
                Prim.Make(PrimitiveType.Cube, body, new Vector3(x, 0.72f, L * 0.5f + 0.005f), new Vector3(0.34f, 0.12f, 0.03f), head);
                Prim.Make(PrimitiveType.Cube, body, new Vector3(x, 0.78f, -L * 0.5f - 0.005f), new Vector3(0.32f, 0.12f, 0.03f), tail);
            }
            Prim.Make(PrimitiveType.Cube, body, new Vector3(0, 0.55f, L * 0.5f + 0.01f), new Vector3(0.8f, 0.14f, 0.03f), chrome);
            var ws = new List<Transform>();
            foreach (float x in new[] { -W * 0.5f + 0.08f, W * 0.5f - 0.08f })
                foreach (float z in new[] { L * 0.5f - 0.85f, -L * 0.5f + 0.85f })
                {
                    var pivot = new GameObject("Wheel").transform;
                    pivot.SetParent(body, false);
                    pivot.localPosition = new Vector3(x, 0.34f, z);
                    Prim.Make(PrimitiveType.Cylinder, pivot, Vector3.zero, new Vector3(0.68f, 0.11f, 0.68f), tyre, false, null, Quaternion.Euler(0, 0, 90));
                    Prim.Make(PrimitiveType.Cylinder, pivot, new Vector3(Mathf.Sign(x) * 0.115f, 0, 0), new Vector3(0.42f, 0.01f, 0.42f), chrome, false, null, Quaternion.Euler(0, 0, 90));
                    ws.Add(pivot);
                }
            car.wheels = ws.ToArray();
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.85f, 0); col.size = new Vector3(W, 1.3f, L);
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.None;
            car.renderers = go.GetComponentsInChildren<Renderer>();
            car.box = col;
            foreach (var r in car.renderers) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return car;
        }
    }
}
