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
                foreach (var renderer in set.GetComponentsInChildren<MeshRenderer>())
                    if ((asset == "ServiceConduit" && renderer.name.Contains("Warm")) ||
                        (asset == "LakesideShore" && renderer.name.Contains("Timber")))
                        Debug.Log("[CinematicArt] Axis anchor " + asset + "/" + renderer.name + " " + renderer.bounds);
                if (asset == "ServiceConduit")
                {
                    var renderers = set.GetComponentsInChildren<MeshRenderer>();
                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    Debug.Log("[CinematicArt] ServiceConduit world bounds " + bounds);
                    Assert.That(bounds.center.z, Is.EqualTo(5f).Within(0.1f),
                        "The authored escape duct must extend along the same positive-Z direction as its camera and creature.");
                    var lamps = BoundsForMaterial(set, "CinemaWarm");
                    Assert.That(lamps.center.x, Is.EqualTo(.53f).Within(.02f), "Handedness correction preserves the guide lamps' authored +X side.");
                    Assert.That(lamps.center.z, Is.EqualTo(5f).Within(.1f), "Guide lamps follow the forward-moving escape subject.");
                }
                if (asset == "LakesideShore")
                {
                    var jetty = BoundsForMaterial(set, "CinemaTimber");
                    Assert.That(jetty.center.x, Is.EqualTo(6f).Within(.05f), "The jetty remains on the authored right side of the shore.");
                    Assert.That(jetty.center.z, Is.EqualTo(5.22f).Within(.1f), "The jetty projects forward over the water.");
                    var shore = BoundsForMaterial(set, "CinemaShore");
                    Assert.That(shore.center.z, Is.InRange(-12.3f, -11.5f), "Land extends behind the bench; open water lies ahead.");
                }
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

        static Bounds BoundsForMaterial(GameObject root, string materialName)
        {
            bool found = false; Bounds bounds = default;
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                foreach (var material in renderer.sharedMaterials)
                    if (material && material.name.StartsWith(materialName, System.StringComparison.Ordinal))
                    {
                        if (found) bounds.Encapsulate(renderer.bounds);
                        else { bounds = renderer.bounds; found = true; }
                        break;
                    }
            Assert.IsTrue(found, "Expected exported asymmetric surface " + materialName);
            return bounds;
        }

    }
}
