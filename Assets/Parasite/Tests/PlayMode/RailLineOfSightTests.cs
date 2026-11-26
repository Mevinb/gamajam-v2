using System.Linq;
using NUnit.Framework;
using Parasite.Missions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Parasite.Tests
{
    public sealed class RailLineOfSightTests
    {
        static readonly Vector3 Origin = new(20000, 5000, 20000);

        static Rigidbody Compound(Transform parent, string name)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var body = go.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false; body.detectCollisions = true;
            return body;
        }
        static BoxCollider Box(Transform parent, string name, Vector3 position, Vector3 size)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = position;
            var collider = go.AddComponent<BoxCollider>(); collider.size = size; return collider;
        }
        static RaycastHit[] Hits(Vector3 start, Vector3 end)
        {
            Physics.SyncTransforms(); Vector3 delta = end - start;
            return Physics.RaycastAll(start, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance).ToArray();
        }
        static void Cleanup(GameObject root)
        {
            if (root) { root.SetActive(false); Object.DestroyImmediate(root); }
            Physics.SyncTransforms();
        }

        [Test]
        public void AllowedChildColliderOnKinematicBodyRetainsItsOwnSightIdentity()
        {
            var root = new GameObject("Rail LOS child target fixture");
            try
            {
                root.transform.position = Origin;
                var coach = Compound(root.transform, "Actual kinematic coach");
                var target = Box(coach.transform, "Allowed control child", new Vector3(3, 1, 0), new Vector3(.6f, .8f, .5f));
                Vector3 start = Origin + new Vector3(-3, 1, 0), end = target.transform.position;
                var hits = Hits(start, end);
                Assert.That(hits.Any(h => h.collider == target), Is.True, "The real ray must hit the target's physical child box.");
                var hit = hits.Single(h => h.collider == target);
                Assert.That(target.attachedRigidbody, Is.EqualTo(coach));
                Assert.That(hit.rigidbody, Is.EqualTo(coach));
                Assert.That(hit.transform, Is.EqualTo(coach.transform));
                Assert.That(hit.collider.transform, Is.EqualTo(target.transform));
                Assert.That(hit.transform.IsChildOf(target.transform), Is.False);
                Assert.That(RailCampaign.ClearLine(start, end, null, target.transform), Is.True,
                    "The allowed child must be recognized without allowing the whole coach hierarchy.");
            }
            finally { Cleanup(root); }
        }

        [Test]
        public void SiblingWallOnTheSameRigidbodyStillBlocksTheChildControl()
        {
            var root = new GameObject("Rail LOS same-body sibling fixture");
            try
            {
                root.transform.position = Origin;
                var coach = Compound(root.transform, "Actual kinematic coach");
                var target = Box(coach.transform, "Allowed control child", new Vector3(3, 1, 0), new Vector3(.6f, .8f, .5f));
                var wall = Box(coach.transform, "Real sibling wall", new Vector3(0, 1, 0), new Vector3(.2f, 2, 2));
                Vector3 start = Origin + new Vector3(-3, 1, 0), end = target.transform.position;
                var hits = Hits(start, end);
                Assert.That(wall.attachedRigidbody, Is.EqualTo(coach));
                Assert.That(wall.transform.IsChildOf(target.transform), Is.False);
                Assert.That(hits.Any(h => h.collider == wall), Is.True);
                Assert.That(hits.Any(h => h.collider == target), Is.True);
                Assert.That(hits.Single(h => h.collider == wall).transform, Is.EqualTo(coach.transform));
                Assert.That(RailCampaign.ClearLine(start, end, null, target.transform), Is.False,
                    "A shared Rigidbody never authorizes a sibling wall.");
                wall.enabled = false; Physics.SyncTransforms();
                Assert.That(RailCampaign.ClearLine(start, end, null, target.transform), Is.True,
                    "Removing only the owned fixture obstruction leaves the allowed control visible.");
            }
            finally { Cleanup(root); }
        }

        [Test]
        public void AForeignWallStillBlocksTheChildControl()
        {
            var root = new GameObject("Rail LOS foreign wall fixture");
            try
            {
                root.transform.position = Origin;
                var coach = Compound(root.transform, "Actual kinematic coach");
                var target = Box(coach.transform, "Allowed control child", new Vector3(3, 1, 0), new Vector3(.6f, .8f, .5f));
                var wall = Box(root.transform, "Foreign static wall", new Vector3(0, 1, 0), new Vector3(.2f, 2, 2));
                Vector3 start = Origin + new Vector3(-3, 1, 0), end = target.transform.position;
                var hits = Hits(start, end);
                Assert.That(wall.attachedRigidbody, Is.Null);
                Assert.That(hits.Any(h => h.collider == wall), Is.True);
                Assert.That(hits.Any(h => h.collider == target), Is.True);
                Assert.That(RailCampaign.ClearLine(start, end, null, target.transform), Is.False);
                wall.enabled = false; Physics.SyncTransforms();
                Assert.That(RailCampaign.ClearLine(start, end, null, target.transform), Is.True);
            }
            finally { Cleanup(root); }
        }

        [Test]
        public void OwnBodyChildBelowAnotherRigidbodyUsesItsActualColliderHierarchy()
        {
            var root = new GameObject("Rail LOS own child fixture");
            try
            {
                root.transform.position = Origin;
                var coach = Compound(root.transform, "Actual kinematic coach");
                var target = Box(coach.transform, "Allowed control child", new Vector3(3, 1, 0), new Vector3(.6f, .8f, .5f));
                var ownBody = Compound(root.transform, "Own compound body");
                var ownGroup = new GameObject("Owned body subtree"); ownGroup.transform.SetParent(ownBody.transform, false);
                var ownCollider = Box(ownGroup.transform, "Owned body child", new Vector3(-1, 1, 0), new Vector3(.6f, 1, 1));
                Vector3 start = Origin + new Vector3(-3, 1, 0), end = target.transform.position;
                var hits = Hits(start, end);
                Assert.That(hits.Any(h => h.collider == ownCollider), Is.True);
                var ownHit = hits.Single(h => h.collider == ownCollider);
                Assert.That(ownCollider.attachedRigidbody, Is.EqualTo(ownBody));
                Assert.That(ownHit.transform, Is.EqualTo(ownBody.transform));
                Assert.That(ownHit.transform.IsChildOf(ownGroup.transform), Is.False);
                Assert.That(ownHit.collider.transform.IsChildOf(ownGroup.transform), Is.True);
                Assert.That(RailCampaign.ClearLine(start, end, null, target.transform), Is.False,
                    "The own-body fixture is a real obstruction until its exact owned subtree is supplied.");
                Assert.That(RailCampaign.ClearLine(start, end, ownGroup.transform, target.transform), Is.True);
            }
            finally { Cleanup(root); }
        }
    }
}
