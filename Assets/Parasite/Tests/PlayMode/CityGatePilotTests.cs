using System.Collections;
using System.Linq;
using NUnit.Framework;
using Parasite.Missions;
using Parasite.Testing;
using Parasite.World;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Parasite.Tests
{
    /// <summary>Real loaded scene/PhysX and the original AccessDoor component. No visual/performance claim.</summary>
    public sealed class CityGatePilotTests
    {
        [UnityTest, Timeout(90000)]
        public IEnumerator VisualChildFollowsOriginalDoorWhilePhysicsAndApertureRemainIntact()
        {
            yield return AutoTest.LoadCity();
            var door = Object.FindObjectsByType<AccessDoor>(FindObjectsSortMode.None).Single(d => d.doorName == "Main Gate");
            var panel = door.panel; var collider = panel.GetComponent<BoxCollider>(); var obstacle = panel.GetComponent<NavMeshObstacle>();
            var visual = panel.Find("CityGatePilot_Moving");
            Assert.IsNotNull(visual, "The regenerated scene must contain the actual imported pilot, not its safe fallback.");
            Assert.IsNotNull(collider); Assert.IsNotNull(obstacle); Assert.IsTrue(obstacle.carving);
            Assert.IsTrue(collider.enabled); Assert.IsFalse(collider.isTrigger);
            Assert.That(Vector3.Distance(panel.localPosition, new(0, 1.5f, 0)), Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(panel.localScale, new(2.9f, 3, .2f)), Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(door.transform.position, CityLayout.MainGate), Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(door.openLocalOffset, new(3.05f, 0, 0)), Is.LessThan(.0001f));
            Assert.That(door.openSeconds, Is.EqualTo(1.1f).Within(.0001f));
            Assert.That(Vector3.Distance(door.interactPoint.localPosition, new(0, 1.2f, -.45f)), Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(collider.center, Vector3.zero), Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(collider.size, Vector3.one), Is.LessThan(.0001f));
            Assert.IsFalse(panel.GetComponent<MeshRenderer>().enabled);
            Assert.AreEqual(0, visual.GetComponentsInChildren<Collider>(true).Length);
            var renderer = visual.GetComponentsInChildren<MeshRenderer>(true).Single();
            Assert.AreEqual("CityGatePilot_SharedPBR", renderer.sharedMaterial.name);
            Assert.That(Vector3.Distance(Vector3.Scale(panel.localScale, visual.localScale), Vector3.one), Is.LessThan(.0001f));
            var warning = panel.GetComponentsInChildren<MeshRenderer>(true).Single(r => r.sharedMaterials.Any(m => m && m.name.Contains("WarningLight")));
            Assert.IsTrue(warning.enabled);
            Assert.That(Vector3.Distance(warning.transform.localPosition, new(0, 1.25f / 3, -.12f / .2f)), Is.LessThan(.0001f));
            Physics.SyncTransforms();
            AssertPortal(door, collider, true);
            var leafRelative = door.transform.InverseTransformPoint(renderer.bounds.center) - panel.localPosition;
            var originalScale = panel.localScale; var originalColliderSize = collider.size; var originalColliderCenter = collider.center;
            var originalWarnings = warning.transform.localToWorldMatrix;
            int samples = 0; door.Open(); float began = Time.realtimeSinceStartup, deadline = began + 4;
            while (Time.realtimeSinceStartup < deadline && Vector3.Distance(panel.localPosition, new(3.05f, 1.5f, 0)) > .001f)
            {
                Assert.AreSame(collider, panel.GetComponent<BoxCollider>()); Assert.AreSame(obstacle, panel.GetComponent<NavMeshObstacle>());
                Assert.AreEqual(originalScale, panel.localScale); Assert.AreEqual(originalColliderSize, collider.size); Assert.AreEqual(originalColliderCenter, collider.center);
                Assert.That(Vector3.Distance(door.transform.InverseTransformPoint(renderer.bounds.center) - panel.localPosition, leafRelative), Is.LessThan(.002f), "The actual renderer follows its unchanged parent without stretching or an extra animation.");
                samples++; yield return null;
            }
            Assert.That(samples, Is.GreaterThan(2)); Assert.IsTrue(door.IsOpen);
            Assert.That(Vector3.Distance(panel.localPosition, new(3.05f, 1.5f, 0)), Is.LessThan(.001f));
            Assert.IsFalse(obstacle.enabled);
            Physics.SyncTransforms(); AssertPortal(door, collider, false);
            var path = new NavMeshPath();
            float navUntil = Time.realtimeSinceStartup + 4;
            while ((!NavMesh.CalculatePath(CityLayout.MainGateOutside, CityLayout.Forecourt, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) && Time.realtimeSinceStartup < navUntil) yield return null;
            Assert.IsTrue(NavMesh.CalculatePath(CityLayout.MainGateOutside, CityLayout.Forecourt, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete, "The existing navigation surface connects through the open main gate.");
            float length = 0; for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            Assert.That(length, Is.LessThan(12), "The route crosses the open main gate instead of detouring around the district.");
            door.Close(); deadline = Time.realtimeSinceStartup + 4;
            while (Time.realtimeSinceStartup < deadline && Vector3.Distance(panel.localPosition, new(0, 1.5f, 0)) > .001f) yield return null;
            Assert.IsFalse(door.IsOpen); Assert.IsTrue(obstacle.enabled); Assert.IsTrue(collider.enabled);
            Assert.That(Vector3.Distance(panel.localPosition, new(0, 1.5f, 0)), Is.LessThan(.001f));
            Physics.SyncTransforms(); AssertPortal(door, collider, true);
            for (int i = 0; i < 16; i++) Assert.That(Mathf.Abs(warning.transform.localToWorldMatrix[i] - originalWarnings[i]), Is.LessThan(.001f));
            Debug.Log("[CityGatePilot] PASS real loaded imported leaf, same original panel/collider/carving/interaction point/warning light; physical closed/open/reclosed aperture, original parent motion and open navigation. Native appearance, role access and performance are separate gates.");
        }
        static void AssertPortal(AccessDoor door, BoxCollider panel, bool closed)
        {
            int count = 0;
            for (int x = 0; x < 9; x++) for (int y = 0; y < 11; y++)
            {
                var origin = door.transform.TransformPoint(new Vector3(-1.1f + x * .275f, .15f + y * .27f, -.8f));
                var hits = Physics.RaycastAll(origin, door.transform.forward, 1.6f, ~0, QueryTriggerInteraction.Ignore);
                if (closed) Assert.IsTrue(hits.Any(h => h.collider == panel), "Closed original panel must physically block the authored passage.");
                else Assert.IsFalse(hits.Any(h => h.collider && !h.collider.GetComponentInParent<Parasite.Characters.HostCharacter>()), "Open passage must not gain an invisible static blocker.");
                count++;
            }
            Assert.AreEqual(99, count);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            GameInput.Simulated = false; GameInput.SimMove = Vector2.zero;
            GameInput.SimInteract = GameInput.SimLeave = GameInput.SimSplit = GameInput.SimSwitch = GameInput.SimJump = GameInput.SimSprint = false;
            Time.timeScale = 1; RunState.NewGame(); SceneManager.LoadScene(GameManager.MenuScene); yield return null;
        }
    }
}
