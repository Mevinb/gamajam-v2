using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Parasite.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Parasite.EditorTools
{
    /// <summary>Root-run import verification and explicitly baked-pose GPU controls. Does not save/import/rebuild assets.</summary>
    public static class OralPilotPotUnityReview
    {
        [Serializable] public sealed class TextureRow
        {
            public string property, path, guid, sourceSha256, format, type, npot, filter, standaloneFormat;
            public int width, height, mips, defaultMax, standaloneMax;
            public bool sRGB, streaming, standaloneOverride, normalFlipGreen;
            public string wrap;
            public long nativeTextureObjectBytes;
        }
        [Serializable] public sealed class LodRow
        { public int lod, vertices, triangles, bones, slots; public Bounds reportedBounds, cpuBakedWorldBounds; }
        [Serializable] public sealed class PoseRow
        {
            public string state, provenance;
            public float jawMuscle, boneAngleFromNeutral;
            public Quaternion jawRotation;
            public Color tintA, eyeEmission, skinVeins;
            public List<float> maxVertexDeltaFromNeutral = new List<float>();
        }
        [Serializable] public sealed class Shot
        { public string name, file, sha256, poseProvenance; public int lod, width, height; public long bytes; public float requestedClothMipBias, observedClothMipBias; }
        [Serializable] public sealed class SourcePin { public string path, sha256, guid; }
        [Serializable] public sealed class Receipt
        {
            public string scope = "Actual Unity loaded asset/PBR checks and static meshes baked from real runtime Animator/humanoid controls in generated City. Explicit baked-pose controls avoid same-frame GPU skin caches. Not real-frame animation/native gameplay/FPS/VRAM acceptance.";
            public string output, startedUtc, completedUtc, error, gpu, graphicsApi, avatar, controller, controllerDependencyHash, modelDependencyHash;
            public bool completed, assetsUnchanged, sceneSaved, realFramesEvaluated;
            public int catalogCount, checkedHumanoidPrefabs, materialSlots;
            public Vector3 headWorld, actorRoot;
            public List<TextureRow> textures = new List<TextureRow>();
            public List<LodRow> lods = new List<LodRow>();
            public List<PoseRow> poses = new List<PoseRow>();
            public List<Shot> shots = new List<Shot>();
            public List<SourcePin> sourceInputs = new List<SourcePin>();
            public List<string> renderErrors = new List<string>();
        }
        static string Project => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("[OralPilotReview] " + message); }
        static string Hash(string file) { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file))).Replace("-", "").ToLowerInvariant(); }

        static TextureRow Texture(Receipt receipt, Material cloth, string property, int width, int height, TextureFormat format, bool colour, bool normal, int max, bool mip)
        {
            var texture = cloth.GetTexture(property) as Texture2D; Require(texture, "Missing cloth " + property);
            string path = AssetDatabase.GetAssetPath(texture); var importer = AssetImporter.GetAtPath(path) as TextureImporter; Require(importer, "Missing importer " + path);
            var platform = importer.GetPlatformTextureSettings("Standalone");
            var result = new TextureRow { property = property, path = path, guid = AssetDatabase.AssetPathToGUID(path), sourceSha256 = Hash(Path.Combine(Project, path)), width = texture.width, height = texture.height, format = texture.format.ToString(), mips = texture.mipmapCount, type = importer.textureType.ToString(), npot = importer.npotScale.ToString(), filter = importer.filterMode.ToString(), sRGB = importer.sRGBTexture, streaming = texture.streamingMipmaps, defaultMax = importer.maxTextureSize, standaloneMax = platform.maxTextureSize, standaloneFormat = platform.format.ToString(), standaloneOverride = platform.overridden, normalFlipGreen = importer.flipGreenChannel, wrap = texture.wrapMode.ToString(), nativeTextureObjectBytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture) };
            receipt.textures.Add(result);
            Require(texture.width == width && texture.height == height && texture.format == format && SystemInfo.SupportsTextureFormat(format), "Loaded POT dimension/format unsupported or rescaled: " + path);
            Require(texture.wrapMode == TextureWrapMode.Clamp && importer.wrapMode == TextureWrapMode.Clamp, "Atlas requires Clamp wrapping: " + path);
            Require(importer.sRGBTexture == colour && importer.textureType == (normal ? TextureImporterType.NormalMap : TextureImporterType.Default) && !importer.flipGreenChannel, "Colour/normal policy incorrect: " + path);
            Require(importer.npotScale == TextureImporterNPOTScale.None && importer.maxTextureSize == max && platform.overridden && platform.maxTextureSize == max && platform.format == (normal ? TextureImporterFormat.BC5 : TextureImporterFormat.BC7), "POT/default/platform size/format policy incorrect: " + path);
            Require(importer.mipmapEnabled == mip && texture.mipmapCount == (mip ? 13 : 1) && importer.filterMode == (mip ? FilterMode.Trilinear : FilterMode.Point), "Mip/filter policy incorrect: " + path);
            return result;
        }

        static Bounds WorldBounds(Mesh mesh, Transform transform)
        {
            var vertices = mesh.vertices; Require(vertices.Length > 0, "Empty CPU-baked mesh"); var bounds = new Bounds(transform.TransformPoint(vertices[0]), Vector3.zero);
            foreach (var p in vertices) bounds.Encapsulate(transform.TransformPoint(p)); return bounds;
        }

        public static void RunBatch()
        {
            Require(Application.isBatchMode && !Application.isPlaying, "Batch Editor only"); Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "Actual GPU required; omit -nographics");
            string output = Path.Combine(Project, "Logs/oral-pilot-unity-review-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ")); var args = Environment.GetCommandLineArgs(); int arg = Array.IndexOf(args, "-oralPilotReviewOut"); if (arg >= 0 && arg + 1 < args.Length) output = Path.GetFullPath(args[arg + 1]);
            Require(output.StartsWith(Path.Combine(Project, "Logs") + Path.DirectorySeparatorChar, StringComparison.Ordinal) && !Directory.Exists(output), "Choose fresh Logs output"); Directory.CreateDirectory(output);
            var receipt = new Receipt { output = output, startedUtc = DateTime.UtcNow.ToString("O"), gpu = SystemInfo.graphicsDeviceName, graphicsApi = SystemInfo.graphicsDeviceType.ToString(), actorRoot = new Vector3(-27, .15f, 9), realFramesEvaluated = false, sceneSaved = false };
            var owned = new List<Object>(); var pins = new Dictionary<string, string>(); RenderTexture target = null; Texture2D pixels = null; var active = RenderTexture.active;
            Application.LogCallback captureErrors = (message, stack, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) receipt.renderErrors.Add(message); };
            Application.logMessageReceived += captureErrors;
            try
            {
                foreach (string relative in new[] { "Assets/Parasite/Art/Characters/Models/LakeGirl.fbx", "Assets/Parasite/Art/Characters/Models/LakeGirl.fbx.meta", "Assets/Parasite/Art/Characters/Models/LakeGirl.json", "Assets/Parasite/Art/Characters/Textures/LakeGirl_Cloth_A.png", "Assets/Parasite/Art/Characters/Textures/LakeGirl_Cloth_N.png", "Assets/Parasite/Art/Characters/Textures/LakeGirl_Cloth_M.png", "Assets/Parasite/Resources/Characters/CharacterCatalog.asset", "Assets/Parasite/Resources/Characters/Prefabs/LakeGirl.prefab", "Assets/Parasite/Scenes/City.unity" }) { string path = Path.Combine(Project, relative); pins[path] = Hash(path); receipt.sourceInputs.Add(new SourcePin { path = relative, sha256 = pins[path], guid = AssetDatabase.AssetPathToGUID(relative) }); }
                var catalog = CharacterCatalog.Load(); Require(catalog && catalog.entries.Count == 25 && catalog.entries.Select(e => e.id).Distinct().Count() == 25, "Catalog must retain25 unique entries"); receipt.catalogCount = catalog.entries.Count;
                foreach (var entry in catalog.entries)
                {
                    var animator = entry.prefab ? entry.prefab.GetComponent<Animator>() : null; var renderers = entry.prefab ? entry.prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true) : Array.Empty<SkinnedMeshRenderer>();
                    Require(animator && animator.avatar && animator.avatar.isValid && animator.avatar.isHuman && renderers.Length == 3 && renderers.All(r => r.sharedMesh && r.sharedMaterials.Length == 4), "Invalid existing catalog prefab/avatar/slots: " + entry.id); receipt.checkedHumanoidPrefabs++;
                }
                EditorSceneManager.OpenScene("Assets/Parasite/Scenes/City.unity", OpenSceneMode.Single);
                foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) camera.enabled = false;
                var actor = new GameObject("Oral review LakeGirl - unsaved"); owned.Add(actor); actor.transform.position = receipt.actorRoot; var rig = actor.AddComponent<HumanoidRig>(); rig.VariantOverride = "LakeGirl"; rig.AmbientGestures = false; rig.Build(Profession.Civilian, 731, false);
                Require(rig.Animator && rig.UsesAnimator && rig.Animator.avatar.isValid && rig.Animator.avatar.isHuman, "Actual humanoid Animator required; no fallback"); rig.SetPose(ActorPose.None); rig.Animate(Vector3.zero, true, HostState.Normal, 4.6f, 0); rig.Animator.SetInteger(HumanAnimParams.Profession, (int)Profession.Civilian); rig.Animator.SetFloat(HumanAnimParams.Variant, .25f); rig.Animator.Play(HumanAnimParams.StateIdleCivilian, 0, .25f); rig.FastForward(.8f); rig.SetEyesGlow(false);
                receipt.avatar = rig.Animator.avatar.name; receipt.controller = AssetDatabase.GetAssetPath(rig.Animator.runtimeAnimatorController); receipt.controllerDependencyHash = AssetDatabase.GetAssetDependencyHash(receipt.controller).ToString(); receipt.modelDependencyHash = AssetDatabase.GetAssetDependencyHash("Assets/Parasite/Art/Characters/Models/LakeGirl.fbx").ToString(); receipt.headWorld = rig.Head.position; receipt.materialSlots = 4;
                Require(receipt.headWorld.y - actor.transform.position.y > .8f && receipt.headWorld.y - actor.transform.position.y < 2.2f && Vector3.Distance(receipt.headWorld, actor.transform.position) < 3, "Retarget/unit failure: Head is outside ordinary metre-scale body; refuse sky-shot capture");
                var smrs = rig.Model.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderBy(r => r.name).ToArray(); Require(smrs.Length == 3, "Expected3 LOD renderers");
                var mats = smrs.SelectMany(r => r.sharedMaterials).Distinct().ToArray(); var cloth = mats.Single(m => m.name.Contains("_Cloth")); var skin = mats.Single(m => m.name.Contains("_Skin")); var eyes = mats.Single(m => m.name.Contains("_Eyes"));
                Texture(receipt, cloth, "_BaseMap", 2048, 4096, TextureFormat.BC7, true, false, 4096, true); Texture(receipt, cloth, "_BumpMap", 2048, 4096, TextureFormat.BC5, false, true, 4096, true); Texture(receipt, cloth, "_MaskMap", 1024, 2048, TextureFormat.BC7, false, false, 2048, false);
                var camGo = new GameObject("Oral review static-control camera - unsaved"); owned.Add(camGo); var cam = camGo.AddComponent<Camera>(); cam.allowHDR = true; cam.nearClipPlane = .05f; cam.farClipPlane = 900; var cameraData = camGo.AddComponent<UniversalAdditionalCameraData>(); cameraData.renderPostProcessing = true; cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; cameraData.renderShadows = true;
                var lamps = Object.FindObjectsByType<FX.LampLights>(FindObjectsSortMode.None); Vector3 headLook = rig.Head.position + Vector3.up * .045f; var baselineVertices = new Dictionary<int, Vector3[]>(); Color originalTint = cloth.GetColor("_TintA");
                using var poses = new HumanPoseHandler(rig.Animator.avatar, rig.Animator.transform); var neutral = new HumanPose(); poses.GetHumanPose(ref neutral); int jawIndex = Array.FindIndex(HumanTrait.MuscleName, n => n == "Jaw Close"); Require(jawIndex >= 0, "Jaw muscle missing"); var jaw = rig.Animator.GetBoneTransform(HumanBodyBones.Jaw); Require(jaw, "Mapped Jaw missing"); Quaternion neutralJaw = jaw.localRotation;
                foreach (string state in new[] { "neutral", "open", "tint_open", "possession_open" })
                {
                    rig.SetEyesGlow(false); cloth.SetColor("_TintA", originalTint);
                    var pose = neutral; pose.muscles = (float[])neutral.muscles.Clone(); if (state != "neutral") pose.muscles[jawIndex] = -1; poses.SetHumanPose(ref pose);
                    if (state == "tint_open") cloth.SetColor("_TintA", new Color(.12f, .38f, .90f).gamma);
                    if (state == "possession_open") rig.SetEyesGlow(true);
                    var readback = new HumanPose(); poses.GetHumanPose(ref readback); var row = new PoseRow { state = state, provenance = "EXPLICIT STATIC BAKED POSE: real runtime Animator neutral, then optional HumanPose Jaw Close=-1 and actual material APIs; no real-frame animation claim", jawMuscle = readback.muscles[jawIndex], jawRotation = jaw.localRotation, boneAngleFromNeutral = Quaternion.Angle(neutralJaw, jaw.localRotation), tintA = cloth.GetColor("_TintA"), eyeEmission = eyes.GetColor("_EmissionColor"), skinVeins = skin.GetColor("_VeinColor") }; receipt.poses.Add(row);
                    if (state != "neutral") Require(row.boneAngleFromNeutral > .1f, "Open-jaw control produced no mapped bone motion");
                    for (int lod = 0; lod < 3; lod++)
                    {
                        var source = smrs.Single(r => r.name.EndsWith("_LOD" + lod, StringComparison.Ordinal)); var baked = new Mesh { name = state + " LOD" + lod + " CPU immutable pose" }; source.BakeMesh(baked); owned.Add(baked); var bounds = WorldBounds(baked, source.transform);
                        Require(bounds.size.y > 1 && bounds.size.y < 2.3f && Vector3.Distance(bounds.center, actor.transform.position) < 3, "CPU BakeMesh exposes unit/bind failure; renderer bounds alone are insufficient");
                        int triangles = (int)Enumerable.Range(0, source.sharedMesh.subMeshCount).Sum(s => (long)source.sharedMesh.GetIndexCount(s)) / 3;
                        if (state == "neutral") { baselineVertices[lod] = baked.vertices; receipt.lods.Add(new LodRow { lod = lod, vertices = source.sharedMesh.vertexCount, triangles = triangles, bones = source.bones.Length, slots = source.sharedMaterials.Length, reportedBounds = source.bounds, cpuBakedWorldBounds = bounds }); Require(triangles == new[] { 28234, 8073, 2597 }[lod] && source.sharedMaterials.Length == 4 && source.bones.Length == 56, "Imported LOD triangles/slots/bones mismatch"); }
                        float delta = 0; var points = baked.vertices; for (int v = 0; v < points.Length; v++) delta = Mathf.Max(delta, Vector3.Distance(points[v], baselineVertices[lod][v])); row.maxVertexDeltaFromNeutral.Add(delta); if (state != "neutral") Require(delta > .0001f, "Open jaw did not change actual CPU-baked geometry");
                        foreach (var original in smrs) original.enabled = false;
                        var drawGo = new GameObject("Static baked " + state + " LOD" + lod); drawGo.transform.SetParent(source.transform, false); var filter = drawGo.AddComponent<MeshFilter>(); filter.sharedMesh = baked; var draw = drawGo.AddComponent<MeshRenderer>(); var copies = source.sharedMaterials.Select(m => new Material(m) { name = m.name + " immutable " + state + " LOD" + lod }).ToArray(); draw.sharedMaterials = copies; draw.shadowCastingMode = ShadowCastingMode.On; draw.receiveShadows = true; draw.lightProbeUsage = source.lightProbeUsage; draw.reflectionProbeUsage = source.reflectionProbeUsage;
                        try
                        {
                            foreach (string view in state == "neutral" && lod == 2 ? new[] { "full", "portrait", "portrait_mip6", "portrait_mip12" } : new[] { "full", "portrait" })
                            {
                                const int width = 960, height = 1280; if (!target) { target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 }; target.Create(); pixels = new Texture2D(width, height, TextureFormat.RGB24, false); }
                                Vector3 position = view == "full" ? actor.transform.position + new Vector3(.65f, 1.05f, 4.4f) : headLook + new Vector3(.22f, .025f, 1.45f); Vector3 look = view == "full" ? actor.transform.position + Vector3.up * .92f : headLook; cam.transform.SetPositionAndRotation(position, Quaternion.LookRotation(look - position)); cam.fieldOfView = view == "full" ? 29 : 26; cam.aspect = (float)width / height; cam.targetTexture = target;
                                var biased = new List<Texture2D>(); float requestedBias = 0, observedBias = 0;
                                try
                                {
                                    if (view.StartsWith("portrait_mip", StringComparison.Ordinal))
                                    {
                                        float bias = view.EndsWith("12", StringComparison.Ordinal) ? 12 : 6; requestedBias = bias; var drawCloth = copies.Single(m => m.name.Contains("_Cloth"));
                                        foreach (string property in new[] { "_BaseMap", "_BumpMap" })
                                        {
                                            var original = (Texture2D)drawCloth.GetTexture(property);
                                            Require((SystemInfo.copyTextureSupport & CopyTextureSupport.Basic) != 0, "GPU texture copying is required for unreadable compressed textures");
                                            var clone = new Texture2D(original.width, original.height, original.format, original.mipmapCount > 1, property != "_BaseMap") { name = original.name + " isolated GPU-copy mip-bias stress control", filterMode = original.filterMode, wrapMode = original.wrapMode, anisoLevel = original.anisoLevel };
                                            biased.Add(clone); Require(clone.mipmapCount == original.mipmapCount && clone.format == original.format, "GPU control texture format/mip mismatch"); Graphics.CopyTexture(original, clone);
                                            clone.mipMapBias = bias; observedBias = clone.mipMapBias; Require(Mathf.Abs(observedBias - bias) < .001f, "Mip-bias stress control was clamped by this API"); drawCloth.SetTexture(property, clone);
                                        }
                                    }
                                foreach (var lamp in lamps) lamp.Select(position, true);
                                for (int repeat = 0; repeat < 3; repeat++) { var request = new RenderPipeline.StandardRequest { destination = target }; if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request); else cam.Render(); }
                                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply(); RenderTexture.active = active; string name = "baked_" + state + "_lod" + lod + "_" + view; string file = Path.Combine(output, name + ".png"); File.WriteAllBytes(file, pixels.EncodeToPNG()); receipt.shots.Add(new Shot { name = name, file = file, sha256 = Hash(file), poseProvenance = row.provenance + (biased.Count > 0 ? "; isolated cloth A/N mip-bias stress " + view + "; not natural distance/LOD proof" : ""), lod = lod, width = width, height = height, bytes = new FileInfo(file).Length, requestedClothMipBias = requestedBias, observedClothMipBias = observedBias });
                                }
                                finally
                                {
                                    var drawCloth = copies.Single(m => m.name.Contains("_Cloth")); drawCloth.SetTexture("_BaseMap", cloth.GetTexture("_BaseMap")); drawCloth.SetTexture("_BumpMap", cloth.GetTexture("_BumpMap")); foreach (var clone in biased) Object.DestroyImmediate(clone);
                                }
                            }
                        }
                        finally { Object.DestroyImmediate(drawGo); foreach (var copy in copies) Object.DestroyImmediate(copy); }
                    }
                }
                poses.SetHumanPose(ref neutral); rig.SetEyesGlow(false); cloth.SetColor("_TintA", originalTint); receipt.assetsUnchanged = pins.All(p => Hash(p.Key) == p.Value); Require(receipt.assetsUnchanged, "Review changed live inputs"); Require(receipt.renderErrors.Count == 0, "GPU review logged errors; inspect receipt and log"); receipt.completed = true;
            }
            catch (Exception e) { receipt.error = e.ToString(); Debug.LogException(e); }
            finally
            {
                RenderTexture.active = active; foreach (var o in owned) { var go = o as GameObject; var camera = go ? go.GetComponent<Camera>() : null; if (camera) camera.targetTexture = null; } if (target) Object.DestroyImmediate(target); if (pixels) Object.DestroyImmediate(pixels); foreach (var o in owned.AsEnumerable().Reverse()) if (o) Object.DestroyImmediate(o); Application.logMessageReceived -= captureErrors; if (receipt.renderErrors.Count > 0) receipt.completed = false; receipt.completedUtc = DateTime.UtcNow.ToString("O"); File.WriteAllText(Path.Combine(output, "receipt.json"), JsonUtility.ToJson(receipt, true) + "\n");
            }
            EditorApplication.Exit(receipt.completed ? 0 : 1);
        }
    }
}
