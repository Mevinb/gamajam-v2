using NUnit.Framework;
using Parasite.Missions;
using UnityEngine;
using UnityEngine.Rendering;

namespace Parasite.Tests
{
    public sealed class RailWorldLabelMaterialTests
    {
        GameObject sign;
        Material original, replacement;

        [TearDown]
        public void Cleanup()
        {
            if (sign) Object.DestroyImmediate(sign);
            if (original) Object.DestroyImmediate(original);
            if (replacement) Object.DestroyImmediate(replacement);
        }

        RailWorldLabelMaterial Create()
        {
            sign = new GameObject("Owned Northline label fixture");
            var text = sign.AddComponent<TextMesh>(); text.text = "NORTHLINE / Ω"; text.fontSize = 64;
            text.font = Resources.Load<Font>("UI/Rajdhani-Medium");
            Assert.That(text.font, Is.Not.Null);
            original = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            original.mainTexture = Texture2D.whiteTexture;
            var renderer = sign.GetComponent<MeshRenderer>(); renderer.sharedMaterial = original;
            renderer.shadowCastingMode = ShadowCastingMode.TwoSided; renderer.receiveShadows = true;
            return sign.AddComponent<RailWorldLabelMaterial>();
        }

        [Test]
        public void CorrectFontAtlasIsOwnedAndDisableReenableRestoresRendererWithoutChangingSource()
        {
            var owner = Create(); var renderer = sign.GetComponent<MeshRenderer>(); var text = sign.GetComponent<TextMesh>();
            var first = owner.OwnedMaterial;
            Assert.That(first && first != original && renderer.sharedMaterial == first, Is.True);
            Assert.That(first.shader.name, Is.EqualTo(RailWorldLabelMaterial.ShaderName));
            Assert.That(first.mainTexture, Is.SameAs(text.font.material.mainTexture));
            Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off)); Assert.That(renderer.receiveShadows, Is.False);
            Assert.That(original.shader.name, Is.EqualTo("Universal Render Pipeline/Unlit"));
            Assert.That(original.mainTexture, Is.SameAs(Texture2D.whiteTexture));
            first.mainTexture = Texture2D.blackTexture;
            text.text += " / MEDICAL"; owner.RefreshAtlas();
            Assert.That(first.mainTexture, Is.SameAs(text.font.material.mainTexture));

            owner.enabled = false;
            Assert.That(first == null, Is.True);
            Assert.That(owner.OwnedMaterial, Is.Null); Assert.That(renderer.sharedMaterial, Is.SameAs(original));
            Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.TwoSided)); Assert.That(renderer.receiveShadows, Is.True);
            owner.RefreshAtlas(); Assert.That(owner.OwnedMaterial, Is.Null, "Disabled labels cannot reacquire materials from font callbacks.");
            owner.enabled = true;
            var second = owner.OwnedMaterial; Assert.That(second, Is.Not.Null); Assert.That(second, Is.Not.SameAs(first));
            Assert.That(renderer.sharedMaterial, Is.SameAs(second));
            Object.DestroyImmediate(owner);
            Assert.That(second == null, Is.True);
            Assert.That(renderer.sharedMaterial, Is.SameAs(original));
        }

        [Test]
        public void CleanupReleasesOnlyOwnedMaterialAndPreservesLaterRendererHandoff()
        {
            var owner = Create(); var renderer = sign.GetComponent<MeshRenderer>(); var owned = owner.OwnedMaterial;
            replacement = new Material(original); renderer.sharedMaterial = replacement;
            owner.enabled = false;
            Assert.That(owned == null, Is.True);
            Assert.That(renderer.sharedMaterial, Is.SameAs(replacement)); Assert.That(replacement, Is.Not.Null);
        }
    }
}
