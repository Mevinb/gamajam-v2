using System.Collections.Generic;
using UnityEngine;

namespace Parasite.World
{
    /// <summary>
    /// Ambient traffic: procedural cars driving fixed lanes (right-hand traffic), braking for people, the amoeba and the
    /// car ahead; they appear/disappear behind the road-closed barriers at the district edge.
    /// </summary>
    public class TrafficSystem : MonoBehaviour
    {
        public static TrafficSystem Instance { get; private set; }

        public class Lane
        {
            public Vector3[] points;
            public readonly List<TrafficCar> cars = new();
            public float Length;
            public Lane(params Vector3[] p)
            {
                points = p;
                for (int i = 1; i < p.Length; i++) Length += Vector3.Distance(p[i - 1], p[i]);
            }

            public void Sample(float s, out Vector3 pos, out Vector3 dir)
            {
                for (int i = 1; i < points.Length; i++)
                {
                    float seg = Vector3.Distance(points[i - 1], points[i]);
                    if (s <= seg || i == points.Length - 1)
                    {
                        dir = (points[i] - points[i - 1]).normalized;
                        pos = Vector3.Lerp(points[i - 1], points[i], Mathf.Clamp01(s / Mathf.Max(0.01f, seg)));
                        return;
                    }
                    s -= seg;
                }
                pos = points[^1]; dir = Vector3.forward;
            }
        }

        public readonly List<Lane> Lanes = new();

        static readonly Color[] Paints =
        {
            new(0.55f, 0.04f, 0.04f), new(0.85f, 0.85f, 0.86f), new(0.05f, 0.05f, 0.06f), new(0.12f, 0.2f, 0.42f),
            new(0.45f, 0.46f, 0.48f), new(0.9f, 0.68f, 0.05f), new(0.1f, 0.3f, 0.2f), new(0.35f, 0.22f, 0.12f),
        };

        public static TrafficSystem Create(int carsPerLane = 2)
        {
            var go = new GameObject("Traffic");
            var t = go.AddComponent<TrafficSystem>();
            t.Build(carsPerLane);
            return t;
        }

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Build(int carsPerLane)
        {
            const float y = 0.02f, edge = 88f;
            Lanes.Add(new Lane(new Vector3(-edge, y, -29.6f), new Vector3(edge, y, -29.6f)));   // Market St eastbound
            Lanes.Add(new Lane(new Vector3(edge, y, -26.4f), new Vector3(-edge, y, -26.4f)));   // Market St westbound
            Lanes.Add(new Lane(new Vector3(-edge, y, 44.4f), new Vector3(edge, y, 44.4f)));     // Campus Rd eastbound
            Lanes.Add(new Lane(new Vector3(edge, y, 47.6f), new Vector3(-edge, y, 47.6f)));     // Campus Rd westbound
            Lanes.Add(new Lane(new Vector3(1.8f, y, -88f), new Vector3(1.8f, y, 44.4f), new Vector3(edge, y, 44.4f)));   // Avenue north → east
            Lanes.Add(new Lane(new Vector3(edge, y, 47.6f), new Vector3(-1.8f, y, 47.6f), new Vector3(-1.8f, y, -88f))); // west → Avenue south
            int seed = 7;
            foreach (var lane in Lanes)
                for (int i = 0; i < carsPerLane; i++)
                {
                    var car = TrafficCar.Build(transform, Paints[(seed++ * 5) % Paints.Length], seed);
                    car.Init(this, lane, lane.Length * (i + Random.Range(0.1f, 0.6f)) / carsPerLane);
                    lane.cars.Add(car);
                }
        }
    }

    public class TrafficCar : MonoBehaviour
    {
        public const float CruiseSpeed = 9f;
        public float Speed { get; private set; }
        public float Distance { get; private set; }
        TrafficSystem.Lane lane;
        Transform[] wheels;
        Renderer[] renderers;
        BoxCollider box;
        bool shown = true;
        float cruise, waitT;
        static readonly RaycastHit[] hits = new RaycastHit[8];

        public void Init(TrafficSystem sys, TrafficSystem.Lane l, float startDistance)
        {
            lane = l; Distance = startDistance;
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

        /// <summary>People, the amoeba, or another lane's car in front of the bumper.</summary>
        bool Blocked(out float dist)
        {
            dist = 0f;
            Vector3 o = transform.position + Vector3.up * 0.8f + transform.forward * 2.5f;
            int n = Physics.SphereCastNonAlloc(o, 1.0f, transform.forward, hits, 9f, ~0, QueryTriggerInteraction.Collide);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                int l = c.gameObject.layer;
                bool person = l == Layers.Characters || l == Layers.Amoeba;
                var other = person ? null : c.GetComponentInParent<TrafficCar>();
                if (!person && (!other || other == this || (waitT > 4f))) continue;
                if (hits[i].distance < best) best = hits[i].distance;
            }
            if (best == float.MaxValue) return false;
            dist = best;
            return true;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float target = cruise;
            float gap = CarAheadGap();
            if (gap < 16f) target = Mathf.Min(target, Mathf.Max(0f, (gap - 7f) * 1.2f));
            if (Blocked(out float pd)) { target = Mathf.Min(target, Mathf.Max(0f, (pd - 1.5f) * 1.4f)); waitT += dt; }
            else waitT = 0f;
            Speed = Mathf.MoveTowards(Speed, target, (target < Speed ? 14f : 4f) * dt);
            Distance += Speed * dt;
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
