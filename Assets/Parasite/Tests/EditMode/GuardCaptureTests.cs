using System.Reflection;
using NUnit.Framework;
using Parasite.Characters;
using Parasite.Possession;
using Parasite.Stealth;
using UnityEngine;

namespace Parasite.Tests
{
    public class GuardCaptureTests
    {
        GameObject root;
        PossessionManager oldPossession;
        SuspicionSystem oldSuspicion;
        Random.State oldRandom;
        int oldCaptures, oldHosts;
        float oldPeak;

        [SetUp]
        public void SaveState()
        {
            oldPossession = PossessionManager.Instance;
            oldSuspicion = SuspicionSystem.Instance;
            oldCaptures = RunState.Captures; oldHosts = RunState.HostsPossessed; oldPeak = RunState.PeakSuspicion;
            oldRandom = Random.state;
        }

        [TearDown]
        public void Cleanup()
        {
            if (root) Object.DestroyImmediate(root);
            SetInstance(typeof(PossessionManager), oldPossession);
            SetInstance(typeof(SuspicionSystem), oldSuspicion);
            RunState.Captures = oldCaptures; RunState.HostsPossessed = oldHosts; RunState.PeakSuspicion = oldPeak;
            Random.state = oldRandom;
        }

        static void SetInstance(System.Type type, Object instance) =>
            type.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public).SetValue(null, instance);

        HostCharacter MakeHost(string name, Profession profession, Vector3 position)
        {
            var go = new GameObject(name); go.transform.SetParent(root.transform); go.transform.position = position;
            go.AddComponent<CharacterController>();
            HostBrain brain = profession == Profession.Guard ? go.AddComponent<GuardAI>() : go.AddComponent<HostBrain>();
            var host = go.AddComponent<HostCharacter>();
            host.Init(name, profession, false, 741);
            brain.Setup(host, null);
            return host;
        }

        [TestCase(true)]
        [TestCase(false)]
        public void CaptureUsesCurrentCoverInsteadOfCachedPursuitVisibility(bool addCover)
        {
            root = new GameObject("Guard capture regression");
            var possession = root.AddComponent<PossessionManager>();
            var suspicion = root.AddComponent<SuspicionSystem>();
            SetInstance(typeof(PossessionManager), possession);
            SetInstance(typeof(SuspicionSystem), suspicion);

            var origin = new Vector3(800, 0, 800);
            var guard = MakeHost("Capture test guard", Profession.Guard, origin);
            var target = MakeHost("Borrowed test civilian", Profession.Civilian, origin + Vector3.forward * 2f);
            var agent = new AmoebaAgent { Id = 1, Host = target };
            possession.Agents.Add(agent);
            target.BeginPossession(agent);
            suspicion.ReportDirect(ViolationType.WitnessedPossession, target.ChestPosition, target, 100f, "Regression witness");
            Physics.SyncTransforms();

            var brain = (GuardAI)guard.Brain;
            brain.Tick(0.001f); // establish visible pursuit outside capture range
            Assert.AreEqual(GuardState.Pursue, brain.GState);
            Assert.AreEqual(HostState.Possessed, target.State);

            if (addCover)
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.SetParent(root.transform);
                wall.transform.position = origin + new Vector3(0, 1.5f, 0.65f);
                wall.transform.localScale = new Vector3(3, 3, 0.2f);
            }
            target.transform.position = origin + Vector3.forward * 1.3f;
            Physics.SyncTransforms();
            brain.Tick(0.01f); // intentionally before the next throttled visibility sample

            Assert.AreEqual(addCover ? HostState.Possessed : HostState.Captured, target.State,
                addCover ? "Crossing behind cover must prevent a grab immediately, without waiting for the next vision scan."
                    : "A visible borrowed host in reach must still be captured through the normal pursuit flow.");
            Assert.AreEqual(!addCover, agent.CapturedPendingEject);
        }
    }
}
