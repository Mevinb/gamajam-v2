using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Parasite.Possession;
using Parasite.Testing;
using Parasite.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Parasite.Tests
{
    public sealed class BodyLifetimeTests
    {
        [UnityTest, Timeout(90000)]
        public IEnumerator ActualActiveBlobDestructionFailsClosedUntilNormalCheckpointRecovery()
        {
            yield return AutoTest.LoadCity();
            var manager = GameManager.Instance; var pm = PossessionManager.Instance;
            Assert.That(manager && manager.IsPlaying && pm && pm.Active != null, Is.True);
            var agent = pm.Active; var blob = agent.Blob; IControllable originalBody = pm.ActiveBody;
            int count = pm.Agents.Count, resets = RunState.Resets, captures = RunState.Captures, hosts = RunState.HostsPossessed, mission = RunState.MissionIndex;
            var flags = new HashSet<string>(RunState.Flags);
            Assert.That(count, Is.EqualTo(1)); Assert.That(blob && blob.gameObject.activeInHierarchy, Is.True);
            Assert.That(ReferenceEquals(originalBody, blob), Is.True);
            Object.Destroy(blob.gameObject);
            yield return null; yield return null; // Actual destruction plus audio/HUD/mission/input updates; no expected-log suppression.
            Assert.That(blob == null, Is.True);
            Assert.That(ReferenceEquals(originalBody, null), Is.False, "The retained interface demonstrates why a CLR null check alone was unsafe.");
            Assert.That(ReferenceEquals(agent.Body, null), Is.True);
            Assert.That(ReferenceEquals(pm.ActiveBody, null), Is.True);
            Assert.That(pm.ComputeTarget().kind, Is.EqualTo(TargetKind.None));
            Assert.That(pm.TrySplit(), Is.False);
            Assert.That(pm.Agents.Count, Is.EqualTo(count)); Assert.That(ReferenceEquals(pm.Active, agent), Is.True);
            Assert.That(ReferenceEquals(agent.Blob, blob), Is.True, "This fix does not prune/rebind the registry or invent a replacement body.");
            Assert.That(manager.IsPlaying && RunState.Resets == resets && RunState.Captures == captures && RunState.HostsPossessed == hosts
                && RunState.MissionIndex == mission && flags.SetEquals(RunState.Flags), Is.True, "Body loss grants no new objective, capture, reset or host ownership.");
            LogAssert.NoUnexpectedReceived();

            manager.ResetToCheckpoint("Body lifetime fixture uses the normal checkpoint");
            Assert.That(manager.State, Is.EqualTo(GameState.Resetting)); Assert.That(RunState.Resets, Is.EqualTo(resets + 1));
            float deadline = Time.realtimeSinceStartup + 30;
            while ((GameManager.Instance == manager || !CityDirector.Instance || !CityDirector.Instance.Ready)
                && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(GameManager.Instance && GameManager.Instance != manager && CityDirector.Instance && CityDirector.Instance.Ready, Is.True);
            yield return null; yield return null;
            var restored = PossessionManager.Instance;
            Assert.That(restored && restored != pm && restored.Agents.Count == 1 && restored.Active != null, Is.True);
            Assert.That(ReferenceEquals(restored.Active, agent), Is.False);
            Assert.That(restored.Agents.Contains(agent), Is.False);
            Assert.That(restored.Active.IsFree && restored.Active.Blob && restored.Active.Blob.Controller.enabled, Is.True);
            Assert.That(ReferenceEquals(restored.ActiveBody, restored.Active.Blob), Is.True);
            Assert.That(RunState.Resets, Is.EqualTo(resets + 1));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void LiveHiddenAndDisabledBlobRetainsTheSameBodyIdentity()
        {
            var agent = new AmoebaAgent { Id = 91 }; AmoebaController blob = null;
            try
            {
                blob = AmoebaController.Create(new Vector3(20000, 5000, 20000), agent); IControllable body = blob;
                Assert.That(ReferenceEquals(agent.Body, body), Is.True);
                blob.enabled = false; blob.Controller.enabled = false;
                Assert.That(ReferenceEquals(agent.Body, body), Is.True, "Intentional component/controller disabling is not object destruction.");
                blob.gameObject.SetActive(false);
                Assert.That(ReferenceEquals(agent.Body, body), Is.True, "A live dormant free body must remain available during possession/transit.");
                blob.gameObject.SetActive(true); blob.enabled = true; blob.Controller.enabled = true;
                Assert.That(ReferenceEquals(agent.Body, body), Is.True);
            }
            finally { if (blob) { blob.gameObject.SetActive(false); Object.DestroyImmediate(blob.gameObject); } Physics.SyncTransforms(); }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            GameInput.Simulated = false; GameInput.SimMove = Vector2.zero;
            GameInput.SimInteract = GameInput.SimLeave = GameInput.SimSplit = GameInput.SimSwitch = GameInput.SimJump = GameInput.SimSprint = GameInput.SimAbility = false;
            Time.timeScale = 1; RunState.NewGame(); SceneManager.LoadScene(GameManager.MenuScene); yield return null;
        }
    }
}
