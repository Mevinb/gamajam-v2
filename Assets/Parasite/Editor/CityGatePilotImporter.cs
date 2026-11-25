using System;
using UnityEditor;
using UnityEngine;

namespace Parasite.EditorTools
{
    /// <summary>Only this new pilot prefix; no other city/island texture policy is touched.</summary>
    public sealed class CityGatePilotImporter : AssetPostprocessor
    {
        const string Root = "Assets/Parasite/Art/CityGatePilot/";
        void OnPreprocessModel()
        {
            if (assetPath != Root + "CityGatePilot.fbx") return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1; importer.useFileScale = true; importer.bakeAxisConversion = true;
            importer.importAnimation = importer.importCameras = importer.importLights = false;
            importer.addCollider = false; importer.isReadable = true; // required only for WorldKit's editor-time static merge
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import; importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
        }
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root + "Textures/CityGatePilot_", StringComparison.Ordinal) || !assetPath.EndsWith(".png", StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.textureType = assetPath.EndsWith("_N.png", StringComparison.Ordinal) ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = assetPath.EndsWith("_A.png", StringComparison.Ordinal);
            importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.mipmapEnabled = true; importer.maxTextureSize = 2048;
            importer.wrapMode = TextureWrapMode.Repeat; importer.anisoLevel = 4; importer.textureCompression = TextureImporterCompression.CompressedHQ;
            var platform = importer.GetPlatformTextureSettings("Standalone"); platform.name = "Standalone"; platform.overridden = true;
            platform.maxTextureSize = 2048; platform.format = TextureImporterFormat.BC7; platform.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SetPlatformTextureSettings(platform);
        }
    }
}
