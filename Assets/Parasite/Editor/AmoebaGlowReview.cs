using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Parasite.FX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using L = Parasite.World.CityLayout;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Parasite.EditorTools
{
    /// <summary>Unsaved, public-AmoebaVisual-API glow review in the real city. Does not exercise gameplay possession.</summary>
    public static class AmoebaGlowReview
    {
        const int Seed = 74821, Steps = 180;
        const float Dt = 1f / 60f, StartTime = 20f;
        [Serializable] sealed class ComponentInfo
        {
            public string name, shader;
            public Vector3 localPosition, localScale;
            public Color color;
            public float intensity, phase, pulseRate;
        }
        [Serializable] sealed class StateInfo
        {
            public string state, apiSequence, file;
            public Vector3 ownerPosition, bodyScale;
            public float activity, infectionPulse, shellEmission, refraction, alpha, nucleusIntensity, radianceScale, lightIntensity, lightRange;
            public float shaderClockStart, shaderClockEnd, simulationTime;
            public List<ComponentInfo> interior = new();
            public long pngBytes;
        }
        [Serializable] sealed class Receipt
        {
            public string scope = "Visual-only edit-mode API review. No physical movement, possession/split/mission eligibility, native gameplay or performance acceptance claim.";
            public string shaderTime = "Public Step uses the same fixed simulation clock. URP's native edit-time shader clock is retained and recorded, so these are state-matched views, not identical-phase pixel-difference tests.";
            public string output, startedUtc, completedUtc, gpu, graphicsApi, pipeline, quality, colorSpace, error;
            public Vector3 cameraPosition, cameraTarget;
            public float fieldOfView = 60, renderScale, rain = .5f, ssrSteps = 28, bloomIntensity = -1, simulationDt = Dt;
            public int seed = Seed, simulationSteps = Steps, width = 1600, height = 900, msaaSamples;
            public bool completed, hdr = true, postProcessing = true, sceneSaved, liveMaterialsEdited;
            public List<string> shaderMessages = new();
            public List<StateInfo> states = new();
        }

        static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("[AmoebaGlowReview] " + message); }

        static string Output()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-amoebaReviewOut");
            string path = Path.GetFullPath(index >= 0 && index + 1 < args.Length ? args[index + 1]
                : Path.Combine(project, "Logs", "amoeba-glow-review-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ")));
            Require(path.StartsWith(Path.Combine(project, "Logs") + Path.DirectorySeparatorChar, StringComparison.Ordinal), "Output must be inside project Logs.");
            Require(!Directory.Exists(path) && !File.Exists(path), "Choose a fresh -amoebaReviewOut directory.");
            Directory.CreateDirectory(path); return path;
        }

        static List<string> ShaderMessages()
        {
            var result = new List<string>();
            foreach (string name in new[] { "Parasite/Amoeba", "Parasite/AmoebaNucleus" })
            {
                var shader = Shader.Find(name); Require(shader && shader.isSupported, "Actual GPU does not support " + name);
                foreach (var message in ShaderUtil.GetShaderMessages(shader))
                    result.Add(message.severity + " " + name + " " + message.platform + " " + message.message);
            }
            return result;
        }

        public static void Capture()
        {
            Require(Application.isBatchMode && !Application.isPlaying, "Use the serialized batch Editor wrapper in edit mode.");
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "Actual supported GPU rendering is required.");
            string output = Output();
            var receipt = new Receipt { output = output, startedUtc = DateTime.UtcNow.ToString("O"), gpu = SystemInfo.graphicsDeviceName,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(), quality = QualitySettings.names[QualitySettings.GetQualityLevel()], colorSpace = QualitySettings.activeColorSpace.ToString() };
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Require(pipeline, "Production URP asset is missing."); receipt.pipeline = pipeline.name; receipt.renderScale = pipeline.renderScale; receipt.msaaSamples = pipeline.msaaSampleCount;
            var randomState = Random.state; var oldActive = RenderTexture.active; float oldClock = SlimeTrail.ClockOverride;
            float oldRain = Shader.GetGlobalFloat("_ParasiteRain"), oldSteps = Shader.GetGlobalFloat("_ParasiteSSRSteps");
            bool oldSsrOff = Shader.IsKeywordEnabled("_PARASITE_SSR_OFF"); int oldBudget = LampLights.Budget; float oldDistance = LampLights.MaxDistance;
            var cameras = new List<(Camera camera, bool enabled)>(); GameObject cameraObject = null, blob = null;
            RenderTexture rt = null; Texture2D pixels = null;
            try
            {
                EditorSceneManager.OpenScene(CityBuilder.CityPath, OpenSceneMode.Single);
                foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) { cameras.Add((camera, camera.enabled)); camera.enabled = false; }
                foreach (var volume in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
                    if (volume.isGlobal && volume.sharedProfile && volume.sharedProfile.TryGet<Bloom>(out var bloom)) receipt.bloomIntensity = bloom.intensity.value;
                cameraObject = new GameObject("Amoeba glow review camera - unsaved"); var cam = cameraObject.AddComponent<Camera>();
                cam.fieldOfView = 60; cam.nearClipPlane = .08f; cam.farClipPlane = 900; cam.allowHDR = true;
                var extra = cameraObject.AddComponent<UniversalAdditionalCameraData>(); extra.renderPostProcessing = true;
                extra.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; extra.renderShadows = true;
                // Exact PhotoTool alley camera and final slide endpoint, followed by a stationary settling interval.
                Vector3 start = L.PlayerSpawn + new Vector3(.4f, 0, 7) - Vector3.forward * 1.8f;
                Vector3 finish = start + Vector3.forward * 3.48f;
                receipt.cameraPosition = L.PlayerSpawn + new Vector3(1.4f, 1.05f, 11.6f); receipt.cameraTarget = finish + Vector3.up * .32f;
                cam.transform.SetPositionAndRotation(receipt.cameraPosition, Quaternion.LookRotation(receipt.cameraTarget - receipt.cameraPosition));
                rt = new RenderTexture(receipt.width, receipt.height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1, hideFlags = HideFlags.DontSave }; rt.Create();
                pixels = new Texture2D(receipt.width, receipt.height, TextureFormat.RGB24, false) { hideFlags = HideFlags.DontSave }; cam.targetTexture = rt;
                LampLights.Budget = 32; LampLights.MaxDistance = 70; Shader.SetGlobalFloat("_ParasiteRain", .5f); Shader.SetGlobalFloat("_ParasiteSSRSteps", 28); Shader.DisableKeyword("_PARASITE_SSR_OFF");
                var lamps = Object.FindObjectsByType<LampLights>(FindObjectsSortMode.None); foreach (var lamp in lamps) lamp.Select(receipt.cameraPosition, instant: true);
                foreach (string state in new[] { "inactive", "active", "infection" })
                {
                    Random.InitState(Seed);
                    blob = new GameObject("Glow review " + state + " - unsaved"); blob.transform.position = start;
                    int layer = LayerMask.NameToLayer("Amoeba"); if (layer >= 0) blob.layer = layer;
                    var visual = blob.AddComponent<AmoebaVisual>(); visual.Build(.38f); visual.SetActive(true);
                    var particles = blob.GetComponentsInChildren<ParticleSystem>(true);
                    for (int i = 0; i < particles.Length; i++)
                    {
                        particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); particles[i].useAutoRandomSeed = false;
                        particles[i].randomSeed = (uint)(Seed + i * 3131); particles[i].Play();
                    }
                    for (int i = 0; i < Steps; i++)
                    {
                        if (state == "inactive" && i == 120) visual.SetActive(false);
                        // Real public control-reactivation API produces the transient infect pulse while keeping its current active level.
                        // This is a visual feedback probe, not evidence that gameplay possession eligibility was exercised.
                        if (state == "infection" && i == Steps - 1) { visual.SetActive(false); visual.SetActive(true); }
                        float speed = i < 80 ? 2.4f : i < 108 ? .6f : 0;
                        blob.transform.position += Vector3.forward * speed * Dt; visual.SetMotion(Vector3.forward * speed, true);
                        float time = StartTime + (i + 1) * Dt; SlimeTrail.ClockOverride = time; visual.Step(Dt, time);
                        foreach (var ps in particles) ps.Simulate(Dt, withChildren: false, restart: false, fixedTimeStep: true);
                    }
                    Require(Vector3.Distance(blob.transform.position, finish) < .002f, "The fixed public-motion preview missed its common endpoint.");
                    var body = blob.transform.Find("AmoebaPivot/AmoebaBody"); Require(body, "AmoebaVisual did not build its actual shell.");
                    var shell = body.GetComponent<MeshRenderer>().sharedMaterial; var nucleus = body.Find("Nucleus").GetComponent<MeshRenderer>().sharedMaterial;
                    Require(shell && shell.shader.name == "Parasite/Amoeba" && nucleus && nucleus.shader.name == "Parasite/AmoebaNucleus", "Shader fallback cannot satisfy this review.");
                    var light = blob.GetComponentsInChildren<Light>().Single();
                    var row = new StateInfo { state = state, ownerPosition = blob.transform.position, bodyScale = body.parent.localScale,
                        apiSequence = state == "inactive" ? "Build; SetActive(true); 2s common Step; SetActive(false); 1s Step"
                            : state == "active" ? "Build; SetActive(true); 3s fixed Step"
                            : "Build; SetActive(true); fixed Step; before final Step SetActive(false), SetActive(true); final1/60s Step",
                        activity = shell.GetFloat("_Activity"), infectionPulse = shell.GetFloat("_InfectPulse"), shellEmission = shell.GetFloat("_Emission"),
                        refraction = shell.GetFloat("_Refraction"), alpha = shell.GetFloat("_Alpha"), nucleusIntensity = nucleus.GetFloat("_Intensity"),
                        radianceScale = nucleus.GetFloat("_RadianceScale"), lightIntensity = light.intensity, lightRange = light.range, simulationTime = StartTime + Steps * Dt };
                    Require(Mathf.Abs(row.activity - (state == "inactive" ? 0 : 1)) < .001f, state + " did not reach its public API activity level.");
                    Require(state == "infection" ? row.infectionPulse > .6f : row.infectionPulse < .001f, state + " has an unexpected pulse state.");
                    Require(Mathf.Abs(row.radianceScale - .18f) < .001f && Mathf.Abs(row.shellEmission - .12f) < .001f && Mathf.Abs(row.refraction - .012f) < .001f,
                        "Review requires the actual regenerated calibrated material templates.");
                    foreach (var renderer in body.GetComponentsInChildren<MeshRenderer>().Where(r => r.name == "Nucleus" || r.name.StartsWith("Organelle", StringComparison.Ordinal)))
                    {
                        var material = renderer.sharedMaterial;
                        row.interior.Add(new ComponentInfo { name = renderer.name, shader = material.shader.name, localPosition = renderer.transform.localPosition, localScale = renderer.transform.localScale,
                            color = material.GetColor("_Color"), intensity = material.GetFloat("_Intensity"), phase = material.GetFloat("_Phase"), pulseRate = material.GetFloat("_PulseRate") });
                    }
                    Require(row.interior.Count == 6, "Expected real nucleus plus five actual organelles.");
                    row.shaderClockStart = Time.realtimeSinceStartup;
                    for (int i = 0; i < 3; i++)
                    {
                        var request = new RenderPipeline.StandardRequest { destination = rt };
                        if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request); else cam.Render();
                    }
                    row.shaderClockEnd = Time.realtimeSinceStartup;
                    RenderTexture.active = rt; pixels.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); pixels.Apply(); RenderTexture.active = oldActive;
                    row.file = Path.Combine(output, state + ".png"); File.WriteAllBytes(row.file, pixels.EncodeToPNG()); row.pngBytes = new FileInfo(row.file).Length; receipt.states.Add(row);
                    Object.DestroyImmediate(blob); blob = null;
                }
                Require(receipt.states[1].nucleusIntensity > receipt.states[0].nucleusIntensity && receipt.states[2].nucleusIntensity > receipt.states[1].nucleusIntensity,
                    "Nucleus active/infection response did not remain ordered.");
                Require(receipt.states[1].lightIntensity > receipt.states[0].lightIntensity && receipt.states[2].lightIntensity > receipt.states[1].lightIntensity,
                    "Point-light active/infection feedback did not remain ordered.");
                receipt.shaderMessages = ShaderMessages(); Require(!receipt.shaderMessages.Any(m => m.StartsWith("Error ", StringComparison.Ordinal)), "Actual shader compiler reported errors.");
                receipt.completed = true;
            }
            catch (Exception e) { receipt.error = e.ToString(); throw; }
            finally
            {
                if (blob) Object.DestroyImmediate(blob); if (cameraObject) Object.DestroyImmediate(cameraObject);
                if (rt) Object.DestroyImmediate(rt); if (pixels) Object.DestroyImmediate(pixels);
                foreach (var pair in cameras) if (pair.camera) pair.camera.enabled = pair.enabled;
                Random.state = randomState; SlimeTrail.ClockOverride = oldClock; RenderTexture.active = oldActive;
                LampLights.Budget = oldBudget; LampLights.MaxDistance = oldDistance; Shader.SetGlobalFloat("_ParasiteRain", oldRain); Shader.SetGlobalFloat("_ParasiteSSRSteps", oldSteps);
                if (oldSsrOff) Shader.EnableKeyword("_PARASITE_SSR_OFF"); else Shader.DisableKeyword("_PARASITE_SSR_OFF");
                receipt.completedUtc = DateTime.UtcNow.ToString("O"); File.WriteAllText(Path.Combine(output, "receipt.json"), JsonUtility.ToJson(receipt, true) + "\n");
            }
            Debug.Log("[AmoebaGlowReview] complete " + output);
        }

        public static void CaptureBatch()
        {
            try { Capture(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
