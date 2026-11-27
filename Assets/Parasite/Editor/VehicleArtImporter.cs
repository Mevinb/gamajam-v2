using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Parasite.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Parasite.EditorTools
{
    /// <summary>Owns only the original Blender road-vehicle kit, its shared surfaces and visual-only LOD prefabs.</summary>
    public sealed class VehicleArtImporter : AssetPostprocessor
    {
        public const string ArtRoot = "Assets/Parasite/Art/Vehicles/";
        public const string Root = "Assets/Parasite/Resources/Vehicles/";
        const string Source = "ArtSource/Vehicles/";
        static readonly string[] Materials = { "VehiclePaint", "VehicleTrim", "VehicleRubber", "VehicleAlloy", "VehicleInterior", "VehicleHeadlamp", "VehicleTaillamp", "VehicleAmber", "VehiclePlate", "VehicleGlass" };
        static readonly string[] PaintNames = { "CarRed", "CarBlue", "CarWhite", "CarBlack", "CarSilver", "CarGreen", "CarTaxi", "CarBeige" };
        static readonly Color[] PaintColors = { new(.55f,.04f,.06f), new(.04f,.14f,.45f), new(.92f,.92f,.94f), new(.02f,.02f,.025f), new(.65f,.67f,.72f), new(.04f,.22f,.12f), new(.98f,.68f,.04f), new(.55f,.48f,.38f) };
        [Serializable] sealed class ModelPin { public string name, file, sha256; }
        [Serializable] sealed class TexturePin { public string name, file, sha256; }
        [Serializable] sealed class Manifest { public int schema, texture_size; public ModelPin[] models; public TexturePin[] textures; }

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ArtRoot + "Models/", StringComparison.Ordinal) || !assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) return;
            var model = (ModelImporter)assetImporter;
            model.globalScale = 1; model.useFileScale = true; model.bakeAxisConversion = true; model.preserveHierarchy = true;
            model.animationType = ModelImporterAnimationType.None; model.importAnimation = model.importCameras = model.importLights = model.addCollider = false;
            model.isReadable = false; model.meshCompression = ModelImporterMeshCompression.Off;
            model.importNormals = ModelImporterNormals.Import; model.importTangents = ModelImporterTangents.CalculateMikk;
            model.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; model.materialLocation = ModelImporterMaterialLocation.InPrefab;
        }

        Material OnAssignMaterialModel(Material material, Renderer renderer)
        {
            if (!assetPath.StartsWith(ArtRoot + "Models/", StringComparison.Ordinal)) return null;
            string name = Materials.SingleOrDefault(value => material.name == value || material.name.StartsWith(value + ".", StringComparison.Ordinal));
            if (name == null) throw new InvalidOperationException("Unexpected vehicle material: " + material.name);
            var shared = AssetDatabase.LoadAssetAtPath<Material>(Root + "Surfaces/" + name + ".mat");
            if (!shared) throw new InvalidOperationException("Vehicle materials must be prepared before FBX import: " + name);
            return shared;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot + "Textures/", StringComparison.Ordinal)) return;
            bool normal = assetPath.EndsWith("_N.png", StringComparison.Ordinal), albedo = assetPath.EndsWith("_A.png", StringComparison.Ordinal);
            var texture = (TextureImporter)assetImporter;
            texture.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            texture.sRGBTexture = albedo; texture.isReadable = false; texture.convertToNormalmap = false; texture.flipGreenChannel = false;
            texture.alphaIsTransparency = false; texture.alphaSource = normal ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            texture.mipmapEnabled = true; texture.streamingMipmaps = true; texture.maxTextureSize = 2048;
            texture.wrapMode = TextureWrapMode.Repeat; texture.filterMode = FilterMode.Trilinear; texture.anisoLevel = 4;
            texture.npotScale = TextureImporterNPOTScale.None; texture.textureCompression = TextureImporterCompression.CompressedHQ; texture.crunchedCompression = false;
            var platform = texture.GetPlatformTextureSettings("Standalone"); platform.name = "Standalone"; platform.overridden = true;
            platform.maxTextureSize = 2048; platform.format = normal ? TextureImporterFormat.BC5 : TextureImporterFormat.BC7;
            platform.textureCompression = TextureImporterCompression.CompressedHQ; texture.SetPlatformTextureSettings(platform);
        }

        static string Hash(string path)
        { using var sha = SHA256.Create(); using var file = File.OpenRead(path); return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant(); }

        static void Copy(string source, string target, string expected)
        {
            if (Hash(source) != expected) throw new InvalidOperationException("Authored vehicle source changed: " + source);
            Folder(Path.GetDirectoryName(target));
            if (!File.Exists(target) || Hash(target) != expected) File.Copy(source, target, true);
            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        static void Folder(string path)
        {
            path = path.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (parent != null && parent != path) Folder(parent);
            Directory.CreateDirectory(path); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (!AssetDatabase.IsValidFolder(path)) throw new InvalidOperationException("Vehicle asset folder was not imported: " + path);
        }

        static Material Surface(string name, Color color, float smoothness, float metallic, bool glass = false, float emission = 0)
        {
            string path = Root + "Surfaces/" + name + ".mat";
            bool paint = name == "VehiclePaint";
            var shader = Shader.Find(paint ? "Universal Render Pipeline/Complex Lit" : "Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("Installed URP vehicle surface shader missing.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(shader) { name = name }; AssetDatabase.CreateAsset(material, path); }
            material.shader = shader; material.shaderKeywords = Array.Empty<string>();
            material.SetFloat("_WorkflowMode", 1); material.SetFloat("_Surface", glass ? 1 : 0); material.SetFloat("_Blend", 0); material.SetFloat("_BlendModePreserveSpecular", 1);
            material.SetFloat("_AlphaClip", 0); material.SetFloat("_Cull", (float)CullMode.Back); material.SetFloat("_ZWrite", glass ? 0 : 1);
            material.SetFloat("_SrcBlend", (float)BlendMode.One); material.SetFloat("_DstBlend", glass ? (float)BlendMode.OneMinusSrcAlpha : 0);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); material.SetFloat("_DstBlendAlpha", glass ? (float)BlendMode.OneMinusSrcAlpha : 0);
            material.SetColor("_BaseColor", color); material.SetFloat("_Metallic", metallic); material.SetFloat("_Smoothness", smoothness); material.SetFloat("_SmoothnessTextureChannel", 0);
            material.SetFloat("_SpecularHighlights", 1); material.SetFloat("_EnvironmentReflections", 1); material.SetFloat("_ReceiveShadows", 1);
            material.SetTexture("_BaseMap", null); material.SetTexture("_BumpMap", null); material.SetTexture("_MetallicGlossMap", null);
            material.SetTexture("_OcclusionMap", null);
            material.SetColor("_EmissionColor", emission > 0 ? color * emission : Color.black);
            material.globalIlluminationFlags = emission > 0 ? MaterialGlobalIlluminationFlags.BakedEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            if (emission > 0) material.EnableKeyword("_EMISSION");
            material.SetOverrideTag("RenderType", glass ? "Transparent" : "Opaque"); material.renderQueue = glass ? 3000 : 2000;
            material.SetShaderPassEnabled("ShadowCaster", !glass); material.SetShaderPassEnabled("DepthOnly", !glass);
            if (glass) { material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.EnableKeyword("_ALPHAPREMULTIPLY_ON"); }
            else
            {
                foreach (var pair in new[] { ("_BaseMap", "A"), ("_BumpMap", "N") })
                {
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "Textures/VehicleDetail_" + pair.Item2 + ".png");
                    if (!texture) throw new InvalidOperationException("Shared vehicle texture not imported: " + pair.Item2);
                    material.SetTexture(pair.Item1, texture); material.SetTextureScale(pair.Item1, new Vector2(2, 2));
                }
                material.SetFloat("_BumpScale", paint ? .08f : name == "VehicleRubber" ? .65f : .30f);
                // URP replaces _Metallic with the map's red channel. This shared detail map has R=1,
                // so using it as metallic would turn rubber, upholstery and lamps into solid metal.
                // Keep the authored per-surface metal/roughness values and use the map's local AO only.
                material.SetTexture("_OcclusionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "Textures/VehicleDetail_M.png")); material.SetTextureScale("_OcclusionMap", new Vector2(2, 2));
                material.SetFloat("_OcclusionStrength", 1); material.EnableKeyword("_OCCLUSIONMAP");
                material.EnableKeyword("_NORMALMAP");
            }
            if (paint) { material.SetFloat("_ClearCoat", 1f); material.SetFloat("_ClearCoatMask", .85f); material.SetFloat("_ClearCoatSmoothness", .86f); material.EnableKeyword("_CLEARCOAT"); }
            material.enableInstancing = true; EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material); return material;
        }

        static void Surfaces()
        {
            Folder(Root + "Surfaces/");
            Surface("VehiclePaint", Color.white, .78f, .38f);
            Surface("VehicleTrim", new Color(.018f,.021f,.024f), .40f, 0);
            Surface("VehicleRubber", new Color(.027f,.029f,.030f), .25f, 0);
            Surface("VehicleAlloy", new Color(.56f,.58f,.61f), .78f, .96f);
            Surface("VehicleInterior", new Color(.051f,.054f,.057f), .19f, 0);
            Surface("VehicleHeadlamp", new Color(.80f,.85f,.81f), .82f, .20f, emission:.35f);
            Surface("VehicleTaillamp", new Color(.26f,.006f,.008f), .75f, .05f, emission:.7f);
            Surface("VehicleAmber", new Color(.65f,.18f,.004f), .70f, 0, emission:.35f);
            Surface("VehiclePlate", new Color(.79f,.80f,.77f), .56f, 0);
            Surface("VehicleGlass", new Color(.80f,.91f,.90f,.16f), .92f, 0, true);
            var paint = AssetDatabase.LoadAssetAtPath<Material>(Root + "Surfaces/VehiclePaint.mat");
            for (int i = 0; i < PaintNames.Length; i++)
            {
                string path = Root + "Surfaces/" + PaintNames[i] + ".mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!material) { material = new Material(paint); AssetDatabase.CreateAsset(material, path); }
                material.CopyPropertiesFromMaterial(paint); material.name = PaintNames[i]; material.SetColor("_BaseColor", PaintColors[i]);
                EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
            }
        }

        static void Prefab(string name)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "Models/" + name + ".fbx");
            if (!model) throw new InvalidOperationException("Vehicle FBX not imported: " + name);
            var root = new GameObject(name);
            try
            {
                var geometry = Object.Instantiate(model, root.transform, false); geometry.name = "Geometry";
                // FBX suffix recognition can create an automatic group; the prefab below owns all LODs.
                foreach (var importedGroup in geometry.GetComponentsInChildren<LODGroup>(true)) Object.DestroyImmediate(importedGroup);
                if (geometry.GetComponentsInChildren<Collider>(true).Length != 0 || geometry.GetComponentsInChildren<Rigidbody>(true).Length != 0)
                    throw new InvalidOperationException("Vehicle kit must be visual only.");
                bool sign = name == "VehicleTaxiSign";
                if (!sign)
                {
                    var body = (VehicleBody)Enum.Parse(typeof(VehicleBody), name.Substring("Vehicle".Length));
                    var centres = VehicleVisuals.WheelCentres(body);
                    for (int wheel = 0; wheel < centres.Length; wheel++)
                    {
                        var pivot = new GameObject("WheelPivot_" + wheel).transform; pivot.SetParent(root.transform, false); pivot.localPosition = centres[wheel];
                        var meshes = geometry.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.Contains("_Wheel_" + wheel + "_LOD")).ToArray();
                        if (meshes.Length != 3) throw new InvalidOperationException("Three visible LOD meshes required per authored wheel: " + name + "/" + wheel);
                        foreach (var mesh in meshes) mesh.transform.SetParent(pivot, true);
                    }
                    var group = root.AddComponent<LODGroup>(); var lods = new LOD[3];
                    for (int lod = 0; lod < 3; lod++)
                    {
                        var renderers = root.GetComponentsInChildren<Renderer>(true).Where(r => r.name.Contains("_LOD" + lod)).ToArray();
                        if (renderers.Length != centres.Length + 2) throw new InvalidOperationException("Vehicle body, glass and wheel LOD set is incomplete: " + name);
                        lods[lod] = new LOD(new[] { .22f, .085f, .012f }[lod], renderers);
                    }
                    group.SetLODs(lods); group.fadeMode = LODFadeMode.None; group.RecalculateBounds();
                }
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (renderer.sharedMaterials.Any(m => !m || !AssetDatabase.GetAssetPath(m).StartsWith(Root + "Surfaces/", StringComparison.Ordinal)))
                        throw new InvalidOperationException("An imported vehicle surface escaped the shared material mapping.");
                    renderer.shadowCastingMode = renderer.name.Contains("_Glass_") ? ShadowCastingMode.Off : ShadowCastingMode.On;
                    renderer.receiveShadows = true; renderer.lightProbeUsage = LightProbeUsage.BlendProbes; renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                }
                PrefabUtility.SaveAsPrefabAsset(root, Root + name + ".prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }

        /// <summary>Refreshes only the existing Van model/prefab, preserving the prepared shared surfaces.</summary>
        public static void PrepareVanBatch()
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Source + "production-manifest.json"));
            if (manifest == null || manifest.schema != 1 || manifest.texture_size != 2048 || manifest.models?.Length != 6 || manifest.textures?.Length != 3)
                throw new InvalidOperationException("Complete reviewed six-export/three-map vehicle manifest is required.");
            var record = manifest.models.Single(model => model != null && model.name == "VehicleVan");
            if (record.file != "Exports/VehicleVan.fbx") throw new InvalidOperationException("Van-only import requires the canonical manifest export path.");
            const string modelPath = ArtRoot + "Models/VehicleVan.fbx", prefabPath = Root + "VehicleVan.prefab";
            foreach (string path in new[] { modelPath, prefabPath })
                if (!File.Exists(path) || !File.Exists(path + ".meta") || !AssetDatabase.LoadAssetAtPath<GameObject>(path))
                    throw new InvalidOperationException("Van-only import requires an existing prepared model/prefab and meta: " + path);
            foreach (string name in Materials)
            {
                string path = Root + "Surfaces/" + name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!File.Exists(path) || !File.Exists(path + ".meta") || !material || !material.shader)
                    throw new InvalidOperationException("Van-only import requires all canonical prepared shared materials: " + name);
            }
            string modelGuid = AssetDatabase.AssetPathToGUID(modelPath), prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);
            if (string.IsNullOrEmpty(modelGuid) || string.IsNullOrEmpty(prefabGuid)) throw new InvalidOperationException("Existing Van asset GUIDs are required.");
            Copy(Source + record.file, modelPath, record.sha256);
            Prefab("VehicleVan");
            if (AssetDatabase.AssetPathToGUID(modelPath) != modelGuid || AssetDatabase.AssetPathToGUID(prefabPath) != prefabGuid)
                throw new InvalidOperationException("Van-only import changed an existing asset GUID.");
            Debug.Log("[VehicleArt] Existing Van model/prefab refreshed from its manifest. Regenerate City through CityBuilder to update serialized mesh/material bindings.");
        }

        public static void PrepareBatch()
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Source + "production-manifest.json"));
            if (manifest == null || manifest.schema != 1 || manifest.texture_size != 2048 || manifest.models?.Length != 6 || manifest.textures?.Length != 3)
                throw new InvalidOperationException("Complete reviewed six-export/three-map vehicle manifest is required.");
            Folder(Root);
            foreach (var texture in manifest.textures) Copy(Source + texture.file, ArtRoot + "Textures/" + texture.name + ".png", texture.sha256);
            Surfaces();
            foreach (var model in manifest.models) Copy(Source + model.file, ArtRoot + "Models/" + model.name + ".fbx", model.sha256);
            foreach (var model in manifest.models) Prefab(model.name);
            AssetDatabase.SaveAssets();
            Debug.Log("[VehicleArt] Five original Blender vehicle bodies, taxi sign, shared2K maps, bounded three LODs and pivot-correct visual prefabs prepared. Renderer/gameplay acceptance remains separate.");
        }
    }
}
