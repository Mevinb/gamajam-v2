using System.Collections;
using System.IO;
using NUnit.Framework;
using Parasite.Missions;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Parasite.Tests
{
    public sealed class RailWorldLabelRenderingTests
    {
        [System.Serializable] sealed class Measurement
        {
            public string device, backend;
            public string metric = "Same-camera, same-frame text-on/text-off pairs; count pixels with maximum absolute RGB-channel difference greater than 20.";
            public int width = 640, height = 320, rgbDifferenceThreshold = 20, frontChangedPixels, backChangedPixels, occludedChangedPixels;
        }

        // Isolated real shader/depth test; the unchanged chapter scene and physical bindings have their own tests.
        [UnityTest, Timeout(30000)]
        public IEnumerator ActualFontIsVisibleFromFrontCulledFromBackAndBlockedByOpaqueGeometry()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Actual world-label visibility requires a rendered backend.");
            const int layer = 30;
            var root = new GameObject("Owned Northline world-label rendering fixture");
            var target = new RenderTexture(640, 320, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            var pixels = new Texture2D(640, 320, TextureFormat.RGB24, false);
            var oldActive = RenderTexture.active;
            Material wallMaterial = null;
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "northline-label-rendered-" + System.DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ")));
            Directory.CreateDirectory(directory);
            var measurement = new Measurement { device = SystemInfo.graphicsDeviceName, backend = SystemInfo.graphicsDeviceType.ToString() };
            try
            {
                var sign = new GameObject("Actual font sign") { layer = layer }; sign.transform.SetParent(root.transform, false);
                var text = sign.AddComponent<TextMesh>(); text.text = "NORTHLINE"; text.fontSize = 64; text.characterSize = 1;
                text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center; text.color = Color.white;
                text.font = Resources.Load<Font>("UI/Rajdhani-Medium"); Assert.That(text.font, Is.Not.Null);
                var owner = sign.AddComponent<RailWorldLabelMaterial>();
                Assert.That(owner.OwnedMaterial.shader.name, Is.EqualTo(RailWorldLabelMaterial.ShaderName));
                var cameraObject = new GameObject("Owned world-label camera"); cameraObject.transform.SetParent(root.transform, false);
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.cullingMask = 1 << layer;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.aspect = 2; camera.nearClipPlane = .01f; camera.farClipPlane = 10;
                camera.allowHDR = camera.allowMSAA = false; Assert.That(target.Create(), Is.True);
                yield return null; yield return null;
                var textRenderer = sign.GetComponent<MeshRenderer>();
                var bounds = textRenderer.bounds;
                camera.orthographicSize = Mathf.Max(.1f, Mathf.Max(bounds.extents.y, bounds.extents.x / 2f) * 1.3f);

                Color32[] Render(string name)
                {
                    var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                    Assert.That(RenderPipeline.SupportsRenderRequest(camera, request), Is.True);
                    RenderPipeline.SubmitRenderRequest(camera, request);
                    RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 640, 320), 0, 0); pixels.Apply();
                    var result = pixels.GetPixels32();
                    File.WriteAllBytes(Path.Combine(directory, name + ".png"), pixels.EncodeToPNG());
                    return result;
                }
                IEnumerator CapturePair(string name, Vector3 side, System.Action<int> count)
                {
                    camera.transform.SetPositionAndRotation(bounds.center + side * 3, Quaternion.LookRotation(-side));
                    Color32[] visible, hidden;
                    bool wasEnabled = textRenderer.enabled;
                    try
                    {
                        // No yield or camera/state change occurs between these renders. The material owner stays active;
                        // the production atmosphere/background remains in both images and is removed only by comparison.
                        textRenderer.enabled = true; visible = Render(name + "-text-on");
                        textRenderer.enabled = false; hidden = Render(name + "-text-off");
                    }
                    finally { textRenderer.enabled = wasEnabled; }
                    int changed = 0;
                    for (int i = 0; i < visible.Length; i++)
                    {
                        var a = visible[i]; var b = hidden[i];
                        int difference = Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Max(Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b)));
                        if (difference > measurement.rgbDifferenceThreshold) changed++;
                    }
                    count(changed);
                    yield return null;
                }
                yield return CapturePair("front", Vector3.back, n => measurement.frontChangedPixels = n);
                yield return CapturePair("back", Vector3.forward, n => measurement.backChangedPixels = n);
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "Owned opaque occlusion control";
                wall.layer = layer; wall.transform.SetParent(root.transform, false); wall.transform.position = bounds.center + Vector3.back * 1.5f;
                wall.transform.localScale = new Vector3(Mathf.Max(.2f, bounds.size.x * 1.2f), Mathf.Max(.2f, bounds.size.y * 1.2f), .2f);
                wallMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")); wallMaterial.SetColor("_BaseColor", Color.black);
                wall.GetComponent<MeshRenderer>().sharedMaterial = wallMaterial;
                yield return CapturePair("opaque-wall", Vector3.back, n => measurement.occludedChangedPixels = n);
                File.WriteAllText(Path.Combine(directory, "measurement.json"), JsonUtility.ToJson(measurement, true));
                Assert.That(measurement.frontChangedPixels, Is.GreaterThan(100), "The real font/material pair must draw visible glyph area against the identical no-text background.");
                Assert.That(measurement.backChangedPixels, Is.LessThanOrEqualTo(3), "A world sign cannot show mirrored glyphs from its back.");
                Assert.That(measurement.occludedChangedPixels, Is.LessThanOrEqualTo(3), "Opaque scene geometry must hide the text through depth testing.");
                Assert.That(owner.OwnedMaterial.mainTexture, Is.SameAs(text.font.material.mainTexture));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                RenderTexture.active = oldActive; target.Release(); Object.Destroy(target); Object.Destroy(pixels);
                Object.Destroy(root); if (wallMaterial) Object.Destroy(wallMaterial);
            }
            yield return null;
        }
    }
}
