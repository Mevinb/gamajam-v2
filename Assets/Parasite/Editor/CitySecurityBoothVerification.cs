using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace Parasite.EditorTools
{
    /// <summary>Actual importer evidence only. Does not bake, generate a scene, change physics or render a preview.</summary>
    public static class CitySecurityBoothVerification
    {
        [Serializable] sealed class Result
        {
            public string status, utc, unityVersion, device, modelSha256, materialPath, materialSha256;
            public string scope = "Actual Unity model import and existing shared-atlas validation only. Scene integration, exact physics, rendered appearance and player performance remain separate gates.";
            public int triangles, vertices, importedMeshes, materials, importedColliders;
            public Vector3 min, max;
            public string[] importedMeshObjects, importedMeshNames;
            public List<string> passed = new(), failures = new();
            public List<TextureRecord> sharedTextures = new();
        }
        [Serializable] sealed class TextureRecord
        {
            public string property, path, sha256, metaSha256;
            public int width, height;
        }

        public static void VerifyImportBatch()
        {
            var result = new Result { status = "FAIL", utc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion, device = SystemInfo.graphicsDeviceName };
            string directory = Path.Combine("Logs", "city-security-booth", DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff"));
            Directory.CreateDirectory(directory);
            try
            {
                AssetDatabase.ImportAsset(CitySecurityBoothArt.ModelPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(CitySecurityBoothArt.ModelPath);
                var filters = model ? model.GetComponentsInChildren<MeshFilter>(true) : Array.Empty<MeshFilter>();
                result.importedMeshObjects = filters.Select(filter => filter.name).ToArray();
                result.importedMeshNames = filters.Select(filter => filter.sharedMesh ? filter.sharedMesh.name : "missing").ToArray();
                Check(CitySecurityBoothArt.TryLoad(out var kit, out string reason), reason ?? "Ready visual-only booth kit", result);
                result.importedMeshes = model.GetComponentsInChildren<MeshFilter>(true).Length;
                result.importedColliders = model.GetComponentsInChildren<Collider>(true).Length;
                result.vertices = kit.mesh.vertexCount;
                result.triangles = kit.mesh.triangles.Length / 3;
                result.min = kit.bounds.min;
                result.max = kit.bounds.max;
                result.materials = kit.mesh.subMeshCount;
                Check(result.importedMeshes == 1 && result.materials == 1 && result.importedColliders == 0, "One visual mesh, one material partition and no imported colliders", result);
                Check(result.triangles <= 7000, "Original booth geometry budget", result);
                Check(kit.material.name == "CityGatePilot_SharedPBR", "Reuse the exact existing gate material", result);
                result.materialPath = AssetDatabase.GetAssetPath(kit.material);
                result.materialSha256 = Sha(result.materialPath);
                result.modelSha256 = Sha(CitySecurityBoothArt.ModelPath);
                foreach (string property in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap" })
                {
                    var texture = kit.material.GetTexture(property) as Texture2D;
                    Check(texture && texture.width == 2048 && texture.height == 2048, property + " retains its existing real 2K Texture2D", result);
                    string path = AssetDatabase.GetAssetPath(texture);
                    Check(path.StartsWith(CityGatePilotArt.Root + "Textures/", StringComparison.Ordinal), property + " points into the existing gate atlas", result);
                    result.sharedTextures.Add(new TextureRecord { property = property, path = path, sha256 = Sha(path), metaSha256 = Sha(path + ".meta"), width = texture.width, height = texture.height });
                }
                Check(!Directory.EnumerateFiles(Path.GetDirectoryName(CitySecurityBoothArt.ModelPath), "*", SearchOption.AllDirectories).Any(p => new[] { ".png", ".jpg", ".tga", ".mat", ".blend" }.Contains(Path.GetExtension(p).ToLowerInvariant())), "Booth runtime asset directory introduces no textures, materials or editable Blender source", result);
                result.status = "PASS";
            }
            catch (Exception error)
            {
                result.failures.Add(error.ToString());
                Debug.LogError("[SecurityBooth] Import verification failed: " + error.Message);
            }
            string file = Path.Combine(directory, "unity-import.json");
            File.WriteAllText(file, JsonUtility.ToJson(result, true) + "\n");
            Debug.Log("[SecurityBooth] " + result.status + " import checks=" + result.passed.Count + " report=" + file);
            if (Application.isBatchMode) EditorApplication.Exit(result.status == "PASS" ? 0 : 1);
        }

        static void Check(bool condition, string detail, Result result)
        {
            if (!condition) throw new InvalidOperationException(detail);
            result.passed.Add(detail);
        }
        static string Sha(string path)
        {
            using var hash = SHA256.Create();
            using var stream = File.OpenRead(path);
            return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
