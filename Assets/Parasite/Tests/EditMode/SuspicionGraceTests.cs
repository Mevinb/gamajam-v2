using System.Reflection;
using NUnit.Framework;
using Parasite.Characters;
using Parasite.Possession;
using Parasite.Stealth;
using UnityEngine;

namespace Parasite.Tests
{
    public class SuspicionGraceTests
    {
        GameObject root;
        HostCharacter witness, body;
        PossessionManager oldPossession;
        SuspicionSystem oldSuspicion;
        float oldPeak;

        [SetUp]
        public void SaveState()
        {
            oldPossession = PossessionManager.Instance;
            oldSuspicion = SuspicionSystem.Instance;
            oldPeak = RunState.PeakSuspicion;
        }

        [TearDown]
        public void Cleanup()
        {
            HostCharacter.All.Remove(witness); HostCharacter.All.Remove(body);
            if (root) Object.DestroyImmediate(root);
            SetInstance(typeof(PossessionManager), oldPossession);
            SetInstance(typeof(SuspicionSystem), oldSuspicion);
            RunState.PeakSuspicion = oldPeak;
        }

        static void SetInstance(System.Type type, Object instance) =>
            type.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public).SetValue(null, instance);

        HostCharacter MakeHost(string name, Vector3 position)
        {
            var go = new GameObject(name) { layer = Layers.Characters };
            go.transform.SetParent(root.transform); go.transform.position = position;
            var host = go.AddComponent<HostCharacter>();
            if (!HostCharacter.All.Contains(host)) HostCharacter.All.Add(host);
            return host;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EjectionGraceDefersBodyDiscoveryUntilWitnessCanReportIt(bool cctvWitness)
        {
            root = new GameObject("Suspicion grace regression");
            var possession = root.AddComponent<PossessionManager>();
            var suspicion = root.AddComponent<SuspicionSystem>();
            SetInstance(typeof(PossessionManager), possession);
            SetInstance(typeof(SuspicionSystem), suspicion);
            var origin = new Vector3(900, 0, 900);
            witness = MakeHost("Awake witness", origin);
            body = MakeHost("Unconscious borrowed host", origin + Vector3.forward * 3f);
            // Rendering/animation is irrelevant to this vision fixture; set the discovery state directly.
            typeof(HostCharacter).GetProperty("State").SetValue(body, HostState.Unconscious);
            Component scanner = suspicion;
            if (cctvWitness)
            {
                var cameraObject = new GameObject("CCTV witness"); cameraObject.transform.SetParent(root.transform);
                cameraObject.transform.position = origin + Vector3.up * 1.62f;
                cameraObject.transform.rotation = Quaternion.LookRotation(body.transform.position + Vector3.up * 0.3f - cameraObject.transform.position);
                var camera = cameraObject.AddComponent<SecurityCamera>(); camera.head = cameraObject.transform;
                scanner = camera;
            }
            Physics.SyncTransforms();
            suspicion.OnAmoebaEjected(null, origin);
            Assert.That(suspicion.GraceRemaining, Is.GreaterThan(0f));
            var scan = scanner.GetType().GetMethod("Scan", BindingFlags.Instance | BindingFlags.NonPublic);
            scan.Invoke(scanner, new object[] { 0.2f });
            Assert.IsFalse(body.KnockoutDiscovered, "Suppressed reports must not consume the body's one-time discovery.");
            Assert.AreEqual(0f, suspicion.Value);

            // Advance only the grace deadline, without depending on Editor frame timing.
            typeof(SuspicionSystem).GetField("graceUntil", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(suspicion, float.NegativeInfinity);
            scan.Invoke(scanner, new object[] { 0.2f });
            Assert.IsTrue(body.KnockoutDiscovered, "The same visible body must become discoverable after grace expires.");
            Assert.That(suspicion.Value, Is.GreaterThan(0f));
        }
    }
}
