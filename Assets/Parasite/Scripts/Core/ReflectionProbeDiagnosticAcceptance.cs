using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Parasite.Testing
{
    /// <summary>Bounded native probe experiment. Never changes river policy or treats elapsed time as completion.</summary>
    public static class ReflectionProbeDiagnosticAcceptance
    {
        const int Layer = 30, Resolution = 128;
        const float DeadlineSeconds = 8;
        static readonly Vector3 Origin = new(0,-250,0);
        static readonly Vector3[] Directions = {Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
        static readonly Color[] Colors = {Color.red,Color.green,Color.blue,Color.yellow,Color.magenta,Color.cyan};
        [Serializable] public sealed class FaceResult {public string face;public int mip;public Color center;public bool valid;}
        [Serializable] public sealed class CaseResult
        {
            public string name,slicing,target,status,failure,textureKind;public Vector4 hdrDecode;
            public bool qualityFlag,completed,imageValid;
            public int renderId,renderedFrames,textureUpdateCount,mipCount;
            public float seconds;
            public FaceResult[] faces;
        }
        [Serializable] sealed class Report {public string device,api;public int graphicsPreset;public bool originalQualityFlag;public CaseResult[] cases;}

        public static IEnumerator Run(string outputDirectory)
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new InvalidOperationException("Probe diagnostics require an actual GPU.");
            yield return AutoTest.LoadCity();
            bool original=QualitySettings.realtimeReflectionProbes;
            var root=new GameObject("Owned probe diagnostic markers");
            var materials=new List<Material>();var results=new List<CaseResult>();
            try
            {
                Directory.CreateDirectory(outputDirectory);
                var shader=Shader.Find("Universal Render Pipeline/Unlit");
                if(!shader)throw new InvalidOperationException("URP Unlit proof shader is missing.");
                for(int i=0;i<Directions.Length;i++)
                {
                    var item=GameObject.CreatePrimitive(PrimitiveType.Quad);item.name="Probe face "+(CubemapFace)i;
                    item.layer=Layer;item.transform.SetParent(root.transform,false);
                    item.transform.position=Origin+Directions[i]*2;
                    item.transform.rotation=Quaternion.LookRotation(Directions[i],Mathf.Abs(Directions[i].y)>.9f?Vector3.forward:Vector3.up);
                    item.transform.localScale=Vector3.one*4.1f;
                    var collider=item.GetComponent<Collider>();collider.enabled=false;Object.Destroy(collider);
                    var material=new Material(shader);material.SetColor("_BaseColor",Colors[i]);material.SetInt("_Cull",0);
                    item.GetComponent<Renderer>().sharedMaterial=material;materials.Add(material);
                }
                // Keep the probe volume visible to a real game camera, separating visibility from quality/target behavior.
                var observer=new GameObject("Probe diagnostic observer");observer.transform.SetParent(root.transform,false);
                observer.transform.SetPositionAndRotation(Origin,Quaternion.identity);
                var camera=observer.AddComponent<Camera>();camera.cullingMask=1<<Layer;camera.nearClipPlane=.1f;camera.farClipPlane=5;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.depth=100;
                camera.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
                yield return null;
                yield return Case(root.transform,results,"off-individual-external",false,ReflectionProbeTimeSlicingMode.IndividualFaces,true,outputDirectory);
                yield return Case(root.transform,results,"on-individual-external",true,ReflectionProbeTimeSlicingMode.IndividualFaces,true,outputDirectory);
                yield return Case(root.transform,results,"on-unsliced-external",true,ReflectionProbeTimeSlicingMode.NoTimeSlicing,true,outputDirectory);
                yield return Case(root.transform,results,"on-unsliced-internal",true,ReflectionProbeTimeSlicingMode.NoTimeSlicing,false,outputDirectory);
                var report=new Report{device=SystemInfo.graphicsDeviceName,api=SystemInfo.graphicsDeviceType.ToString(),graphicsPreset=GraphicsPreset.Current,originalQualityFlag=original,cases=results.ToArray()};
                File.WriteAllText(Path.Combine(outputDirectory,"probe-matrix.json"),JsonUtility.ToJson(report,true));
                bool usable=false;foreach(var result in results)if(result.qualityFlag&&result.completed&&result.imageValid)usable=true;
                if(!usable)throw new InvalidOperationException("No enabled-quality candidate completed with all face/mip images valid. See probe-matrix.json; river readiness remains unresolved.");
                Debug.Log("[ProbeDiagnostic] Matrix recorded; at least one candidate has native completion and actual face/mip proof. This is not a river acceptance pass.");
            }
            finally
            {
                QualitySettings.realtimeReflectionProbes=original;
                Object.Destroy(root);foreach(var material in materials)Object.Destroy(material);
                GameInput.Simulated=false;Time.timeScale=1;SceneManager.LoadScene(GameManager.MenuScene);
            }
            yield return null;
            if(QualitySettings.realtimeReflectionProbes!=original)throw new InvalidOperationException("Probe diagnostic did not restore quality state.");
        }

        static IEnumerator Case(Transform parent,List<CaseResult> results,string name,bool quality,ReflectionProbeTimeSlicingMode slicing,bool external,string directory)
        {
            QualitySettings.realtimeReflectionProbes=quality;
            var result=new CaseResult{renderId=-1,name=name,qualityFlag=quality,slicing=slicing.ToString(),target=external?"owned ARGBHalf cube":"Unity-owned default",status="pending"};
            var go=new GameObject(name);go.layer=Layer;go.transform.SetParent(parent,false);go.transform.position=Origin;
            var probe=go.AddComponent<ReflectionProbe>();RenderTexture owned=null;
            try
            {
                float began=Time.realtimeSinceStartup;int firstFrame=Time.renderedFrameCount;
                try
                {
                    probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.ViaScripting;probe.timeSlicingMode=slicing;
                    probe.resolution=Resolution;probe.hdr=true;probe.nearClipPlane=.1f;probe.farClipPlane=5;probe.cullingMask=1<<Layer;
                    probe.clearFlags=ReflectionProbeClearFlags.SolidColor;probe.backgroundColor=Color.black;probe.size=Vector3.one*10;
                    if(external)
                    {
                        owned=new RenderTexture(Resolution,Resolution,16,RenderTextureFormat.ARGBHalf){dimension=TextureDimension.Cube,useMipMap=true,autoGenerateMips=false};
                        if(!owned.Create())throw new InvalidOperationException("Diagnostic HDR cube allocation failed.");
                        ClearCube(owned);probe.realtimeTexture=owned;
                    }
                    result.renderId=external?probe.RenderProbe(owned):probe.RenderProbe();
                }
                catch(Exception error){result.failure="Setup/request failure: "+error.Message;}
                while(result.renderId>=0&&!probe.IsFinishedRendering(result.renderId)&&Time.realtimeSinceStartup-began<DeadlineSeconds)yield return null;
                result.seconds=Time.realtimeSinceStartup-began;result.renderedFrames=Time.renderedFrameCount-firstFrame;
                result.completed=result.renderId>=0&&probe.IsFinishedRendering(result.renderId);
                if(result.completed)
                {
                    try
                    {
                        Texture texture=probe.realtimeTexture?probe.realtimeTexture:probe.texture;
                        if(!texture||texture.dimension!=TextureDimension.Cube||texture.mipmapCount<2)
                            throw new InvalidOperationException("Finished request did not expose a valid mipmapped cube.");
                        result.textureKind=texture.GetType().Name+"/"+texture.graphicsFormat;
                        result.hdrDecode=probe.textureHDRDecodeValues;
                        result.textureUpdateCount=(int)texture.updateCount;result.mipCount=texture.mipmapCount;
                        var faces=new List<FaceResult>();bool valid=true;
                        for(int face=0;face<6;face++)for(int mip=0;mip<2;mip++)
                        {
                            var sample=ReadFace(texture,result.hdrDecode,(CubemapFace)face,mip,Path.Combine(directory,name+"-"+(CubemapFace)face+"-mip"+mip+".png"));
                            bool ok=Finite(sample)&&sample.maxColorComponent>.05f&&(mip>0||Matches(sample,Colors[face]));
                            faces.Add(new FaceResult{face=((CubemapFace)face).ToString(),mip=mip,center=sample,valid=ok});valid&=ok;
                        }
                        result.faces=faces.ToArray();result.imageValid=valid;
                        if(!valid)result.failure="Native request completed but expected face signatures or nonblack mip images failed.";
                    }
                    catch(Exception error){result.failure=error.Message;}
                }
                else if(result.failure==null)result.failure="Native completion did not occur within the bounded deadline.";
                result.status=result.completed&&result.imageValid?"complete-with-image-proof":"FAIL";
                results.Add(result);Debug.Log("[ProbeDiagnostic] "+JsonUtility.ToJson(result));
            }
            finally
            {
                probe.enabled=false;probe.realtimeTexture=null;Object.Destroy(go);
                // Destroy is deferred by Unity so pending native work does not observe an explicitly released target.
                if(owned)Object.Destroy(owned);
            }
            yield return null;
        }
        static void ClearCube(RenderTexture texture)
        {
            var old=RenderTexture.active;
            try{for(int mip=0;mip<texture.mipmapCount;mip++)for(int face=0;face<6;face++){Graphics.SetRenderTarget(texture,mip,(CubemapFace)face,0);GL.Clear(true,true,Color.black);}}
            finally{RenderTexture.active=old;}
        }
        static Color ReadFace(Texture texture,Vector4 decode,CubemapFace face,int mip,string output)
        {
            int size=Mathf.Max(1,texture.width>>mip);var pixels=new Texture2D(size,size,TextureFormat.RGBAHalf,false,true);
            var preview=new Texture2D(size,size,TextureFormat.RGB24,false);var old=RenderTexture.active;RenderTexture copy=null;
            try
            {
                if(texture is RenderTexture cube)Graphics.SetRenderTarget(cube,mip,face,0);
                else
                {
                    copy=new RenderTexture(new RenderTextureDescriptor(size,size,texture.graphicsFormat,0));
                    if(!copy.Create())throw new InvalidOperationException("Native cube face readback target allocation failed.");
                    Graphics.CopyTexture(texture,(int)face,mip,copy,0,0);RenderTexture.active=copy;
                }
                pixels.ReadPixels(new Rect(0,0,size,size),0,0,false);pixels.Apply(false);
                var colors=pixels.GetPixels();
                for(int i=0;i<colors.Length;i++)colors[i]=Decode(colors[i],decode);
                preview.SetPixels(colors);preview.Apply(false);File.WriteAllBytes(output,preview.EncodeToPNG());
                Color center=Color.clear;int count=0;
                for(int y=size/2-2;y<size/2+2;y++)for(int x=size/2-2;x<size/2+2;x++){center+=colors[y*size+x];count++;}
                return center/count;
            }
            finally{RenderTexture.active=old;if(copy)Object.Destroy(copy);Object.Destroy(pixels);Object.Destroy(preview);}
        }
        // Same decode equation as Core ShaderLibrary/EntityLighting.hlsl for encoded environment cubes.
        static Color Decode(Color c,Vector4 decode)
        {
            float alpha=Mathf.Max(decode.w*(c.a-1)+1,0);float scale=decode.x*Mathf.Pow(alpha,decode.y);
            return new Color(c.r*scale,c.g*scale,c.b*scale,1);
        }
        static bool Finite(Color c)=>!float.IsNaN(c.r)&&!float.IsInfinity(c.r)&&!float.IsNaN(c.g)&&!float.IsInfinity(c.g)&&!float.IsNaN(c.b)&&!float.IsInfinity(c.b);
        static bool Matches(Color actual,Color expected)=>Channel(actual.r,expected.r)&&Channel(actual.g,expected.g)&&Channel(actual.b,expected.b);
        static bool Channel(float actual,float expected)=>expected>.5f?actual>.7f:actual<.25f;
    }
}
