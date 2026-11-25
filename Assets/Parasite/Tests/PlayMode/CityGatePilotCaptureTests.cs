using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using Parasite.Characters;
using Parasite.FX;
using Parasite.Possession;
using Parasite.Testing;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Parasite.Tests
{
    /// <summary>Fixed-camera actual-scene comparison. No body teleport, gameplay pass or pixel-identical animation claim.</summary>
    public sealed class CityGatePilotCaptureTests
    {
        const int W = 1920, H = 1080;
        static readonly (string name, Vector3 eye, Vector3 target, float fov)[] Views = {
            ("outside-context",new(7,2.6f,47),new(0,1.9f,57.5f),55),
            ("low-amoeba",new(0,.52f,54.9f),new(0,.72f,57.4f),62),
            ("badge-detail",new(.75f,1.5f,55.8f),new(0,1.35f,57.37f),43),
            ("inside-context",new(-5.5f,2.1f,65.5f),new(0,1.8f,57.5f),55),
        };
        [Serializable] sealed class Shot
        {
            public string name,kind,file,sha256,request="RenderPipeline.StandardRequest",format,graphicsFormat,tonemap,conversion,staticContract;
            public Vector3 eye,target; public Quaternion rotation; public float fov,aspect,renderScale,engineTime,rain,linearPeak;
            public float[] worldToCamera,projection; public int width,height,screenWidth,screenHeight,overlaySubmissions,activeOverlayCanvases;
            public bool hdr,post,tonemapActive,targetSrgb,actualOverlayModesRetained;
        }
        [Serializable] sealed class Report
        {
            public string phase,utc,device,api,sceneSha256,status,exception,settingsJson,matchingBeforeReport;
            public string scope="Fixed staged cameras, real scene and actual OverlayUI renderer lists after authored post. NPC/root physics is frozen without relocation. Scene-only images use linear FP16 and one gamma encode. This is not human gameplay or a performance test.";
            public string animationLimit="Authored static night lighting/fog/rain strength and camera/settings contracts match; actual engine time, animated water/rain/grain phase and NPC poses are recorded, not claimed pixel-identical between runs.";
            public bool restorationVerified,matchedBeforeStaticContracts; public List<string> restorationFailures=new(); public List<Shot> shots=new(); public string[] actorPoses; public int moviesBefore,moviesAfter,pendingBefore,pendingAfter;
        }
        sealed class CamState
        {
            readonly string name;readonly Camera c;readonly bool enabled,hdr,msaa;readonly Vector3 p;readonly Quaternion q;readonly float fov,aspect,near,far;readonly Rect rect;readonly RenderTexture target;
            public CamState(Camera value){name=value.name+"/"+value.cameraType;c=value;enabled=c.enabled;hdr=c.allowHDR;msaa=c.allowMSAA;p=c.transform.position;q=c.transform.rotation;fov=c.fieldOfView;aspect=c.aspect;near=c.nearClipPlane;far=c.farClipPlane;rect=c.rect;target=c.targetTexture;}
            public void Restore(){if(!c)return;if(c.enabled!=enabled)c.enabled=enabled;if(c.allowHDR!=hdr)c.allowHDR=hdr;if(c.allowMSAA!=msaa)c.allowMSAA=msaa;if(c.transform.position!=p||c.transform.rotation!=q)c.transform.SetPositionAndRotation(p,q);if(c.fieldOfView!=fov)c.fieldOfView=fov;if(c.aspect!=aspect)c.aspect=aspect;if(c.nearClipPlane!=near)c.nearClipPlane=near;if(c.farClipPlane!=far)c.farClipPlane=far;if(c.rect!=rect)c.rect=rect;if(c.targetTexture!=target)c.targetTexture=target;}
            public string Diff()=>!c?"camera destroyed: "+name:"camera "+name+" enabled "+enabled+"/"+c.enabled+" HDR "+hdr+"/"+c.allowHDR+" MSAA "+msaa+"/"+c.allowMSAA+" pos "+p.ToString("F8")+"/"+c.transform.position.ToString("F8")+" rot "+q.ToString("F8")+"/"+c.transform.rotation.ToString("F8")+" fov "+fov+"/"+c.fieldOfView+" aspect "+aspect+"/"+c.aspect+" clip "+near+","+far+"/"+c.nearClipPlane+","+c.farClipPlane+" rect "+rect+"/"+c.rect+" target "+(target?target.name:"null")+"/"+(c.targetTexture?c.targetTexture.name:"null");
            public bool Matches()=>c&&c.enabled==enabled&&c.allowHDR==hdr&&c.allowMSAA==msaa&&c.transform.position==p&&c.transform.rotation==q&&c.fieldOfView==fov&&c.aspect==aspect&&c.nearClipPlane==near&&c.farClipPlane==far&&c.rect==rect&&c.targetTexture==target;
        }
        sealed class LampState
        {
            public LampLights lamp; public bool enabled; public readonly List<(FieldInfo field,object original,Array contents)> fields=new();
            public LampState(LampLights value){lamp=value;enabled=value.enabled;foreach(var f in typeof(LampLights).GetFields(BindingFlags.Instance|BindingFlags.NonPublic)){var v=f.GetValue(value);fields.Add((f,v,v is Array a?(Array)a.Clone():null));}}
            public void Restore(){if(!lamp)return;foreach(var f in fields){if(f.contents!=null)Array.Copy(f.contents,(Array)f.original,f.contents.Length);f.field.SetValue(lamp,f.original);}lamp.enabled=enabled;}
        }
        [UnityTest,Timeout(180000)]
        public IEnumerator CaptureFixedGateViewsWithActualOverlayHudAndHdrScene()
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)Assert.Fail("Gate comparison requires actual GPU rendering.");
            string phase=Argument("-gatePilotCapturePhase");Assert.IsTrue(phase=="before"||phase=="after","Use explicit -gatePilotCapturePhase before|after.");
            yield return AutoTest.LoadCity();
            int pilot=Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Count(r=>r.sharedMaterial&&r.sharedMaterial.name=="CityGatePilot_SharedPBR");
            Assert.AreEqual(phase=="before"?0:2,pilot,"Capture the old saved scene BEFORE regeneration, then the actual integrated scene AFTER.");
            var oldSettings=GraphicsPreset.CaptureSettings();
            var main=Camera.main;Assert.IsNotNull(main);var cameras=Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(c=>new CamState(c)).ToArray();
            var cameraEnables=Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(c=>(c,c.enabled)).ToArray();
            var brains=Object.FindObjectsByType<Behaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(b=>b.GetType().FullName=="Unity.Cinemachine.CinemachineBrain").Select(b=>(b,b.enabled)).ToArray();
            var animators=Object.FindObjectsByType<Animator>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(a=>(a,a.speed)).ToArray();
            var lights=Object.FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(l=>(l,l.enabled,l.intensity,l.color)).ToArray();
            var lamps=Object.FindObjectsByType<LampLights>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(l=>new LampState(l)).ToArray();
            var rainSystems=Object.FindObjectsByType<RainSystem>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(r=>(r,r.enabled)).ToArray();
            var particles=Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(p=>(p,p.isPlaying,p.isPaused)).ToArray();
            var canvases=Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(c=>(c,c.enabled,c.renderMode,c.worldCamera,c.planeDistance,c.scaleFactor,rect:(RectTransform)c.transform,position:c.transform.localPosition,rotation:c.transform.localRotation,scale:c.transform.localScale,size:((RectTransform)c.transform).sizeDelta)).ToArray();
            var hosts=HostCharacter.All.Where(h=>h).Select(h=>(root:h.transform,position:h.transform.position,rotation:h.transform.rotation)).ToArray();
            var blobs=PossessionManager.Instance.Agents.Where(a=>a.Blob).Select(a=>(root:a.Blob.transform,position:a.Blob.transform.position,rotation:a.Blob.transform.rotation)).ToArray();
            var colliders=Object.FindObjectsByType<Collider>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(c=>(c,c.enabled,c.isTrigger,c.bounds)).ToArray();
            var flags=new HashSet<string>(RunState.Flags);int mission=RunState.MissionIndex;float playTime=RunState.PlayTime;var gameState=GameManager.Instance.State;
            bool oldLock=PossessionManager.Instance.InputLocked;float oldScale=Time.timeScale;var oldRandom=UnityEngine.Random.state;
            var report=new Report{phase=phase,utc=DateTime.UtcNow.ToString("O"),device=SystemInfo.graphicsDeviceName,api=SystemInfo.graphicsDeviceType.ToString(),settingsJson=GraphicsSettingsStore.Serialize(GraphicsPreset.CaptureSettings()),moviesBefore=CinematicFilmLifetime.ResourceCount,pendingBefore=CinematicFilmLifetime.PendingCount,
                actorPoses=hosts.Select(h=>h.root.name+"|"+h.position.ToString("F6")+"|"+h.rotation.ToString("F6")).ToArray()};
            string directory=Path.GetFullPath(Path.Combine(Application.dataPath,"..","Logs","city-gate-pilot","captures",phase+"-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff")));Directory.CreateDirectory(directory);
#if UNITY_EDITOR
            report.sceneSha256=Sha(Path.Combine(Application.dataPath,"Parasite/Scenes/City.unity"));
#endif
            GraphicsPreset.ApplySettings(GraphicsSettingsData.ForPreset(2),save:false);report.settingsJson=GraphicsSettingsStore.Serialize(GraphicsPreset.CaptureSettings());
            var go=new GameObject("FIXED Gate QA camera - not player relocation");var camera=go.AddComponent<Camera>();camera.CopyFrom(main);camera.enabled=false;camera.targetTexture=null;camera.aspect=(float)W/H;camera.nearClipPlane=.04f;camera.farClipPlane=900;camera.rect=new Rect(0,0,1,1);camera.cullingMask|=1<<5;
            var extra=go.AddComponent<UniversalAdditionalCameraData>();var oldExtra=main.GetUniversalAdditionalCameraData();extra.renderPostProcessing=true;extra.renderShadows=true;extra.volumeLayerMask=oldExtra.volumeLayerMask;extra.volumeTrigger=camera.transform;GraphicsPreset.ApplyCamera(camera);
            try
            {
                Time.timeScale=0;PossessionManager.Instance.InputLocked=true;
                foreach(var value in cameraEnables)value.c.enabled=false;foreach(var value in brains)value.b.enabled=false;foreach(var value in animators)value.a.speed=0;
                foreach(var value in lamps)value.lamp.enabled=false;foreach(var value in rainSystems)value.r.enabled=false;foreach(var value in particles)if(value.isPlaying)value.p.Pause(false);
                Assert.AreEqual(0,report.moviesBefore);Assert.AreEqual(0,report.pendingBefore);
                foreach(var view in Views)
                {
                    camera.transform.SetPositionAndRotation(view.eye,Quaternion.LookRotation(view.target-view.eye));camera.fieldOfView=view.fov;
                    // Invoke the unchanged normal High lamp-selection policy, not new authored powers/colors.
                    foreach(var value in lamps)value.lamp.Select(view.eye,instant:true);
                    foreach(var value in canvases)value.c.enabled=value.enabled;
                    Capture(camera,extra,view.name,view.target,"ACTUAL-OVERLAY-HUD",true,canvases.Where(c=>c.enabled&&c.renderMode==RenderMode.ScreenSpaceOverlay&&c.c.gameObject.activeInHierarchy).Select(c=>c.c).ToArray(),directory,report);
                    foreach(var value in canvases)value.c.enabled=false;
                    Capture(camera,extra,view.name,view.target,"SCENE-ONLY-HDR-QA",false,Array.Empty<Canvas>(),directory,report);
                }
                Assert.IsTrue(hosts.All(h=>h.root.position==h.position&&h.root.rotation==h.rotation)&&blobs.All(h=>h.root.position==h.position&&h.root.rotation==h.rotation),"Actor/body transforms remain frozen; no relocation is used for capture.");
                if(phase=="after")
                {
                    string pointer=Path.Combine(Directory.GetParent(directory).FullName,"before-latest.json");Assert.IsTrue(File.Exists(pointer),"A completed before capture must precede scene regeneration.");
                    var before=JsonUtility.FromJson<Report>(File.ReadAllText(File.ReadAllText(pointer).Trim()));report.matchingBeforeReport=File.ReadAllText(pointer).Trim();
                    report.matchedBeforeStaticContracts=before.shots.Count==report.shots.Count&&report.shots.All(shot=>before.shots.Any(b=>b.name==shot.name&&b.kind==shot.kind&&b.staticContract==shot.staticContract));
                    Assert.IsTrue(report.matchedBeforeStaticContracts,"Each actual pair must match camera/settings/static-light/fog/rain contracts. Animated phase is recorded separately.");
                }
                report.status="PASS";
            }
            catch(Exception e){report.status="FAIL";report.exception=e.ToString();throw;}
            finally
            {
                foreach(var value in lamps)value.Restore();foreach(var value in lights)if(value.l){value.l.enabled=value.enabled;value.l.intensity=value.intensity;value.l.color=value.color;}
                foreach(var value in rainSystems)if(value.r)value.r.enabled=value.enabled;
                foreach(var value in particles)if(value.p){if(value.isPlaying)value.p.Play(false);else if(value.isPaused)value.p.Pause(false);}
                foreach(var value in animators)if(value.a)value.a.speed=value.speed;
                foreach(var value in canvases)if(value.c){value.c.enabled=value.enabled;value.c.scaleFactor=value.scaleFactor;value.rect.localPosition=value.position;value.rect.localRotation=value.rotation;value.rect.localScale=value.scale;value.rect.sizeDelta=value.size;Assert.AreEqual(value.renderMode,value.c.renderMode,"The actual overlay canvas mode was never converted.");}
                foreach(var value in brains)if(value.b)value.b.enabled=value.enabled;
                if(go)Object.Destroy(go);Time.timeScale=oldScale;PossessionManager.Instance.InputLocked=oldLock;UnityEngine.Random.state=oldRandom;
                GraphicsPreset.ApplySettings(oldSettings,save:false);foreach(var value in cameras)value.Restore();
                report.moviesAfter=CinematicFilmLifetime.ResourceCount;report.pendingAfter=CinematicFilmLifetime.PendingCount;
                report.restorationVerified=lights.All(v=>v.l&&v.l.enabled==v.enabled&&v.l.intensity==v.intensity&&v.l.color==v.color)&&rainSystems.All(v=>v.r&&v.r.enabled==v.enabled)&&cameras.All(c=>c.Matches())&&canvases.All(v=>v.c&&v.c.enabled==v.enabled&&v.c.renderMode==v.renderMode&&v.c.worldCamera==v.worldCamera&&v.c.planeDistance==v.planeDistance&&v.c.scaleFactor==v.scaleFactor&&v.rect.localPosition==v.position&&v.rect.localRotation==v.rotation&&v.rect.localScale==v.scale&&v.rect.sizeDelta==v.size)
                    &&brains.All(v=>v.b&&v.b.enabled==v.enabled)&&animators.All(v=>v.a&&v.a.speed==v.speed)&&hosts.All(h=>h.root&&h.root.position==h.position&&h.root.rotation==h.rotation)&&blobs.All(h=>h.root&&h.root.position==h.position&&h.root.rotation==h.rotation)
                    &&colliders.All(v=>v.c&&v.c.enabled==v.enabled&&v.c.isTrigger==v.isTrigger&&v.c.bounds.Equals(v.bounds))&&flags.SetEquals(RunState.Flags)&&mission==RunState.MissionIndex&&playTime==RunState.PlayTime&&gameState==GameManager.Instance.State&&Time.timeScale==oldScale&&PossessionManager.Instance.InputLocked==oldLock
                    &&report.moviesAfter==report.moviesBefore&&report.pendingAfter==report.pendingBefore&&GraphicsPreset.CaptureSettings().Equals(oldSettings);
                foreach(var c in cameras)if(!c.Matches())report.restorationFailures.Add(c.Diff());
                foreach(var v in canvases)if(!v.c||v.c.enabled!=v.enabled||v.c.renderMode!=v.renderMode||v.c.worldCamera!=v.worldCamera||v.c.planeDistance!=v.planeDistance||v.c.scaleFactor!=v.scaleFactor)report.restorationFailures.Add("canvas "+(v.c?v.c.name:"destroyed")+" enabled="+v.enabled+"/"+(v.c&&v.c.enabled)+" scale="+v.scaleFactor+"/"+(v.c?v.c.scaleFactor:0)+" mode="+v.renderMode+"/"+(v.c?v.c.renderMode:0));
                foreach(var v in lights)if(!v.l||v.l.enabled!=v.enabled||v.l.intensity!=v.intensity||v.l.color!=v.color)report.restorationFailures.Add("light "+(v.l?v.l.name:"destroyed")+" enabled="+v.enabled+"/"+(v.l&&v.l.enabled)+" intensity="+v.intensity+"/"+(v.l?v.l.intensity:0));
                foreach(var h in hosts.Concat(blobs))if(!h.root||h.root.position!=h.position||h.root.rotation!=h.rotation)report.restorationFailures.Add("actor "+(h.root?h.root.name:"destroyed")+" pos="+h.position.ToString("F8")+"/"+(h.root?h.root.position.ToString("F8"):"null")+" rot="+h.rotation.ToString("F8")+"/"+(h.root?h.root.rotation.ToString("F8"):"null"));
                foreach(var v in colliders)if(!v.c||v.c.enabled!=v.enabled||v.c.isTrigger!=v.isTrigger||!v.c.bounds.Equals(v.bounds))report.restorationFailures.Add("collider "+(v.c?v.c.name:"destroyed")+" enabled="+v.enabled+"/"+(v.c&&v.c.enabled)+" bounds="+v.bounds+"/"+(v.c?v.c.bounds:default));
                if(!flags.SetEquals(RunState.Flags)||mission!=RunState.MissionIndex||playTime!=RunState.PlayTime||gameState!=GameManager.Instance.State)report.restorationFailures.Add("game mission="+mission+"/"+RunState.MissionIndex+" playTime="+playTime+"/"+RunState.PlayTime+" state="+gameState+"/"+GameManager.Instance.State);
                if(!GraphicsPreset.CaptureSettings().Equals(oldSettings))report.restorationFailures.Add("graphics config differs");
                if(report.moviesAfter!=report.moviesBefore||report.pendingAfter!=report.pendingBefore)report.restorationFailures.Add("media counts differ");
                if(!report.restorationVerified)report.status="FAIL_RESTORATION";
                string file=Path.Combine(directory,"capture.json");File.WriteAllText(file,JsonUtility.ToJson(report,true)+"\n");
                if(report.status=="PASS")File.WriteAllText(Path.Combine(Directory.GetParent(directory).FullName,phase+"-latest.json"),file+"\n");
                Assert.IsTrue(report.restorationVerified,"Full touched camera/UI/light/cache/animation/input/settings/physics/mission/media state is restored.");
            }
            Debug.Log("[GatePilotCapture] "+phase+" PASS8actual1920x1080images StandardRequest/authoredHDRpost/actualoverlayrendererlist; no bodyteleport; "+directory);
        }
        static void Capture(Camera camera,UniversalAdditionalCameraData extra,string name,Vector3 target,string kind,bool hud,Canvas[] overlay,string directory,Report report)
        {
            var rt=new RenderTexture(W,H,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear){antiAliasing=1};var read=new Texture2D(W,H,TextureFormat.RGBAHalf,false,true);var png=new Texture2D(W,H,TextureFormat.RGB24,false,false);var old=RenderTexture.active;int overlayDraws=0;
            void Overlay(ScriptableRenderContext context,Camera rendered)
            {
                if(!hud||rendered!=camera)return;
                var command=CommandBufferPool.Get("Actual OverlayUI after authored scene post");
                try{command.SetRenderTarget(rt);command.SetViewport(new Rect(0,0,W,H));command.DrawRendererList(context.CreateUIOverlayRendererList(camera));context.ExecuteCommandBuffer(command);context.Submit();overlayDraws++;}
                finally{CommandBufferPool.Release(command);}
            }
            try
            {
                Assert.IsTrue(rt.Create()&&!rt.sRGB&&rt.format==RenderTextureFormat.ARGBHalf);camera.targetTexture=rt;Canvas.ForceUpdateCanvases();
                if(hud)Assert.IsNotEmpty(overlay,"Real active OverlayUI is required; no recreated/fake HUD.");
                RenderPipelineManager.endCameraRendering+=Overlay;
                var request=new RenderPipeline.StandardRequest{destination=rt};Assert.IsTrue(RenderPipeline.SupportsRenderRequest(camera,request));
                RenderPipeline.SubmitRenderRequest(camera,request);RenderPipeline.SubmitRenderRequest(camera,request);
                RenderPipeline.SubmitRenderRequest(camera,request);
                var stack=extra.volumeStack??VolumeManager.instance.stack;var tone=stack.GetComponent<Tonemapping>();Assert.IsTrue(tone&&tone.active&&tone.IsActive(),"Actual post-request tone mapping must be active.");
                Assert.IsTrue(camera.allowHDR&&extra.renderPostProcessing);if(hud)Assert.That(overlayDraws,Is.GreaterThan(0));
                RenderTexture.active=rt;read.ReadPixels(new Rect(0,0,W,H),0,0);read.Apply();var colors=read.GetPixels();float peak=0,min=100,max=0;
                for(int i=0;i<colors.Length;i++){var c=colors[i];if(float.IsNaN(c.r)||float.IsInfinity(c.r)||float.IsNaN(c.g)||float.IsInfinity(c.g)||float.IsNaN(c.b)||float.IsInfinity(c.b))throw new InvalidOperationException("Nonfinite HDR readback");peak=Mathf.Max(peak,Mathf.Max(c.r,Mathf.Max(c.g,c.b)));min=Mathf.Min(min,c.grayscale);max=Mathf.Max(max,c.grayscale);colors[i]=c.gamma;colors[i].a=1;}
                Assert.That(max-min,Is.GreaterThan(.0001f),"Finite HDR image must contain actual spatial content; night closeups are not required to have bright pixels.");png.SetPixels(colors);png.Apply();string file=Path.Combine(directory,kind+"-"+name+".png");File.WriteAllBytes(file,png.EncodeToPNG());
                var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                string light=string.Join(";",Object.FindObjectsByType<Light>(FindObjectsSortMode.None).OrderBy(l=>l.name,StringComparer.Ordinal).Select(l=>l.name+"|"+l.type+"|"+l.transform.position.ToString("F5")+"|"+l.transform.rotation.ToString("F5")+"|"+l.enabled+"|"+l.intensity.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"|"+l.color));
                string staticContract=ShaText(name+kind+camera.transform.position.ToString("F6")+camera.transform.rotation.ToString("F6")+camera.fieldOfView+camera.aspect+report.settingsJson+light+RenderSettings.fog+RenderSettings.fogDensity+RenderSettings.fogColor+RenderSettings.ambientMode+RenderSettings.ambientIntensity+Shader.GetGlobalFloat("_ParasiteRain"));
                report.shots.Add(new Shot{name=name,kind=kind,file=file,sha256=Sha(file),eye=camera.transform.position,target=target,rotation=camera.transform.rotation,fov=camera.fieldOfView,aspect=camera.aspect,worldToCamera=Matrix(camera.worldToCameraMatrix),projection=Matrix(camera.projectionMatrix),width=W,height=H,screenWidth=Screen.width,screenHeight=Screen.height,renderScale=pipeline.renderScale,engineTime=Time.time,rain=Shader.GetGlobalFloat("_ParasiteRain"),linearPeak=peak,hdr=camera.allowHDR,post=extra.renderPostProcessing,tonemap=tone.mode.value.ToString(),tonemapActive=tone.active&&tone.IsActive(),targetSrgb=rt.sRGB,format=rt.format.ToString(),graphicsFormat=rt.graphicsFormat.ToString(),overlaySubmissions=overlayDraws,activeOverlayCanvases=overlay.Length,actualOverlayModesRetained=overlay.All(c=>c.renderMode==RenderMode.ScreenSpaceOverlay),conversion="Postprocessed linearFP16 plus native OverlayUI afterpost (HUDkind only), Color.gamma once toRGB24PNG; noCPUtone map",staticContract=staticContract});
            }
            finally{RenderPipelineManager.endCameraRendering-=Overlay;camera.targetTexture=null;RenderTexture.active=old;rt.Release();Object.Destroy(rt);Object.Destroy(read);Object.Destroy(png);}
        }
        static string Argument(string key){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,key);return i>=0&&i+1<a.Length?a[i+1]:"";}
        static float[] Matrix(Matrix4x4 m){var a=new float[16];for(int i=0;i<16;i++)a[i]=m[i];return a;}
        static string Sha(string path){using var sha=SHA256.Create();using var f=File.OpenRead(path);return BitConverter.ToString(sha.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
        static string ShaText(string text){using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}
        [UnityTearDown] public IEnumerator Cleanup(){GameInput.Simulated=false;GameInput.SimMove=Vector2.zero;Time.timeScale=1;RunState.NewGame();SceneManager.LoadScene(GameManager.MenuScene);yield return null;}
    }
}
