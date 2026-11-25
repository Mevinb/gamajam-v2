using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Parasite.Missions
{
    /// <summary>One native capture per enabled quality interval; only completed image-validated cubes influence the river.</summary>
    public sealed class RiverReflectionCapture : MonoBehaviour, IDisposable
    {
        const float CaptureTimeout = 8f;
        static readonly Vector3 InfluenceSize = new(430,70,260), InfluenceCenter = new(0,12,-45);
        sealed class Ticket
        {
            public int epoch, renderId;
            public float started;
            public ReflectionProbe probe;
            public RenderTexture target;
        }
        ReflectionProbe publisher;
        Cubemap published;
        bool sourceCreatedBeforeRetire, sourceCreatedAfterRetire;
        Ticket pending;
        Vector3 origin;
        bool configured, started, knownPolicy, wanted, disposed, imageValidated;
        int epoch, publishedEpoch = -1, renderId = -1, captureCount, publishCount;
        string status = "not configured";

        public int Epoch => epoch;
        public int PublishedEpoch => publishedEpoch;
        public int RenderId => renderId;
        public int CaptureCount => captureCount;
        public int PublishCount => publishCount;
        public bool HasPendingCapture => pending != null;
        public RenderTexture PendingTarget => pending?.target;
        public ReflectionProbe PendingProbe => pending?.probe;
        public Cubemap PublishedTexture => published;
        public ReflectionProbe Publisher => publisher;
        public bool Ready => !disposed && isActiveAndEnabled && WantsCapture && imageValidated &&
            publishedEpoch == epoch && publisher && publisher.enabled && published && publisher.customBakedTexture == published;
        public bool UsingFallback => !Ready;
        public string Status => (pending == null ? status : $"capturing epoch {pending.epoch}, request {pending.renderId}, {Time.realtimeSinceStartup-pending.started:0.00}s; sky/default fallback") +
            $" | active={isActiveAndEnabled} disposed={disposed} preset={GraphicsPreset.Current} native={QualitySettings.realtimeReflectionProbes} device={SystemInfo.graphicsDeviceType} image={imageValidated} epoch={epoch}/{publishedEpoch} publisherExists={(bool)publisher} publisherEnabled={(publisher&&publisher.enabled)} textureExists={(bool)published} textureBound={(publisher&&published&&publisher.customBakedTexture==published)} captureCreatedBeforeRetire={sourceCreatedBeforeRetire} afterRetire={sourceCreatedAfterRetire}";
        bool WantsCapture => GraphicsPreset.Current >= 2 && QualitySettings.realtimeReflectionProbes &&
            SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;

        public void Configure(Vector3 localOrigin)
        {
            if (configured) throw new InvalidOperationException("River reflection capture already configured.");
            configured = true; origin = localOrigin;
            var go = new GameObject("Riverside local reflection probe"); go.transform.SetParent(transform,false);go.transform.localPosition=origin;
            publisher = go.AddComponent<ReflectionProbe>(); publisher.enabled = false;
            publisher.mode = ReflectionProbeMode.Custom; publisher.hdr = true;
            publisher.size = InfluenceSize; publisher.center = InfluenceCenter;
            publisher.blendDistance = 15; publisher.importance = 3; publisher.boxProjection = false;
            ApplyPolicy();
        }
        void OnEnable() { if(disposed)return;GraphicsPreset.Applied += OnPreset; if(configured&&started)ApplyPolicy(); }
        void OnDisable() { GraphicsPreset.Applied -= OnPreset; Invalidate(); knownPolicy=false; }
        void OnPreset(int _) => ApplyPolicy();
        IEnumerator Start() { yield return null; started=true; ApplyPolicy(); }
        void Update()
        {
            ApplyPolicy();
            var ticket = pending;
            if(ticket==null)return;
            if(ticket.epoch!=epoch||!WantsCapture){Invalidate();knownPolicy=false;return;}
            if(!ticket.probe||!ticket.target){Fail(ticket,"capture resources disappeared");return;}
            if(ticket.probe.IsFinishedRendering(ticket.renderId))
            {
                Cubemap copy=null;
                try
                {
                    // A realtime probe can release its render-target backing when retired. Publish an independently owned immutable cube.
                    if((SystemInfo.copyTextureSupport&CopyTextureSupport.RTToTexture)==0)throw new InvalidOperationException("RT-to-cubemap GPU copy unsupported");
                    copy=new Cubemap(ticket.target.width,TextureFormat.RGBAHalf,true){name="Riverside published epoch "+epoch,hideFlags=HideFlags.DontSave};
                    copy.Apply(false,true); // Allocate/discard the CPU buffer before the GPU copy, never overwrite the copy afterward.
                    Graphics.CopyTexture(ticket.target,copy);
                    if(!ValidateImage(copy,out string error))throw new InvalidOperationException("completed publication image rejected: "+error);
                    if(ticket.epoch!=epoch||ticket!=pending||!WantsCapture){DestroyOwned(copy);return;}
                    var source=ticket.target;sourceCreatedBeforeRetire=source.IsCreated();
                    published=copy;copy=null;pending=null;
                    publisher.customBakedTexture=published;publisher.enabled=true;
                    publishedEpoch=epoch;imageValidated=true;publishCount++;
                    status=$"ready: copied image-validated epoch {epoch}, native request {ticket.renderId}";
                    Retire(ticket);sourceCreatedAfterRetire=source&&source.IsCreated();
                }
                catch(Exception error){DestroyOwned(copy);Fail(ticket,error.Message);}
            }
            else if(Time.realtimeSinceStartup-ticket.started>CaptureTimeout)
                Fail(ticket,$"native request {ticket.renderId} timed out after {CaptureTimeout:0}s");
        }
        void ApplyPolicy()
        {
            if(disposed||!configured||!started||!isActiveAndEnabled)return;
            bool next=WantsCapture;
            if(knownPolicy&&wanted==next)return;
            knownPolicy=true;wanted=next;Invalidate();
            status=SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null?"no graphics device; sky/default fallback":"quality disabled; sky/default fallback";
            if(next)BeginCapture();
        }
        void BeginCapture()
        {
            var ticket=new Ticket{epoch=epoch,renderId=-1,started=Time.realtimeSinceStartup};
            try
            {
                var go=new GameObject("Riverside pending reflection capture");go.transform.SetParent(transform,false);go.transform.localPosition=origin;
                ticket.probe=go.AddComponent<ReflectionProbe>();
                var probe=ticket.probe;probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.ViaScripting;
                probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.IndividualFaces;probe.resolution=256;
                // Only the capture origin controls the cubemap camera. Keep this temporary probe's sampling volume outside all gameplay.
                probe.size=Vector3.one;probe.center=new Vector3(0,100000,0);probe.blendDistance=1;
                probe.hdr=true;probe.nearClipPlane=.15f;probe.farClipPlane=220;
                probe.clearFlags=ReflectionProbeClearFlags.Skybox;probe.cullingMask=~((1<<4)|(1<<2)|(1<<30));
                ticket.target=new RenderTexture(256,256,16,RenderTextureFormat.ARGBHalf)
                {name="Riverside capture epoch "+epoch,dimension=TextureDimension.Cube,useMipMap=true,autoGenerateMips=false,hideFlags=HideFlags.DontSave};
                if(!ticket.target.Create())throw new InvalidOperationException("HDR cubemap allocation failed");
                Clear(ticket.target);probe.realtimeTexture=ticket.target;
                pending=ticket;captureCount++;
                renderId=ticket.renderId=probe.RenderProbe(ticket.target);
                if(ticket.renderId<0)Fail(ticket,"native render request was rejected");
            }
            catch(Exception error){pending=ticket;Fail(ticket,"capture setup failed: "+error.Message);}
        }
        void Fail(Ticket ticket,string failure)
        {
            if(ticket!=pending)return;
            pending=null;Retire(ticket);
            status=failure+"; sky/default fallback until quality is re-enabled";
            Debug.LogWarning("[RiverReflection] "+status);
        }
        void Invalidate()
        {
            epoch++;imageValidated=false;publishedEpoch=-1;renderId=-1;
            var ticket=pending;pending=null;Retire(ticket);
            if(publisher){publisher.enabled=false;publisher.customBakedTexture=null;}
            DestroyOwned(published);published=null;
        }
        static void Retire(Ticket ticket)
        {
            if(ticket==null)return;
            if(ticket.probe){ticket.probe.enabled=false;ticket.probe.realtimeTexture=null;DestroyOwned(ticket.probe.gameObject);}
            // Deferred destruction lets Unity retire a native request without releasing a live target prematurely.
            DestroyOwned(ticket.target);ticket.target=null;ticket.probe=null;
        }
        static void DestroyOwned(UnityEngine.Object value)
        {
            if(!value)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);
        }
        static void Clear(RenderTexture cube)
        {
            var old=RenderTexture.active;
            try{for(int mip=0;mip<cube.mipmapCount;mip++)for(int face=0;face<6;face++){Graphics.SetRenderTarget(cube,mip,(CubemapFace)face,0);GL.Clear(true,true,Color.black);}}
            finally{RenderTexture.active=old;}
        }
        public static bool ValidateImage(Texture cube,out string error,Vector4? hdrDecode=null,bool requireConvolvedMips=true)
        {
            error=null;
            if(!cube||cube.dimension!=TextureDimension.Cube||cube.mipmapCount<1||(requireConvolvedMips&&cube.mipmapCount<2)){error="missing mipmapped cube";return false;}
            try
            {
                using(var readback=new ReflectionCubeReadback(cube,hdrDecode??new Vector4(1,1,0,0)))
                {
                    for(int mip=0;mip<Mathf.Min(2,cube.mipmapCount);mip++)
                    {
                        float energy=0;
                        for(int face=0;face<6;face++)foreach(var color in readback.Read((CubemapFace)face,mip))
                        {
                            if(!Finite(color.r)||!Finite(color.g)||!Finite(color.b)||!Finite(color.a)){error=$"nonfinite face {face}, mip {mip}";return false;}
                            energy+=Mathf.Max(0,color.r)+Mathf.Max(0,color.g)+Mathf.Max(0,color.b);
                        }
                        if(energy<.0001f){error="black mip "+mip;return false;}
                    }
                }
                return true;
            }
            catch(Exception exception){error=exception.Message;return false;}
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        public void Dispose()
        {
            if(disposed)return;disposed=true;GraphicsPreset.Applied-=OnPreset;Invalidate();
            if(publisher)DestroyOwned(publisher.gameObject);publisher=null;
            status="disposed; sky/default fallback";
        }
        void OnDestroy()=>Dispose();
    }
}
