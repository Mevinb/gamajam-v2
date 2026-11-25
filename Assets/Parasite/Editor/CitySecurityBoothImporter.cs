using UnityEditor;

namespace Parasite.EditorTools
{
    /// <summary>The booth shares the existing gate atlas and never changes a texture import policy.</summary>
    public sealed class CitySecurityBoothImporter : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (assetPath != CitySecurityBoothArt.ModelPath) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            // Retain the semantic mesh child instead of collapsing a single-mesh FBX into the filename root.
            importer.preserveHierarchy = true;
            importer.importAnimation = importer.importCameras = importer.importLights = false;
            importer.addCollider = false;
            importer.isReadable = true;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
        }
    }
}
