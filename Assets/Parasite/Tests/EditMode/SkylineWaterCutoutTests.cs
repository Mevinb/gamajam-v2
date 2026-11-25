using System.Linq;
using NUnit.Framework;
using Parasite.Missions;
using UnityEngine;

namespace Parasite.Tests
{
    public class SkylineWaterCutoutTests
    {
        GameObject root;
        Mesh sourceMesh;
        Material material;
        SkylineWaterCutout owner;
        static readonly Bounds[] Water = { new(new Vector3(230,-.3f,-87),new Vector3(380,0,106)), new(new Vector3(270,-.3f,-31),new Vector3(10,0,14)) };
        MeshRenderer Source()
        {
            root = new GameObject("Skyline cutout fixture");
            var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
            source.name = "Skyline_Ground_0_0"; source.transform.SetParent(root.transform);
            Object.DestroyImmediate(source.GetComponent<Collider>());
            var filter = source.GetComponent<MeshFilter>(); sourceMesh = Object.Instantiate(filter.sharedMesh);
            var points = sourceMesh.vertices;
            for (int i=0;i<points.Length;i++) points[i]=Vector3.Scale(points[i],new Vector3(1000,.1f,1000))+Vector3.down*.06f;
            sourceMesh.vertices=points;
            // Independent fixture projection reproduces the existing CityAssets/WorldKit source convention.
            sourceMesh.uv=points.Select((p,i)=>{
                var n=sourceMesh.normals[i];
                return Mathf.Abs(n.x)>.9f?new Vector2(n.x>0?-p.z:p.z,p.y)*.5f:Mathf.Abs(n.y)>.9f?new Vector2(p.x,p.z)*.5f:new Vector2(n.z>0?p.x:-p.x,p.y)*.5f;
            }).ToArray();
            sourceMesh.RecalculateBounds();filter.sharedMesh=sourceMesh;
            material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Ground"};
            var renderer=source.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            owner=root.AddComponent<SkylineWaterCutout>();return renderer;
        }
        [TearDown]
        public void Cleanup()
        {
            if(owner)owner.Dispose();if(root)Object.DestroyImmediate(root);if(sourceMesh)Object.DestroyImmediate(sourceMesh);if(material)Object.DestroyImmediate(material);
        }
        [Test]
        public void ActualWaterUnionOpensBothSlabFacesAndKeepsWorldTexturePhase()
        {
            var source=Source();Assert.IsTrue(owner.Apply(source,Water),owner.LastFailure);
            var mesh=owner.Replacement.GetComponent<MeshFilter>().sharedMesh;
            var vertices=mesh.vertices;var normals=mesh.normals;var uv=mesh.uv;var indices=mesh.triangles;
            double top=0,bottom=0;
            for(int i=0;i<indices.Length;i+=3)
            {
                int a=indices[i],b=indices[i+1],c=indices[i+2];
                double area=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]).magnitude*.5;
                if(normals[a].y>.9f)top+=area;if(normals[a].y<-.9f)bottom+=area;
            }
            // Main river40280m² + berth140m² - overlap40m² =40380m² of actual opening.
            Assert.That(top,Is.EqualTo(959620).Within(1));Assert.That(bottom,Is.EqualTo(959620).Within(1));
            for(int i=0;i<vertices.Length;i++)if(normals[i].y>.9f)
            {Assert.That(uv[i].x,Is.EqualTo(vertices[i].x*.5f).Within(.0001f));Assert.That(uv[i].y,Is.EqualTo(vertices[i].z*.5f).Within(.0001f));}
            Assert.AreSame(material,owner.Replacement.sharedMaterial);Assert.AreSame(sourceMesh,source.GetComponent<MeshFilter>().sharedMesh);
            Assert.IsFalse(source.enabled);Assert.IsTrue(owner.Replacement.enabled);
            Assert.IsEmpty(owner.Replacement.GetComponents<Collider>());
        }
        [Test]
        public void RepeatedApplyAndDisposePreserveOtherRenderersAndPhysics()
        {
            var source=Source();var other=GameObject.CreatePrimitive(PrimitiveType.Cube);other.transform.SetParent(root.transform);
            var collider=other.GetComponent<BoxCollider>();var before=collider.bounds;
            var renderer=other.GetComponent<MeshRenderer>();var beforeMesh=other.GetComponent<MeshFilter>().sharedMesh;
            Assert.IsTrue(owner.Apply(source,Water),owner.LastFailure);
            var mesh=owner.Replacement.GetComponent<MeshFilter>().sharedMesh;
            Assert.IsTrue(owner.Apply(source,Water));Assert.AreSame(mesh,owner.Replacement.GetComponent<MeshFilter>().sharedMesh);
            Assert.AreEqual(before,collider.bounds);Assert.IsTrue(renderer.enabled);Assert.AreSame(beforeMesh,other.GetComponent<MeshFilter>().sharedMesh);
            owner.Dispose();owner.Dispose();Assert.IsTrue(source.enabled);Assert.IsFalse(mesh);Assert.IsFalse(owner.Applied);
        }
        [Test]
        public void UnexpectedSourceGeometryCannotBeSilentlyReplaced()
        {
            var source=Source();var vertices=sourceMesh.vertices;vertices[0]=Vector3.zero;sourceMesh.vertices=vertices;
            Assert.IsFalse(owner.Apply(source,Water));Assert.IsTrue(source.enabled);Assert.IsNull(owner.Replacement);
            Assert.That(owner.LastFailure,Does.Contain("non-box"));
        }
    }
}
