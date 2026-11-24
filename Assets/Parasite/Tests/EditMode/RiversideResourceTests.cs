using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Parasite.Tests
{
    /// <summary>Verify the actual imported runtime art, including export scale and original packed PBR maps.</summary>
    public class RiversideResourceTests
    {
        [TestCase("RiversideFerry", 6f, 5.2511f, 8.1975f, 17398)]
        [TestCase("FloodgateMachinery", 3.4f, 2.45f, 2.1f, 15396)]
        [TestCase("RadioMast", 2.2f, 16.2411f, 1.51f, 19656)]
        [TestCase("IndustrialConsole", 1.1f, 1.32f, 0.65f, 6380)]
        [TestCase("RadioConsole", 1.1f, 1.32f, 0.63f, 9344)]
        [TestCase("ClinicConsole", 1.1f, 1.57f, 0.63f, 7020)]
        [TestCase("ClinicScreen", 0.304f, 1.885f, 4.39f, 4716)]
        [TestCase("RiversideBanks", 360f, 11.1614f, 78.7657f, 4800)]
        public void ImportedAssemblyKeepsItsMetreScaleMaterialsAndTriangleBudget(string name, float x, float y, float z, int expectedTriangles)
        {
            var asset = Resources.Load<GameObject>("Riverside/" + name);
            Assert.IsNotNull(asset, "The standalone Resources path resolves " + name);
            var instance = Object.Instantiate(asset);
            try
            {
                var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
                Assert.IsNotEmpty(renderers);
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                    Assert.IsNotEmpty(renderer.sharedMaterials);
                    Assert.IsTrue(renderer.sharedMaterials.All(material => material), renderer.name + " has every authored surface slot.");
                }
                Assert.That(bounds.size.x, Is.EqualTo(x).Within(0.015f), "X scale in metres");
                Assert.That(bounds.size.y, Is.EqualTo(y).Within(0.015f), "Y scale in metres");
                Assert.That(bounds.size.z, Is.EqualTo(z).Within(0.015f), "Z scale in metres");
                int triangles = 0;
                foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
                {
                    Assert.IsNotNull(filter.sharedMesh);
                    for (int i = 0; i < filter.sharedMesh.subMeshCount; i++) triangles += (int)filter.sharedMesh.GetIndexCount(i) / 3;
                }
                Assert.AreEqual(expectedTriangles, triangles, "FBX import preserves the verified geometry budget.");
                Assert.IsEmpty(instance.GetComponentsInChildren<Collider>(true), "Visual replacements cannot change the tested collision/navigation layout.");
            }
            finally { Object.DestroyImmediate(instance); }
        }

        // The intake status lamp sits on its starboard edge, not on the intake centreline.
        [TestCase("RiversideFerry", "CinemaGlow", 0.72f, 4.18f)]
        [TestCase("RiversideFerry", "CinemaWarm", 0f, -2.5f)]
        [TestCase("ClinicConsole", "CinemaGlass", -0.21f, 0.12f)]
        [TestCase("RadioMast", "CinemaCream", 0.735f, 0.1f)]
        [TestCase("RadioConsole", "CinemaCream", 0f, -0.348f)]
        public void AsymmetricFeaturesKeepTheirAuthoredSideAfterUnityImport(string asset, string surface, float expectedX, float expectedZ)
        {
            var model = Resources.Load<GameObject>("Riverside/" + asset);
            Assert.IsNotNull(model);
            var instance = Object.Instantiate(model);
            try
            {
                var renderers = instance.GetComponentsInChildren<MeshRenderer>(true)
                    .Where(renderer => renderer.sharedMaterials.Any(material => material && material.name == surface)).ToArray();
                Assert.IsNotEmpty(renderers, surface + " is an actual authored feature.");
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                Assert.That(bounds.center.x, Is.EqualTo(expectedX).Within(0.05f), "The FBX boundary preserves the feature's X side.");
                Assert.That(bounds.center.z, Is.EqualTo(expectedZ).Within(0.05f), "The FBX boundary preserves front/back alignment with fixed gameplay anchors.");
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void FarBankRisesAwayFromTheRiverWithoutReflectingIntoThePlayableBerth()
        {
            var model = Resources.Load<GameObject>("Riverside/RiversideBanks");
            Assert.IsNotNull(model);
            var instance = Object.Instantiate(model);
            try
            {
                var bounds = instance.GetComponentInChildren<MeshRenderer>().bounds;
                Assert.That(bounds.min.z, Is.LessThan(-75f));
                Assert.That(bounds.max.z, Is.LessThan(4f));
                Assert.That(bounds.center.z, Is.LessThan(-30f));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [TestCase("MarinePaint")]
        [TestCase("Rubber")]
        [TestCase("ClinicEnamel")]
        [TestCase("ClinicFabric")]
        public void OriginalSurfacesHaveAllThreeRuntimePbrMaps(string surface)
        {
            foreach (var channel in new[] { "_A", "_N", "_M" })
            {
                var texture = Resources.Load<Texture2D>("Riverside/Textures/" + surface + channel);
                Assert.IsNotNull(texture, surface + channel + " must be included in player Resources.");
                Assert.AreEqual(512, texture.width);
                Assert.AreEqual(512, texture.height);
                Assert.That(texture.mipmapCount, Is.GreaterThan(1), "Distant riverside surfaces retain mipmaps.");
            }
        }
    }
}
