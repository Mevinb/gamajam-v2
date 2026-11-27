#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Parasite.EditorTools
{
    /// <summary>Accepted LakeGirl-only hair surface binding. No model/prefab/catalog rebuild or other material write.</summary>
    public static class LakeGirlHairSurfaceImporter
    {
        public const string NormalPath = CharacterModelImporter.TexturesDir + "/LakeGirl_HairBundle_N.png";
        public const string FlowPath = CharacterModelImporter.TexturesDir + "/LakeGirl_HairBundle_Flow.png";
        const string MaterialPath = CharacterModelImporter.MaterialsDir + "/LakeGirl_Hair.mat";
        const string NormalSha = "f03fdf19d64411569a20679ac450f9e1890c3298a3dd8027ac46eacf4af83d24";
        const string FlowSha = "999b7115c81964b00662431fdfbb7a7b559d257ffa88c78d05b5947e1534ac40";
        const string HairAlbedoSha = "d6e9a831936e3536940dbc21eb7f850cdf25a61516139959979f6d7e78873022";
        const string Keyword = "_PARASITE_HAIR_DIRECTIONAL";
        [Serializable] sealed class Pin { public string path, sha256; }
        [Serializable] sealed class Receipt
        {
            public string scope = "LakeGirl Hair only: configure two new normal/flow assets and update existing material in place; unchanged geometry/rig/UV/alpha and other materials. Accepted inherited cap silhouette remains.";
            public string startedUtc, endedUtc, error, materialGuidBefore, materialGuidAfter, materialShaBefore, materialShaAfter, normalGuid, flowGuid;
            public bool applied, protectedInputsUnchanged; public List<Pin> protectedInputs = new List<Pin>();
        }
        static string Project => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        static string Hash(string relative) { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Project, relative)))).Replace("-", "").ToLowerInvariant(); }
        static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException("[LakeGirlHair] " + reason); }
        static void CheckSource()
        { Require(Hash(NormalPath) == NormalSha && Hash(FlowPath) == FlowSha, "Accepted normal/flow source bytes differ"); Require(Hash(CharacterModelImporter.TexturesDir + "/LakeGirl_Hair_A.png") == HairAlbedoSha, "Original albedo/alpha bytes changed"); }
        static void Configure(string path, bool normal)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter; Require(importer, "Import accepted source first: " + path);
            int size = normal ? 2048 : 1024;
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = false; importer.flipGreenChannel = false; importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = size; importer.mipmapEnabled = true; importer.filterMode = FilterMode.Trilinear; importer.wrapMode = TextureWrapMode.Clamp;
            importer.anisoLevel = 4; importer.streamingMipmaps = false; importer.isReadable = false;
            importer.alphaSource = normal ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false; importer.mipMapsPreserveCoverage = false;
            importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.crunchedCompression = false;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Standalone", overridden = true, maxTextureSize = size, format = normal ? TextureImporterFormat.BC5 : TextureImporterFormat.BC7, textureCompression = TextureImporterCompression.CompressedHQ, compressionQuality = 100 });
            importer.SaveAndReimport();
        }
        static Texture2D Texture(string path, bool normal)
        {
            var value = AssetDatabase.LoadAssetAtPath<Texture2D>(path); var importer = AssetImporter.GetAtPath(path) as TextureImporter; Require(value && importer, "Missing accepted imported map: " + path);
            var platform = importer.GetPlatformTextureSettings("Standalone"); int size = normal ? 2048 : 1024;
            Require(value.width == size && value.height == size && value.mipmapCount == (normal ? 12 : 11) && value.activeMipmapLimit == 0, "Actual map size/mips/limit differ: " + path);
            Require(value.format == (normal ? TextureFormat.BC5 : TextureFormat.BC7) && SystemInfo.SupportsTextureFormat(value.format) && !GraphicsFormatUtility.IsSRGBFormat(value.graphicsFormat), "Actual map compression/linear format differs: " + path);
            Require(importer.textureType == (normal ? TextureImporterType.NormalMap : TextureImporterType.Default) && !importer.sRGBTexture && !importer.flipGreenChannel && importer.npotScale == TextureImporterNPOTScale.None, "Map type/handedness differs: " + path);
            Require(platform.overridden && platform.maxTextureSize == size && platform.format == (normal ? TextureImporterFormat.BC5 : TextureImporterFormat.BC7) && importer.maxTextureSize == size, "Explicit Standalone policy differs: " + path);
            Require(importer.mipmapEnabled && !value.streamingMipmaps && !value.isReadable && value.filterMode == FilterMode.Trilinear && value.wrapMode == TextureWrapMode.Clamp && !importer.alphaIsTransparency, "Map sampling/residency differs: " + path);
            if (!normal) Require(importer.alphaSource == TextureImporterAlphaSource.FromInput, "Flow alpha must retain cap validity");
            return value;
        }
        static void CheckMaterial(Material material)
        {
            Require(material && material.name == "LakeGirl_Hair" && material.shader && material.shader.name == "Parasite/Character", "Only existing canonical LakeGirl Hair may bind");
            Require(material.HasProperty("_HairFlowMap") && material.HasProperty("_HairDirectional") && material.HasProperty("_HairAnisotropy") && material.HasTexture("_BumpMap") && material.HasTexture("_HairFlowMap"), "Accepted shared shader support is missing");
            Require(AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap")) == CharacterModelImporter.TexturesDir + "/LakeGirl_Hair_A.png" && material.GetColor("_BaseColor") == Color.white, "Existing albedo binding/colour differs");
            foreach (var item in new[] { ("_CharacterMode", 2f), ("_Metallic", 0f), ("_Smoothness", .43f), ("_Cutoff", .4f), ("_HairForwardCutoff", .02f), ("_AlphaClip", 1f), ("_Surface", 1f), ("_SrcBlend", 5f), ("_DstBlend", 10f), ("_ZWrite", 1f), ("_Cull", 0f) }) Require(material.GetFloat(item.Item1) == item.Item2, "Accepted coverage/material value differs: " + item.Item1);
            Require(material.renderQueue == 3000 && material.GetTag("RenderType", false, "") == "Transparent" && material.IsKeywordEnabled("_ALPHATEST_ON") && material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"), "Existing coverage/queue/keywords differ");
        }
        internal static void BindReviewed(Material material)
        {
            CheckSource(); CheckMaterial(material); var normal = Texture(NormalPath, true); var flow = Texture(FlowPath, false);
            material.SetTexture("_BumpMap", normal); material.SetFloat("_BumpScale", 1f); material.SetTexture("_HairFlowMap", flow);
            material.SetFloat("_HairDirectional", 1f); material.SetFloat("_HairAnisotropy", .65f); material.EnableKeyword("_NORMALMAP"); material.EnableKeyword(Keyword);
            Require(material.GetTexture("_BumpMap") == normal && material.GetTexture("_HairFlowMap") == flow && material.IsKeywordEnabled("_NORMALMAP") && material.IsKeywordEnabled(Keyword) && material.GetFloat("_HairDirectional") == 1 && material.GetFloat("_HairAnisotropy") == .65f, "Actual reviewed map/keyword binding failed");
            CheckMaterial(material);
        }
        static List<Pin> Protected()
        {
            var paths = new[] { CharacterModelImporter.ModelsDir, CharacterModelImporter.ResourcesDir, CharacterModelImporter.TexturesDir }.SelectMany(directory => Directory.GetFiles(Path.Combine(Project, directory), "*", SearchOption.AllDirectories));
            return paths.Select(p => p.Substring(Project.Length + 1).Replace('\\', '/')).Where(p => p != MaterialPath && p != NormalPath && p != NormalPath + ".meta" && p != FlowPath && p != FlowPath + ".meta").Distinct().OrderBy(p => p).Select(p => new Pin { path = p, sha256 = Hash(p) }).ToList();
        }
        public static void ApplyBatch()
        {
            var receipt = new Receipt { startedUtc = DateTime.UtcNow.ToString("O") }; string output = Path.Combine(Project, "Logs/lakegirl-hair-canonical-import-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ")); Directory.CreateDirectory(output);
            try
            {
                CheckSource(); var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath); CheckMaterial(material);
                receipt.materialGuidBefore = AssetDatabase.AssetPathToGUID(MaterialPath); receipt.materialShaBefore = Hash(MaterialPath); receipt.protectedInputs = Protected();
                Configure(NormalPath, true); Configure(FlowPath, false); BindReviewed(material); EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
                receipt.materialGuidAfter = AssetDatabase.AssetPathToGUID(MaterialPath); receipt.materialShaAfter = Hash(MaterialPath); receipt.normalGuid = AssetDatabase.AssetPathToGUID(NormalPath); receipt.flowGuid = AssetDatabase.AssetPathToGUID(FlowPath);
                receipt.protectedInputsUnchanged = receipt.protectedInputs.All(p => Hash(p.path) == p.sha256); Require(receipt.materialGuidBefore == receipt.materialGuidAfter && receipt.protectedInputsUnchanged, "Existing GUID or protected character bytes changed");
                receipt.applied = true;
            }
            catch (Exception error) { receipt.error = error.ToString(); Debug.LogException(error); }
            finally
            {
                if (receipt.protectedInputs.Count > 0) receipt.protectedInputsUnchanged = receipt.protectedInputs.All(p => File.Exists(Path.Combine(Project, p.path)) && Hash(p.path) == p.sha256);
                receipt.applied &= receipt.protectedInputsUnchanged; receipt.endedUtc = DateTime.UtcNow.ToString("O"); File.WriteAllText(Path.Combine(output, "receipt.json"), JsonUtility.ToJson(receipt, true) + "\n"); if (Application.isBatchMode) EditorApplication.Exit(receipt.applied ? 0 : 1);
            }
        }
    }
}
#endif
