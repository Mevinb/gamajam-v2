using System.Linq;
using NUnit.Framework;
using Parasite.Missions;
using UnityEngine;

namespace Parasite.Tests
{
    public class AfterlakeSurfaceUVTests
    {
        GameObject root;
        Material material;

        [TearDown]
        public void Cleanup()
        {
            if (root)
            {
                foreach (var projection in root.GetComponentsInChildren<AfterlakeSurfaceUV>(true)) projection.Dispose();
                Object.DestroyImmediate(root);
            }
            if (material) Object.DestroyImmediate(material);
        }

        [TestCase("Sidewalk", 9f, 3f, true)]
        [TestCase("BrickDark", 6f, 2f, false)]
        public void ArchitecturalTextureRepeatsMatchPhysicalMetres(string surface, float width, float repeats, bool topFace)
        {
            root = new GameObject("Riverside texture density fixture");
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.transform.SetParent(root.transform);
            slab.transform.position = new Vector3(194, 0.15f, -23);
            slab.transform.localScale = new Vector3(width, topFace ? 0.14f : 3f, topFace ? 6f : 0.3f);
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = surface };
            var renderer = slab.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            var filter = slab.GetComponent<MeshFilter>(); var original = filter.sharedMesh;
            var collisionBounds = slab.GetComponent<BoxCollider>().bounds;

            var projection = root.AddComponent<AfterlakeSurfaceUV>();
            projection.Apply();
            Assert.AreNotSame(original, filter.sharedMesh);
            Assert.AreSame(material, renderer.sharedMaterial, "Texture density never creates per-object material instances.");
            Assert.AreEqual(collisionBounds, slab.GetComponent<BoxCollider>().bounds, "Visual UVs cannot change collision geometry.");
            var mesh = filter.sharedMesh;
            var faceUV = mesh.uv.Where((_, i) => topFace ? mesh.normals[i].y > 0.9f : mesh.normals[i].z > 0.9f).ToArray();
            Assert.That(faceUV.Max(uv => uv.x) - faceUV.Min(uv => uv.x), Is.EqualTo(repeats).Within(0.0001f));
            Assert.That(faceUV.Max(uv => uv.y) - faceUV.Min(uv => uv.y), Is.EqualTo(topFace ? 2f : 1f).Within(0.0001f));
            Assert.AreEqual(mesh.vertexCount, mesh.tangents.Length, "Normal-map tangents follow the corrected face orientation.");
            // EditMode tools call Apply explicitly; runtime callbacks are not activated for this fixture.
            // They must also use the explicit disposal API rather than assume a PlayMode OnDestroy callback.
            projection.Dispose(); projection.Dispose();
            Assert.AreSame(original, filter.sharedMesh, "Cleanup restores shared primitive mesh ownership.");
            Assert.IsFalse(mesh, "Disposing the district projection releases its owned meshes.");
        }

        [Test]
        public void BlenderAssembliesKeepTheirAuthoredMeshesAndUVs()
        {
            root = new GameObject("Authored riverside UV fixture");
            var asset = Resources.Load<GameObject>("Riverside/RiversideFerry");
            Assert.IsNotNull(asset);
            var ferry = Object.Instantiate(asset, root.transform, false);
            var filters = ferry.GetComponentsInChildren<MeshFilter>(true);
            var originals = filters.Select(filter => filter.sharedMesh).ToArray();
            Assert.IsNotEmpty(originals);
            root.AddComponent<AfterlakeSurfaceUV>().Apply();
            for (int i = 0; i < filters.Length; i++)
                Assert.AreSame(originals[i], filters[i].sharedMesh, "Blender UV atlases must remain intact.");
        }
    }
}
