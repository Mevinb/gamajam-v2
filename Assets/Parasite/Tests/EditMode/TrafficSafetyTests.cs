using System.Reflection;
using NUnit.Framework;
using Parasite.World;
using UnityEngine;

namespace Parasite.Tests
{
    public class TrafficSafetyTests
    {
        GameObject root;
        Random.State previousRandom;

        [SetUp]
        public void PreserveRandom() => previousRandom = Random.state;

        [TearDown]
        public void Cleanup()
        {
            if (root) Object.DestroyImmediate(root);
            Random.state = previousRandom;
        }

        [TestCase(0.3f, 3.7f)]
        [TestCase(3f, 16f)]
        public void FrameHitchCannotDriveThroughPedestrian(float frameSeconds, float pedestrianDistance)
        {
            root = new GameObject("Traffic safety fixture");
            var lane = new TrafficSystem.Lane(90, 1, new Vector3(800, 0, 800), new Vector3(800, 0, 1000));
            Random.InitState(7);
            var car = TrafficCar.Build(root.transform, Color.gray, 7);
            car.Init(null, lane, 20f);
            lane.cars.Add(car);
            car.enabled = false;
            Vector3 initial = car.transform.position;
            var person = new GameObject("Stationary pedestrian") { layer = Layers.Characters };
            person.transform.SetParent(root.transform);
            person.transform.position = initial + Vector3.forward * pedestrianDistance;
            var capsule = person.AddComponent<CapsuleCollider>();
            capsule.center = Vector3.up * 0.9f;
            capsule.height = 1.8f;
            capsule.radius = 0.3f;
            Physics.SyncTransforms();

            var tick = typeof(TrafficCar).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(tick);
            tick.Invoke(car, new object[] { frameSeconds });
            Physics.SyncTransforms();
            float bumper = car.transform.position.z + car.GetComponent<BoxCollider>().size.z * 0.5f;
            Assert.That(bumper, Is.LessThan(person.transform.position.z - capsule.radius),
                "Displacement must respect physical clearance even before the car finishes decelerating.");

            Object.DestroyImmediate(person);
            Physics.SyncTransforms();
            float stoppedAt = car.Distance;
            tick.Invoke(car, new object[] { 0.3f });
            Assert.That(car.Distance, Is.GreaterThan(stoppedAt), "Traffic resumes when the pedestrian clears the lane.");
        }

        [Test]
        public void YieldingTrafficStopsItsBumperOutsideOccupiedIntersection()
        {
            root = new GameObject("Intersection safety fixture");
            var traffic = TrafficSystem.Create(0);
            traffic.transform.SetParent(root.transform);
            var incoming = TrafficCar.Build(root.transform, Color.gray, 9);
            incoming.Init(traffic, traffic.Lanes[0], 73f); // x=-15, eastbound, five metres before the boundary
            traffic.Lanes[0].cars.Add(incoming);
            var crossing = TrafficCar.Build(root.transform, Color.white, 10);
            crossing.Init(traffic, traffic.Lanes[4], 60f); // Avenue car already crossing Market Street
            traffic.Lanes[4].cars.Add(crossing);
            incoming.enabled = crossing.enabled = false;
            Physics.SyncTransforms();
            Assert.IsTrue(crossing.IsInIntersection(out var intersection));
            typeof(TrafficCar).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(incoming, new object[] { 1f });
            float bumper = incoming.transform.position.x + incoming.GetComponent<BoxCollider>().size.z * 0.5f;
            Assert.That(bumper, Is.LessThanOrEqualTo(intersection.bounds.min.x - 0.59f),
                "A yielding car must keep its whole body outside the crossing, including during a slow frame.");
        }
    }
}
