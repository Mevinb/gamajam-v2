using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;

namespace Parasite.EditorTools
{
    /// <summary>Run in a separate batch invocation after the changed importer has compiled. Never publishes assets.</summary>
    public static class IslandWallImportGate
    {
        const string Root = "Assets/Parasite/Resources/Island/";
        const string ReportPath = "Logs/island-wall-import-verification.json";
        [Serializable] sealed class Check { public string name, expected, actual; public bool pass; }
        [Serializable] sealed class TextureRow
        {
            public string path, sha256, type, shape, loadedAssetType, format, graphicsFormat; public int width, height, maxTextureSize, mipCount, shapeValue;
            public bool srgb, compressed, mipmaps, oldIslandMap; public long runtimeMemoryBytes;
        }
        [Serializable] sealed class MeshRow { public string name, material; public long triangles; public Vector3 min, max; }
        [Serializable] sealed class Report
        {
            public int schema = 2; public int globalTextureMipmapLimit; public string status, startedUtc, finishedUtc, unityVersion, buildTarget, graphicsDevice;
            public string importerSourceSha256, editorAssemblySha256, modelSha256, exception;
            public bool synchronousForceReimport; public List<Check> checks = new(); public List<TextureRow> textures = new(); public List<MeshRow> meshes = new();
            public string evidenceScope = "Actual Unity imports in this invocation. Existing Blender/CPU proofs are inputs, not Unity results. Runtime traversal/render verification is separate.";
        }
        public static void ImportAndVerifyBatch()
        {
            var report = new Report { startedUtc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
                buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(), graphicsDevice = SystemInfo.graphicsDeviceType.ToString(), globalTextureMipmapLimit=QualitySettings.globalTextureMipmapLimit };
            try
            {
                Require(report, "Importer compilation has completed", !EditorApplication.isCompiling && !EditorApplication.isUpdating, "not compiling/updating", EditorApplication.isCompiling + "/" + EditorApplication.isUpdating);
                Require(report, "Linux target", EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneLinux64, "StandaloneLinux64", report.buildTarget);
                if (report.checks.Any(c => !c.pass)) throw new InvalidOperationException("Import gate preconditions failed; run after importer compilation on the Linux target.");
                report.importerSourceSha256 = Sha("Assets/Parasite/Editor/IslandArtImporter.cs");
                report.editorAssemblySha256 = Sha(typeof(IslandArtImporter).Assembly.Location);
                // Do not Refresh the project or mutate importer settings. The compiled prefix policy must do this work.
                foreach (string suffix in new[] { "A", "N", "M" })
                    AssetDatabase.ImportAsset(Root + "Textures/IslandQuarantine_" + suffix + ".png", ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(Root + "IslandQuarantineWalls.fbx", ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                report.synchronousForceReimport = true;
                foreach (string suffix in new[] { "A", "N", "M" }) Texture(report, "IslandQuarantine", suffix, 2048, false);
                foreach (string surface in new[] { "IslandClinic", "IslandConcrete", "IslandFabric", "IslandMetal", "IslandOxide", "IslandTimber" })
                    foreach (string suffix in new[] { "A", "N", "M" }) Texture(report, surface, suffix, 512, true);
                Model(report);
            }
            catch (Exception e) { report.exception = e.ToString(); }
            report.status = report.exception == null && report.checks.All(c => c.pass) ? "PASS" : "FAIL";
            report.finishedUtc = DateTime.UtcNow.ToString("O");
            string destination=Path.Combine(Directory.GetParent(Application.dataPath).FullName,ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.WriteAllText(destination, JsonUtility.ToJson(report, true) + "\n");
            Debug.Log("[IslandWallImport] " + report.status + " " + report.checks.Count + " actual checks; " + destination);
            EditorApplication.Exit(report.status == "PASS" ? 0 : 1);
        }
        static void Texture(Report report, string surface, string suffix, int size, bool old)
        {
            string path = Root + "Textures/" + surface + "_" + suffix + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var mainAsset = AssetDatabase.LoadMainAssetAtPath(path);
            // Record the importer's shape even when a Cubemap makes the Texture2D load fail.
            var row = new TextureRow { path = path, sha256 = importer ? Sha(path) : null, oldIslandMap = old,
                shape = importer ? importer.textureShape.ToString() : "missing importer", shapeValue = importer ? (int)importer.textureShape : -1,
                loadedAssetType = mainAsset ? mainAsset.GetType().FullName : "missing asset", type = importer ? importer.textureType.ToString() : "missing importer",
                maxTextureSize = importer ? importer.maxTextureSize : 0, srgb = importer && importer.sRGBTexture, mipmaps = importer && importer.mipmapEnabled };
            report.textures.Add(row);
            Require(report, path + " 2D shape", importer && importer.textureShape == TextureImporterShape.Texture2D,
                TextureImporterShape.Texture2D.ToString(), row.shape + " (" + row.shapeValue + "); loaded=" + row.loadedAssetType);
            Require(report, path + " exists", importer && texture, "TextureImporter + Texture2D", (importer ? "importer" : "missing importer") + "/" + row.loadedAssetType);
            if (!importer || !texture) return;
            bool normal = suffix == "N", srgb = suffix == "A";
            bool compressed = GraphicsFormatUtility.IsCompressedFormat(texture.graphicsFormat);
            row.width = texture.width; row.height = texture.height; row.format = texture.format.ToString();
            row.graphicsFormat = texture.graphicsFormat.ToString(); row.compressed = compressed; row.mipCount = texture.mipmapCount;
            row.runtimeMemoryBytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture);
            Require(report, path + " actual size and cap", texture.width == size && texture.height == size && importer.maxTextureSize == size,
                size + "x" + size + "; max=" + size, texture.width + "x" + texture.height + "; max=" + importer.maxTextureSize);
            Require(report, path + " type", importer.textureType == (normal ? TextureImporterType.NormalMap : TextureImporterType.Default), normal ? "NormalMap" : "Default", importer.textureType.ToString());
            Require(report, path + " colour space", importer.sRGBTexture == srgb && GraphicsFormatUtility.IsSRGBFormat(texture.graphicsFormat) == srgb,
                srgb ? "sRGB" : "linear", importer.sRGBTexture + "/" + texture.graphicsFormat);
            Require(report, path + " compressed HQ mips", compressed && importer.textureCompression == TextureImporterCompression.CompressedHQ && importer.mipmapEnabled && texture.mipmapCount > 1,
                "compressed/HQ/mipmapped", texture.format + "/" + texture.graphicsFormat + "/" + importer.textureCompression + "/mips=" + texture.mipmapCount);
        }
        static void Model(Report report)
        {
            string path = Root + "IslandQuarantineWalls.fbx"; report.modelSha256 = Sha(path);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Require(report, "Wall model exists", importer && prefab, "ModelImporter + prefab", (importer ? "importer" : "missing") + "/" + (prefab ? "prefab" : "missing"));
            if (!importer || !prefab) return;
            Require(report, "Model metre/axis/collider policy", Mathf.Abs(importer.globalScale - 1) < .00001f && importer.useFileScale && importer.bakeAxisConversion && !importer.addCollider && !importer.importAnimation,
                "scale1/fileScale/bakedAxes/no generated collider/no animation", importer.globalScale + "/" + importer.useFileScale + "/" + importer.bakeAxisConversion + "/" + importer.addCollider + "/" + importer.importAnimation);
            var preview = EditorSceneManager.NewPreviewScene(); GameObject instance = null;
            try
            {
                instance = UnityEngine.Object.Instantiate(prefab); SceneManager.MoveGameObjectToScene(instance, preview);
                var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
                Require(report, "Exactly two model renderers", renderers.Length == 2, "2", renderers.Length.ToString());
                Require(report, "No model colliders", instance.GetComponentsInChildren<Collider>(true).Length == 0, "0", instance.GetComponentsInChildren<Collider>(true).Length.ToString());
                var materials = renderers.SelectMany(r => r.sharedMaterials).Distinct().ToArray();
                Require(report, "One shared authored material", materials.Length == 1 && materials[0] && materials[0].name == "IslandQuarantineConcrete" && renderers.All(r => r.sharedMaterials.Length == 1),
                    "one IslandQuarantineConcrete shared by both single-slot renderers", string.Join(",", materials.Select(m => m ? m.name : "null")));
                long total = 0;
                foreach (var renderer in renderers)
                {
                    var filter = renderer.GetComponent<MeshFilter>(); var mesh = filter ? filter.sharedMesh : null;
                    Require(report, renderer.name + " mesh", mesh, "MeshFilter + mesh", mesh ? mesh.name : "missing"); if (!mesh) continue;
                    long triangles = 0;
                    for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    { Require(report, renderer.name + " topology", mesh.GetTopology(sub) == MeshTopology.Triangles, "Triangles", mesh.GetTopology(sub).ToString()); triangles += (long)mesh.GetIndexCount(sub) / 3; }
                    total += triangles;
                    bool main = renderer.name.StartsWith("QuarantineFacade", StringComparison.Ordinal), south = renderer.name.StartsWith("SouthernQuayFacade", StringComparison.Ordinal);
                    Vector3 min = main ? new(-53,.15f,-18.2f) : new(-52,.15f,-38), max = main ? new(53,3.75f,-17.8f) : new(52,1.35f,-37.4f);
                    Require(report, renderer.name + " identity/count/bounds", (main || south) && triangles == (main ? 7664 : 4140) && Vector3.Distance(renderer.bounds.min, min) < .005f && Vector3.Distance(renderer.bounds.max, max) < .005f,
                        (main ? "7664" : "4140") + " triangles; " + min + ".." + max, triangles + "; " + renderer.bounds.min + ".." + renderer.bounds.max);
                    report.meshes.Add(new MeshRow { name = renderer.name, triangles = triangles, material = renderer.sharedMaterial ? renderer.sharedMaterial.name : "null", min = renderer.bounds.min, max = renderer.bounds.max });
                }
                Require(report, "Exact total geometry", total == 11804, "11804 triangles", total.ToString());
            }
            finally { if (instance) UnityEngine.Object.DestroyImmediate(instance); EditorSceneManager.ClosePreviewScene(preview); }
        }
        static void Require(Report report, string name, bool pass, string expected, string actual) => report.checks.Add(new Check { name=name, pass=pass, expected=expected, actual=actual });
        static string Sha(string path) { if(!Path.IsPathRooted(path))path=Path.Combine(Directory.GetParent(Application.dataPath).FullName,path); using var sha = SHA256.Create(); using var file = File.OpenRead(path); return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant(); }
    }
}
