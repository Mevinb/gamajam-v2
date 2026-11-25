using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Parasite.Characters;
using Parasite.Missions;
using Parasite.Possession;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Parasite.Testing
{
    /// <summary>Optional staged art-QA cameras. These environment-only pairs are not gameplay screenshots.</summary>
    public static class IslandWallArtCapture
    {
        const int Width=1920,Height=1080;
        static readonly string[] WallNames={"Quarantine wall","Low passage lintel","Service entrance lintel","South coast west wall","South coast east wall"};
        static readonly (string name,Vector3 eye,Vector3 target,float fov)[] Views={
            ("entrances-context",new(-16,3,-33),new(-16,1.85f,-18),55),
            ("precast-panel-detail",new(-31,1.7f,-23),new(-29,1.8f,-18.1f),45),
            ("southern-quay-detail",new(-42,1.7f,-42),new(-42,.85f,-37.7f),45),
        };
        [Serializable] sealed class Shot { public string name,variant,path,kind="staged scene-only art-QA; actor renderers/UI hidden, owned lights retained; not gameplay";public Vector3 eye,target;public float fov,aspect,renderScale,postprocessedLinearPeak;public int width,height,cameraId;public bool normalAtmosphere,cameraHDR,postProcessing,targetSRGB,tonemappingActive;public string targetFormat,targetGraphicsFormat,readbackFormat,colourSpace,tonemapping,conversion; }
        [Serializable] sealed class Report { public string utc,scope="Fixed normal-shaded same-camera primitive/authored comparison; actor renderers/UI hidden, existing lights retained; no gameplay or performance claim.";public int preset;public string[] atmosphereAsFound;public bool restorationVerified;public List<Shot> shots=new(); }
        sealed class CameraState
        {
            public Camera camera;public bool enabled,hdr,msaa;public Vector3 position;public Quaternion rotation;public float fov,aspect,near,far;public Rect rect;public RenderTexture target;
            public UniversalAdditionalCameraData data;public bool post;public AntialiasingMode aa;
            public CameraState(Camera c){camera=c;enabled=c.enabled;hdr=c.allowHDR;msaa=c.allowMSAA;position=c.transform.position;rotation=c.transform.rotation;fov=c.fieldOfView;aspect=c.aspect;near=c.nearClipPlane;far=c.farClipPlane;rect=c.rect;target=c.targetTexture;data=c.GetComponent<UniversalAdditionalCameraData>();if(data){post=data.renderPostProcessing;aa=data.antialiasing;}}
            public void Restore()
            {
                if(!camera)return;
                // The pre-existing cameras are never staged. Avoid setter side effects (such as changing an
                // automatic aspect to an explicit one) when their value is already the original value.
                if(camera.enabled!=enabled)camera.enabled=enabled;if(camera.allowHDR!=hdr)camera.allowHDR=hdr;if(camera.allowMSAA!=msaa)camera.allowMSAA=msaa;
                if(!camera.transform.position.Equals(position)||!camera.transform.rotation.Equals(rotation))camera.transform.SetPositionAndRotation(position,rotation);
                if(camera.fieldOfView!=fov)camera.fieldOfView=fov;if(camera.aspect!=aspect)camera.aspect=aspect;if(camera.nearClipPlane!=near)camera.nearClipPlane=near;if(camera.farClipPlane!=far)camera.farClipPlane=far;
                if(!camera.rect.Equals(rect))camera.rect=rect;if(camera.targetTexture!=target)camera.targetTexture=target;
                if(data){if(data.renderPostProcessing!=post)data.renderPostProcessing=post;if(data.antialiasing!=aa)data.antialiasing=aa;}
            }
            public bool Matches()=>camera&&camera.enabled==enabled&&camera.allowHDR==hdr&&camera.allowMSAA==msaa&&camera.transform.position.Equals(position)&&camera.transform.rotation.Equals(rotation)&&camera.fieldOfView==fov&&camera.aspect==aspect&&camera.nearClipPlane==near&&camera.farClipPlane==far&&camera.rect.Equals(rect)&&camera.targetTexture==target&&(!data||data.renderPostProcessing==post&&data.antialiasing==aa);
        }
        public static IEnumerator Run()
        {
            Check(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null,"Normal-shaded wall art-QA requires a GPU.");
            yield return IslandEntryAcceptance.EnterFromFerry(EndingOutcome.Peace);
            CaptureCurrent();
        }
        public static void CaptureCurrent()
        {
            Check(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null,"Actual art-QA cannot run on Null graphics.");
            var root=GameObject.Find(IslandDistrict.RootName);Check(root,"The actual island must already be loaded.");
            var model=root.transform.Find("IslandQuarantineWalls (Blender)");Check(model,"Authored wall kit exists.");
            var authored=model.GetComponentsInChildren<MeshRenderer>(true);Check(authored.Length==2,"Two authored wall renderers exist.");
            var primitive=new List<MeshRenderer>();for(int i=0;i<root.transform.childCount;i++){var child=root.transform.GetChild(i);if(WallNames.Contains(child.name)){var r=child.GetComponent<MeshRenderer>();if(r)primitive.Add(r);}}
            Check(primitive.Count==7,"All seven original primitive wall renderers exist.");
            var bodyRenderers=new HashSet<Renderer>();foreach(var host in HostCharacter.All)if(host)foreach(var r in host.GetComponentsInChildren<Renderer>(true))bodyRenderers.Add(r);
            var pm=PossessionManager.Instance;if(pm)foreach(var agent in pm.Agents)if(agent.Blob)foreach(var r in agent.Blob.GetComponentsInChildren<Renderer>(true))bodyRenderers.Add(r);
            var changed=bodyRenderers.Concat(authored).Concat(primitive).Distinct().Select(r=>(renderer:r,enabled:r.enabled,layer:r.gameObject.layer,materials:r.sharedMaterials)).ToArray();
            var cameras=UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(c=>new CameraState(c)).ToArray();
            var brains=UnityEngine.Object.FindObjectsByType<CinemachineBrain>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(b=>(brain:b,enabled:b.enabled)).ToArray();
            var canvases=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(c=>(canvas:c,enabled:c.enabled)).ToArray();
            var colliders=root.GetComponentsInChildren<Collider>(true).Select(c=>(collider:c,enabled:c.enabled,trigger:c.isTrigger,bounds:c.bounds)).ToArray();
            var features=new List<(FullScreenPassRendererFeature feature,bool active)>();var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Check(QualitySettings.activeColorSpace==ColorSpace.Linear&&GraphicsPreset.Current>=2&&pipeline&&pipeline.supportsHDR,
                "Faithful shaded art-QA requires Linear colour space and the normal High/Ultra HDR pipeline; no automatic quality override is applied.");
            if(pipeline)foreach(var data in pipeline.rendererDataList)if(data)foreach(var feature in data.rendererFeatures)
                if(feature is FullScreenPassRendererFeature full&&full.passMaterial&&full.passMaterial.shader.name=="Parasite/Atmosphere"&&!features.Any(f=>f.feature==full))features.Add((full,full.isActive));
            Check(features.Count>0&&features.All(value=>value.active),"Normal shaded art-QA requires the authored atmosphere active; it is not disabled for these pairs.");
            var flags=new HashSet<string>(RunState.Flags);int mission=RunState.MissionIndex;float scale=Time.timeScale;var gameState=GameManager.Instance.State;
            string directory=Path.GetFullPath(Path.Combine("Logs","island-wall-art-qa",DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff")));
            Directory.CreateDirectory(directory);var report=new Report{utc=DateTime.UtcNow.ToString("O"),preset=GraphicsPreset.Current,atmosphereAsFound=features.Select(v=>v.feature.name+"="+v.active).ToArray()};
            var cameraObject=new GameObject("STAGED art-QA wall camera (not gameplay)");var camera=cameraObject.AddComponent<Camera>();
            if(Camera.main)camera.CopyFrom(Camera.main);camera.enabled=false;camera.targetTexture=null;camera.aspect=(float)Width/Height;camera.nearClipPlane=.08f;camera.farClipPlane=900;
            camera.cullingMask=Layers.EnvironmentMask;camera.clearFlags=CameraClearFlags.Skybox;camera.orthographic=false;
            var extra=cameraObject.AddComponent<UniversalAdditionalCameraData>();extra.renderPostProcessing=true;extra.renderShadows=true;GraphicsPreset.ApplyCamera(camera);
            try
            {
                foreach(var value in bodyRenderers)value.enabled=false;
                foreach(var value in canvases)value.canvas.enabled=false;
                foreach(var view in Views)
                {
                    camera.transform.SetPositionAndRotation(IslandCampaign.Origin+view.eye,Quaternion.LookRotation(view.target-view.eye));camera.fieldOfView=view.fov;
                    foreach(var r in primitive)r.enabled=true;foreach(var r in authored)r.enabled=false;
                    Capture(camera,view.name,"primitive-before",view.target,features,directory,report);
                    foreach(var r in primitive)r.enabled=false;foreach(var r in authored)r.enabled=true;
                    Capture(camera,view.name,"authored-after",view.target,features,directory,report);
                }
            }
            finally
            {
                foreach(var value in changed)if(value.renderer){value.renderer.enabled=value.enabled;value.renderer.gameObject.layer=value.layer;value.renderer.sharedMaterials=value.materials;}
                foreach(var value in canvases)if(value.canvas)value.canvas.enabled=value.enabled;
                foreach(var value in brains)if(value.brain)value.brain.enabled=value.enabled;
                foreach(var value in cameras)value.Restore();
                UnityEngine.Object.Destroy(cameraObject);
                report.restorationVerified=changed.All(v=>v.renderer&&v.renderer.enabled==v.enabled&&v.renderer.gameObject.layer==v.layer&&v.renderer.sharedMaterials.SequenceEqual(v.materials))
                    &&canvases.All(v=>v.canvas&&v.canvas.enabled==v.enabled)&&brains.All(v=>v.brain&&v.brain.enabled==v.enabled)&&cameras.All(c=>c.Matches())
                    &&colliders.All(v=>v.collider&&v.collider.enabled==v.enabled&&v.collider.isTrigger==v.trigger&&v.collider.bounds.Equals(v.bounds))
                    &&features.All(v=>v.feature&&v.feature.isActive==v.active)&&flags.SetEquals(RunState.Flags)&&RunState.MissionIndex==mission&&Time.timeScale==scale&&GameManager.Instance.State==gameState;
                File.WriteAllText(Path.Combine(directory,"art-qa.json"),JsonUtility.ToJson(report,true)+"\n");
                Check(report.restorationVerified,"Exact camera/brain/renderer/UI/atmosphere/physics/mission state is restored after staged art-QA.");
            }
            Debug.Log("[IslandWallArtQA] six normal-shaded staged comparison images saved="+directory+" restorationVerified="+report.restorationVerified);
        }
        static void Capture(Camera camera,string name,string variant,Vector3 look,List<(FullScreenPassRendererFeature feature,bool active)> atmosphere,string directory,Report report)
        {
            var extra=camera.GetComponent<UniversalAdditionalCameraData>();var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Check(atmosphere.All(v=>v.feature.isActive==v.active),"The normal authored atmosphere remains active exactly as found during art-QA.");
            Check(QualitySettings.activeColorSpace==ColorSpace.Linear&&camera.allowHDR&&pipeline&&pipeline.supportsHDR&&extra&&extra.renderPostProcessing,
                "Shaded capture requires the actual HDR/Linear/postprocessing path, rather than an LDR or disabled-post approximation.");
            // Installed URP clones an external target's descriptor, so the request must be HDR from the start.
            // With a targetTexture, UniversalCameraData.requireSrgbConversion is false; retain linear FP16
            // through ordinary authored post/ACES, then encode display-sRGB exactly once for the PNG.
            var rt=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear){antiAliasing=1};
            var pixels=new Texture2D(Width,Height,TextureFormat.RGBAHalf,false,true);
            var png=new Texture2D(Width,Height,TextureFormat.RGB24,false,false);var old=RenderTexture.active;
            try
            {
                Check(rt.Create()&&rt.format==RenderTextureFormat.ARGBHalf&&!rt.sRGB,"The external target is actual linear FP16 HDR, with no ARGB32 intermediate.");
                var request=new UniversalRenderPipeline.SingleCameraRequest{destination=rt};Check(RenderPipeline.SupportsRenderRequest(camera,request),"URP supports normal HDR art capture.");
                RenderPipeline.SubmitRenderRequest(camera,request);RenderPipeline.SubmitRenderRequest(camera,request);
                Check(camera.allowHDR&&extra.renderPostProcessing,"Normal HDR and postprocessing remain enabled through the actual render callback.");
                var stack=extra.volumeStack??VolumeManager.instance.stack;
                var tone=stack!=null?stack.GetComponent<Tonemapping>():null;
                bool toneActive=tone&&tone.active&&tone.IsActive();
                Check(toneActive,"The actual post-request tone-map is active before exporting faithful SDR art-QA.");
                RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,Width,Height),0,0);pixels.Apply();
                var colours=pixels.GetPixels();float peak=0;
                for(int index=0;index<colours.Length;index++)
                {
                    var linear=colours[index];
                    Check(!float.IsNaN(linear.r)&&!float.IsInfinity(linear.r)&&!float.IsNaN(linear.g)&&!float.IsInfinity(linear.g)&&!float.IsNaN(linear.b)&&!float.IsInfinity(linear.b),"HDR readback contains finite RGB before conversion and encoding.");
                    peak=Mathf.Max(peak,Mathf.Max(linear.r,Mathf.Max(linear.g,linear.b)));
                    colours[index]=linear.gamma;colours[index].a=1; // Encoding only: do not tone-map a second time.
                }
                png.SetPixels(colours);png.Apply();
                string path=Path.Combine(directory,"STAGED-SCENE-ART-QA-"+name+"-"+variant+".png");File.WriteAllBytes(path,png.EncodeToPNG());
                report.shots.Add(new Shot{name=name,variant=variant,path=path,cameraId=camera.GetInstanceID(),eye=camera.transform.position,target=IslandCampaign.Origin+look,
                    fov=camera.fieldOfView,aspect=camera.aspect,width=Width,height=Height,normalAtmosphere=atmosphere.All(v=>v.feature.isActive==v.active),
                    cameraHDR=camera.allowHDR,postProcessing=extra.renderPostProcessing,renderScale=pipeline.renderScale,targetFormat=rt.format.ToString(),targetGraphicsFormat=rt.graphicsFormat.ToString(),targetSRGB=rt.sRGB,
                    readbackFormat=pixels.format.ToString(),colourSpace=QualitySettings.activeColorSpace.ToString(),tonemapping=tone.mode.value.ToString(),tonemappingActive=toneActive,
                    postprocessedLinearPeak=peak,conversion="Authored postprocessed linear FP16 RGB to sRGB once via Color.gamma; RGB24 PNG; no additional CPU tone mapping"});
            }
            finally{RenderTexture.active=old;rt.Release();UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(pixels);UnityEngine.Object.Destroy(png);}
        }
        static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
    }
}
