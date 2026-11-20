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

}
