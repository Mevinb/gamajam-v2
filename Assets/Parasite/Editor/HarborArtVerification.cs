using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Parasite.EditorTools
{
    /// <summary>Read-only Null import receipts. Does not access CPU mesh arrays, reimport assets, bake or change scenes.</summary>
    public static class HarborArtVerification
    {
        const string ManifestHash = "cb923b5a6b79e29ecb84ad5badd70c1f7ad2dca381b68f7ec3c63e97bda4ebf5";
        const string ContractHash = "467df71ac4f4f2a58a61b25a158be81b476bde5968757b65040d40bec78c957b";
        const string BaselineCommit = "19bcdba68b41fb7601606430f5bb16f464447547";
        const string Root = "Assets/Parasite/Resources/Harbor/", GateRoot = "Assets/Parasite/Art/CityGatePilot/";
        static readonly string[] Names = { "HarborValveBase", "HarborValveWheel", "HarborGaugeBase", "HarborGaugeNeedle", "HarborLiftGate", "HarborConveyor", "HarborSealedCrate", "HarborRelayPad", "HarborEmitter" };
        static readonly int[] Counts = { 848,456,1008,132,1076,3020,728,828,232 };
        [Serializable] sealed class FilePin { public string path, sha256; public long bytes; }
        [Serializable] sealed class ModelPin { public string name, sourcePath, assetPath, sha256; public int triangles; public Vector3 min,max; }
        [Serializable] sealed class TexturePin { public string suffix, property, path, guid; public bool normal,sRGB; }
        [Serializable] sealed class Contract
        {
            public int schema, distinctTriangles; public string scope, sourceManifestPath, sourceManifestSha256, producerSha256, baselineCommit;
            public ModelPin[] models; public FilePin[] unchangedGateFiles, sourceAtlasFiles; public TexturePin[] textures;
        }
        [Serializable] sealed class Check { public string name,detail; public bool passed; }
        [Serializable] sealed class FileReceipt { public string phase,path,expectedSha256,actualSha256; public long bytes; public bool matches; }
        [Serializable] sealed class ModelReceipt
        {
            public string assetPath,sourcePath,sourceSha256,assetSha256,filterName,meshName,rendererMaterial;
            public int filters,renderers,submeshes,vertices,colliders,rigidbodies,animations,animators,cameras,lights,animationClips; public long triangles;
            public string importerAnimationType; public string[] importedComponentTypes, importedComponentHierarchy;
            public bool readable,normals,tangents,uv0,noImportedBehavior,policyMatches;
            public Vector3 meshBoundsMin,meshBoundsMax,transformedMin,transformedMax,expectedMin,expectedMax;
            public Matrix4x4 importedHierarchyMatrix; public float boundsError;
        }
        [Serializable] sealed class TextureReceipt
        {
            public string path,guid,property,actualType,format,graphicsFormat,normalType,platformFormat;
            public int width,height,mips,platformMaxSize; public bool sharedSameObject,sRGB,actualSRGB,normal,platformOverride;
        }
        [Serializable] sealed class Report
        {
            public int schema=2,expectedNativeExit=1; public string result="FAIL",phase="Null Editor import metadata and source-integrity verification",startedUtc,endedUtc,device,api,error;
            public string scope="Actual imported asset metadata, transformed bounds, shared texture references and immutable source hashes. No rendering, pixel, navigation or gameplay acceptance is inferred.";
            public string contractPath,contractSha256,sourceManifestPath,sourceManifestSha256,baselineCommit,verifierSourceSha256,sceneBefore,sceneAfter;
            public int expectedModels=9,distinctTriangles=8328; public string output;
            public List<Check> checks=new();public List<ModelReceipt> models=new();public List<TextureReceipt> textures=new();public List<FileReceipt> files=new();
        }
        static Report report; static bool failed;
        static void Require(bool condition,string name,string detail="")
        {
            report.checks.Add(new Check { name=name,detail=detail,passed=condition });
            if(!condition){failed=true;throw new InvalidOperationException(name+": "+detail);}
        }
        static void Run(string phase,Action body)
        {
            try { body(); }
            catch(Exception error)
            {
                failed=true;report.checks.Add(new Check { name=phase+" exception",passed=false,detail=error.ToString() });
            }
        }
        static string Hash(string path)
        {
            using var stream=File.OpenRead(path);using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
        }
        static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
        static bool Finite(Vector3 v)=>Finite(v.x)&&Finite(v.y)&&Finite(v.z);
        static bool Finite(Matrix4x4 matrix){for(int i=0;i<16;i++)if(!Finite(matrix[i]))return false;return true;}
        static string SceneSignature()
        {
            var scenes=new List<string>();for(int i=0;i<SceneManager.sceneCount;i++){var s=SceneManager.GetSceneAt(i);scenes.Add(s.handle+":"+s.path+":"+s.isLoaded+":"+s.isDirty);}
            return SceneManager.GetActiveScene().handle+"|"+string.Join("|",scenes);
        }
        public static void VerifyBatch()
        {
            failed=false;report=new Report { startedUtc=DateTime.UtcNow.ToString("O"),device=SystemInfo.graphicsDeviceName,api=SystemInfo.graphicsDeviceType.ToString(),sceneBefore=SceneSignature() };
            string directory=Path.GetFullPath(Path.Combine("Logs/harbor-art",DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ")+"-"+Guid.NewGuid().ToString("N").Substring(0,8)));
            report.output=Path.Combine(directory,"import-verification.json");Contract contract=null;
            try
            {
                Directory.CreateDirectory(directory);
                string source="Assets/Parasite/Editor/HarborArtVerification.cs";report.verifierSourceSha256=File.Exists(source)?Hash(source):"source-file-not-present";
                Require(Application.isBatchMode&&!Application.isPlaying&&SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null,"Actual Null Editor batch",report.api+" / batch="+Application.isBatchMode);
                var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-harborArtContract");
                report.contractPath=Path.GetFullPath(at>=0&&at+1<args.Length?args[at+1]:"Logs/harbor-art/import-contract.json");
                report.contractSha256=Hash(report.contractPath);Require(report.contractSha256==ContractHash,"Exact canonical contract bytes",report.contractSha256);
                contract=JsonUtility.FromJson<Contract>(File.ReadAllText(report.contractPath));
                Require(contract!=null&&contract.schema==1&&contract.baselineCommit==BaselineCommit&&contract.distinctTriangles==8328,"Canonical contract schema and git baseline");
                report.baselineCommit=contract.baselineCommit;report.sourceManifestPath=contract.sourceManifestPath;report.sourceManifestSha256=Hash(contract.sourceManifestPath);
                Require(contract.sourceManifestPath=="ArtSource/Harbor/production-manifest.json"&&contract.sourceManifestSha256==ManifestHash&&report.sourceManifestSha256==ManifestHash,"Frozen production manifest source SHA",report.sourceManifestSha256);
                Require(contract.models!=null&&contract.models.Length==9&&contract.unchangedGateFiles?.Length==8&&contract.sourceAtlasFiles?.Length==4&&contract.textures?.Length==3,"Exact contract inventory");
                Require(contract.models.Select(x=>x.name).OrderBy(x=>x).SequenceEqual(Names.OrderBy(x=>x)),"Exact nine semantic model names");
                foreach(var row in contract.models)
                {
                    int index=Array.IndexOf(Names,row.name);
                    Require(index>=0&&row.triangles==Counts[index]&&row.sourcePath=="ArtSource/Harbor/Exports/"+row.name+".fbx"&&row.assetPath==Root+row.name+".fbx"&&Finite(row.min)&&Finite(row.max),"Exact model source/count contract "+row.name);
                }
                foreach(var pin in contract.unchangedGateFiles)Run("Before baseline "+pin.path,()=>CheckFile(pin,"before"));
                foreach(var pin in contract.sourceAtlasFiles)Run("Original authored atlas "+pin.path,()=>CheckFile(pin,"before"));
                Require(Directory.GetFiles(Root,"*.fbx",SearchOption.AllDirectories).Length==9,"Exactly nine owned runtime FBX files");
                Require(Directory.GetFiles(Root,"*.png",SearchOption.AllDirectories).Length==0,"No duplicated Harbor runtime PBR PNG files");
                foreach(var row in contract.models)Run("Imported model "+row.name,()=>VerifyModel(row));
                Run("Shared material and original maps",()=>VerifyMaterial(contract));
            }
            catch(Exception error){failed=true;report.error=error.ToString();}
            finally
            {
                if(contract!=null)
                {
                    foreach(var pin in contract.unchangedGateFiles??Array.Empty<FilePin>())Run("After baseline "+pin.path,()=>CheckFile(pin,"after"));
                    foreach(var pin in contract.sourceAtlasFiles??Array.Empty<FilePin>())Run("After original atlas "+pin.path,()=>CheckFile(pin,"after"));
                }
                report.sceneAfter=SceneSignature();Run("Scene preservation",()=>Require(report.sceneBefore==report.sceneAfter,"Loaded scenes and dirty flags unchanged",report.sceneBefore+" -> "+report.sceneAfter));
                report.endedUtc=DateTime.UtcNow.ToString("O");report.result=failed?"FAIL":"PASS";report.expectedNativeExit=failed?1:0;
                try
                {
                    Directory.CreateDirectory(directory);string temporary=report.output+".tmp";File.WriteAllText(temporary,JsonUtility.ToJson(report,true));File.Move(temporary,report.output);
                    Debug.Log("[HarborArtVerification] "+report.result+" "+report.models.Count+" actual imported model receipts; "+report.output);
                }
                catch(Exception error){failed=true;Debug.LogError("[HarborArtVerification] Report publication failed: "+error);}
                EditorApplication.Exit(failed?1:0);
            }
        }
        static void CheckFile(FilePin pin,string phase)
        {
            var row=new FileReceipt { phase=phase,path=pin.path,expectedSha256=pin.sha256 };report.files.Add(row);
            row.actualSha256=Hash(pin.path);row.bytes=new FileInfo(pin.path).Length;row.matches=row.actualSha256==pin.sha256&&row.bytes==pin.bytes;
            Require(row.matches,"Original file unchanged "+phase+" "+pin.path,row.actualSha256);
        }
        static void VerifyModel(ModelPin pin)
        {
            var row=new ModelReceipt { assetPath=pin.assetPath,sourcePath=pin.sourcePath,expectedMin=pin.min,expectedMax=pin.max };report.models.Add(row);
            row.sourceSha256=Hash(pin.sourcePath);row.assetSha256=Hash(pin.assetPath);
            Require(row.sourceSha256==pin.sha256&&row.assetSha256==pin.sha256,"FBX is byte-identical to frozen authored export "+pin.name,row.assetSha256);
            var importer=AssetImporter.GetAtPath(pin.assetPath) as ModelImporter;Require(importer,"Actual ModelImporter "+pin.name);
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(pin.assetPath);Require(model,"Actual imported GameObject "+pin.name);
            // Capture raw imported evidence BEFORE either static-policy or component guards can stop this model's checks.
            var components=model.GetComponentsInChildren<Component>(true);
            row.importedComponentTypes=components.Select(c=>c?c.GetType().FullName:"missing component").Distinct().OrderBy(x=>x,StringComparer.Ordinal).ToArray();
            row.importedComponentHierarchy=components.Select(c=>c?ComponentPath(model.transform,c.transform)+" : "+c.GetType().FullName:"missing component").OrderBy(x=>x,StringComparer.Ordinal).ToArray();
            row.colliders=model.GetComponentsInChildren<Collider>(true).Length;row.rigidbodies=model.GetComponentsInChildren<Rigidbody>(true).Length;
            row.animations=model.GetComponentsInChildren<Animation>(true).Length;row.animators=model.GetComponentsInChildren<Animator>(true).Length;
            row.cameras=model.GetComponentsInChildren<Camera>(true).Length;row.lights=model.GetComponentsInChildren<Light>(true).Length;
            row.animationClips=AssetDatabase.LoadAllAssetsAtPath(pin.assetPath).OfType<AnimationClip>().Count();
            var filters=model.GetComponentsInChildren<MeshFilter>(true);var renderers=model.GetComponentsInChildren<MeshRenderer>(true);row.filters=filters.Length;row.renderers=renderers.Length;
            row.importerAnimationType=importer.animationType.ToString();
            row.policyMatches=Mathf.Abs(importer.globalScale-1)<.000001f&&importer.useFileScale&&importer.bakeAxisConversion&&importer.preserveHierarchy&&!importer.isReadable&&!importer.addCollider&&importer.animationType==ModelImporterAnimationType.None&&!importer.importAnimation&&!importer.importCameras&&!importer.importLights&&importer.meshCompression==ModelImporterMeshCompression.Off&&importer.importNormals==ModelImporterNormals.Import&&importer.importTangents==ModelImporterTangents.CalculateMikk&&importer.materialImportMode==ModelImporterMaterialImportMode.None;
            row.noImportedBehavior=row.colliders==0&&row.rigidbodies==0&&row.animations==0&&row.animators==0&&row.cameras==0&&row.lights==0&&row.animationClips==0;
            Require(row.policyMatches,"Scoped static visual metre-scale importer policy "+pin.name,"animationType="+row.importerAnimationType+", readable="+importer.isReadable+", normals="+importer.importNormals+", tangents="+importer.importTangents);
            Require(row.noImportedBehavior,"No imported physics/animation/cameras/lights "+pin.name,JsonUtility.ToJson(row));
            Require(filters.Length==1&&renderers.Length==1,"Exactly one actual MeshFilter and MeshRenderer "+pin.name,"filters="+filters.Length+", renderers="+renderers.Length);
            var filter=filters[0];var mesh=filter.sharedMesh;Require(mesh,"Actual imported shared mesh "+pin.name);
            row.filterName=filter.name;row.meshName=mesh.name;row.readable=mesh.isReadable;
            Require(mesh.name.StartsWith(pin.name,StringComparison.Ordinal),"Semantic imported mesh prefix "+pin.name,mesh.name+" / object="+filter.name);
            row.submeshes=mesh.subMeshCount;row.vertices=mesh.vertexCount;
            Require(mesh.subMeshCount==1&&mesh.GetTopology(0)==MeshTopology.Triangles&&mesh.GetIndexCount(0)%3==0,"Single triangle submesh "+pin.name);
            row.triangles=(long)mesh.GetIndexCount(0)/3;Require(row.triangles==pin.triangles&&row.vertices>0&&!mesh.isReadable,"Exact source triangle count without CPU readability "+pin.name,row.triangles.ToString());
            row.normals=mesh.HasVertexAttribute(VertexAttribute.Normal)&&mesh.GetVertexAttributeDimension(VertexAttribute.Normal)==3;
            row.tangents=mesh.HasVertexAttribute(VertexAttribute.Tangent)&&mesh.GetVertexAttributeDimension(VertexAttribute.Tangent)==4;
            row.uv0=mesh.HasVertexAttribute(VertexAttribute.TexCoord0)&&mesh.GetVertexAttributeDimension(VertexAttribute.TexCoord0)>=2;
            Require(row.normals&&row.tangents&&row.uv0,"Actual normal/tangent/UV attributes "+pin.name);
            var materials=renderers[0].sharedMaterials;Require(materials.Length==1,"Single imported renderer slot "+pin.name);row.rendererMaterial=materials[0]?materials[0].name:"unassigned (runtime shared PBR binding)";
            var matrix=model.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;row.importedHierarchyMatrix=matrix;
            Require(Finite(matrix)&&matrix.determinant>0,"Finite positive imported hierarchy transform "+pin.name);
            var bounds=mesh.bounds;row.meshBoundsMin=bounds.min;row.meshBoundsMax=bounds.max;Require(Finite(bounds.center)&&Finite(bounds.extents)&&bounds.extents.x>0&&bounds.extents.y>0&&bounds.extents.z>0,"Finite nonzero imported mesh bounds "+pin.name);
            Bounds transformed=default;bool first=true;
            for(int i=0;i<8;i++)
            {
                var corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                var point=matrix.MultiplyPoint3x4(corner);Require(Finite(point),"Finite transformed bounds corner "+pin.name+" "+i);
                if(first){transformed=new Bounds(point,Vector3.zero);first=false;}else transformed.Encapsulate(point);
            }
            row.transformedMin=transformed.min;row.transformedMax=transformed.max;row.boundsError=Mathf.Max(Vector3.Distance(transformed.min,pin.min),Vector3.Distance(transformed.max,pin.max));
            Require(row.boundsError<=.001f,"Actual hierarchy-transformed bounds match frozen source "+pin.name,JsonUtility.ToJson(row));
        }
        static string ComponentPath(Transform root,Transform current)
        {
            var path=new List<string>();while(current){path.Add(current.name);if(current==root)break;current=current.parent;}path.Reverse();return string.Join("/",path);
        }
        static void VerifyMaterial(Contract contract)
        {
            var gate=AssetDatabase.LoadAssetAtPath<Material>(GateRoot+"CityGatePilot_SharedPBR.mat");var harbor=AssetDatabase.LoadAssetAtPath<Material>(Root+"HarborSharedPBR.mat");
            Require(gate&&harbor&&gate!=harbor,"Owned Harbor material is a distinct asset referencing the gate maps");
            Require(gate.shader&&harbor.shader&&gate.shader==harbor.shader&&harbor.shader.name=="Universal Render Pipeline/Lit","Actual shared URP Lit shader");
            Require(harbor.IsKeywordEnabled("_NORMALMAP")&&harbor.IsKeywordEnabled("_METALLICSPECGLOSSMAP")&&!harbor.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT")&&harbor.GetFloat("_Surface")==0&&harbor.GetFloat("_Metallic")==1&&harbor.GetFloat("_Smoothness")==1,"Opaque packed PBR material policy");
            foreach(var pin in contract.textures)Run("Imported texture "+pin.suffix,()=>
            {
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(pin.path);var policy=AssetImporter.GetAtPath(pin.path) as TextureImporter;
                Require(texture&&policy,"Actual original Texture2D and importer "+pin.suffix);
                var platform=policy.GetPlatformTextureSettings("Standalone");string guid=AssetDatabase.AssetPathToGUID(pin.path);
                var row=new TextureReceipt { path=pin.path,guid=guid,property=pin.property,actualType=texture.GetType().FullName,width=texture.width,height=texture.height,mips=texture.mipmapCount,format=texture.format.ToString(),graphicsFormat=texture.graphicsFormat.ToString(),normalType=policy.textureType.ToString(),sRGB=policy.sRGBTexture,actualSRGB=GraphicsFormatUtility.IsSRGBFormat(texture.graphicsFormat),normal=policy.textureType==TextureImporterType.NormalMap,platformOverride=platform.overridden,platformFormat=platform.format.ToString(),platformMaxSize=platform.maxTextureSize,sharedSameObject=gate.GetTexture(pin.property)==texture&&harbor.GetTexture(pin.property)==texture };report.textures.Add(row);
                Require(row.sharedSameObject&&guid==pin.guid&&AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(harbor.GetTexture(pin.property)))==pin.guid,"Same actual texture object and original GUID in both materials "+pin.suffix,guid);
                Require(texture.GetType()==typeof(Texture2D)&&texture.dimension==TextureDimension.Tex2D&&texture.width==2048&&texture.height==2048&&texture.mipmapCount>1,"Actual original 2K mipmapped Texture2D "+pin.suffix);
                Require(policy.textureShape==TextureImporterShape.Texture2D&&policy.textureType==(pin.normal?TextureImporterType.NormalMap:TextureImporterType.Default)&&policy.sRGBTexture==pin.sRGB&&row.actualSRGB==pin.sRGB&&policy.mipmapEnabled&&policy.maxTextureSize==2048,"Actual original normal/sRGB/2D import policy "+pin.suffix,JsonUtility.ToJson(row));
                Require(platform.overridden&&platform.maxTextureSize==2048&&platform.format==TextureImporterFormat.BC7&&texture.format==TextureFormat.BC7,"Actual BC7 texture and Standalone policy "+pin.suffix,JsonUtility.ToJson(row));
            });
        }
    }
}
