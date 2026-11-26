using System;
using System.Linq;
using NUnit.Framework;
using Parasite.Missions;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Parasite.Tests
{
    // Actual imported Resources/materials are required. Clones below never mutate an imported asset.
    public sealed class RailCoachMaterialTests
    {
        internal static Material[] LoadMaterials() => new[] {
            Resources.Load<Material>(RailArtContract.CoachResourceRoot + RailArtContract.CoachOpaqueName),
            Resources.Load<Material>(RailArtContract.CoachResourceRoot + RailArtContract.CoachGlassName) };

        internal static void AssertRuntimeMaterials(Material[] materials)
        {
            var expected = LoadMaterials();
            Assert.That(materials.Length, Is.EqualTo(2));
            Assert.That(expected.All(m => m), Is.True, "Both actual coach material assets must exist before binding.");
            Assert.That(materials[0], Is.SameAs(expected[0])); Assert.That(materials[1], Is.SameAs(expected[1]));
            var opaque = materials[0]; var glass = materials[1];
            Assert.That(RailAuthoredArt.CoachOpaqueContract(opaque, out var reason), Is.True, reason);
            Assert.That(RailAuthoredArt.CoachGlassContract(glass, out reason), Is.True, reason);
            Assert.That(opaque.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            Assert.That(glass.shader, Is.SameAs(opaque.shader));
            Assert.That(opaque.renderQueue, Is.EqualTo((int)RenderQueue.Geometry));
            Assert.That(glass.renderQueue, Is.EqualTo((int)RenderQueue.Transparent));
            foreach (var descriptor in RailArtContract.CoachTextures)
            {
                var actual = Resources.Load<Texture2D>(descriptor.ResourcePath);
                Assert.That(actual, Is.Not.Null, descriptor.ResourcePath);
                Assert.That(opaque.GetTexture(descriptor.Property), Is.SameAs(actual), "The dedicated source map, not a compatible substitute, is bound.");
                Assert.That(actual.width == 2048 && actual.height == 2048 && actual.mipmapCount == 12 && !actual.isReadable, Is.True);
                Assert.That(actual.dimension, Is.EqualTo(TextureDimension.Tex2D));
                Assert.That(actual.format, Is.EqualTo(descriptor.Format));
                Assert.That(GraphicsFormatUtility.IsSRGBFormat(actual.graphicsFormat), Is.EqualTo(descriptor.Srgb));
                Assert.That(opaque.GetTextureScale(descriptor.Property), Is.EqualTo(Vector2.one));
                Assert.That(opaque.GetTextureOffset(descriptor.Property), Is.EqualTo(Vector2.zero));
                Assert.That(glass.GetTexture(descriptor.Property), Is.Null, "Transparent glass has no opaque/normal/packed map.");
            }
            Assert.That(RailArtContract.CoachTextures.Count, Is.EqualTo(3));
            Assert.That(Resources.LoadAll<Texture2D>(RailArtContract.CoachResourceRoot.TrimEnd('/')).Length, Is.EqualTo(3));
            Assert.That(opaque.GetColor("_BaseColor"), Is.EqualTo(Color.white));
            foreach (string property in new[] { "_Metallic", "_Smoothness", "_BumpScale", "_SpecularHighlights", "_EnvironmentReflections", "_ZWrite" })
                Assert.That(opaque.GetFloat(property), Is.EqualTo(1f), property);
            Assert.That(opaque.GetFloat("_Surface"), Is.Zero); Assert.That(opaque.GetFloat("_AlphaClip"), Is.Zero);
            Assert.That(opaque.IsKeywordEnabled("_NORMALMAP") && opaque.IsKeywordEnabled("_METALLICSPECGLOSSMAP"), Is.True);
            Assert.That(opaque.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT") || opaque.IsKeywordEnabled("_EMISSION"), Is.False);

            var tint = glass.GetColor("_BaseColor");
            Assert.That(tint.r, Is.EqualTo(.87f).Within(.0001f)); Assert.That(tint.g, Is.EqualTo(.94f).Within(.0001f));
            Assert.That(tint.b, Is.EqualTo(.96f).Within(.0001f)); Assert.That(tint.a, Is.EqualTo(.18f).Within(.0001f));
            Assert.That(glass.GetFloat("_Surface"), Is.EqualTo(1f)); Assert.That(glass.GetFloat("_Blend"), Is.Zero);
            Assert.That(glass.GetFloat("_BlendModePreserveSpecular"), Is.EqualTo(1f));
            Assert.That(glass.GetFloat("_SrcBlend"), Is.EqualTo((float)BlendMode.One));
            Assert.That(glass.GetFloat("_SrcBlendAlpha"), Is.EqualTo((float)BlendMode.One));
            Assert.That(glass.GetFloat("_DstBlend"), Is.EqualTo((float)BlendMode.OneMinusSrcAlpha));
            Assert.That(glass.GetFloat("_DstBlendAlpha"), Is.EqualTo((float)BlendMode.OneMinusSrcAlpha));
            Assert.That(glass.GetFloat("_ZWrite"), Is.Zero); Assert.That(glass.GetFloat("_Cull"), Is.EqualTo((float)CullMode.Back));
            Assert.That(glass.GetFloat("_Metallic"), Is.Zero); Assert.That(glass.GetFloat("_Smoothness"), Is.EqualTo(.92f).Within(.0001f));
            Assert.That(glass.GetFloat("_AlphaClip"), Is.Zero);
            Assert.That(glass.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT") && glass.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON"), Is.True);
            foreach (string keyword in new[] { "_ALPHATEST_ON", "_ALPHAMODULATE_ON", "_NORMALMAP", "_METALLICSPECGLOSSMAP", "_EMISSION" })
                Assert.That(glass.IsKeywordEnabled(keyword), Is.False, keyword);
            Assert.That(glass.GetShaderPassEnabled("ShadowCaster") || glass.GetShaderPassEnabled("DepthOnly"), Is.False);
            foreach (string property in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap", "_EmissionMap", "_DetailMask", "_DetailAlbedoMap", "_DetailNormalMap", "_ParallaxMap" })
                if (glass.HasProperty(property)) Assert.That(glass.GetTexture(property), Is.Null, property);
        }

        [Test]
        public void ActualImportedCoachHasTwoOrderedSubmeshesAndDedicatedPbrGlassAssets()
        {
            AssertRuntimeMaterials(LoadMaterials());
            int legacy = 0;
            foreach (var definition in RailArtContract.Models)
            {
                var prefab = Resources.Load<GameObject>(RailArtContract.ResourceRoot + definition.Name);
                Assert.That(RailAuthoredArt.ModelContract(prefab, definition, out var reason), Is.True, reason);
                var mesh = prefab.GetComponentInChildren<MeshFilter>(true).sharedMesh;
                Assert.That(mesh.isReadable, Is.False);
                if (definition.IsCoach)
                {
                    Assert.That(mesh.subMeshCount, Is.EqualTo(2));
                    Assert.That(mesh.GetIndexCount(0), Is.EqualTo(11024u * 3u));
                    Assert.That(mesh.GetIndexCount(1), Is.EqualTo(120u * 3u));
                }
                else
                {
                    legacy++; Assert.That(mesh.subMeshCount, Is.EqualTo(1));
                    Assert.That(mesh.GetIndexCount(0), Is.EqualTo((uint)definition.Triangles * 3u));
                }
            }
            Assert.That(legacy, Is.EqualTo(10));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void CompatibleOriginalGateAlbedoCannotReplaceTheDedicatedCoachSource()
        {
            var actual = LoadMaterials()[0]; Assert.That(actual, Is.Not.Null);
            var source = actual.GetTexture("_BaseMap");
            var gate = Resources.Load<Material>(RailArtContract.ResourceRoot + RailArtContract.MaterialName);
            Assert.That(RailAuthoredArt.MaterialContract(gate, out var reason), Is.True, reason);
            Assert.That(gate.GetTexture("_BaseMap"), Is.Not.SameAs(source));
            var clone = new Material(actual) { name = actual.name };
            try
            {
                Assert.That(RailAuthoredArt.CoachOpaqueContract(clone, out reason), Is.True, "The control clone is valid before the one-field mutation: " + reason);
                clone.SetTexture("_BaseMap", gate.GetTexture("_BaseMap"));
                Assert.That(RailAuthoredArt.CoachOpaqueContract(clone, out reason), Is.False, "Matching format/size does not establish source identity.");
                Assert.That(reason, Is.Not.Empty); Assert.That(actual.GetTexture("_BaseMap"), Is.SameAs(source));
            }
            finally { Object.DestroyImmediate(clone); }
            LogAssert.NoUnexpectedReceived();
        }

        [TestCase("ZWrite"), TestCase("ShadowCaster"), TestCase("DepthOnly"), TestCase("Blend"), TestCase("Texture")]
        public void ActualGlassRejectsOpaqueDepthShadowBlendOrMapRegressions(string mutation)
        {
            var actual = LoadMaterials()[1]; Assert.That(actual, Is.Not.Null);
            Assert.That(RailAuthoredArt.CoachGlassContract(actual, out var reason), Is.True, reason);
            var clone = new Material(actual) { name = actual.name };
            try
            {
                Assert.That(RailAuthoredArt.CoachGlassContract(clone, out reason), Is.True, "The control clone is valid before the one-field mutation: " + reason);
                switch (mutation)
                {
                    case "ZWrite": clone.SetFloat("_ZWrite", 1); break;
                    case "ShadowCaster": clone.SetShaderPassEnabled("ShadowCaster", true); break;
                    case "DepthOnly": clone.SetShaderPassEnabled("DepthOnly", true); break;
                    case "Blend": clone.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); break;
                    case "Texture": clone.SetTexture("_BaseMap", Resources.Load<Texture2D>(RailArtContract.CoachTextures[0].ResourcePath)); break;
                    default: throw new InvalidOperationException(mutation);
                }
                Assert.That(RailAuthoredArt.CoachGlassContract(clone, out reason), Is.False, mutation);
                Assert.That(reason, Is.Not.Empty);
                Assert.That(RailAuthoredArt.CoachGlassContract(actual, out reason), Is.True, "Imported material remains untouched: " + reason);
            }
            finally { Object.DestroyImmediate(clone); }
            LogAssert.NoUnexpectedReceived();
        }
    }
}
