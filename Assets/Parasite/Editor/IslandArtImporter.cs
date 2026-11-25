using System;
using UnityEditor;
namespace Parasite.EditorTools
{
    public sealed class IslandArtImporter : AssetPostprocessor
    {
        const string Root="Assets/Parasite/Resources/Island/";
        void OnPreprocessModel()
        {
            if(!assetPath.StartsWith(Root,StringComparison.Ordinal))return;
            var model=(ModelImporter)assetImporter;
            model.globalScale=1;model.useFileScale=true;model.bakeAxisConversion=true;
            model.importAnimation=model.importCameras=model.importLights=false;
            model.addCollider=false;model.isReadable=false;
            model.importNormals=ModelImporterNormals.Import;model.importTangents=ModelImporterTangents.CalculateMikk;
            model.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;model.materialLocation=ModelImporterMaterialLocation.InPrefab;
        }
        void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith(Root+"Textures/",StringComparison.Ordinal))return;
            var texture=(TextureImporter)assetImporter;
            texture.textureType=assetPath.EndsWith("_N.png",StringComparison.Ordinal)?TextureImporterType.NormalMap:TextureImporterType.Default;
            texture.sRGBTexture=assetPath.EndsWith("_A.png",StringComparison.Ordinal);
            texture.alphaSource=TextureImporterAlphaSource.FromInput;texture.mipmapEnabled=true;texture.maxTextureSize=512;
            texture.wrapMode=UnityEngine.TextureWrapMode.Repeat;texture.anisoLevel=4;texture.textureCompression=TextureImporterCompression.CompressedHQ;
        }
    }
}
