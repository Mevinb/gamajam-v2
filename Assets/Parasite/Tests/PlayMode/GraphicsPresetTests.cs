using NUnit.Framework;
using Parasite.FX;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Parasite.Tests
{
    public class GraphicsPresetTests
    {
        int originalPreset;
        GameObject cameraObject;

        [SetUp]
        public void SavePreset() => originalPreset = GraphicsPreset.Current;

        [TearDown]
        public void RestorePreset()
        {
            if (cameraObject) Object.DestroyImmediate(cameraObject);
            GraphicsPreset.Apply(originalPreset, save: false);
        }

        [Test]
        public void LoweringPresetReleasesHdrMsaaAndReflectionBudget()
        {
            cameraObject = new GameObject("Graphics preset regression camera");
            var camera = cameraObject.AddComponent<Camera>();
            var data = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Assert.IsNotNull(pipeline, "The generated player uses URP.");

            GraphicsPreset.Apply(3, save: false);
            Assert.IsTrue(camera.allowHDR);
            Assert.IsTrue(camera.allowMSAA);
            Assert.AreEqual(4, pipeline.msaaSampleCount);
            Assert.AreEqual(36, Shader.GetGlobalFloat("_ParasiteSSRSteps"));

            GraphicsPreset.Apply(0, save: false);
            Assert.IsFalse(camera.allowHDR, "Ultra -> Low must undo HDR on existing cameras.");
            Assert.IsFalse(camera.allowMSAA);
            Assert.IsFalse(pipeline.supportsHDR);
            Assert.AreEqual(1, pipeline.msaaSampleCount);
            Assert.IsTrue(Shader.IsKeywordEnabled("_PARASITE_SSR_OFF"));
            Assert.AreEqual(0, Shader.GetGlobalFloat("_ParasiteSSRSteps"));
            Assert.AreEqual(AntialiasingMode.FastApproximateAntialiasing, data.antialiasing);
            Assert.AreEqual(AntialiasingQuality.Medium, data.antialiasingQuality);

            GraphicsPreset.Apply(2, save: false);
            Assert.IsTrue(camera.allowHDR);
            Assert.IsFalse(camera.allowMSAA);
            Assert.IsFalse(Shader.IsKeywordEnabled("_PARASITE_SSR_OFF"));
            Assert.AreEqual(28, Shader.GetGlobalFloat("_ParasiteSSRSteps"));
            Assert.AreEqual(AntialiasingMode.SubpixelMorphologicalAntiAliasing, data.antialiasing);
        }

        [Test]
        public void NewlyCreatedCinematicCameraUsesSavedPolicyWithoutChangingPreset()
        {
            GraphicsPreset.Apply(1, save: false);
            cameraObject = new GameObject("Late cinematic camera");
            var camera = cameraObject.AddComponent<Camera>();
            var data = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            camera.allowHDR = true;
            camera.allowMSAA = true;
            data.antialiasing = AntialiasingMode.None;
            data.renderPostProcessing = false;
            GraphicsPreset.ApplyCamera(camera);
            Assert.AreEqual(1, GraphicsPreset.Current);
            Assert.IsFalse(camera.allowHDR);
            Assert.IsFalse(camera.allowMSAA);
            Assert.AreEqual(AntialiasingMode.FastApproximateAntialiasing, data.antialiasing);
            Assert.IsFalse(data.renderPostProcessing, "Preset changes preserve a film set's authored post-processing choice.");
            Assert.AreEqual(16, Shader.GetGlobalFloat("_ParasiteSSRSteps"));
        }

        [Test]
        public void IsolatedStreetLampKeepsFullIntensityWhenBudgetIsNotExhausted()
        {
            int oldBudget = LampLights.Budget;
            float oldDistance = LampLights.MaxDistance;
            cameraObject = new GameObject("Isolated street lamp regression");
            var lamp = cameraObject.AddComponent<Light>();
            lamp.intensity = 4f;
            var controller = cameraObject.AddComponent<LampLights>();
            controller.lights = new[] { lamp };
            controller.baseIntensity = new[] { 4f };
            try
            {
                LampLights.Budget = 64;
                LampLights.MaxDistance = 110f;
                controller.Select(cameraObject.transform.position, instant: true);
                Assert.IsTrue(lamp.enabled);
                Assert.That(lamp.intensity, Is.EqualTo(4f).Within(0.001f));
            }
            finally
            {
                LampLights.Budget = oldBudget;
                LampLights.MaxDistance = oldDistance;
            }
        }
    }
}
