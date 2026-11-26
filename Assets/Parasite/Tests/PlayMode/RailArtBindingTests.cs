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
                Assert.That(container.GetComponentInChildren<MeshRenderer>(true).sharedMaterial,Is.SameAs(shared));
                if(name=="RailCrossingBarrier")Assert.That(actual.size.x,Is.EqualTo(8f).Within(.002f),"8m authored leaf must never inherit8x physical scale into64m");
            }
            var crank=GameObject.Find("Northline dead-man crank");var wheel=crank.transform.Find("Harbor ValveWheel visual");Assert.That(wheel,Is.Not.Null);
            Assert.That(wheel.GetComponentsInChildren<MeshRenderer>(true).All(r=>r.enabled),Is.True,"The actual animated handwheel stays rendered and bound to its original transform");
        }
        static string PathOf(Transform value){var parts=new System.Collections.Generic.List<string>();for(var t=value;t;t=t.parent)parts.Add(t.name);parts.Reverse();return string.Join("/",parts);}
        [UnityTearDown] public IEnumerator Cleanup()
        {GameInput.Simulated=false;GameInput.SimMove=Vector2.zero;GameInput.SimInteract=GameInput.SimLeave=GameInput.SimSplit=GameInput.SimSwitch=GameInput.SimJump=GameInput.SimSprint=false;Time.timeScale=1;RunState.NewGame();SceneManager.LoadScene(GameManager.MenuScene);yield return null;}
    }
}
