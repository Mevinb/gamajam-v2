using System;
using UnityEditor;
using UnityEngine;

namespace Parasite.EditorTools
{
    /// <summary>Import policy owned only by the Blender-authored environment detail kit.</summary>
    public sealed class EnvironmentRealismImporter : AssetPostprocessor
    {
        public const string Root = "Assets/Parasite/Art/EnvironmentRealism/";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root, StringComparison.Ordinal) || !assetPath.EndsWith(".fbx", StringComparison.Ordinal)) return;
            var m = (ModelImporter)assetImporter;
            m.globalScale = 1f; m.useFileScale = true; m.bakeAxisConversion = true; m.preserveHierarchy = true;
            m.animationType = ModelImporterAnimationType.None;
            m.importAnimation = m.importCameras = m.importLights = m.addCollider = false;
            m.isReadable = true; // WorldKit reads vertices once while generating its static cell batches.
            m.meshCompression = ModelImporterMeshCompression.Off;
            m.importNormals = ModelImporterNormals.Import; m.importTangents = ModelImporterTangents.CalculateMikk;
            m.materialImportMode = ModelImporterMaterialImportMode.None;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root + "Textures/EnvironmentRealism_", StringComparison.Ordinal) || !assetPath.EndsWith(".png", StringComparison.Ordinal)) return;
            bool normal = assetPath.EndsWith("_N.png", StringComparison.Ordinal);
            var t = (TextureImporter)assetImporter;
            t.textureShape = TextureImporterShape.Texture2D;
            t.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            t.sRGBTexture = assetPath.EndsWith("_A.png", StringComparison.Ordinal);
            t.alphaSource = normal ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            t.mipmapEnabled = true; t.maxTextureSize = 2048; t.anisoLevel = 4;
            t.wrapMode = TextureWrapMode.Clamp; t.textureCompression = TextureImporterCompression.CompressedHQ;
            var p = t.GetPlatformTextureSettings("Standalone");
            p.name = "Standalone"; p.overridden = true; p.maxTextureSize = 2048;
            p.format = normal ? TextureImporterFormat.BC5 : TextureImporterFormat.BC7;
            p.textureCompression = TextureImporterCompression.CompressedHQ;
            t.SetPlatformTextureSettings(p);
        }
    }
}
