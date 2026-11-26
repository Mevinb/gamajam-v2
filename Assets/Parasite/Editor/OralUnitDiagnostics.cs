using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Parasite.Characters;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Parasite.EditorTools
{
    /// <summary>Root-run CPU-only diagnostic. Reads existing assets; never imports, saves, rescales or replaces them.</summary>
    public static class OralUnitDiagnostics
    {
        [Serializable] public sealed class Node
        {
            public string path; public Vector3 localPosition, localScale, worldPosition, lossyScale;
            public Quaternion localRotation; public int instanceId;
        }
        [Serializable] public sealed class Skin
        {
            public string name, headPath, bindPoseReadError;
            public int vertices, bones, bindPoses;
            public Bounds reportedWorldBounds, reportedLocalBounds, bakedLocalBounds, bakedWorldBounds;
            public Node rendererNode, headNode;
            public bool rendererHeadIsAnimatorHead;
            public float maximumRestBindError;
        }
        [Serializable] public sealed class Snapshot
        {
            public string stage, avatar; public bool valid, human;
            public float humanScale; public Node root, animatorHead;
            public List<Node> allNamedHeads = new List<Node>();
            public List<Node> headParentChain = new List<Node>();
            public List<Skin> skins = new List<Skin>();
        }
        [Serializable] public sealed class Skeleton
        { public string name; public Vector3 position, scale; public Quaternion rotation; }
        [Serializable] public sealed class Model
        {
            public string id, path, sourceSha256, sourceGuid, avatarGuid, error, freshAvatarControlError;
            public float globalScale, fileScale; public bool useFileScale, bakeAxisConversion;
            public string animationType, avatarSetup;
            public List<Skeleton> importerSkeleton = new List<Skeleton>();
            public List<Skeleton> avatarSkeleton = new List<Skeleton>();
            public List<Snapshot> snapshots = new List<Snapshot>();
        }
        [Serializable] public sealed class Report
        {
            public string scope = "Read-only existing Unity asset/root/bone/CPU BakeMesh probes; no GPU, import, globalScale, source or mapping edits.";
            public string startedUtc, completedUtc, error, output, baselineMetaPath, baselineSkeletonText, liveSkeletonText;
            public bool completed, sourceFilesUnchanged;
            public List<Model> models = new List<Model>();
        }

        static string Project => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        static string Hash(string path)
        { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
        static string Hierarchy(Transform transform)
        { if (!transform) return "null"; var parts = new List<string>(); while (transform) { parts.Add(transform.name); transform = transform.parent; } parts.Reverse(); return string.Join("/", parts); }
        static Node Inspect(Transform t) => !t ? null : new Node { path = Hierarchy(t), localPosition = t.localPosition, localRotation = t.localRotation, localScale = t.localScale, worldPosition = t.position, lossyScale = t.lossyScale, instanceId = t.GetInstanceID() };
        static List<Skeleton> SkeletonRows(HumanDescription description) => (description.skeleton ?? Array.Empty<SkeletonBone>()).Where(s => new[] { "LakeGirl", "CivOmar", "Armature", "Root", "Hips", "Head", "Jaw" }.Contains(s.name)).Select(s => new Skeleton { name = s.name, position = s.position, rotation = s.rotation, scale = s.scale }).ToList();

        static Snapshot Take(GameObject root, Animator animator, string stage)
        {
            // GetBoneTransform can lazily initialize an Animator. Preserve the
            // raw disabled snapshot by reading named hierarchy nodes only.
            bool mapped = !stage.StartsWith("raw", StringComparison.Ordinal);
            Transform head = mapped && animator && animator.avatar && animator.avatar.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            var result = new Snapshot { stage = stage, root = Inspect(root.transform), animatorHead = Inspect(head), avatar = animator && animator.avatar ? animator.avatar.name : "none", valid = animator && animator.avatar && animator.avatar.isValid, human = animator && animator.avatar && animator.avatar.isHuman, humanScale = mapped && animator && animator.avatar && animator.avatar.isHuman ? animator.humanScale : 0 };
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == "Head") result.allNamedHeads.Add(Inspect(t));
            for (var t = head; t; t = t.parent) result.headParentChain.Add(Inspect(t));
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var boneHead = renderer.bones.FirstOrDefault(t => t && t.name == "Head");
                var row = new Skin { name = renderer.name, vertices = renderer.sharedMesh ? renderer.sharedMesh.vertexCount : 0, bones = renderer.bones.Length, reportedWorldBounds = renderer.bounds, reportedLocalBounds = renderer.localBounds, rendererNode = Inspect(renderer.transform), headNode = Inspect(boneHead), headPath = Hierarchy(boneHead), rendererHeadIsAnimatorHead = boneHead == head };
                var baked = new Mesh { name = "Read-only unit CPU bake" };
                try
                {
                    renderer.BakeMesh(baked); baked.RecalculateBounds(); row.bakedLocalBounds = baked.bounds;
                    var vertices = baked.vertices;
                    if (vertices.Length > 0)
                    {
                        var bounds = new Bounds(renderer.transform.TransformPoint(vertices[0]), Vector3.zero);
                        foreach (var v in vertices) bounds.Encapsulate(renderer.transform.TransformPoint(v));
                        row.bakedWorldBounds = bounds;
                    }
                    try
                    {
                        var bind = renderer.sharedMesh.bindposes; row.bindPoses = bind.Length;
                        for (int i = 0; i < Math.Min(bind.Length, renderer.bones.Length); i++)
                        {
                            if (!renderer.bones[i]) continue;
                            var product = renderer.worldToLocalMatrix * renderer.bones[i].localToWorldMatrix * bind[i];
                            for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++) row.maximumRestBindError = Mathf.Max(row.maximumRestBindError, Mathf.Abs(product[r, c] - (r == c ? 1 : 0)));
                        }
                    }
                    catch (Exception e) { row.bindPoseReadError = e.Message; }
                }
                finally { Object.DestroyImmediate(baked); }
                result.skins.Add(row);
            }
            return result;
        }

        static void ProbeModel(Report report, string id)
        {
            string path = "Assets/Parasite/Art/Characters/Models/" + id + ".fbx";
            var row = new Model { id = id, path = path, sourceSha256 = Hash(Path.Combine(Project, path)), sourceGuid = AssetDatabase.AssetPathToGUID(path) }; report.models.Add(row);
            var owned = new List<GameObject>(); var ownedAvatars = new List<Avatar>();
            try
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter; Require(importer, "Missing importer " + path);
                row.globalScale = importer.globalScale; row.fileScale = importer.fileScale; row.useFileScale = importer.useFileScale; row.bakeAxisConversion = importer.bakeAxisConversion; row.animationType = importer.animationType.ToString(); row.avatarSetup = importer.avatarSetup.ToString(); row.importerSkeleton = SkeletonRows(importer.humanDescription);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path); Require(asset, "Missing model " + path);
                var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault(v => v.isValid && v.isHuman); Require(avatar, "No human avatar " + id); row.avatarGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(avatar)); row.avatarSkeleton = SkeletonRows(avatar.humanDescription);
                var controller = Resources.Load<RuntimeAnimatorController>(HumanAnimParams.ControllerResource); Require(controller, "Missing shared controller");
                var go = Object.Instantiate(asset); owned.Add(go); go.hideFlags = HideFlags.DontSave; go.name = asset.name; go.transform.position = Vector3.zero;
                var animator = go.GetComponent<Animator>() ?? go.AddComponent<Animator>(); animator.enabled = false; animator.runtimeAnimatorController = null; animator.avatar = avatar;
                row.snapshots.Add(Take(go, animator, "raw loaded model; Animator disabled; no Rebind"));
                animator.enabled = true; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.runtimeAnimatorController = controller; animator.Rebind();
                row.snapshots.Add(Take(go, animator, "after imported-avatar Rebind"));
                animator.SetInteger(HumanAnimParams.Profession, (int)Profession.Civilian); animator.SetFloat(HumanAnimParams.Variant, .25f); animator.Play(HumanAnimParams.StateIdleCivilian, 0, .25f); animator.Update(.2f);
                row.snapshots.Add(Take(go, animator, "after actual shared IdleCivilian Animator.Update(.2)"));

                // An in-memory fresh avatar is an explicitly separate control.
                // It never replaces the imported avatar or writes a mapping.
                var freshGo = Object.Instantiate(asset); owned.Add(freshGo); freshGo.hideFlags = HideFlags.DontSave; freshGo.name = asset.name; freshGo.transform.position = Vector3.zero;
                var freshAnimator = freshGo.GetComponent<Animator>() ?? freshGo.AddComponent<Animator>(); freshAnimator.enabled = false; freshAnimator.runtimeAnimatorController = null;
                var description = importer.humanDescription;
                description.skeleton = freshGo.GetComponentsInChildren<Transform>(true).Select(t => new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale }).ToArray();
                var freshAvatar = AvatarBuilder.BuildHumanAvatar(freshGo, description); ownedAvatars.Add(freshAvatar);
                if (!freshAvatar || !freshAvatar.isValid || !freshAvatar.isHuman) row.freshAvatarControlError = "In-memory fresh avatar control invalid; imported-avatar observations above remain available.";
                else
                {
                    freshAnimator.avatar = freshAvatar; freshAnimator.runtimeAnimatorController = controller; freshAnimator.enabled = true; freshAnimator.applyRootMotion = false; freshAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate; freshAnimator.Rebind(); freshAnimator.SetInteger(HumanAnimParams.Profession, (int)Profession.Civilian); freshAnimator.SetFloat(HumanAnimParams.Variant, .25f); freshAnimator.Play(HumanAnimParams.StateIdleCivilian, 0, .25f); freshAnimator.Update(.2f);
                    row.snapshots.Add(Take(freshGo, freshAnimator, "CONTROL only: in-memory avatar built from loaded local transforms; actual idle update"));
                }
            }
            catch (Exception e) { row.error = e.ToString(); }
            finally
            {
                foreach (var o in owned.AsEnumerable().Reverse()) if (o) Object.DestroyImmediate(o);
                foreach (var avatar in ownedAvatars) if (avatar) Object.DestroyImmediate(avatar);
            }
        }

        static string SkeletonText(string file)
        { string text = File.ReadAllText(file); int start = text.IndexOf("    skeleton:", StringComparison.Ordinal); if (start < 0) return "No serialized skeleton"; int end = text.IndexOf("    armTwist:", start, StringComparison.Ordinal); return text.Substring(start, end > start ? end - start : Math.Min(text.Length - start, 15000)); }

        public static void RunBatch()
        {
            var report = new Report { startedUtc = DateTime.UtcNow.ToString("O") }; string output = Path.Combine(Project, "Logs/oral-unit-diagnostics-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ"));
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-oralUnitOut"); if (i >= 0 && i + 1 < args.Length) output = Path.GetFullPath(args[i + 1]);
            Require(Application.isBatchMode && !Application.isPlaying, "Batch Editor only"); Require(output.StartsWith(Path.Combine(Project, "Logs") + Path.DirectorySeparatorChar, StringComparison.Ordinal) && !Directory.Exists(output), "Choose a fresh Logs output directory"); Directory.CreateDirectory(output); report.output = output;
            var pins = new Dictionary<string, string>();
            foreach (string id in new[] { "LakeGirl", "CivOmar", "DrPetrov" })
                foreach (string ext in new[] { ".fbx", ".fbx.meta" }) { string p = Path.Combine(Project, "Assets/Parasite/Art/Characters/Models/" + id + ext); pins[p] = Hash(p); }
            try
            {
                report.baselineMetaPath = Path.Combine(Project, "Logs/unity-oral-pilot-lakegirl-v2-stage/baseline/Assets/Parasite/Art/Characters/Models/LakeGirl.fbx.meta"); report.baselineSkeletonText = SkeletonText(report.baselineMetaPath); report.liveSkeletonText = SkeletonText(Path.Combine(Project, "Assets/Parasite/Art/Characters/Models/LakeGirl.fbx.meta"));
                foreach (string id in new[] { "LakeGirl", "CivOmar", "DrPetrov" }) ProbeModel(report, id);
                report.sourceFilesUnchanged = pins.All(pair => Hash(pair.Key) == pair.Value); Require(report.sourceFilesUnchanged, "Diagnostic changed source/model meta"); report.completed = true;
            }
            catch (Exception e) { report.error = e.ToString(); Debug.LogException(e); }
            finally { report.completedUtc = DateTime.UtcNow.ToString("O"); File.WriteAllText(Path.Combine(output, "receipt.json"), JsonUtility.ToJson(report, true) + "\n"); }
            EditorApplication.Exit(report.completed ? 0 : 1);
        }
    }
}
