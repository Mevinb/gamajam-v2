using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Parasite.Missions;
using UnityEditor;
using UnityEngine;
namespace Parasite.EditorTools
{
    /// <summary>Scoped Northline-only static imports. Never changes existing gate/Harbor model or texture importers.</summary>
    public sealed class RailArtImporter : AssetPostprocessor
    {
        public const string Root = "Assets/Parasite/Resources/NorthlineRail/";
        public const string OriginalMaterial = "Assets/Parasite/Art/CityGatePilot/CityGatePilot_SharedPBR.mat";
        public sealed class FilePin
        {
            public readonly string Path,Sha; public readonly long Bytes;
            public FilePin(string path,string sha,long bytes){Path=path;Sha=sha;Bytes=bytes;}
        }
        public static readonly FilePin[] OriginalGateFiles = {
            new FilePin("Assets/Parasite/Art/CityGatePilot/CityGatePilot_SharedPBR.mat","738945d7214237ddd98676dc1a8580fe23ad868ee6c6f1547639efcd67012115",4076),
            new FilePin("Assets/Parasite/Art/CityGatePilot/CityGatePilot_SharedPBR.mat.meta","3a8e1f6904aa53a2865a3286dd2b78fd67f9f90e655132d375682a71808eaa91",188),
            new FilePin("Assets/Parasite/Art/CityGatePilot/Textures/CityGatePilot_A.png","5bcbce1ec0eb19b0aa709b2ce75d571037fa0dc029a25fd9a74e0cf187976006",3758966),
            new FilePin("Assets/Parasite/Art/CityGatePilot/Textures/CityGatePilot_A.png.meta","96f19db00cb0104d85d4ca23fec2b759d3b0d6474ab8e59b7d19426ba74e4f78",2652),
            new FilePin("Assets/Parasite/Art/CityGatePilot/Textures/CityGatePilot_N.png","60882f3511d1c629881e4a0bb9787331efad59700895bdeea67fd343ec323a64",3124980),
            new FilePin("Assets/Parasite/Art/CityGatePilot/Textures/CityGatePilot_N.png.meta","eb6718f5f4207f157e72d37ce5d4f80bbdc7a6fd67130daabb1ae76a2849ee4f",2652),
            new FilePin("Assets/Parasite/Art/CityGatePilot/Textures/CityGatePilot_M.png","c075395e26934f8ad21990ed9340c9b1548f78cc9068ecf716c9bc59f3b83cfb",1666303),
            new FilePin("Assets/Parasite/Art/CityGatePilot/Textures/CityGatePilot_M.png.meta","a4bc7d255f8c286450f73304b9cd1382ebba92e4f3dd9bb57e192a383fba7f08",2652),
        };
        void OnPreprocessModel()
        {
            if(!assetPath.StartsWith(Root,StringComparison.Ordinal)||!assetPath.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase))return;
            string name=Path.GetFileNameWithoutExtension(assetPath);
            if(!RailArtContract.Models.Any(m=>m.Name==name))throw new InvalidOperationException("Unknown Northline model: "+assetPath);
            var model=(ModelImporter)assetImporter;model.globalScale=1;model.useFileScale=true;model.bakeAxisConversion=true;model.preserveHierarchy=true;
            model.animationType=ModelImporterAnimationType.None;model.importAnimation=model.importCameras=model.importLights=model.addCollider=false;
            model.isReadable=false;model.meshCompression=ModelImporterMeshCompression.Off;model.importNormals=ModelImporterNormals.Import;model.importTangents=ModelImporterTangents.CalculateMikk;
            model.materialImportMode=ModelImporterMaterialImportMode.None;
        }
        [Serializable] sealed class Receipt
        {
            public string status="FAIL",startedUtc,endedUtc,source,error;public int expectedNativeExit=1;
            public List<string> copied=new(),unchanged=new();public string scope="Owned eleven FBX/material preparation and immutable gate-file checks only; no import/model/UV/pixel/physics/gameplay result is inferred.";
        }
        public static string Hash(string path)
        {using var sha=SHA256.Create();using var file=File.OpenRead(path);return BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();}
        public static string Option(string name,string fallback)
        {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
        public static string FreshLogPath(string option,string stem)
        {
            string project=Path.GetFullPath(Path.Combine(Application.dataPath,".."));string path=Path.GetFullPath(Option(option,Path.Combine(project,"Logs",stem+"-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ")+".json")));
            if(!path.StartsWith(Path.Combine(project,"Logs")+Path.DirectorySeparatorChar,StringComparison.Ordinal)||File.Exists(path)||Directory.Exists(path))throw new ArgumentException("Fresh report path under project Logs is required");
            return path;
        }
        public static string ValidateSource()
        {
            string project=Path.GetFullPath(Path.Combine(Application.dataPath,".."));string source=Path.GetFullPath(Option("-northlineArtSource",Path.Combine(project,"ArtSource","NorthlineRail")));
            if(!source.StartsWith(Path.Combine(project,"Logs")+Path.DirectorySeparatorChar,StringComparison.Ordinal)&&source!=Path.Combine(project,"ArtSource","NorthlineRail"))throw new ArgumentException("Source must be reviewed project Logs output or canonical ArtSource/NorthlineRail");
            if((File.GetAttributes(source)&FileAttributes.ReparsePoint)!=0||(File.GetAttributes(Path.Combine(source,"Exports"))&FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException("Reviewed source folders must be real directories, not links");
            if(Hash(Path.Combine(source,"production-manifest.json"))!=RailArtContract.AuthorManifestSha256)throw new InvalidOperationException("Exact root-reviewed author manifest differs");
            if(Directory.GetFiles(Path.Combine(source,"Exports"),"*.fbx").Length!=RailArtContract.Models.Count)throw new InvalidOperationException("Exactly eleven original exports are required");
            foreach(var model in RailArtContract.Models)if(Hash(Path.Combine(source,"Exports",model.Name+".fbx"))!=model.SourceSha256)throw new InvalidOperationException("Source export hash differs: "+model.Name);
            return source;
        }
        public static void CheckOriginals()
        {
            foreach(var pin in OriginalGateFiles)if(!File.Exists(pin.Path)||new FileInfo(pin.Path).Length!=pin.Bytes||Hash(pin.Path)!=pin.Sha)throw new InvalidOperationException("Original gate resource changed: "+pin.Path);
        }
        public static void PrepareBatch()
        {
            string output=FreshLogPath("-northlineArtPrepareOut","northline-art-prepare");var report=new Receipt{startedUtc=DateTime.UtcNow.ToString("O")};
            try
            {
                if(!Application.isBatchMode||Application.isPlaying)throw new InvalidOperationException("Run this explicitly in batch EditMode, outside play");
                CheckOriginals();report.source=ValidateSource();var material=AssetDatabase.LoadAssetAtPath<Material>(OriginalMaterial);
                if(!material)throw new InvalidOperationException("Original gate PBR material is not imported");
                if(Directory.Exists(Root))
                {
                    if((File.GetAttributes(Root)&FileAttributes.ReparsePoint)!=0||Directory.GetFileSystemEntries(Root,"*",SearchOption.AllDirectories).Any(path=>(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0))throw new InvalidOperationException("Owned Northline asset paths must not be links");
                    foreach(string asset in Directory.GetFiles(Root,"*.fbx",SearchOption.AllDirectories))if(!RailArtContract.Models.Any(m=>Path.GetFileName(asset)==m.Name+".fbx"))throw new InvalidOperationException("Unowned FBX under Northline root: "+asset);
                    if(Directory.GetFiles(Root,"*.png",SearchOption.AllDirectories).Length!=0)throw new InvalidOperationException("Northline must reference original maps, not duplicate textures");
                }
                Directory.CreateDirectory(Root);
                foreach(var model in RailArtContract.Models)
                {
                    string from=Path.Combine(report.source,"Exports",model.Name+".fbx"),to=Root+model.Name+".fbx";
                    if(!File.Exists(to)||Hash(to)!=model.SourceSha256){File.Copy(from,to,true);report.copied.Add(to);}else report.unchanged.Add(to);
                    AssetDatabase.ImportAsset(to,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
                }
                string materialPath=Root+RailArtContract.MaterialName+".mat";var owned=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if(!owned){owned=new Material(material){name=RailArtContract.MaterialName};AssetDatabase.CreateAsset(owned,materialPath);}
                else{owned.CopyPropertiesFromMaterial(material);owned.name=RailArtContract.MaterialName;EditorUtility.SetDirty(owned);}
                AssetDatabase.SaveAssetIfDirty(owned);CheckOriginals();report.status="PREPARED_IMPORT_VERIFICATION_PENDING";report.expectedNativeExit=0;
            }
            catch(Exception error){report.error=error.ToString();}
            finally
            {
                try{CheckOriginals();}catch(Exception error){report.status="FAIL";report.expectedNativeExit=1;report.error+=("\n"+error);}
                report.endedUtc=DateTime.UtcNow.ToString("O");Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,JsonUtility.ToJson(report,true));
                Debug.Log("[NorthlineArt] "+report.status+" "+output);EditorApplication.Exit(report.expectedNativeExit);
            }
        }
    }
}
