using System.Collections;
using NUnit.Framework;
using Parasite.Characters;
using Parasite.Missions;
using Parasite.Possession;
using Parasite.Stealth;
using Parasite.Testing;
using Parasite.World;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Parasite.Tests
{
    public class MovingHostAimTests
    {
        sealed class NearbyInteraction : IInteractable
        {
            public Vector3 InteractPoint { get; set; }
            public float InteractRange => .7f;
            public bool IsAvailable => true;
            public string GetPrompt(in InteractContext ctx) => "Moving-aim fixture interaction";
            public bool CanInteract(in InteractContext ctx, out string reason) { reason = null; return true; }
            public void Interact(in InteractContext ctx) { }
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator LiveOfficerMotionRemainsTargetedThroughNormalCameraControls()
        {
            yield return AutoTest.LoadCity();
            MissionManager.SetFlag(MissionFlags.Confronted);
            float until = Time.realtimeSinceStartup + 3;
            while ((!HunterSquad.Instance || !HunterSquad.Instance.Officer) && Time.realtimeSinceStartup < until) yield return null;
            var officer = HunterSquad.Instance ? HunterSquad.Instance.Officer : null;
            Assert.IsTrue(officer && officer.CanBePossessed);
            Assert.IsTrue(officer.Brain is GuardAI);
            foreach (var host in HostCharacter.All)
                if (host && host != officer && host.Brain is GuardAI guard) guard.Stun(30);

            var pm = PossessionManager.Instance;
            Assert.IsTrue(pm.Active.IsFree);
            Assert.IsTrue(NavMesh.SamplePosition(new Vector3(17.5f, CityLayout.LabFloor, 79.2f), out var stand, .5f, NavMesh.AllAreas));
            // Independent fixture setup only. The actor remains stationary during the actual moving-target aim.
            pm.Active.Blob.Place(stand.position);
            GameInput.SimMove = Vector2.zero; GameInput.SimSprint = false;
            SuspicionSystem.Instance.SetValue(0); SuspicionSystem.Instance.OnSuspectGone();
            yield return null; yield return null;
            yield return AutoTest.AimSettle(officer);
            Assert.AreSame(officer, pm.ComputeTarget().host, "The actual officer must be targetable before the moving control.");

            Assert.IsTrue(NavMesh.SamplePosition(new Vector3(14f, CityLayout.LabFloor, 75.4f), out var destination, .5f, NavMesh.AllAreas));
            var path = new NavMeshPath();
            Assert.IsTrue(NavMesh.CalculatePath(officer.transform.position, destination.position, NavMesh.AllAreas, path));
            Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status);
            Vector3 initialChest = officer.ChestPosition, initialBody = pm.Active.Position;
            ((GuardAI)officer.Brain).OrderInvestigate(destination.position);
            float began = Time.realtimeSinceStartup;
            yield return AutoTest.AimSettle(officer);
            float elapsed = Time.realtimeSinceStartup - began;
            var cam = PlayerCamera.Instance;
            float moved = Vector3.Distance(initialChest, officer.ChestPosition);
            float angle = Vector3.Angle(cam.Forward, officer.ChestPosition - cam.Position);
            float staleAngle = Vector3.Angle(cam.Forward, initialChest - cam.Position);
            float range = Vector3.Distance(pm.ActiveBody.EyePosition, officer.ChestPosition);
            bool los = pm.LineOfSight(pm.ActiveBody.EyePosition, officer.ChestPosition, pm.ActiveBody.Root, officer.transform);
            var target = pm.ComputeTarget();
            TestContext.WriteLine($"[MovingOfficerAim] moved={moved:F4} elapsed={elapsed:F4} actualAngle={angle:F4} stalePointAngle={staleAngle:F4} range={range:F4} los={los} pitch={cam.Pitch:F4} target={target.kind}/{(target.host ? target.host.DisplayName : "-")} initialChest={initialChest:F4} actualChest={officer.ChestPosition:F4}");
            Assert.Greater(moved, .4f, "The real NavMesh guard must move during the unchanged settling interval.");
            Assert.Greater(staleAngle, PossessionManager.AimConeDegrees, "The old snapshot point must diverge far enough to exercise live tracking.");
            Assert.That(elapsed, Is.InRange(.75f, 1.1f));
            Assert.Less(Vector2.Distance(new Vector2(initialBody.x, initialBody.z), new Vector2(pm.Active.Position.x, pm.Active.Position.z)), .05f,
                "Aiming must not move or warp the player to manufacture spacing.");
            Assert.That(cam.Pitch, Is.InRange(-25f, 65f));
            Assert.LessOrEqual(angle, PossessionManager.AimConeDegrees); Assert.LessOrEqual(range, PossessionManager.PossessRange); Assert.IsTrue(los);
            Assert.AreEqual(TargetKind.Possess, target.kind); Assert.AreSame(officer, target.host);
            GameInput.SimInteract = true; yield return null; GameInput.SimInteract = false;
            until = Time.realtimeSinceStartup + PossessionManager.LaunchSeconds + 1;
            while (pm.Active.Host != officer && Time.realtimeSinceStartup < until) yield return null;
            Assert.AreSame(officer, pm.Active.Host, "Normal E input must possess the same moving officer after the real target check.");
            Assert.AreSame(pm.Active, officer.Possessor);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator CrowdingInvestigatingOfficerRequiresRealRetreatBeforeAimAndE()
        {
            yield return AutoTest.LoadCity();
            MissionManager.SetFlag(MissionFlags.Confronted);
            float until = Time.realtimeSinceStartup + 3;
            while ((!HunterSquad.Instance || !HunterSquad.Instance.Officer) && Time.realtimeSinceStartup < until) yield return null;
            var officer = HunterSquad.Instance ? HunterSquad.Instance.Officer : null;
            Assert.IsTrue(officer && officer.CanBePossessed && officer.Brain is GuardAI);
            foreach (var host in HostCharacter.All)
                if (host && host != officer && host.Brain is GuardAI guard) guard.Stun(30);
            var pm = PossessionManager.Instance;
            Assert.IsTrue(pm.Active.IsFree);
            Assert.IsTrue(NavMesh.SamplePosition(new Vector3(17.134f, CityLayout.LabFloor, 80.27f), out var stand, .5f, NavMesh.AllAreas));
            pm.Active.Blob.Place(stand.position); // Initial independent fixture placement only.
            GameInput.SimMove = Vector2.zero; GameInput.SimSprint = false;
            SuspicionSystem.Instance.SetValue(0); SuspicionSystem.Instance.OnSuspectGone();
            Assert.IsTrue(NavMesh.SamplePosition(new Vector3(16.8f, CityLayout.LabFloor, 82f), out var goal, .5f, NavMesh.AllAreas));
            ((GuardAI)officer.Brain).OrderInvestigate(goal.position);
            until = Time.realtimeSinceStartup + 4;
            while (Vector3.Distance(pm.ActiveBody.EyePosition, officer.ChestPosition) > 2f && Time.realtimeSinceStartup < until) yield return null;
            yield return AutoTest.AimSettle(officer);
            var cam = PlayerCamera.Instance;
            float closeRange = Vector3.Distance(pm.ActiveBody.EyePosition, officer.ChestPosition);
            float blockedAngle = Vector3.Angle(cam.Forward, officer.ChestPosition - cam.Position);
            bool closeLos = pm.LineOfSight(pm.ActiveBody.EyePosition, officer.ChestPosition, pm.ActiveBody.Root, officer.transform);
            TestContext.WriteLine($"[CrowdedOfficerAim] before retreat range={closeRange:F4} angle={blockedAngle:F4} pitch={cam.Pitch:F4} los={closeLos} officer={officer.transform.position:F4} body={pm.Active.Position:F4}");
            Assert.Less(closeRange, 2f); Assert.IsTrue(closeLos);
            Assert.Greater(blockedAngle, PossessionManager.AimConeDegrees, "Real crowding must reproduce the upward-clamp limit before retreat.");
            Assert.That(cam.Pitch, Is.EqualTo(-25f).Within(.2f));
            string hint = "Step back to aim at " + officer.DisplayName;
            var closeTarget = pm.ComputeTarget();
            Assert.AreEqual(TargetKind.None, closeTarget.kind); Assert.IsFalse(closeTarget.allowed);
            Assert.AreSame(officer, closeTarget.host); Assert.AreEqual(hint, closeTarget.prompt);
            Assert.IsNull(pm.Active.Host, "Guidance must not turn an illegal aim into possession.");

            var nearby = new NearbyInteraction { InteractPoint = pm.Active.Position };
            Interactables.Register(nearby);
            try
            {
                var interaction = pm.ComputeTarget();
                Assert.AreEqual(TargetKind.Interact, interaction.kind); Assert.AreSame(nearby, interaction.interactable);
                Assert.IsTrue(interaction.allowed, "A real nearby interaction keeps priority over denied close-aim guidance.");
            }
            finally { Interactables.Unregister(nearby); }

            var occluder = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                occluder.name = "Owned close-aim LOS control";
                occluder.GetComponent<Renderer>().enabled = false;
                Vector3 eye = pm.ActiveBody.EyePosition, chest = officer.ChestPosition;
                occluder.transform.SetPositionAndRotation((eye + chest) * .5f, Quaternion.LookRotation(chest - eye));
                occluder.transform.localScale = new Vector3(.8f, .8f, .1f);
                var wall = occluder.GetComponent<Collider>();
                // The owned query occluder must not push either participant while testing actual vision.
                foreach (var collider in officer.GetComponentsInChildren<Collider>()) if (collider.enabled) Physics.IgnoreCollision(wall, collider);
                foreach (var collider in pm.Active.Blob.GetComponentsInChildren<Collider>()) if (collider.enabled) Physics.IgnoreCollision(wall, collider);
                Physics.SyncTransforms();
                Assert.IsFalse(pm.LineOfSight(eye, officer.ChestPosition, pm.ActiveBody.Root, officer.transform));
                var hidden = pm.ComputeTarget();
                Assert.AreNotSame(officer, hidden.host); Assert.AreNotEqual(hint, hidden.prompt, "Guidance cannot reveal a host through an actual occluder.");
            }
            finally
            {
                occluder.GetComponent<Collider>().enabled = false; Object.Destroy(occluder); Physics.SyncTransforms();
            }
            cam.SetYaw(cam.Yaw + 180f); yield return null; yield return null;
            var turnedAway = pm.ComputeTarget();
            Assert.AreNotSame(officer, turnedAway.host); Assert.AreNotEqual(hint, turnedAway.prompt, "A host behind the view cannot leak its close-aim guidance.");
            yield return AutoTest.AimSettle(officer);
            var restoredHint = pm.ComputeTarget();
            Assert.AreEqual(hint, restoredHint.prompt); Assert.AreEqual(TargetKind.None, restoredHint.kind); Assert.IsFalse(restoredHint.allowed);
            TestContext.WriteLine("[CrowdedOfficerAim] denied hint, nearby-interaction priority, real occlusion and turn-away negatives, then live-facing hint restoration all pass");
            Vector3 before = pm.Active.Position;
            float deadline = Time.realtimeSinceStartup + 12f;
            yield return AutoTest.EnsureHunterAimSpacing(officer, deadline);
            float moved = Vector3.Distance(before, pm.Active.Position);
            yield return AutoTest.AimSettle(officer);
            float range = Vector3.Distance(pm.ActiveBody.EyePosition, officer.ChestPosition);
            float angle = Vector3.Angle(cam.Forward, officer.ChestPosition - cam.Position);
            var target = pm.ComputeTarget();
            TestContext.WriteLine($"[CrowdedOfficerAim] after retreat moved={moved:F4} range={range:F4} angle={angle:F4} pitch={cam.Pitch:F4} remaining={deadline-Time.realtimeSinceStartup:F4} target={target.kind}/{(target.host ? target.host.DisplayName : "-")} officer={officer.transform.position:F4} body={pm.Active.Position:F4}");
            Assert.Greater(moved, 1f, "The free body must physically retreat; changing camera/range rules cannot satisfy this.");
            Assert.LessOrEqual(range, PossessionManager.PossessRange); Assert.LessOrEqual(angle, PossessionManager.AimConeDegrees);
            Assert.IsTrue(pm.LineOfSight(pm.ActiveBody.EyePosition, officer.ChestPosition, pm.ActiveBody.Root, officer.transform));
            Assert.AreEqual(TargetKind.Possess, target.kind); Assert.AreSame(officer, target.host);
            GameInput.SimInteract = true; yield return null; GameInput.SimInteract = false;
            until = Time.realtimeSinceStartup + PossessionManager.LaunchSeconds + 1;
            while (pm.Active.Host != officer && Time.realtimeSinceStartup < until) yield return null;
            Assert.AreSame(officer, pm.Active.Host); Assert.AreSame(pm.Active, officer.Possessor);
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            GameInput.SimInteract = false; GameInput.SimMove = Vector2.zero; GameInput.Simulated = false;
            Time.timeScale = 1; RunState.NewGame();
            SceneManager.LoadScene(GameManager.MenuScene); yield return null;
        }
    }
}
