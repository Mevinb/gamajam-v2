using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Parasite.Missions;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Parasite.Testing
{
    /// <summary>Shared actual world/material/physics and GPU aperture proof, after the authored wall kit is imported.</summary>
    public static class IslandWallAcceptance
    {
        [Serializable] sealed class CameraSnapshot
        {
            public string phase, type, clear, target, renderer, antialiasing; public Vector3 position, forward; public Color background;
            public float aspect, orthoSize, near, far, renderScale; public bool orthographic, hdr, msaa, postProcessing, dynamicResolution;
            public int mask, pixelWidth, pixelHeight, scaledWidth, scaledHeight; public Rect rect, pixelRect;
            public float[] projection, nonJitteredProjection, gpuProjection, worldToCamera;
        }
        [Serializable] sealed class PixelSample
        {
            public string label; public Vector3 world, viewport; public bool inside; public Color rgba, flipY, flipX, flipXY; public float grayscale;
        }
        [Serializable] sealed class ViewDiagnostic
        {
            public string aperture, role, image; public CameraSnapshot before, after; public List<CameraSnapshot> duringBegin=new();
            public List<PixelSample> samples=new(); public int width, height, msaa; public string format, graphicsFormat; public bool srgb;
        }
        [Serializable] sealed class RendererDiagnostic
        {
            public string name, mesh, shader; public Vector3 min, max, localScale, meshMin, meshMax; public float determinant; public float[] localToWorld;
        }
        [Serializable] sealed class PixelCheck { public bool pass; public string message; }
        [Serializable] sealed class ApertureDiagnostic
        {
            public string utc, unity, device, api, colourSpace, directory; public int preset; public string[] rendererFeatures, atmosphereBefore, atmosphereDuring, atmosphereRestored; public bool atmosphereRestoredExactly;
            public string scope="Actual runtime mesh rendered in isolation. Mirrored samples are diagnostic controls, not acceptance replacements. Original thresholds remain unchanged.";
            public List<RendererDiagnostic> renderers=new(); public List<ViewDiagnostic> views=new(); public List<PixelCheck> checks=new();
        }
        readonly struct Wall
        {
            public readonly string Name; public readonly Vector3 Center, Size;
            public Wall(string name, Vector3 center, Vector3 size) { Name=name; Center=center; Size=size; }
        }
        static readonly Wall[] Walls = {
            new("Quarantine wall",new(-39.85f,1.95f,-18),new(26.3f,3.6f,.4f)),
            new("Quarantine wall",new(-16.75f,1.95f,-18),new(17.1f,3.6f,.4f)),
            new("Quarantine wall",new(23.6f,1.95f,-18),new(58.8f,3.6f,.4f)),
            new("Low passage lintel",new(-26,2.55f,-18),new(1.4f,2.4f,.4f)),
            new("Service entrance lintel",new(-7,3.25f,-18),new(2.4f,1,.4f)),
            new("South coast west wall",new(-44.75f,.75f,-37.7f),new(14.5f,1.2f,.6f)),
            new("South coast east wall",new(8.75f,.75f,-37.7f),new(86.5f,1.2f,.6f)),
        };
        public static IEnumerator Run(Func<string,IEnumerator> capture=null)
        {
            RequireGpu(); yield return IslandEntryAcceptance.EnterFromFerry(EndingOutcome.Peace);
            yield return VerifyCurrent(GameObject.Find(IslandDistrict.RootName), capture);
        }
        public static IEnumerator VerifyCurrent(GameObject root, Func<string,IEnumerator> capture=null)
        {
            RequireGpu(); Check(root,"The actual generated island exists."); Physics.SyncTransforms();
            var model=root.transform.Find("IslandQuarantineWalls (Blender)"); Check(model,"The imported wall model is required before hiding primitive visuals.");
            var renderers=model.GetComponentsInChildren<MeshRenderer>(true);
            Check(renderers.Length==2&&renderers.All(r=>r.enabled&&r.gameObject.activeInHierarchy),"Both authored wall renderers are active and enabled.");
            Check(model.GetComponentsInChildren<Collider>(true).Length==0,"The visual model introduces no colliders or navigation sources.");
            long triangles=0;
            foreach(var renderer in renderers)
            {
                bool main=renderer.name.StartsWith("QuarantineFacade",StringComparison.Ordinal);
                Check(main||renderer.name.StartsWith("SouthernQuayFacade",StringComparison.Ordinal),"The authored wall group identity is preserved.");
                Vector3 min=IslandCampaign.Origin+(main?new Vector3(-53,.15f,-18.2f):new Vector3(-52,.15f,-38));
                Vector3 max=IslandCampaign.Origin+(main?new Vector3(53,3.75f,-17.8f):new Vector3(52,1.35f,-37.4f));
                Check(Vector3.Distance(renderer.bounds.min,min)<.005f&&Vector3.Distance(renderer.bounds.max,max)<.005f,"The actual new mesh keeps its metre bounds aligned with the retained boxes.");
                var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
                for(int sub=0;sub<mesh.subMeshCount;sub++)triangles+=(long)mesh.GetIndexCount(sub)/3;
            }
            Check(triangles==11804,"The actual runtime model retains the imported11804triangle geometry.");
            var materials=renderers.SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
            Check(materials.Length==1&&materials[0]&&renderers.All(r=>r.sharedMaterials.Length==1),"Both wall meshes share one runtime material.");
            var material=materials[0];
            Check(material.name.StartsWith("IslandQuarantineConcrete",StringComparison.Ordinal)&&material.renderQueue<2500,"The wall uses the intended opaque mapped surface.");
            foreach(var map in new[]{("_BaseMap","A"),("_BumpMap","N"),("_MetallicGlossMap","M")})
            {
                var expected=Resources.Load<Texture2D>("Island/Textures/IslandQuarantine_"+map.Item2);
                Check(expected&&expected.width==2048&&expected.height==2048&&material.GetTexture(map.Item1)==expected,"Actual wall material binds the imported2K "+map.Item2+" map.");
            }
            Check(material.IsKeywordEnabled("_NORMALMAP")&&material.IsKeywordEnabled("_METALLICSPECGLOSSMAP")&&Mathf.Abs(material.GetFloat("_Smoothness")-1)<.001f,
                "Runtime normal/mask keywords and direct mask-alpha smoothness are preserved.");
            var direct=new List<Transform>();for(int i=0;i<root.transform.childCount;i++)direct.Add(root.transform.GetChild(i));
            var dressed=direct.Where(t=>Walls.Any(w=>w.Name==t.name)).ToArray();
            Check(dressed.Length==7,"Exactly the seven original direct wall objects are dressed.");
            var boxes=new List<BoxCollider>();
            foreach(var wall in Walls)
            {
                var matches=dressed.Where(t=>t.name==wall.Name&&Vector3.Distance(t.localPosition,wall.Center)<.001f).ToArray();
                Check(matches.Length==1,"The exact original wall center still exists: "+wall.Name+" "+wall.Center);
                var t=matches[0];var box=t.GetComponent<BoxCollider>();var renderer=t.GetComponent<MeshRenderer>();
                Check(renderer&&!renderer.enabled&&box&&box.enabled&&box.gameObject.activeInHierarchy&&!box.isTrigger,
                    "Only the original renderer is disabled; its real solid box remains active: "+wall.Name);
                Check(Vector3.Distance(t.localScale,wall.Size)<.001f&&Quaternion.Angle(t.localRotation,Quaternion.identity)<.001f
                    &&Vector3.Distance(box.center,Vector3.zero)<.001f&&Vector3.Distance(box.size,Vector3.one)<.001f
                    &&Vector3.Distance(box.bounds.center,IslandCampaign.Origin+wall.Center)<.002f&&Vector3.Distance(box.bounds.size,wall.Size)<.002f,
                    "Original transform and active world collider bounds are exact: "+wall.Name);
                boxes.Add(box);
            }
            // Structural aperture rays deliberately test the retained wall boxes themselves, not the correctly
            // closed moving service/tide panels. No gate, collider, route or NavMesh state is changed by this proof.
            CheckPhysicsAperture(boxes,-26,.15f,1.4f,1.2f,-18);
            CheckPhysicsAperture(boxes,-7,.15f,2.4f,2.6f,-18);
            CheckPhysicsAperture(boxes,-36,.15f,3,1.2f,-37.7f);
            const int proofLayer=31;
            Check(!UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Any(r=>r.enabled&&r.gameObject.activeInHierarchy&&r.gameObject.layer==proofLayer),"The proof layer is free of unrelated scene geometry.");
            var cameraObject=new GameObject("Island wall aperture proof");var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
            var additional=cameraObject.AddComponent<UniversalAdditionalCameraData>();additional.renderPostProcessing=false;additional.antialiasing=AntialiasingMode.None;
            camera.cullingMask=1<<proofLayer;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.nearClipPlane=.05f;camera.farClipPlane=6;camera.orthographic=true;camera.aspect=1;
            var white=new Material(MatFactory.Unlit(Color.white));
            var saved=renderers.Select(r=>(r,r.gameObject.layer,r.sharedMaterials)).ToArray();
            try
            {
                foreach(var r in renderers){r.gameObject.layer=proofLayer;r.sharedMaterial=white;}
                string directory=Path.GetFullPath(Path.Combine("Logs","island-wall-aperture-diagnostics",DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff")+"-f"+Time.frameCount));
                Directory.CreateDirectory(directory);
                var diagnostic=new ApertureDiagnostic { utc=DateTime.UtcNow.ToString("O"), unity=Application.unityVersion, device=SystemInfo.graphicsDeviceName,
                    api=SystemInfo.graphicsDeviceType.ToString(), colourSpace=QualitySettings.activeColorSpace.ToString(), preset=GraphicsPreset.Current, directory=directory, rendererFeatures=RendererFeatures() };
                foreach(var r in renderers)diagnostic.renderers.Add(new RendererDiagnostic { name=r.name, mesh=r.GetComponent<MeshFilter>().sharedMesh.name,
                    shader=r.sharedMaterial.shader.name, min=r.bounds.min, max=r.bounds.max, localScale=r.transform.localScale, meshMin=r.GetComponent<MeshFilter>().sharedMesh.bounds.min, meshMax=r.GetComponent<MeshFilter>().sharedMesh.bounds.max,
                    determinant=r.localToWorldMatrix.determinant, localToWorld=Matrix(r.localToWorldMatrix) });
                var atmosphere=AtmosphereFeatures().Select(feature=>(feature,wasActive:feature.isActive)).ToArray();
                diagnostic.atmosphereBefore=atmosphere.Select(value=>value.feature.name+"="+value.wasActive).ToArray();
                try
                {
                    // Structural raster isolation only: empty depth must remain the camera's black clear colour.
                    // The normal shaded scene retains its authored atmospheric feature immediately afterward.
                    foreach(var value in atmosphere)value.feature.SetActive(false);
                    diagnostic.atmosphereDuring=atmosphere.Select(value=>value.feature.name+"="+value.feature.isActive).ToArray();
                    Check(atmosphere.All(value=>!value.feature.isActive),"Only the atmosphere feature is disabled for isolated structural captures.");
                    RenderAperture(camera,-26,.15f,1.4f,1.2f,-18,true,"culvert",diagnostic);
                    RenderAperture(camera,-7,.15f,2.4f,2.6f,-18,true,"service",diagnostic);
                    RenderAperture(camera,-36,.15f,3,1.2f,-37.7f,false,"arrival gangway",diagnostic);
                }
                finally
                {
                    foreach(var value in atmosphere)if(value.feature)value.feature.SetActive(value.wasActive);
                    diagnostic.atmosphereRestored=atmosphere.Select(value=>value.feature?value.feature.name+"="+value.feature.isActive:"missing feature").ToArray();
                    diagnostic.atmosphereRestoredExactly=atmosphere.All(value=>value.feature&&value.feature.isActive==value.wasActive);
                    File.WriteAllText(Path.Combine(directory,"diagnostic.json"),JsonUtility.ToJson(diagnostic,true)+"\n");
                    Debug.Log("[IslandWallDiagnostic] saved="+directory+" views="+diagnostic.views.Count+" failedOriginalChecks="+diagnostic.checks.Count(c=>!c.pass)
                        +" atmosphereRestoredExactly="+diagnostic.atmosphereRestoredExactly);
                    Check(diagnostic.atmosphereRestoredExactly,"The atmosphere feature's exact original active state is restored before any shaded gameplay capture.");
                }
                // Collect every intended/mirrored image before running the same original threshold assertions.
                foreach(var result in diagnostic.checks)Check(result.pass,result.message);
            }
            finally
            {
                foreach(var value in saved)if(value.r){value.r.gameObject.layer=value.layer;value.r.sharedMaterials=value.sharedMaterials;}
                UnityEngine.Object.Destroy(cameraObject);UnityEngine.Object.Destroy(white);
            }
            if(capture!=null)
            {
                Possession.PlayerCamera.Instance.SetYaw(0);Possession.PlayerCamera.Instance.SetPitch(0);yield return new WaitForSeconds(.3f);
                yield return capture("island-authored-walls-actual-materials.png");
            }
            Debug.Log("[IslandWall] PASS2active meshes/one mapped2K material;7disabled primitive renderers/exact active boxes;3physical+rendered clear apertures. Moving route gates were untouched.");
        }
        static void CheckPhysicsAperture(List<BoxCollider> boxes,float x,float bottom,float width,float height,float z)
        {
            for(int ix=0;ix<5;ix++)for(int iy=0;iy<5;iy++)
            {
                var p=IslandCampaign.Origin+new Vector3(x+Mathf.Lerp(-width*.5f+.08f,width*.5f-.08f,ix/4f),bottom+Mathf.Lerp(.08f,height-.08f,iy/4f),z);
                var ray=new Ray(p+Vector3.back,Vector3.forward);
                Check(boxes.All(b=>!b.Raycast(ray,out _,2)),"The original wall boxes retain their clear aperture at "+p);
            }
        }
        static void RenderAperture(Camera camera,float x,float bottom,float width,float height,float z,bool lintel,string name,ApertureDiagnostic diagnostic)
        {
            CaptureAperture(camera,x,bottom,width,height,z,lintel,name,false,diagnostic);
            CaptureAperture(camera,-x,bottom,width,height,z,lintel,name,true,diagnostic);
        }
        static void CaptureAperture(Camera camera,float x,float bottom,float width,float height,float z,bool lintel,string name,bool mirrored,ApertureDiagnostic diagnostic)
        {
            var center=IslandCampaign.Origin+new Vector3(x,bottom+height*.5f,z);
            camera.transform.SetPositionAndRotation(center+Vector3.back*3,Quaternion.identity);camera.orthographicSize=Mathf.Max(width,height)*.5f+.5f;
            var rt=new RenderTexture(256,256,24,RenderTextureFormat.ARGB32){antiAliasing=1};var pixels=new Texture2D(256,256,TextureFormat.RGB24,false);var old=RenderTexture.active;
            var view=new ViewDiagnostic { aperture=name, role=mirrored?"mirrored positive-X control (CPU contract expects solid)":"intended negative-X aperture", width=256,height=256,msaa=rt.antiAliasing,
                format=rt.format.ToString(), graphicsFormat=rt.graphicsFormat.ToString(), srgb=rt.sRGB };
            diagnostic.views.Add(view);
            Action<ScriptableRenderContext,Camera> observe=(context,current)=>{if(current==camera)view.duringBegin.Add(Snapshot(camera,"beginCameraRendering after previously registered callbacks"));};
            try
            {
                Check(rt.Create(),"The GPU aperture proof target allocates.");var request=new UniversalRenderPipeline.SingleCameraRequest{destination=rt};
                Check(RenderPipeline.SupportsRenderRequest(camera,request),"URP supports actual wall aperture rendering.");
                view.before=Snapshot(camera,"before request");RenderPipelineManager.beginCameraRendering+=observe;
                try { RenderPipeline.SubmitRenderRequest(camera,request); }
                finally { RenderPipelineManager.beginCameraRendering-=observe; }
                view.after=Snapshot(camera,"after request");
                RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,256,256),0,0);pixels.Apply();
                view.image=Path.Combine(diagnostic.directory,name.Replace(" ","-")+(mirrored?"-mirrored":"-intended")+".png");
                File.WriteAllBytes(view.image,pixels.EncodeToPNG()); // Save the full raw image before any pixel assertion.
                PixelSample At(Vector3 p,string label,bool originalSample=true)
                {
                    var uv=camera.WorldToViewportPoint(p);var sample=new PixelSample { label=label,world=p,viewport=uv,inside=uv.x>0&&uv.x<1&&uv.y>0&&uv.y<1,
                        rgba=pixels.GetPixelBilinear(uv.x,uv.y),flipY=pixels.GetPixelBilinear(uv.x,1-uv.y),flipX=pixels.GetPixelBilinear(1-uv.x,uv.y),flipXY=pixels.GetPixelBilinear(1-uv.x,1-uv.y) };
                    sample.grayscale=sample.rgba.grayscale;view.samples.Add(sample);
                    Debug.Log("[IslandWallPixel] aperture="+name+" role="+view.role+" sample="+label+" world="+p.ToString("F4")+" uv="+uv.ToString("F4")
                        +" rgba="+sample.rgba.ToString("F4")+" gray="+sample.grayscale.ToString("F4")+" flipY="+sample.flipY.ToString("F4"));
                    if(!mirrored&&originalSample)diagnostic.checks.Add(new PixelCheck { pass=sample.inside,message="Aperture sample lies inside the actual proof view." });
                    return sample;
                }
                At(center,"center handedness control",false);
                for(int ix=0;ix<5;ix++)for(int iy=0;iy<5;iy++)
                {
                    var p=IslandCampaign.Origin+new Vector3(x+Mathf.Lerp(-width*.5f+.08f,width*.5f-.08f,ix/4f),bottom+Mathf.Lerp(.08f,height-.08f,iy/4f),z);
                    var value=At(p,"grid "+ix+","+iy);
                    if(!mirrored)diagnostic.checks.Add(new PixelCheck { pass=value.grayscale<.1f,message="Actual wall pixels leave the "+name+" opening clear at "+p });
                }
                foreach(float side in new[]{-1f,1f})
                {
                    var value=At(center+Vector3.right*side*(width*.5f+.16f),side<0?"left opaque side":"right opaque side");
                    if(!mirrored)diagnostic.checks.Add(new PixelCheck { pass=value.grayscale>.8f,message="Actual opaque wall pixels frame the "+name+" opening." });
                }
                if(lintel)
                {
                    var value=At(IslandCampaign.Origin+new Vector3(x,bottom+height+.16f,z),"opaque lintel");
                    if(!mirrored)diagnostic.checks.Add(new PixelCheck { pass=value.grayscale>.8f,message="The actual "+name+" lintel remains opaque above the opening." });
                }
                Debug.Log("[IslandWallCamera] aperture="+name+" role="+view.role+" before="+JsonUtility.ToJson(view.before)+" after="+JsonUtility.ToJson(view.after));
            }
            finally {RenderPipelineManager.beginCameraRendering-=observe;RenderTexture.active=old;rt.Release();UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(pixels);}
        }
        static CameraSnapshot Snapshot(Camera camera,string phase)
        {
            var extra=camera.GetComponent<UniversalAdditionalCameraData>();var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            return new CameraSnapshot { phase=phase,type=camera.cameraType.ToString(),position=camera.transform.position,forward=camera.transform.forward,
                aspect=camera.aspect,orthoSize=camera.orthographicSize,near=camera.nearClipPlane,far=camera.farClipPlane,orthographic=camera.orthographic,
                hdr=camera.allowHDR,msaa=camera.allowMSAA,dynamicResolution=camera.allowDynamicResolution,postProcessing=extra&&extra.renderPostProcessing,
                antialiasing=extra?extra.antialiasing.ToString():"no URP data",renderer=extra&&extra.scriptableRenderer!=null?extra.scriptableRenderer.GetType().FullName:"none",
                target=camera.targetTexture?camera.targetTexture.name+":"+camera.targetTexture.width+"x"+camera.targetTexture.height:"null",mask=camera.cullingMask,
                pixelWidth=camera.pixelWidth,pixelHeight=camera.pixelHeight,scaledWidth=camera.scaledPixelWidth,scaledHeight=camera.scaledPixelHeight,
                rect=camera.rect,pixelRect=camera.pixelRect,clear=camera.clearFlags.ToString(),background=camera.backgroundColor,renderScale=pipeline?pipeline.renderScale:1,
                projection=Matrix(camera.projectionMatrix),nonJitteredProjection=Matrix(camera.nonJitteredProjectionMatrix),gpuProjection=Matrix(GL.GetGPUProjectionMatrix(camera.projectionMatrix,true)),worldToCamera=Matrix(camera.worldToCameraMatrix) };
        }
        static FullScreenPassRendererFeature[] AtmosphereFeatures()
        {
            var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;if(!pipeline)return Array.Empty<FullScreenPassRendererFeature>();
            var found=new List<FullScreenPassRendererFeature>();
            foreach(var data in pipeline.rendererDataList)if(data)foreach(var entry in data.rendererFeatures)
                if(entry is FullScreenPassRendererFeature feature&&feature&&feature.passMaterial&&feature.passMaterial.shader
                    &&feature.passMaterial.shader.name=="Parasite/Atmosphere"&&!found.Contains(feature))found.Add(feature);
            return found.ToArray();
        }
        static string[] RendererFeatures()
        {
            var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;if(!pipeline)return Array.Empty<string>();
            var rows=new List<string>();
            foreach(var data in pipeline.rendererDataList)if(data)foreach(var feature in data.rendererFeatures)if(feature)
            {
                string row=data.name+"/"+feature.name+" type="+feature.GetType().FullName+" active="+feature.isActive;
                if(feature is FullScreenPassRendererFeature full&&full.passMaterial)
                {
                    var material=full.passMaterial;row+=" shader="+material.shader.name+" material="+material.name;
                    foreach(string key in new[]{"_SkyHaze","_StartDistance","_Density","_MaxOpacity","_BaseHeight"})if(material.HasProperty(key))row+=" "+key+"="+material.GetFloat(key);
                    if(material.HasProperty("_FogColor"))row+=" _FogColor="+material.GetColor("_FogColor");
                }
                rows.Add(row);
            }
            return rows.ToArray();
        }
        static float[] Matrix(Matrix4x4 matrix)
        {
            var values=new float[16];for(int row=0;row<4;row++)for(int column=0;column<4;column++)values[row*4+column]=matrix[row,column];return values;
        }
        static void RequireGpu()=>Check(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null,"Actual wall visual verification requires a real graphics device.");
        static void Check(bool pass,string message){if(!pass)throw new InvalidOperationException(message);}
    }
}
