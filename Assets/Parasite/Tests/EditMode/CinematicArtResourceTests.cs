using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Parasite.Tests
{
    public class CinematicArtResourceTests
    {
        [TestCase("ContainmentLaboratory")]
        [TestCase("ServiceConduit")]
        [TestCase("LakesideShore")]
        [TestCase("LakesideBench")]
        public void BlenderSetsHaveRealMeshesAndTexturedSurfaces(string asset)
        {
            var parent = new GameObject("Cinematic asset verification");
            var materials = new List<Material>();
            try
            {
                var set = CinematicArt.Instantiate(asset, parent.transform, 30, materials);
                var meshes = set.GetComponentsInChildren<MeshFilter>();
                Assert.IsNotEmpty(meshes);
                int vertices = 0;
                foreach (var mesh in meshes) { Assert.IsNotNull(mesh.sharedMesh); vertices += mesh.sharedMesh.vertexCount; }
                Assert.That(vertices, Is.GreaterThan(500), "Authored film props contain detailed geometry.");
                Assert.That(materials.Count, Is.GreaterThanOrEqualTo(2));
                bool textured = false;
                foreach (var material in materials)
                {
                    Assert.AreEqual("Universal Render Pipeline/Lit", material.shader.name);
                    if (material.GetTexture("_BaseMap"))
                    {
                        textured = true;
                        Assert.IsNotNull(material.GetTexture("_BumpMap"));
                        Assert.IsNotNull(material.GetTexture("_MetallicGlossMap"));
                    }
                }
                Assert.IsTrue(textured, "PBR textures are assigned to the exported material slots.");
                foreach (var item in set.GetComponentsInChildren<Transform>()) Assert.AreEqual(30, item.gameObject.layer);
                if (asset == "LakesideBench")
                {
                    var renderers = set.GetComponentsInChildren<MeshRenderer>();
                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    Assert.That(bounds.size.x, Is.InRange(1.7f, 1.9f), "FBX exports and imports in meters.");
                    Assert.That(bounds.size.y, Is.InRange(1.0f, 1.2f), "The bench remains Y-up after Blender/FBX conversion.");
                    Assert.That(bounds.size.z, Is.InRange(.5f, .8f));
                }
            }
            finally
            {
                Object.DestroyImmediate(parent);
                foreach (var material in materials) Object.DestroyImmediate(material);
            }
        }
    }
}
