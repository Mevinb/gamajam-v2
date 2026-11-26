using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Parasite.Missions;
using Parasite.Testing;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace Parasite.Tests
{
    public sealed class RailArtBindingTests
    {
        [UnityTest,Timeout(60000)] public IEnumerator CompleteAuthoredKitPreservesMetreScaleAndPhysics()
        {yield return RailAcceptance.Enter(EndingOutcome.Peace);CheckBindings();}
        [UnityTest,Timeout(420000)] public IEnumerator AuthoredModelsFollowActualCrossingAndCoachChapter()
        {yield return RailAcceptance.RunChapter(EndingOutcome.Dominion,false);CheckBindings();}
        static void CheckBindings()
        {
            var root=GameObject.Find(RailLayout.RootName);Assert.That(root,Is.Not.Null);
            var art=root.GetComponent<RailAuthoredArt>();var shared=root.GetComponent<HarborArt>().Shared;
            Assert.That(art.Applied,Is.True,art.LastFailure);Assert.That(art.Bindings.Count,Is.EqualTo(21));
            Assert.That(art.PhysicsBeforeSha256,Is.Not.Empty);Assert.That(art.PhysicsAfterSha256,Is.EqualTo(art.PhysicsBeforeSha256));
            var containers=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Northline authored Rail",StringComparison.Ordinal)).ToArray();Assert.That(containers.Length,Is.EqualTo(21));
            foreach(var container in containers)
            {
                string name=container.name.Substring("Northline authored ".Length);var definition=RailArtContract.Models.Single(m=>m.Name==name);
                var filter=container.GetComponentInChildren<MeshFilter>(true);Assert.That(filter,Is.Not.Null);Assert.That(filter.sharedMesh.isReadable,Is.False);
                var receipt=art.Bindings.Single(b=>b.model==name&&b.parent==PathOf(container.parent));
                var parent=container.parent;var inverse=new Vector3(1/parent.localScale.x,1/parent.localScale.y,1/parent.localScale.z);
                Assert.That(Vector3.Distance(container.localScale,inverse),Is.LessThan(.00001f),name+" cancels only physical parent scale");
                var expected=RailAuthoredArt.TransformBounds(definition.Bounds,parent.localToWorldMatrix*Matrix4x4.Scale(inverse)*Matrix4x4.Translate(receipt.metreOffset));
                var actual=RailAuthoredArt.TransformBounds(filter.sharedMesh.bounds,filter.transform.localToWorldMatrix);
                Assert.That(Vector3.Distance(actual.min,expected.min),Is.LessThan(.002f),name+" actual moving-parent minimum");Assert.That(Vector3.Distance(actual.max,expected.max),Is.LessThan(.002f),name+" actual moving-parent maximum");
                Assert.That(container.GetComponentsInChildren<Collider>(true).Length,Is.Zero);Assert.That(container.GetComponentsInChildren<MonoBehaviour>(true).Length,Is.Zero);
                var renderer=container.GetComponentInChildren<MeshRenderer>(true);
                if(definition.IsCoach)RailCoachMaterialTests.AssertRuntimeMaterials(renderer.sharedMaterials);
                else Assert.That(renderer.sharedMaterial,Is.SameAs(shared));
                Assert.That(renderer.sharedMaterials.Length,Is.EqualTo(definition.SubmeshTriangles.Count));
                CollectionAssert.AreEqual(renderer.sharedMaterials.Select(m=>m.GetInstanceID()).ToArray(),receipt.materialInstanceIds);
                CollectionAssert.AreEqual(renderer.sharedMaterials.Select(m=>m.name).ToArray(),receipt.materialNames);
                CollectionAssert.AreEqual(Enumerable.Range(0,filter.sharedMesh.subMeshCount).Select(i=>filter.sharedMesh.GetIndexCount(i)).ToArray(),receipt.submeshIndexCounts);
                Assert.That(receipt.originalRendererIds.Length,Is.GreaterThan(0));
                foreach(int originalId in receipt.originalRendererIds)
                {
                    var original=root.GetComponentsInChildren<MeshRenderer>(true).Single(r=>r.GetInstanceID()==originalId);
                    Assert.That(original.enabled,Is.False,"Only the captured provisional face is hidden after complete validation.");
                }
                Assert.That(receipt.actualColliders,Is.Zero);Assert.That(receipt.actualBehaviours,Is.Zero);
                if(name=="RailCrossingBarrier")Assert.That(actual.size.x,Is.EqualTo(8f).Within(.002f),"8m authored leaf must never inherit8x physical scale into64m");
            }
            CheckNewSeatLayoutAndSignature(root);
            var crank=GameObject.Find("Northline dead-man crank");var wheel=crank.transform.Find("Harbor ValveWheel visual");Assert.That(wheel,Is.Not.Null);
            Assert.That(wheel.GetComponentsInChildren<MeshRenderer>(true).All(r=>r.enabled),Is.True,"The actual animated handwheel stays rendered and bound to its original transform");
        }
        static void CheckNewSeatLayoutAndSignature(GameObject root)
        {
            // Equality is for the new32 precise pan/back/support shapes, not the old unseated compound.
            var coach=root.GetComponent<RailDeparture>().Coach;
            var seats=coach.GetComponentsInChildren<BoxCollider>(true).Where(c=>c.name.StartsWith("Northline coach solid seat ",StringComparison.Ordinal)).ToArray();
            Assert.That(seats.Length,Is.EqualTo(32));Assert.That(RailLayout.CoachSeatBoundsLocal.Length,Is.EqualTo(32));
            Assert.That(RailAuthoredArt.CoachSeatContract(coach.transform,out string reason),Is.True,reason);
            for(int i=0;i<32;i++)
            {
                var expected=RailLayout.CoachSeatBoundsLocal[i];var seat=seats.Single(c=>c.name=="Northline coach solid seat "+i);
                Assert.That(seat.enabled&&!seat.isTrigger&&seat.gameObject.activeInHierarchy&&seat.attachedRigidbody==coach,Is.True);
                var size=Vector3.Scale(seat.size,seat.transform.localScale);
                Assert.That(Vector3.Distance(seat.transform.localPosition,expected.center),Is.LessThan(.00001f));
                Assert.That(Vector3.Distance(size,expected.size),Is.LessThan(.00001f));
                Assert.That(Mathf.Abs(expected.center.x)-size.x*.5f,Is.GreaterThan(.75f),"Precise fittings leave the central aisle clear.");
                Assert.That(Mathf.Abs(expected.center.z)-size.z*.5f,Is.GreaterThan(1.85f),"The original boarding/control zone stays clear.");
                if(i%4==0)Assert.That(expected.max.y,Is.EqualTo(1.25f).Within(.00001f),"Seat pans must be landable, without an invisible whole-seat box above them.");
                string present=RailAuthoredArt.PhysicalSignature(root.transform);
                try
                {
                    seat.enabled=false;
                    Assert.That(RailAuthoredArt.PhysicalSignature(root.transform),Is.Not.EqualTo(present),"Every actual fitting belongs to the signature.");
                    Assert.That(RailAuthoredArt.CoachSeatContract(coach.transform,out reason),Is.False,"A disabled physical fitting invalidates the complete seat contract.");
                }
                finally{seat.enabled=true;}
                Assert.That(RailAuthoredArt.PhysicalSignature(root.transform),Is.EqualTo(present));
            }
        }
        static string PathOf(Transform value){var parts=new System.Collections.Generic.List<string>();for(var t=value;t;t=t.parent)parts.Add(t.name);parts.Reverse();return string.Join("/",parts);}
        [UnityTearDown] public IEnumerator Cleanup()
        {GameInput.Simulated=false;GameInput.SimMove=Vector2.zero;GameInput.SimInteract=GameInput.SimLeave=GameInput.SimSplit=GameInput.SimSwitch=GameInput.SimJump=GameInput.SimSprint=false;Time.timeScale=1;RunState.NewGame();SceneManager.LoadScene(GameManager.MenuScene);yield return null;}
    }
}
