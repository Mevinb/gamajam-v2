using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Parasite.EditorTools
{
    /// <summary>Harbor-only visual imports. Reuses the existing gate maps without changing gate assets or texture policies.</summary>
    public sealed class HarborArtImporter : AssetPostprocessor
    {
        public const string Root = "Assets/Parasite/Resources/Harbor/";
        static readonly string[] Models = { "ValveBase", "ValveWheel", "GaugeBase", "GaugeNeedle", "LiftGate", "Conveyor", "SealedCrate", "RelayPad", "Emitter" };
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root, StringComparison.Ordinal) || !assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) return;
            var model = (ModelImporter)assetImporter;
            model.globalScale = 1; model.useFileScale = true; model.bakeAxisConversion = true; model.preserveHierarchy = true;
            model.animationType = ModelImporterAnimationType.None;
            model.importAnimation = model.importCameras = model.importLights = model.addCollider = false;
            model.isReadable = false; model.meshCompression = ModelImporterMeshCompression.Off;
            model.importNormals = ModelImporterNormals.Import; model.importTangents = ModelImporterTangents.CalculateMikk;
            model.materialImportMode = ModelImporterMaterialImportMode.None;
        }
        public static void PrepareBatch()
        {
            Directory.CreateDirectory(Root);
            foreach (var name in Models)
            {
                string source = "ArtSource/Harbor/Exports/Harbor" + name + ".fbx", target = Root + "Harbor" + name + ".fbx";
                if (!File.Exists(source)) throw new InvalidOperationException("Missing verified Harbor export " + source);
                if (!File.Exists(target) || !SameBytes(source, target)) File.Copy(source, target, true);
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }
            var original = AssetDatabase.LoadAssetAtPath<Material>("Assets/Parasite/Art/CityGatePilot/CityGatePilot_SharedPBR.mat");
            if (!original) throw new InvalidOperationException("Original shared gate PBR material is missing.");
            string path = Root + "HarborSharedPBR.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(original) { name = "HarborSharedPBR" }; AssetDatabase.CreateAsset(material, path); }
            else { material.CopyPropertiesFromMaterial(original); material.name = "HarborSharedPBR"; EditorUtility.SetDirty(material); }
            // References point to the exact existing imported map GUIDs; no texture copies are created here.
            AssetDatabase.SaveAssets(); Debug.Log("[HarborArt] Nine visual models and shared original gate-map references prepared.");
        }
        static bool SameBytes(string a, string b)
        {
            var x = File.ReadAllBytes(a); var y = File.ReadAllBytes(b); if (x.Length != y.Length) return false;
            for (int i=0;i<x.Length;i++) if(x[i]!=y[i]) return false; return true;
        }
    }
}
