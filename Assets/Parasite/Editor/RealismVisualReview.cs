using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Parasite.Characters;
using Parasite.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Parasite.EditorTools
{
    /// <summary>Rendered asset review in the generated city. Capture stages unsaved subjects; the explicit build wrapper regenerates through CityBuilder.</summary>
    public static class RealismVisualReview
    {
        [Serializable] sealed class Shot
        {
            public string name, subject, file;
            public Vector3 position, lookAt;
            public float fieldOfView;
            public int width, height;
            public long bytes;
        }
        [Serializable] sealed class TextureInfo
        {
            public string property, path, format, importerType, standaloneFormat;
            public int width, height, mipCount, maxSize;
            public bool sRGB, streaming, normalFlipGreen;
        }
        [Serializable] sealed class MaterialInfo
        {
            public string name, shader;
            public Color rawSpecular, vein, emission;
            public float mode, skinSurfaceEnabled, metallic, smoothness;
            public List<TextureInfo> textures = new();
        }
        [Serializable] sealed class Subject
        {
            public string id, kind, prefab, avatar, controller;
            public bool humanoid, usesAnimator, neutralIdle, fallback;
            public int rendererCount, boneCount, materialSlots, wheelPivots;
            public Vector3 position, boundsSize;
            public List<MaterialInfo> materials = new();
        }
        [Serializable] sealed class ShaderInfo
        {
            public string name, path;
            public bool supported;
            public List<string> messages = new();
        }
        [Serializable] sealed class JawProbe
        {
            public float originalMuscle, readbackMuscle, maximumBakedVertexDelta;
            public Quaternion before, afterSet, afterShoot;
        }
        [Serializable] sealed class Receipt
        {
            public string scope = "Actual generated-city GPU images and asset binding review; no subjective realism, native gameplay, FPS or VRAM acceptance claim.";
            public string startedUtc, completedUtc, output, scene, gpu, graphicsApi, pipeline, quality, error;
            public bool completed, regeneratedBeforeCapture, sceneSaved, assetsChanged;
            public Vector3 sharedHumanLocation;
            public List<Shot> shots = new();
            public List<Subject> subjects = new();
            public List<ShaderInfo> shaders = new();
        }

        static bool regenerated;

        static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("[RealismReview] " + message); }

        static string OutputDirectory()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string logs = Path.Combine(project, "Logs") + Path.DirectorySeparatorChar;
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-realismReviewOut");
            string value = index >= 0 && index + 1 < args.Length ? args[index + 1]
                : Path.Combine(project, "Logs", "realism-visual-review-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ"));
            string path = Path.GetFullPath(value);
            Require(path.StartsWith(logs, StringComparison.Ordinal), "Output must be a fresh directory inside this project's Logs.");
            Require(!Directory.Exists(path) && !File.Exists(path), "Output already exists; choose a fresh -realismReviewOut directory.");
            Directory.CreateDirectory(path); return path;
        }

        static MaterialInfo InspectMaterial(Material m, HashSet<Shader> used)
        {
            Require(m && m.shader && m.shader.name != "Hidden/InternalErrorShader", "Missing/error fallback material.");
            used.Add(m.shader);
            var info = new MaterialInfo { name = m.name, shader = m.shader.name,
                rawSpecular = m.HasProperty("_SpecColor") ? m.GetColor("_SpecColor") : Color.clear,
                vein = m.HasProperty("_VeinColor") ? m.GetColor("_VeinColor") : Color.black,
                emission = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black,
                mode = m.HasProperty("_CharacterMode") ? m.GetFloat("_CharacterMode") : -1,
                skinSurfaceEnabled = m.HasProperty("_SkinSurfaceEnabled") ? m.GetFloat("_SkinSurfaceEnabled") : 0,
                metallic = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0,
                smoothness = m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness") : 0 };
            foreach (string property in m.GetTexturePropertyNames())
            {
                if (!(m.GetTexture(property) is Texture2D texture)) continue;
                string path = AssetDatabase.GetAssetPath(texture);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                var row = new TextureInfo { property = property, path = path, width = texture.width, height = texture.height,
                    mipCount = texture.mipmapCount, format = texture.format.ToString(), streaming = texture.streamingMipmaps };
                if (importer)
                {
                    row.sRGB = importer.sRGBTexture; row.importerType = importer.textureType.ToString();
                    row.standaloneFormat = importer.GetPlatformTextureSettings("Standalone").format.ToString();
                    row.maxSize = importer.maxTextureSize; row.normalFlipGreen = importer.flipGreenChannel;
                }
                info.textures.Add(row);
            }
            return info;
        }

        static void RawF0(Material m, float expected)
        {
            Require(m && m.HasProperty("_SpecColor") && m.IsKeywordEnabled("_SPECULAR_SETUP"), "Expected specular-workflow character material.");
            Color value = m.GetColor("_SpecColor");
            Require(Mathf.Abs(value.r - expected) < .0001f && Mathf.Abs(value.g - expected) < .0001f && Mathf.Abs(value.b - expected) < .0001f,
                m.name + " raw _SpecColor differs from linear " + expected + ": " + value);
        }

        static Subject InspectHuman(HumanoidRig rig, CharacterCatalog.Entry entry, HashSet<Shader> used)
        {
            Require(rig.VariantId == entry.id && rig.Model && rig.Animator && rig.Animator.avatar && rig.Animator.avatar.isValid && rig.Animator.avatar.isHuman && rig.UsesAnimator,
                entry.id + " did not build the named skinned humanoid Animator; emergency/procedural fallback is unacceptable for this review.");
            var renderers = rig.Model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Require(renderers.Length > 0, entry.id + " has no skinned mesh.");
            foreach (var renderer in renderers)
                Require(renderer.sharedMesh && renderer.bones.Length > 20 && renderer.sharedMaterials.Length == 4 &&
                    renderer.sharedMaterials.All(m => m && m.shader && m.shader.name == "Parasite/Character"), entry.id + " has an invalid mesh, skeleton or four-slot material binding.");
            var distinct = renderers.SelectMany(r => r.sharedMaterials).Distinct().ToArray();
            var skin = distinct.Single(m => m.name.Contains("_Skin")); var eyes = distinct.Single(m => m.name.Contains("_Eyes"));
            RawF0(skin, .028f); RawF0(eyes, .025f);
            Require(skin.GetFloat("_SkinSurfaceEnabled") > .5f && skin.GetTexture("_BumpMap") && skin.GetTexture("_SkinSurfaceMap"), entry.id + " is missing authored skin N/M maps.");
            var normal = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(skin.GetTexture("_BumpMap"))) as TextureImporter;
            var mask = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(skin.GetTexture("_SkinSurfaceMap"))) as TextureImporter;
            Require(normal && normal.textureType == TextureImporterType.NormalMap && !normal.sRGBTexture && !normal.flipGreenChannel && normal.mipmapEnabled,
                entry.id + " skin normal import policy is incorrect.");
            Require(mask && !mask.sRGBTexture && mask.mipmapEnabled, entry.id + " skin surface map must be linear with mips.");
            var info = new Subject { id = entry.id, kind = "Runtime HumanoidRig, forced LOD0, neutral civilian idle for a controlled visual comparison",
                prefab = AssetDatabase.GetAssetPath(entry.prefab), avatar = rig.Animator.avatar.name,
                controller = AssetDatabase.GetAssetPath(rig.Animator.runtimeAnimatorController), humanoid = true, usesAnimator = true, neutralIdle = true,
                fallback = false, rendererCount = renderers.Length, boneCount = renderers[0].bones.Length, materialSlots = 4, position = rig.transform.position };
            foreach (var m in distinct) info.materials.Add(InspectMaterial(m, used));
            var bounds = renderers[0].bounds; foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds); info.boundsSize = bounds.size;
            return info;
        }

        static List<ShaderInfo> InspectShaders(HashSet<Shader> used)
        {
            var result = new List<ShaderInfo>();
            foreach (var shader in used.OrderBy(s => s.name))
            {
                var row = new ShaderInfo { name = shader.name, path = AssetDatabase.GetAssetPath(shader), supported = shader.isSupported };
                foreach (var message in ShaderUtil.GetShaderMessages(shader))
                    row.messages.Add(message.severity + " " + message.platform + " " + message.file + ":" + message.line + " " + message.message);
                result.Add(row);
            }
            return result;
        }

        public static void Capture()
        {
            Require(Application.isBatchMode && !Application.isPlaying, "Run through Tools/unity.sh in a batch Editor after scene regeneration.");
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "Actual GPU rendering is required; do not pass -nographics.");
            string directory = OutputDirectory();
            var receipt = new Receipt { startedUtc = DateTime.UtcNow.ToString("O"), output = directory, scene = CityBuilder.CityPath,
                gpu = SystemInfo.graphicsDeviceName, graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                pipeline = GraphicsSettings.currentRenderPipeline ? GraphicsSettings.currentRenderPipeline.name : "none",
                quality = QualitySettings.names[QualitySettings.GetQualityLevel()], sharedHumanLocation = new Vector3(-27f, .15f, 9f),
                regeneratedBeforeCapture = regenerated, sceneSaved = regenerated, assetsChanged = regenerated };
            var owned = new List<GameObject>(); var cameras = new List<(Camera camera, bool enabled)>(); var used = new HashSet<Shader>();
            RenderTexture rt = null; Texture2D pixels = null; var priorActive = RenderTexture.active;
            try
            {
                var menuScene = EditorSceneManager.OpenScene(CityBuilder.BootstrapPath, OpenSceneMode.Single);
                SceneGeometryFingerprint.Write(menuScene, Path.Combine(directory, "Bootstrap.geometry.json"));
                var scene = EditorSceneManager.OpenScene(CityBuilder.CityPath, OpenSceneMode.Single);
                SceneGeometryFingerprint.Write(scene, Path.Combine(directory, "City.geometry.json"));
                int missing = scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
                Require(missing == 0, "Generated city has missing scripts; regenerate before reviewing.");
                foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    foreach (var m in renderer.sharedMaterials) if (m && m.shader) used.Add(m.shader);
                foreach (string name in new[] { "Parasite/Character", "Parasite/RealisticSurface", "Parasite/Atmosphere", "Parasite/Sky" })
                { var shader = Shader.Find(name); Require(shader, "Missing production shader " + name); used.Add(shader); }
                foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) { cameras.Add((camera, camera.enabled)); camera.enabled = false; }
                var cameraGo = new GameObject("Realism review camera - unsaved"); owned.Add(cameraGo);
                var cam = cameraGo.AddComponent<Camera>(); cam.nearClipPlane = .05f; cam.farClipPlane = 900f; cam.allowHDR = true;
                var data = cameraGo.AddComponent<UniversalAdditionalCameraData>(); data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; data.renderShadows = true;
                var lamps = Object.FindObjectsByType<FX.LampLights>(FindObjectsSortMode.None);

                void Shoot(string name, string subject, Vector3 position, Vector3 look, float fov = 60, int width = 1600, int height = 900)
                {
                    if (!rt || rt.width != width || rt.height != height)
                    {
                        if (rt) { cam.targetTexture = null; Object.DestroyImmediate(rt); }
                        if (pixels) Object.DestroyImmediate(pixels);
                        rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1, hideFlags = HideFlags.DontSave };
                        rt.Create(); pixels = new Texture2D(width, height, TextureFormat.RGB24, false) { hideFlags = HideFlags.DontSave };
                    }
                    cam.transform.SetPositionAndRotation(position, Quaternion.LookRotation(look - position)); cam.fieldOfView = fov; cam.aspect = (float)width / height; cam.targetTexture = rt;
                    foreach (var lamp in lamps) lamp.Select(position, instant: true);
                    for (int i = 0; i < 3; i++)
                    {
                        var request = new RenderPipeline.StandardRequest { destination = rt };
                        if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request); else cam.Render();
                    }
                    RenderTexture.active = rt; pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply(); RenderTexture.active = priorActive;
                    string path = Path.Combine(directory, name + ".png"); File.WriteAllBytes(path, pixels.EncodeToPNG());
                    receipt.shots.Add(new Shot { name = name, subject = subject, file = path, position = position, lookAt = look, fieldOfView = fov, width = width, height = height, bytes = new FileInfo(path).Length });
                    Debug.Log("[RealismReview] captured " + name);
                }

                foreach (var shot in PhotoTool.Shots.Where(s => new[] { "01_spawn_alley", "03_avenue_north", "05_plaza_shops", "07_main_gate", "11_lab_corridor", "14_aerial", "15_street_level_detail" }.Contains(s.name)))
                    Shoot("city_" + shot.name, "Generated city; PhotoTool viewpoint", shot.pos, shot.lookAt);
                Shoot("city_ground_curbs", "Existing street footprint with Blender curb stones and scan-derived ground", new Vector3(-5.5f, .85f, -48), new Vector3(-7.2f, .16f, -43), 48);
                Shoot("city_facade_reveals", "Blender window reveals aligned with photographed facade bays", new Vector3(-5.5f, 3.3f, -49), new Vector3(-12.0f, 8f, -53), 45);

                var catalog = CharacterCatalog.Load(); Require(catalog, "Character catalog is missing.");
                foreach (string id in new[] { "LakeGirl", "DrPetrov", "DrVale", "CivOmar" })
                {
                    var entry = catalog.FindById(id); Require(entry != null, "Named character is missing: " + id);
                    var root = new GameObject("Review " + id + " - unsaved"); owned.Add(root); root.transform.position = receipt.sharedHumanLocation;
                    var rig = root.AddComponent<HumanoidRig>(); rig.VariantOverride = id; rig.AmbientGestures = false; rig.Build(entry.profession, 731, id == "DrVale");
                    Require(rig.Animator && rig.UsesAnimator, id + " cannot be reviewed in a fallback pose.");
                    foreach (var lod in root.GetComponentsInChildren<LODGroup>()) lod.ForceLOD(0);
                    rig.SetPose(ActorPose.None); rig.Animate(Vector3.zero, true, HostState.Normal, 4.6f, 0);
                    rig.Animator.SetInteger(HumanAnimParams.Profession, (int)Profession.Civilian); rig.Animator.SetFloat(HumanAnimParams.Variant, .25f);
                    rig.Animator.Play(HumanAnimParams.StateIdleCivilian, 0, .25f); rig.FastForward(.8f); rig.SetEyesGlow(false);
                    Physics.SyncTransforms(); var subject = InspectHuman(rig, entry, used); receipt.subjects.Add(subject);
                    Vector3 head = rig.Head.position + Vector3.up * .045f;
                    Shoot("human_" + id + "_full", id + ": real HumanoidRig neutral idle, LOD0", root.transform.position + new Vector3(.65f, 1.05f, 4.4f), root.transform.position + Vector3.up * .92f, 29, 960, 1280);
                    Shoot("human_" + id + "_portrait", id + ": raw linear skin F0 .028 / eyes .025", head + new Vector3(.22f, .025f, 1.45f), head, 26, 960, 1280);
                    var skin = rig.Renderers.SelectMany(r => r.sharedMaterials).Distinct().Single(m => m.name.Contains("_Skin"));
                    float bump = skin.GetFloat("_BumpScale"), ao = skin.GetFloat("_OcclusionStrength");
                    skin.SetFloat("_BumpScale", 0);
                    Shoot("control_" + id + "_skin_normal_off", id + ": diagnostic only; same camera and lighting, skin normal disabled", head + new Vector3(.22f, .025f, 1.45f), head, 26, 960, 1280);
                    skin.SetFloat("_BumpScale", bump); skin.SetFloat("_OcclusionStrength", 0);
                    Shoot("control_" + id + "_skin_ao_off", id + ": diagnostic only; same camera and lighting, local skin AO disabled", head + new Vector3(.22f, .025f, 1.45f), head, 26, 960, 1280);
                    skin.SetFloat("_OcclusionStrength", ao);
                    if (Array.IndexOf(Environment.GetCommandLineArgs(), "-realismHairControls") >= 0)
                    {
                        var hair = rig.Renderers.SelectMany(r => r.sharedMaterials).Distinct().Single(m => m.name.Contains("_Hair"));
                        float depth = hair.GetFloat("_ZWrite"); hair.SetFloat("_ZWrite", 1);
                        Shoot("control_" + id + "_hair_depth_on", id + ": diagnostic only; same blended hair with forward depth writes", head + new Vector3(.22f, .025f, 1.45f), head, 26, 960, 1280);
                        hair.SetFloat("_ZWrite", depth);
                    }
                    using (var pose = new HumanPoseHandler(rig.Animator.avatar, rig.Animator.transform))
                    {
                        var original = new HumanPose(); pose.GetHumanPose(ref original);
                        int jaw = Array.FindIndex(HumanTrait.MuscleName, name => name == "Jaw Close");
                        if (jaw >= 0)
                        {
                            var jawBone = rig.Animator.GetBoneTransform(HumanBodyBones.Jaw);
                            var probe = new JawProbe { originalMuscle = original.muscles[jaw], before = jawBone ? jawBone.localRotation : Quaternion.identity };
                            var renderer = rig.Model.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name.EndsWith("_LOD0", StringComparison.Ordinal));
                            var neutralMesh = new Mesh(); var closedMesh = new Mesh(); renderer.BakeMesh(neutralMesh);
                            var closed = original; closed.muscles = (float[])original.muscles.Clone(); closed.muscles[jaw] = 1f;
                            Debug.Log("[RealismReview] " + id + " original Jaw Close=" + original.muscles[jaw] + " jaw=" + rig.Animator.GetBoneTransform(HumanBodyBones.Jaw));
                            pose.SetHumanPose(ref closed);
                            probe.afterSet = jawBone ? jawBone.localRotation : Quaternion.identity;
                            var readback = new HumanPose(); pose.GetHumanPose(ref readback); probe.readbackMuscle = readback.muscles[jaw];
                            renderer.BakeMesh(closedMesh); var beforeVertices = neutralMesh.vertices; var afterVertices = closedMesh.vertices;
                            for (int i = 0; i < beforeVertices.Length; i++) probe.maximumBakedVertexDelta = Mathf.Max(probe.maximumBakedVertexDelta, Vector3.Distance(beforeVertices[i], afterVertices[i]));
                            Shoot("control_" + id + "_jaw_closed", id + ": diagnostic only; Jaw Close muscle=1, same camera", head + new Vector3(.22f, .025f, 1.45f), head, 26, 960, 1280);
                            probe.afterShoot = jawBone ? jawBone.localRotation : Quaternion.identity;
                            File.WriteAllText(Path.Combine(directory, id + "-jaw-probe.json"), JsonUtility.ToJson(probe, true) + "\n");
                            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-realismJawBakeControls") >= 0)
                            {
                                // Render explicit baked poses: a same-frame skinned GPU cache can retain
                                // the first pose even though bone transforms and BakeMesh have changed.
                                var baked = new GameObject("Baked jaw diagnostic - unsaved"); owned.Add(baked);
                                baked.transform.SetParent(renderer.transform, false); baked.layer = renderer.gameObject.layer;
                                var filter = baked.AddComponent<MeshFilter>(); var draw = baked.AddComponent<MeshRenderer>();
                                draw.sharedMaterials = renderer.sharedMaterials; draw.shadowCastingMode = renderer.shadowCastingMode;
                                draw.receiveShadows = renderer.receiveShadows; draw.lightProbeUsage = renderer.lightProbeUsage; draw.reflectionProbeUsage = renderer.reflectionProbeUsage;
                                bool enabled = renderer.enabled; renderer.enabled = false; var other = new Mesh();
                                try
                                {
                                    filter.sharedMesh = neutralMesh;
                                    Shoot("control_" + id + "_baked_jaw_neutral", id + ": diagnostic baked Animator neutral geometry", head + new Vector3(.22f, .025f, 1.45f), head, 26, 960, 1280);
                                    filter.sharedMesh = closedMesh;
                                    Shoot("control_" + id + "_baked_jaw_plus_one", id + ": diagnostic baked Jaw Close=+1 geometry", head + new Vector3(.22f, .025f, 1.45f), head, 26, 960, 1280);
                                    closed.muscles[jaw] = -1; pose.SetHumanPose(ref closed); renderer.BakeMesh(other); filter.sharedMesh = other;
                                    Shoot("control_" + id + "_baked_jaw_minus_one", id + ": diagnostic baked Jaw Close=-1 geometry", head + new Vector3(.22f, .025f, 1.45f), head, 26, 960, 1280);
                                }
                                finally { renderer.enabled = enabled; Object.DestroyImmediate(baked); owned.Remove(baked); Object.DestroyImmediate(other); }
                            }
                            Object.DestroyImmediate(neutralMesh); Object.DestroyImmediate(closedMesh);
                            pose.SetHumanPose(ref original);
                        }
                    }
                    rig.SetEyesGlow(true);
                    Require(skin.GetColor("_VeinColor").maxColorComponent > 0, id + " possession veins did not activate through HumanoidRig.");
                    Shoot("human_" + id + "_possession", id + ": same neutral pose and camera; actual SetEyesGlow(true) skin/eyes", head + new Vector3(.22f, .025f, 1.45f), head, 26, 960, 1280);
                    subject.materials = rig.Renderers.SelectMany(r => r.sharedMaterials).Distinct().Select(m => InspectMaterial(m, used)).ToList();
                    Object.DestroyImmediate(root); owned.Remove(root);
                }

                foreach (var body in new[] { VehicleBody.Sedan, VehicleBody.Van })
                {
                    var root = new GameObject("Review " + body + " - unsaved"); owned.Add(root); root.transform.position = new Vector3(-2.5f, 0, -10);
                    var visual = VehicleVisuals.Create(root.transform, body, new Color(.19f, .30f, .36f), false, out var wheels);
                    Require(wheels.Length == VehicleVisuals.WheelCentres(body).Length && wheels.Length == 4, body + " authored wheel pivots are incomplete.");
                    Require(visual.GetComponentsInChildren<Collider>(true).Length == 0, body + " visual resource unexpectedly contains colliders.");
                    foreach (var lod in visual.GetComponentsInChildren<LODGroup>()) lod.ForceLOD(0);
                    var renderers = visual.GetComponentsInChildren<MeshRenderer>(true); Require(renderers.Length > 0, body + " has no Blender renderers.");
                    var distinct = renderers.SelectMany(r => r.sharedMaterials).Distinct().ToArray();
                    Require(distinct.Any(m => m && m.name == "VehiclePaint") && distinct.Any(m => m && m.name == "VehicleGlass"), body + " paint/glass bindings are missing.");
                    var subject = new Subject { id = body.ToString(), kind = "VehicleVisuals.Create, forced LOD0; no traffic/physics harness", prefab = AssetDatabase.GetAssetPath(VehicleVisuals.Prefab(body)), rendererCount = renderers.Length, wheelPivots = wheels.Length, position = root.transform.position };
                    foreach (var m in distinct) subject.materials.Add(InspectMaterial(m, used)); receipt.subjects.Add(subject);
                    float distance = body == VehicleBody.Van ? 8.8f : 7f;
                    Shoot("vehicle_" + body + "_front", body + ": authored body, glass and wheel pivots", root.transform.position + new Vector3(distance * .65f, 1.8f, distance), root.transform.position + Vector3.up * .8f, 39);
                    Shoot("vehicle_" + body + "_rear", body + ": authored rear and side", root.transform.position + new Vector3(-distance * .65f, 1.7f, -distance), root.transform.position + Vector3.up * .8f, 39);
                    Shoot("vehicle_" + body + "_roof", body + ": exterior roof and glazing from above", root.transform.position + new Vector3(distance * .55f, 4.2f, distance * .8f), root.transform.position + Vector3.up, 39);
                    Object.DestroyImmediate(root); owned.Remove(root);
                }
                receipt.shaders = InspectShaders(used);
                Require(receipt.shaders.All(s => s.supported), "A reviewed production shader is unsupported on the actual GPU.");
                Require(!receipt.shaders.Any(s => s.messages.Any(m => m.StartsWith("Error ", StringComparison.Ordinal))), "Actual shader compiler messages contain errors; inspect receipt.json.");
                receipt.completed = true;
            }
            catch (Exception e) { receipt.error = e.ToString(); receipt.shaders = InspectShaders(used); throw; }
            finally
            {
                RenderTexture.active = priorActive;
                foreach (var go in owned) if (go) Object.DestroyImmediate(go);
                foreach (var pair in cameras) if (pair.camera) pair.camera.enabled = pair.enabled;
                if (rt) Object.DestroyImmediate(rt); if (pixels) Object.DestroyImmediate(pixels);
                receipt.completedUtc = DateTime.UtcNow.ToString("O");
                File.WriteAllText(Path.Combine(directory, "receipt.json"), JsonUtility.ToJson(receipt, true) + "\n");
            }
            Debug.Log("[RealismReview] complete " + directory);
        }

        public static void CaptureBatch()
        {
            try { Capture(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void CharactersAndCaptureBatch()
        {
            try { CharacterModelImporter.Rebuild(); Capture(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// <summary>Root-owned combined import, existing asset verification, generated scenes and rendered review.</summary>
        public static void BuildAndCaptureBatch()
        {
            try
            {
                Require(Application.isBatchMode && !Application.isPlaying, "Use the serialized batch Editor wrapper.");
                CharacterModelImporter.Rebuild();
                VehicleArtImporter.PrepareBatch();
                VehicleArtVerification.VerifyBatch();
                CityBuilder.BuildAll();
                regenerated = true;
                Capture();
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
