using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Parasite.Missions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace Parasite.EditorTools
{
    /// <summary>Scoped Northline-only static imports. Never changes existing gate/Harbor model or texture importers.</summary>
    public sealed class RailArtImporter : AssetPostprocessor
    {
        public const string Root = "Assets/Parasite/Resources/NorthlineRail/";
        public const string CoachRoot = Root + "Coach/", CoachTextureRoot = CoachRoot + "Textures/";
        public static string CoachTexturePath(RailArtContract.CoachTexture texture) => CoachTextureRoot + texture.Name + ".png";
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
        void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith(Root,StringComparison.Ordinal))return;
            var definition=RailArtContract.CoachTextures.SingleOrDefault(t=>CoachTexturePath(t)==assetPath);
            if(definition==null)throw new InvalidOperationException("Unowned Northline texture: "+assetPath);
            var texture=(TextureImporter)assetImporter;
            texture.textureType=definition.NormalMap?TextureImporterType.NormalMap:TextureImporterType.Default;
            texture.textureShape=TextureImporterShape.Texture2D;texture.sRGBTexture=definition.Srgb;texture.isReadable=false;
            texture.convertToNormalmap=false;texture.flipGreenChannel=false;texture.alphaIsTransparency=false;
            texture.alphaSource=definition.NormalMap?TextureImporterAlphaSource.None:TextureImporterAlphaSource.FromInput;
            texture.mipmapEnabled=true;texture.streamingMipmaps=false;texture.maxTextureSize=2048;texture.npotScale=TextureImporterNPOTScale.None;
            texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Trilinear;texture.anisoLevel=4;
            texture.textureCompression=TextureImporterCompression.CompressedHQ;texture.crunchedCompression=false;texture.compressionQuality=100;
            var platform=texture.GetPlatformTextureSettings("Standalone");platform.name="Standalone";platform.overridden=true;platform.maxTextureSize=2048;
            platform.format=definition.Format==TextureFormat.BC5?TextureImporterFormat.BC5:TextureImporterFormat.BC7;
            platform.textureCompression=TextureImporterCompression.CompressedHQ;platform.crunchedCompression=false;platform.compressionQuality=100;
            texture.SetPlatformTextureSettings(platform);
        }
        [Serializable] sealed class Receipt
        {
            public string status="FAIL",startedUtc,endedUtc,source,error;public int expectedNativeExit=1;
            public List<string> copied=new(),unchanged=new();public string scope="Owned eleven FBX, legacy shared material and coach-only three-map/two-material preparation. Actual import/data/graphics/physics/gameplay acceptance remains separate.";
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
        static void RejectLinks(string path)
        {
            string project=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
            for(string cursor=Path.GetFullPath(path);cursor!=null;cursor=Path.GetDirectoryName(cursor))
            {
                if((File.Exists(cursor)||Directory.Exists(cursor))&&(File.GetAttributes(cursor)&FileAttributes.ReparsePoint)!=0)
                    throw new InvalidOperationException("Owned/source paths cannot traverse links: "+cursor);
                if(cursor==project)break;
            }
        }
        [Serializable] sealed class SourceSummary
        {public int schema,distinct_triangles,new_textures,new_imported_colliders,unchanged_fbx_count,required_runtime_seat_colliders,required_runtime_seat_assemblies;public string baseline_manifest_sha256;}
        [Serializable] sealed class SeatPart {public float[] centre,size;}
        [Serializable] sealed class SeatLayout {public int schema;public SeatPart[] required_runtime_compound_seats,seat_assemblies;}
        public static string ValidateSource()
        {
            string project=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
            string canonical=Path.GetFullPath(Path.Combine(project,RailArtContract.AuthorSourceDirectory));
            string source=Path.GetFullPath(Option("-northlineArtSource",canonical));
            if(!source.StartsWith(Path.Combine(project,"Logs")+Path.DirectorySeparatorChar,StringComparison.Ordinal)&&source!=canonical)
                throw new ArgumentException("Source must be reviewed project Logs output or canonical "+RailArtContract.AuthorSourceDirectory);
            RejectLinks(source);RejectLinks(Path.Combine(source,"Exports"));RejectLinks(Path.Combine(source,"Textures"));
            string manifest=Path.Combine(source,"production-manifest.json");RejectLinks(manifest);
            if(Hash(manifest)!=RailArtContract.AuthorManifestSha256)throw new InvalidOperationException("Exact root-reviewed schema2 author manifest differs");
            var summary=JsonUtility.FromJson<SourceSummary>(File.ReadAllText(manifest));
            if(summary==null||summary.schema!=2||summary.baseline_manifest_sha256!=RailArtContract.LegacyAuthorManifestSha256||summary.distinct_triangles!=RailArtContract.DistinctTriangles
                ||summary.new_textures!=3||summary.unchanged_fbx_count!=10||summary.new_imported_colliders!=0||summary.required_runtime_seat_colliders!=32||summary.required_runtime_seat_assemblies!=8)
                throw new InvalidOperationException("Schema2/unchanged-ten/coach-only source contract differs");
            string layoutPath=Path.Combine(source,"coach-seat-layout.json");RejectLinks(layoutPath);
            if(Hash(layoutPath)!=RailArtContract.CoachSeatLayoutSha256)throw new InvalidOperationException("Pinned precise seat-part source layout differs");
            var layout=JsonUtility.FromJson<SeatLayout>(File.ReadAllText(layoutPath));
            if(layout==null||layout.schema!=2||layout.required_runtime_compound_seats==null||layout.required_runtime_compound_seats.Length!=32
                ||layout.seat_assemblies==null||layout.seat_assemblies.Length!=8||RailLayout.CoachSeatBoundsLocal.Length!=32)
                throw new InvalidOperationException("Eight authored seat assemblies require32 precise runtime parts");
            for(int i=0;i<32;i++)
            {
                var part=layout.required_runtime_compound_seats[i];
                if(part==null||part.centre==null||part.size==null||part.centre.Length!=3||part.size.Length!=3)
                    throw new InvalidOperationException("Malformed authored seat part "+i);
                var actual=RailLayout.CoachSeatBoundsLocal[i];
                var centre=new Vector3(part.centre[0],part.centre[1],part.centre[2]);var size=new Vector3(part.size[0],part.size[1],part.size[2]);
                if(!(Vector3.Distance(actual.center,centre)<.00001f)||!(Vector3.Distance(actual.size,size)<.00001f))
                    throw new InvalidOperationException("Runtime collider layout differs from authored pan/back/support part "+i);
            }
            var exports=Directory.GetFiles(Path.Combine(source,"Exports"),"*",SearchOption.TopDirectoryOnly).Where(p=>Path.GetExtension(p).Equals(".fbx",StringComparison.OrdinalIgnoreCase)).ToArray();
            if(exports.Length!=RailArtContract.Models.Count||exports.Any(p=>!RailArtContract.Models.Any(m=>Path.GetFileName(p)==m.Name+".fbx")))throw new InvalidOperationException("Exactly eleven named exports are required");
            foreach(var model in RailArtContract.Models)
            {string path=Path.Combine(source,"Exports",model.Name+".fbx");RejectLinks(path);if(Hash(path)!=model.SourceSha256)throw new InvalidOperationException("Source export hash differs: "+model.Name);}
            var textures=Directory.GetFiles(Path.Combine(source,"Textures"),"*",SearchOption.TopDirectoryOnly).Where(p=>Path.GetExtension(p).Equals(".png",StringComparison.OrdinalIgnoreCase)).ToArray();
            if(textures.Length!=3||textures.Any(p=>!RailArtContract.CoachTextures.Any(t=>Path.GetFileName(p)==t.Name+".png")))throw new InvalidOperationException("Exactly three named coach source maps required");
            foreach(var texture in RailArtContract.CoachTextures)
            {string path=Path.Combine(source,"Textures",texture.Name+".png");RejectLinks(path);if(new FileInfo(path).Length!=texture.Bytes||Hash(path)!=texture.SourceSha256)throw new InvalidOperationException("Coach source texture hash/size differs: "+texture.Name);}
            return source;
        }
        public static void CheckOriginals()
        {
            foreach(var pin in OriginalGateFiles)if(!File.Exists(pin.Path)||new FileInfo(pin.Path).Length!=pin.Bytes||Hash(pin.Path)!=pin.Sha)throw new InvalidOperationException("Original gate resource changed: "+pin.Path);
        }
        static void CheckOwnedPaths()
        {
            RejectLinks(Root);
            if(!Directory.Exists(Root))return;
            foreach(string path in Directory.GetFileSystemEntries(Root,"*",SearchOption.AllDirectories))RejectLinks(path);
            foreach(string path in Directory.GetFiles(Root,"*.fbx",SearchOption.AllDirectories))
                if(!RailArtContract.Models.Any(m=>path.Replace('\\','/')==Root+m.Name+".fbx"))throw new InvalidOperationException("Unowned/duplicate Northline FBX: "+path);
            foreach(string path in Directory.GetFiles(Root,"*.png",SearchOption.AllDirectories))
                if(!RailArtContract.CoachTextures.Any(t=>path.Replace('\\','/')==CoachTexturePath(t)))throw new InvalidOperationException("Unowned Northline PNG: "+path);
        }
        static void CopyOwned(string from,string to,string expected,Receipt report)
        {
            RejectLinks(from);RejectLinks(to);
            if(Hash(from)!=expected)throw new InvalidOperationException("Source changed before copy: "+from);
            if(!File.Exists(to)||Hash(to)!=expected){File.Copy(from,to,true);report.copied.Add(to);}else report.unchanged.Add(to);
            if(Hash(to)!=expected)throw new InvalidOperationException("Owned copy differs: "+to);
            AssetDatabase.ImportAsset(to,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
        }
        static Material CoachMaterial(string name,bool glass)
        {
            string path=CoachRoot+name+".mat";RejectLinks(path);
            var shader=Shader.Find("Universal Render Pipeline/Lit");if(!shader)throw new InvalidOperationException("Installed URP Lit shader missing");
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){if(File.Exists(path))throw new InvalidOperationException("Owned material path is not a material: "+path);material=new Material(shader){name=name};AssetDatabase.CreateAsset(material,path);}
            material.shader=shader;material.name=name;material.shaderKeywords=Array.Empty<string>();
            foreach(string property in material.GetTexturePropertyNames())
            {material.SetTexture(property,null);material.SetTextureScale(property,Vector2.one);material.SetTextureOffset(property,Vector2.zero);}
            material.SetFloat("_WorkflowMode",1);material.SetFloat("_Surface",glass?1:0);material.SetFloat("_Blend",0);material.SetFloat("_BlendModePreserveSpecular",1);
            material.SetFloat("_AlphaClip",0);material.SetFloat("_AlphaToMask",0);material.SetFloat("_Cull",(float)CullMode.Back);material.SetFloat("_QueueOffset",0);
            material.SetFloat("_SrcBlend",(float)BlendMode.One);material.SetFloat("_DstBlend",glass?(float)BlendMode.OneMinusSrcAlpha:(float)BlendMode.Zero);
            material.SetFloat("_SrcBlendAlpha",(float)BlendMode.One);material.SetFloat("_DstBlendAlpha",glass?(float)BlendMode.OneMinusSrcAlpha:(float)BlendMode.Zero);material.SetFloat("_ZWrite",glass?0:1);
            material.SetFloat("_ClearCoatMask",0);material.SetFloat("_ReceiveShadows",1);material.SetFloat("_SpecularHighlights",1);material.SetFloat("_EnvironmentReflections",1);
            material.SetFloat("_Metallic",glass?0:1);material.SetFloat("_Smoothness",glass?.92f:1);material.SetFloat("_BumpScale",1);material.SetFloat("_SmoothnessTextureChannel",0);
            material.SetColor("_BaseColor",glass?new Color(.87f,.94f,.96f,.18f):Color.white);material.SetColor("_EmissionColor",Color.black);material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            material.SetOverrideTag("RenderType",glass?"Transparent":"Opaque");material.renderQueue=(int)(glass?RenderQueue.Transparent:RenderQueue.Geometry);
            material.SetShaderPassEnabled("ShadowCaster",!glass);material.SetShaderPassEnabled("DepthOnly",!glass);
            // Installed URP17.3 Alpha+preserve-specular multiplies diffuse in shader. Named Premultiply expects pre-multiplied sourceRGB.
            if(glass){material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.EnableKeyword("_ALPHAPREMULTIPLY_ON");}
            else
            {
                foreach(var texture in RailArtContract.CoachTextures)
                {var actual=AssetDatabase.LoadAssetAtPath<Texture2D>(CoachTexturePath(texture));if(!actual)throw new InvalidOperationException("Coach map not imported: "+texture.Name);material.SetTexture(texture.Property,actual);}
                material.EnableKeyword("_NORMALMAP");material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);return material;
        }
        public static void PrepareBatch()
        {
            string output=FreshLogPath("-northlineArtPrepareOut","northline-art-prepare");var report=new Receipt{startedUtc=DateTime.UtcNow.ToString("O")};
            try
            {
                if(!Application.isBatchMode||Application.isPlaying)throw new InvalidOperationException("Run explicitly in batch EditMode, outside play");
                CheckOriginals();report.source=ValidateSource();CheckOwnedPaths();
                var material=AssetDatabase.LoadAssetAtPath<Material>(OriginalMaterial);if(!material)throw new InvalidOperationException("Original gate PBR material is not imported");
                Directory.CreateDirectory(Root);Directory.CreateDirectory(CoachTextureRoot);
                foreach(var texture in RailArtContract.CoachTextures)CopyOwned(Path.Combine(report.source,"Textures",texture.Name+".png"),CoachTexturePath(texture),texture.SourceSha256,report);
                string materialPath=Root+RailArtContract.MaterialName+".mat";var owned=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if(!owned){owned=new Material(material){name=RailArtContract.MaterialName};AssetDatabase.CreateAsset(owned,materialPath);}
                else{owned.CopyPropertiesFromMaterial(material);owned.name=RailArtContract.MaterialName;EditorUtility.SetDirty(owned);}
                AssetDatabase.SaveAssetIfDirty(owned);
                CoachMaterial(RailArtContract.CoachOpaqueName,false);CoachMaterial(RailArtContract.CoachGlassName,true);
                foreach(var model in RailArtContract.Models)CopyOwned(Path.Combine(report.source,"Exports",model.Name+".fbx"),Root+model.Name+".fbx",model.SourceSha256,report);
                CheckOwnedPaths();CheckOriginals();report.status="PREPARED_IMPORT_VERIFICATION_PENDING";report.expectedNativeExit=0;
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
