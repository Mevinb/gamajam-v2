using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace Parasite.EditorTools
{
    // Two observers surround the normal order0 importer without changing any clip.
    public sealed class HumanJawImportBeforeObserver : AssetPostprocessor
    {
        public override int GetPostprocessOrder() => -10000;
        void OnPostprocessAnimation(GameObject root, AnimationClip clip) => HumanJawImportDiagnostics.Observe("before", assetPath, assetImporter as ModelImporter, root, clip);
    }
    public sealed class HumanJawImportAfterObserver : AssetPostprocessor
    {
        public override int GetPostprocessOrder() => 10000;
        void OnPostprocessAnimation(GameObject root, AnimationClip clip) => HumanJawImportDiagnostics.Observe("after", assetPath, assetImporter as ModelImporter, root, clip);
    }

    public static class HumanJawImportDiagnostics
    {
        [Serializable] sealed class ActiveProbe { public string owner, output; public string[] paths; }
        [Serializable] public sealed class CurveRow
        {
            public string path, type, property; public int keys; public float firstTime, lastTime, atStart, atMiddle, atEnd;
        }
        [Serializable] public sealed class ClipRow
        {
            public string phase, source, clip, animationType, avatarSetup, utc;
            public bool humanMotion, legacy, empty, sourceMappedJaw, sourceRootHasJaw, additive;
            public float length, frameRate; public int sourceHumanBoneCount, bindingCount;
            public List<CurveRow> jawCurves = new();
        }
        [Serializable] sealed class AssetRow
        {
            public string source, sourceBeforeSha256, sourceAfterSha256, guidBefore, guidAfter;
            public List<ClipRow> before = new(), final = new();
        }
        [Serializable] sealed class Report
        {
            public string status = "CALLBACK_AND_FINAL_DATA_OBSERVATION_ONLY", output, owner, utc;
            public List<AssetRow> assets = new();
            public string scope = "Explicit synchronous reimport of three owned animation FBXs; observers read clip data only. No model/controller/scene/render/test changes.";
        }
        static string Project => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        static string ActivePath => Path.Combine(Project, "Logs", "jaw-import-observer.active.json");
        static string Hash(string file)
        {
            using var sha = SHA256.Create(); using var input = File.OpenRead(Path.IsPathRooted(file) ? file : Path.Combine(Project, file));
            return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
        }
        static void Write(string path, object value)
        {
            string stage = path + ".writing"; File.WriteAllText(stage, JsonUtility.ToJson(value, true)); File.Move(stage, path);
        }
        static ClipRow Read(string phase, string source, ModelImporter importer, GameObject root, AnimationClip clip)
        {
            var human = importer.humanDescription.human ?? Array.Empty<HumanBone>();
            var bindings = AnimationUtility.GetCurveBindings(clip);
            var row = new ClipRow { phase = phase, source = source, clip = clip.name, utc = DateTime.UtcNow.ToString("O"), humanMotion = clip.humanMotion,
                legacy = clip.legacy, empty = clip.empty, length = clip.length, frameRate = clip.frameRate,
                animationType = importer.animationType.ToString(), avatarSetup = importer.avatarSetup.ToString(),
                sourceHumanBoneCount = human.Length, sourceMappedJaw = Array.Exists(human, bone => bone.humanName == "Jaw"),
                sourceRootHasJaw = root && root.GetComponentsInChildren<Transform>(true).Any(t => t.name.IndexOf("jaw", StringComparison.OrdinalIgnoreCase) >= 0),
                additive = Path.GetFileNameWithoutExtension(source).StartsWith("Gesture_", StringComparison.Ordinal), bindingCount = bindings.Length };
            foreach (var binding in bindings)
            {
                if (binding.propertyName.IndexOf("jaw", StringComparison.OrdinalIgnoreCase) < 0 && binding.path.IndexOf("jaw", StringComparison.OrdinalIgnoreCase) < 0) continue;
                var curve = AnimationUtility.GetEditorCurve(clip, binding); var keys = curve?.keys ?? Array.Empty<Keyframe>();
                row.jawCurves.Add(new CurveRow { path = binding.path, type = binding.type.FullName, property = binding.propertyName, keys = keys.Length,
                    firstTime = keys.Length > 0 ? keys[0].time : 0, lastTime = keys.Length > 0 ? keys[keys.Length - 1].time : 0,
                    atStart = curve != null ? curve.Evaluate(0) : 0, atMiddle = curve != null ? curve.Evaluate(clip.length * .5f) : 0,
                    atEnd = curve != null ? curve.Evaluate(clip.length) : 0 });
            }
            return row;
        }
        public static void Observe(string phase, string source, ModelImporter importer, GameObject root, AnimationClip clip)
        {
            if (!File.Exists(ActivePath) || importer == null || !source.StartsWith(HumanAnimationImporter.Folder, StringComparison.Ordinal)) return;
            var active = JsonUtility.FromJson<ActiveProbe>(File.ReadAllText(ActivePath));
            if (active == null || active.paths == null || Array.IndexOf(active.paths, source) < 0) return;
            var row = Read(phase, source, importer, root, clip);
            string file = phase + "-" + Path.GetFileNameWithoutExtension(source) + "-" + Guid.NewGuid().ToString("N") + ".json";
            Write(Path.Combine(active.output, file), row);
        }
        static List<ClipRow> Final(string phase, string source)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(source); var root = AssetDatabase.LoadAssetAtPath<GameObject>(source);
            return AssetDatabase.LoadAllAssetsAtPath(source).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal))
                .Select(c => Read(phase, source, importer, root, c)).ToList();
        }
        public static void ReimportAndInspectBatch()
        {
            if (!Application.isBatchMode || Application.isPlaying) throw new InvalidOperationException("Run serialized batch EditMode only.");
            if (File.Exists(ActivePath)) throw new InvalidOperationException("A previous jaw observer owns the active receipt; inspect it before removing.");
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-jawImportProbeOut");
            string output = Path.GetFullPath(at >= 0 && at + 1 < args.Length ? args[at + 1] : Path.Combine(Project, "Logs", "jaw-import-probe-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ")));
            if (!output.StartsWith(Path.Combine(Project, "Logs") + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Directory.Exists(output) || File.Exists(output))
                throw new InvalidOperationException("A fresh output directory under project Logs is required.");
            Directory.CreateDirectory(output);
            var active = new ActiveProbe { owner = Guid.NewGuid().ToString("N"), output = output,
                paths = new[] { HumanAnimationImporter.Folder + "Idle_Neutral.fbx", HumanAnimationImporter.Folder + "Idle_Shift.fbx", HumanAnimationImporter.Folder + "Gesture_Explain.fbx" } };
            var report = new Report { owner = active.owner, output = output, utc = DateTime.UtcNow.ToString("O") };
            Write(ActivePath, active);
            try
            {
                foreach (string source in active.paths)
                {
                    var row = new AssetRow { source = source, sourceBeforeSha256 = Hash(source), guidBefore = AssetDatabase.AssetPathToGUID(source), before = Final("loaded-before-reimport", source) };
                    report.assets.Add(row);
                    AssetDatabase.ImportAsset(source, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    row.final = Final("loaded-final", source); row.sourceAfterSha256 = Hash(source); row.guidAfter = AssetDatabase.AssetPathToGUID(source);
                    if (row.sourceBeforeSha256 != row.sourceAfterSha256 || row.guidBefore != row.guidAfter) throw new InvalidOperationException("Owned animation source/GUID changed during diagnostic.");
                }
            }
            finally
            {
                Write(Path.Combine(output, "summary.json"), report);
                if (File.Exists(ActivePath) && JsonUtility.FromJson<ActiveProbe>(File.ReadAllText(ActivePath)).owner == active.owner) File.Delete(ActivePath);
            }
            Debug.Log("[JawImportProbe] before/after callback and final imported data: " + output);
        }
    }
}
