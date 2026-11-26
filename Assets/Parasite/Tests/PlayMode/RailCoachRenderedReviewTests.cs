using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Parasite.Missions;
using Parasite.Possession;
using Parasite.Testing;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Parasite.Tests
{
    // Actual in-game lighting and imported materials; captures are review artifacts, not an aesthetic/FPS verdict.
    public sealed class RailCoachRenderedReviewTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator ActualGlassTransmitsAnOwnedMarkerAndCoachViewsAreCaptured()
        {
            Assert.That(SystemInfo.graphicsDeviceType, Is.EqualTo(GraphicsDeviceType.Vulkan));
            Assert.That(SystemInfo.graphicsDeviceName, Does.Contain("RTX 4050"));
            LogAssert.Expect(LogType.Log, "[Northline] FIXTURE prior23 summary/flags and initialroleownership only; Northline progress uses actualinput/physics, no actorwarps/artificialcalm.");
            yield return RailAcceptance.Enter(EndingOutcome.Peace);
            var root = GameObject.Find(RailLayout.RootName);
            Assert.That(root.GetComponent<RailAuthoredArt>().Applied, Is.True, root.GetComponent<RailAuthoredArt>().LastFailure);
            var coach = root.GetComponent<RailDeparture>().Coach;
            var renderer = coach.GetComponentsInChildren<MeshRenderer>(true).Single(r => r.sharedMaterials.Length == 2);
            var materials = renderer.sharedMaterials;
            RailCoachMaterialTests.AssertRuntimeMaterials(materials);
            GameManager.Instance.SetPaused(true);
            var driver = PlayerCamera.Instance; var camera = driver.Cam; var brain = camera.GetComponent<CinemachineBrain>();
            bool driverEnabled = driver.enabled, brainEnabled = brain && brain.enabled;
            var oldPosition = camera.transform.position; var oldRotation = camera.transform.rotation;
            var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            float oldFov = camera.fieldOfView, oldAspect = camera.aspect; int oldMask = camera.cullingMask;
            var target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            GameObject marker = null; Mesh markerMesh = null; Material markerMaterial = null, opaqueControl = null;
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "coach-rendered-review-" + System.DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ")));
            Directory.CreateDirectory(directory);
            Color32[] latest = null;
            IEnumerator Capture(string name, Vector3 localPosition, Vector3 localLook)
            {
                camera.transform.position = coach.transform.TransformPoint(localPosition);
                camera.transform.rotation = Quaternion.LookRotation(coach.transform.TransformPoint(localLook) - camera.transform.position);
                camera.targetTexture = target; camera.fieldOfView = 58; camera.aspect = 1600f / 900f;
                yield return null; yield return null;
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera, request), Is.True);
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); pixels.Apply();
                latest = pixels.GetPixels32();
                byte[] png = pixels.EncodeToPNG(); Assert.That(png.Length, Is.GreaterThan(1024));
                File.WriteAllBytes(Path.Combine(directory, name + ".png"), png);
            }
            float GreenDominance(Color32[] image)
            {
                float total = 0;
                for (int y = 400; y < 500; y++) for (int x = 750; x < 850; x++)
                { var colour = image[y * 1600 + x]; total += (colour.g - Mathf.Max(colour.r, colour.b)) / 255f; }
                return total / 10000;
            }
            try
            {
                driver.enabled = false; if (brain) brain.enabled = false;
                camera.cullingMask &= ~(1 << 5); Assert.That(target.Create(), Is.True);
                yield return Capture("west-door", new Vector3(-7.5f, 2.2f, -5), new Vector3(0, 1.6f, 0));
                yield return Capture("east-quarter", new Vector3(7, 2.3f, 5.5f), new Vector3(0, 1.55f, 0));
                yield return Capture("interior-aisle", new Vector3(0, 1.30f, -6.2f), new Vector3(0, 1.8f, 1.5f));
                yield return Capture("wheel-brake-detail", new Vector3(-3.2f, .55f, -4.7f), new Vector3(0, .4f, -4.5f));

                marker = new GameObject("Owned coach glass transmission marker"); marker.transform.SetParent(coach.transform, false);
                markerMesh = new Mesh { name = "Owned marker plane" };
                markerMesh.vertices = new[] { new Vector3(-.8f, 1.92f, -4), new Vector3(-.8f, 2.42f, -4), new Vector3(-.8f, 2.42f, -3.2f), new Vector3(-.8f, 1.92f, -3.2f) };
                markerMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 }; markerMesh.RecalculateNormals(); markerMesh.RecalculateBounds();
                marker.AddComponent<MeshFilter>().sharedMesh = markerMesh;
                markerMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")); markerMaterial.SetColor("_BaseColor", new Color(.03f, 1, .03f, 1));
                var markRenderer = marker.AddComponent<MeshRenderer>(); markRenderer.sharedMaterial = markerMaterial; markRenderer.shadowCastingMode = ShadowCastingMode.Off;
                yield return Capture("window-transmission", new Vector3(-2.4f, 2.17f, -3.6f), new Vector3(-.8f, 2.17f, -3.6f));
                float transmitted = GreenDominance(latest);
                opaqueControl = new Material(materials[1]); var tint = opaqueControl.GetColor("_BaseColor"); tint.a = 1; opaqueControl.SetColor("_BaseColor", tint);
                renderer.sharedMaterials = new[] { materials[0], opaqueControl };
                yield return Capture("window-opacity-control", new Vector3(-2.4f, 2.17f, -3.6f), new Vector3(-.8f, 2.17f, -3.6f));
                float blocked = GreenDominance(latest);
                File.WriteAllText(Path.Combine(directory, "measurement.json"), "{\"transmittedGreenDominance\":" + transmitted.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                    + ",\"opacityControlGreenDominance\":" + blocked.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ",\"width\":1600,\"height\":900}");
                Assert.That(transmitted, Is.GreaterThan(blocked + .12f), "Actual imported glass must transmit the marker; an opaque clone of the same pane must block it.");
            }
            finally
            {
                renderer.sharedMaterials = materials;
                camera.transform.SetPositionAndRotation(oldPosition, oldRotation); camera.targetTexture = oldTarget;
                camera.fieldOfView = oldFov; camera.aspect = oldAspect; camera.cullingMask = oldMask;
                driver.enabled = driverEnabled; if (brain) brain.enabled = brainEnabled;
                RenderTexture.active = oldActive; target.Release();
                if (marker) Object.Destroy(marker); if (markerMesh) Object.Destroy(markerMesh);
                if (markerMaterial) Object.Destroy(markerMaterial); if (opaqueControl) Object.Destroy(opaqueControl);
                Object.Destroy(target); Object.Destroy(pixels);
            }
            Assert.That(renderer.sharedMaterials[0], Is.SameAs(materials[0])); Assert.That(renderer.sharedMaterials[1], Is.SameAs(materials[1]));
            RailCoachMaterialTests.AssertRuntimeMaterials(materials);
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            GameInput.SimMove = Vector2.zero; GameInput.Simulated = false;
            GameInput.SimInteract = GameInput.SimLeave = GameInput.SimSplit = GameInput.SimSwitch = GameInput.SimJump = GameInput.SimSprint = false;
            Time.timeScale = 1; RunState.NewGame(); SceneManager.LoadScene(GameManager.MenuScene); yield return null;
        }
    }
}
