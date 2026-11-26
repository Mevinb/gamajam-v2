using UnityEditor;

namespace Parasite.EditorTools
{
    /// <summary>Consistent meter-scale FBX and PBR texture settings for the reproducible Blender film sets.</summary>
    public sealed class CinematicArtImporter : AssetPostprocessor
    {
        const string Root = "Assets/Parasite/Resources/Cinematics/";
        void OnPreprocessAsset()
        {
            if (!assetPath.StartsWith(Root + "Films/", System.StringComparison.Ordinal) || !(assetImporter is VideoClipImporter video)) return;
            // The source is already Linux-compatible VP8/WebM. Keep it intact; avoid platform-dependent auto transcoding.
            var settings = video.defaultTargetSettings;
            settings.enableTranscoding = false; video.defaultTargetSettings = settings;
            video.ClearTargetSettings("Standalone");
            video.importAudio = false; video.sRGBClip = true;
        }

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root, System.StringComparison.Ordinal)) return;
            var model = (ModelImporter)assetImporter;
            model.globalScale = 1f; model.useFileScale = true; model.bakeAxisConversion = true;
            model.importCameras = model.importLights = model.importAnimation = false;
            model.addCollider = false; model.isReadable = false;
            model.importNormals = ModelImporterNormals.Import;
            model.importTangents = ModelImporterTangents.CalculateMikk;
            model.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            model.materialLocation = ModelImporterMaterialLocation.InPrefab;
        }
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root + "Textures/", System.StringComparison.Ordinal)) return;
            var texture = (TextureImporter)assetImporter;
            bool normal = assetPath.EndsWith("_N.png", System.StringComparison.Ordinal);
            texture.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            texture.sRGBTexture = assetPath.EndsWith("_A.png", System.StringComparison.Ordinal);
            texture.alphaSource = TextureImporterAlphaSource.FromInput;
            texture.mipmapEnabled = true; texture.maxTextureSize = 1024;
            texture.wrapMode = UnityEngine.TextureWrapMode.Repeat;
            texture.anisoLevel = 4;
            texture.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
