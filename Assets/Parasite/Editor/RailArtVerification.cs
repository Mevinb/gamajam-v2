using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Parasite.Missions;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Parasite.EditorTools
{
    /// <summary>Actual import/data verification recipe. Read-only mesh snapshots preserve the non-readable runtime policy.</summary>
    public static class RailArtVerification
    {
        [Serializable] sealed class Check { public string name,detail;public bool passed; }
        [Serializable] sealed class ModelReceipt
        {
            public string name,sourceSha256,assetSha256,mesh,animationType;public int vertices,submeshes,triangles,uvValues,degenerateTriangles,degenerateUv;
            public bool readable,policy;public string[] componentTypes;public Vector3 min,max;public float boundsError;
            public float texelsPerMetreMin,texelsPerMetreMedian,texelsPerMetreMax;
        }
        [Serializable] sealed class TextureReceipt
        {
            public string property,path,guid,format,graphicsFormat;public int width,height,mips;public bool sameObject,sRGB,normal,standaloneBC7;
        }
        [Serializable] sealed class Report
        {
            public int schema=1,expectedNativeExit=1;public string status="FAIL",startedUtc,endedUtc,error,sourceDirectory,sourceManifestSha256,sceneBefore,sceneAfter,device,api;
            public List<Check> checks=new();public List<ModelReceipt> models=new();public List<TextureReceipt> textures=new();
            public string scope="Actual imported metadata, source identities, CPU read-only positions/normals/tangents/UV/index values, shared original texture references and scene preservation only. No runtime visual binding, pixels, art quality, physics/nav, gameplay or performance acceptance.";
            public string artLimit="Low-poly shared-atlas geometry is a foundation. Hero detail, per-surface texel density, material/weathering refinement and final RTX image/performance review remain pending.";
        }
        static Report report;static bool failed;
        static void Require(bool ok,string name,string detail="")
        {report.checks.Add(new Check{name=name,detail=detail,passed=ok});if(!ok)throw new InvalidOperationException(name+": "+detail);}
        static void Run(string name,Action action)
        {try{action();}catch(Exception error){failed=true;report.checks.Add(new Check{name=name,detail=error.ToString(),passed=false});}}
        static string SceneState()
        {var rows=new List<string>();for(int i=0;i<SceneManager.sceneCount;i++){var scene=SceneManager.GetSceneAt(i);rows.Add(scene.handle+":"+scene.path+":"+scene.isLoaded+":"+scene.isDirty);}return SceneManager.GetActiveScene().handle+"|"+string.Join("|",rows);}
        public static void VerifyImportBatch()
        {
            string output=RailArtImporter.FreshLogPath("-northlineArtReport","northline-art-import");failed=false;
            report=new Report{startedUtc=DateTime.UtcNow.ToString("O"),sceneBefore=SceneState(),device=SystemInfo.graphicsDeviceName,api=SystemInfo.graphicsDeviceType.ToString()};
            try
            {
                Require(Application.isBatchMode&&!Application.isPlaying,"Actual Editor batch outside play");
                RailArtImporter.CheckOriginals();report.sourceDirectory=RailArtImporter.ValidateSource();report.sourceManifestSha256=RailArtImporter.Hash(Path.Combine(report.sourceDirectory,"production-manifest.json"));
                Require(report.sourceManifestSha256==RailArtContract.AuthorManifestSha256,"Exact reviewed eleven-model author manifest");
                Require(Directory.GetFiles(RailArtImporter.Root,"*.fbx",SearchOption.AllDirectories).Length==11,"Exactly eleven owned runtime FBX files");
                Require(Directory.GetFiles(RailArtImporter.Root,"*.png",SearchOption.AllDirectories).Length==0,"No duplicated runtime atlas PNGs");
                foreach(var model in RailArtContract.Models)Run(model.Name,()=>VerifyModel(model));
                Run("Shared original material/maps",VerifyMaterial);
            }
            catch(Exception error){failed=true;report.error=error.ToString();}
            finally
            {
                Run("Immutable existing gate files after verification",RailArtImporter.CheckOriginals);report.sceneAfter=SceneState();
                Run("Scene preservation",()=>Require(report.sceneBefore==report.sceneAfter,"Loaded scenes/active scene/dirty flags unchanged"));
                report.status=failed?"FAIL":"IMPORTED_STRUCTURE_DATA_PASS_GRAPHICS_PENDING";report.expectedNativeExit=failed?1:0;report.endedUtc=DateTime.UtcNow.ToString("O");
                Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,JsonUtility.ToJson(report,true));Debug.Log("[NorthlineArtVerification] "+report.status+" "+output);EditorApplication.Exit(report.expectedNativeExit);
            }
        }
        static void VerifyModel(RailArtContract.Model definition)
        {
            string asset=RailArtImporter.Root+definition.Name+".fbx";
            var row=new ModelReceipt{name=definition.Name,sourceSha256=RailArtImporter.Hash(Path.Combine(report.sourceDirectory,"Exports",definition.Name+".fbx")),assetSha256=RailArtImporter.Hash(asset)};report.models.Add(row);
            Require(row.sourceSha256==definition.SourceSha256&&row.assetSha256==definition.SourceSha256,"Actual owned FBX equals frozen export "+definition.Name);
            var importer=AssetImporter.GetAtPath(asset) as ModelImporter;var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(asset);
            Require(importer&&prefab,"Actual ModelImporter/prefab "+definition.Name);
            row.animationType=importer.animationType.ToString();row.componentTypes=prefab.GetComponentsInChildren<Component>(true).Select(c=>c?c.GetType().FullName:"missing").Distinct().OrderBy(x=>x).ToArray();
            row.policy=importer.globalScale==1&&importer.useFileScale&&importer.bakeAxisConversion&&importer.preserveHierarchy&&!importer.isReadable&&!importer.addCollider&&importer.animationType==ModelImporterAnimationType.None
                &&!importer.importAnimation&&!importer.importCameras&&!importer.importLights&&importer.meshCompression==ModelImporterMeshCompression.Off&&importer.importNormals==ModelImporterNormals.Import
                &&importer.importTangents==ModelImporterTangents.CalculateMikk&&importer.materialImportMode==ModelImporterMaterialImportMode.None;
            Require(row.policy,"Scoped static metre-scale/unreadable/None-animation policy "+definition.Name,row.animationType);
            Require(AssetDatabase.LoadAllAssetsAtPath(asset).OfType<AnimationClip>().Count()==0,"No imported animation clips "+definition.Name);
            Require(RailAuthoredArt.ModelContract(prefab,definition,out string reason),"Complete runtime structural contract "+definition.Name,reason);
            var filter=prefab.GetComponentInChildren<MeshFilter>(true);var mesh=filter.sharedMesh;row.mesh=mesh.name;row.vertices=mesh.vertexCount;row.submeshes=mesh.subMeshCount;row.triangles=(int)(mesh.GetIndexCount(0)/3);row.readable=mesh.isReadable;
            var matrix=prefab.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;var actual=RailAuthoredArt.TransformBounds(mesh.bounds,matrix);row.min=actual.min;row.max=actual.max;
            row.boundsError=Mathf.Max(Vector3.Distance(row.min,definition.Min),Vector3.Distance(row.max,definition.Max));Require(row.boundsError<=.001f,"Actual transformed metre bounds "+definition.Name,row.boundsError.ToString("R"));
            // Unity's Editor-only API reads the imported snapshot without setting importer.isReadable or mutating the mesh.
            using var snapshots=MeshUtility.AcquireReadOnlyMeshData(mesh);var data=snapshots[0];
            using var positions=new NativeArray<Vector3>(data.vertexCount,Allocator.TempJob);using var normals=new NativeArray<Vector3>(data.vertexCount,Allocator.TempJob);
            using var tangents=new NativeArray<Vector4>(data.vertexCount,Allocator.TempJob);using var uv=new NativeArray<Vector2>(data.vertexCount,Allocator.TempJob);
            using var indices=new NativeArray<int>((int)mesh.GetIndexCount(0),Allocator.TempJob);
            data.GetVertices(positions);data.GetNormals(normals);data.GetTangents(tangents);data.GetUVs(0,uv);data.GetIndices(indices,0);
            row.uvValues=uv.Length;var densities=new List<float>();Bounds measured=default;
            for(int i=0;i<data.vertexCount;i++)
            {
                Require(Finite(positions[i])&&Finite(normals[i])&&normals[i].sqrMagnitude>.01f&&Finite(tangents[i])&&Mathf.Abs(tangents[i].w)>.5f,"Finite actual vertex/normal/tangent "+definition.Name+" #"+i);
                var t=uv[i];Require(Finite(t.x)&&Finite(t.y)&&t.x>=0&&t.x<=1&&t.y>=0&&t.y<=1,"Actual atlas UV in0..1 "+definition.Name+" #"+i);
                Vector3 point=matrix.MultiplyPoint3x4(positions[i]);if(i==0)measured=new Bounds(point,Vector3.zero);else measured.Encapsulate(point);
            }
            Require(Vector3.Distance(measured.min,definition.Min)<=.001f&&Vector3.Distance(measured.max,definition.Max)<=.001f,"Read-only actual vertex bounds match frozen source "+definition.Name);
            for(int i=0;i<indices.Length;i+=3)
            {
                int a=indices[i],b=indices[i+1],c=indices[i+2];Require(a>=0&&b>=0&&c>=0&&a<data.vertexCount&&b<data.vertexCount&&c<data.vertexCount,"Valid actual triangle indices "+definition.Name+" #"+(i/3));
                float area=Vector3.Cross(matrix.MultiplyVector(positions[b]-positions[a]),matrix.MultiplyVector(positions[c]-positions[a])).magnitude*.5f;
                Vector2 ab=uv[b]-uv[a],ac=uv[c]-uv[a];float uvArea=Mathf.Abs(ab.x*ac.y-ab.y*ac.x)*.5f;
                if(area<=0)row.degenerateTriangles++;if(uvArea<=0)row.degenerateUv++;if(area>0&&uvArea>0)densities.Add(Mathf.Sqrt(uvArea*2048f*2048f/area));
            }
            Require(row.degenerateTriangles==0,"No actual zero-area imported triangles "+definition.Name,row.degenerateTriangles.ToString());
            Require(row.degenerateUv==0,"No actual zero-area UV triangles "+definition.Name,row.degenerateUv.ToString());
            if(densities.Count>0){densities.Sort();row.texelsPerMetreMin=densities[0];row.texelsPerMetreMedian=densities[densities.Count/2];row.texelsPerMetreMax=densities[densities.Count-1];}
            Require(!mesh.isReadable,"Runtime readability policy unchanged after CPU snapshot "+definition.Name);
        }
        static void VerifyMaterial()
        {
            var original=AssetDatabase.LoadAssetAtPath<Material>(RailArtImporter.OriginalMaterial);var material=AssetDatabase.LoadAssetAtPath<Material>(RailArtImporter.Root+RailArtContract.MaterialName+".mat");
            Require(original&&material&&original!=material,"Owned material is distinct and original gate asset remains referenced");
            Require(RailAuthoredArt.MaterialContract(material,out string reason),"Actual complete opaque packed PBR material",reason);
            var properties=new[]{"_BaseMap","_BumpMap","_MetallicGlossMap"};var suffixes=new[]{"A","N","M"};
            for(int i=0;i<properties.Length;i++)
            {
                string path="Assets/Parasite/Art/CityGatePilot/Textures/CityGatePilot_"+suffixes[i]+".png";var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                Require(texture&&importer,"Actual original imported texture "+suffixes[i]);var platform=importer.GetPlatformTextureSettings("Standalone");
                var row=new TextureReceipt{property=properties[i],path=path,guid=AssetDatabase.AssetPathToGUID(path),format=texture.format.ToString(),graphicsFormat=texture.graphicsFormat.ToString(),width=texture.width,height=texture.height,mips=texture.mipmapCount,
                    sameObject=material.GetTexture(properties[i])==texture&&original.GetTexture(properties[i])==texture,sRGB=importer.sRGBTexture,normal=importer.textureType==TextureImporterType.NormalMap,standaloneBC7=platform.overridden&&platform.format==TextureImporterFormat.BC7&&platform.maxTextureSize==2048};report.textures.Add(row);
                Require(row.sameObject&&row.width==2048&&row.height==2048&&row.mips>1&&row.standaloneBC7&&texture.format==TextureFormat.BC7,"Same original2K/mipmapped/BC7 texture object "+suffixes[i]);
                Require(importer.textureShape==TextureImporterShape.Texture2D&&importer.mipmapEnabled&&row.normal==(i==1)&&row.sRGB==(i==0)&&GraphicsFormatUtility.IsSRGBFormat(texture.graphicsFormat)==(i==0),"Original texture shape/type/colour-space policy "+suffixes[i]);
                Require(material.GetTextureScale(properties[i])==original.GetTextureScale(properties[i])&&material.GetTextureOffset(properties[i])==original.GetTextureOffset(properties[i]),"Original atlas scale/offset preserved "+suffixes[i]);
            }
        }
        static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
        static bool Finite(Vector3 p)=>Finite(p.x)&&Finite(p.y)&&Finite(p.z);
        static bool Finite(Vector4 p)=>Finite(p.x)&&Finite(p.y)&&Finite(p.z)&&Finite(p.w);
    }
}
