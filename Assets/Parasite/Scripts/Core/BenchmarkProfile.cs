using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;

namespace Parasite.Testing
{
    /// <summary>Optional measured-route diagnostics. Missing/stripped markers are reported, never substituted with invented timings.</summary>
    public sealed class BenchmarkProfile : IDisposable
    {
        static readonly (string group,string[] aliases)[] Wanted={
            ("main-thread",new[]{"Main Thread","CPU Main Thread Frame Time"}),
            ("render-thread",new[]{"Render Thread","CPU Render Thread Frame Time"}),
            ("present-wait",new[]{"Gfx.WaitForPresentOnGfxThread"}),
            ("present",new[]{"Gfx.PresentFrame","GfxDeviceVulkan.Present","Gfx.Present"}),
            ("target-fps-wait",new[]{"WaitForTargetFPS"}),
            ("render-thread-wait",new[]{"Gfx.WaitForRenderThread"}),
            ("player-loop",new[]{"PlayerLoop"}),
            ("scripts-update",new[]{"Update.ScriptRunBehaviourUpdate","ScriptRunBehaviourUpdate"}),
            ("scripts-late",new[]{"PreLateUpdate.ScriptRunBehaviourLateUpdate","ScriptRunBehaviourLateUpdate"}),
            ("scripts-fixed",new[]{"FixedUpdate.ScriptRunBehaviourFixedUpdate","ScriptRunBehaviourFixedUpdate"}),
            ("physics",new[]{"Physics.Simulate","Physics.Processing"}),
            ("animation",new[]{"Animation.Update","Animator.Update","AnimatorControllerPlayable.Update"}),
            ("culling",new[]{"CullScriptable","CullResults.Cull","ScriptableRenderContext.Cull"}),
            ("render-pipeline",new[]{"UniversalRenderPipeline.RenderSingleCameraInternal","UniversalRenderPipeline.Render","RenderPipelineManager.DoRenderLoop_Internal","RenderPipeline.Render"}),
            ("draw",new[]{"RenderLoop.Draw","RenderLoop.DrawSRPBatcher"}),
            ("draw-calls",new[]{"Draw Calls Count","Draw Call Count"}),
            ("batches",new[]{"Batches Count","Batch Count"}),
            ("triangles",new[]{"Triangles Count","Triangle Count"})
        };
        [Serializable] public sealed class Marker
        {
            public string group,name,category,unit,dataType,flags,status,error;
            public string[] requestedAliases;
            public int samples,noDataFrames;public bool millisecondsAvailable;
            public double meanRaw,p99Raw,maxRaw,meanMs,p99Ms,maxMs;
        }
        sealed class Slot
        {
            public Marker report;
            public ProfilerRecorder recorder;
            public bool allocated,isDouble;
            public readonly List<double> values=new();
        }
        [Serializable] public sealed class CameraCount {public int id,callbacks;public string name,type;}
        [Serializable] sealed class Report
        {
            public string scope="Opt-in instrumentation; last completed recorder values, first profile sample per walk leg excluded. Nested marker times are not additive and include waits. SRP callback totals cover walk windows and their boundary frames, not screenshot waits.";
            public string utc,unity,api,gpu,district,preset,setupError,buildGuid,fullscreen,display,waylandDisplay,sessionType;
            public int width,height,routeReached,routeWaypoints,backgroundTrueSamples,backgroundFalseSamples;
            public bool isEditor,isBatchMode,runInBackground;
            public int baselineFrames,profileSamples,profileBoundarySkips,focusedSamples,unfocusedSamples,focusChanges;
            public int walkFrameDelta,walkRenderedFrameDelta,minRenderFrameInterval,maxRenderFrameInterval;
            public int minVsync,maxVsync,minTargetFps,maxTargetFps,availableHandles,validRecorders;
            public int gameCallbacks,reflectionCallbacks,otherCallbacks,callbackFrames,maxGameCallbacksPerFrame;
            public Marker[] markers;public CameraCount[] cameras;public string[] relatedAvailableMarkers;
        }
        readonly List<Slot> slots=new();
        readonly Dictionary<int,CameraCount> cameras=new();
        readonly Report report=new();
        bool disposed,measuring,skipBoundary,hasFocus;
        int legFrame,legRendered,callbackFrame=-1,gameThisFrame;
        bool previousFocus;

        public BenchmarkProfile()
        {
            report.utc=DateTime.UtcNow.ToString("O");report.unity=Application.unityVersion;
            report.api=SystemInfo.graphicsDeviceType.ToString();report.gpu=SystemInfo.graphicsDeviceName;
            report.display=Environment.GetEnvironmentVariable("DISPLAY");report.waylandDisplay=Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");report.sessionType=Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
            report.buildGuid=Application.buildGUID;report.width=Screen.width;report.height=Screen.height;report.fullscreen=Screen.fullScreenMode.ToString();
            report.isEditor=Application.isEditor;report.isBatchMode=Application.isBatchMode;report.runInBackground=Application.runInBackground;
            report.minRenderFrameInterval=report.minVsync=report.minTargetFps=int.MaxValue;
            report.maxRenderFrameInterval=report.maxVsync=report.maxTargetFps=int.MinValue;
            try
            {
                var handles=new List<ProfilerRecorderHandle>();ProfilerRecorderHandle.GetAvailable(handles);report.availableHandles=handles.Count;
                var descriptions=handles.Where(h=>h.Valid).Select(h=>(handle:h,description:ProfilerRecorderHandle.GetDescription(h))).ToArray();
                report.relatedAvailableMarkers=descriptions.Select(v=>v.description.Name).Where(Related).Distinct().OrderBy(v=>v).Take(96).ToArray();
                foreach(var wanted in Wanted)
                {
                    var marker=new Marker{group=wanted.group,requestedAliases=wanted.aliases,status="MISSING"};var slot=new Slot{report=marker};slots.Add(slot);
                    foreach(string alias in wanted.aliases)
                    {
                        var found=descriptions.Where(v=>v.description.Name==alias).OrderBy(v=>v.description.Category.Name).ToArray();
                        if(found.Length==0)continue;
                        var chosen=found[0];var d=chosen.description;
                        marker.name=d.Name;marker.category=d.Category.Name;marker.unit=d.UnitType.ToString();marker.dataType=d.DataType.ToString();marker.flags=d.Flags.ToString();
                        try
                        {
                            slot.recorder=new ProfilerRecorder(chosen.handle,1,ProfilerRecorderOptions.StartImmediately|ProfilerRecorderOptions.WrapAroundWhenCapacityReached|ProfilerRecorderOptions.SumAllSamplesInFrame);
                            slot.allocated=slot.recorder.Valid;slot.isDouble=marker.dataType.Contains("Double");
                            marker.status=slot.allocated&&slot.recorder.IsRunning?"VALID_NO_SAMPLES":"INVALID_RECORDER";
                            if(slot.allocated&&slot.recorder.IsRunning)report.validRecorders++;
                        }
                        catch(Exception error){marker.status="RECORDER_START_FAILED";marker.error=error.Message;}
                        break;
                    }
                }
            }
            catch(Exception error){report.setupError=error.Message;}
            RenderPipelineManager.beginCameraRendering+=OnCamera;
        }
        static bool Related(string value)=>!string.IsNullOrEmpty(value)&&(value.IndexOf("Thread",StringComparison.OrdinalIgnoreCase)>=0||value.IndexOf("Wait",StringComparison.OrdinalIgnoreCase)>=0||value.IndexOf("Present",StringComparison.OrdinalIgnoreCase)>=0||value.IndexOf("Physics",StringComparison.OrdinalIgnoreCase)>=0||value.IndexOf("Anim",StringComparison.OrdinalIgnoreCase)>=0||value.IndexOf("ScriptRun",StringComparison.OrdinalIgnoreCase)>=0||value.IndexOf("RenderPipeline",StringComparison.OrdinalIgnoreCase)>=0||value.IndexOf("Cull",StringComparison.OrdinalIgnoreCase)>=0);
        public void BeginLeg()
        {
            if(disposed)return;measuring=true;skipBoundary=true;legFrame=Time.frameCount;legRendered=Time.renderedFrameCount;
        }
        public void EndLeg()
        {
            if(!measuring)return;report.walkFrameDelta+=Time.frameCount-legFrame;report.walkRenderedFrameDelta+=Time.renderedFrameCount-legRendered;measuring=false;
        }
        public void Sample()
        {
            if(disposed||!measuring)return;
            if(skipBoundary){skipBoundary=false;report.profileBoundarySkips++;return;}
            report.profileSamples++;
            if(Application.runInBackground)report.backgroundTrueSamples++;else report.backgroundFalseSamples++;
            bool focused=Application.isFocused;if(focused)report.focusedSamples++;else report.unfocusedSamples++;
            if(hasFocus&&previousFocus!=focused)report.focusChanges++;previousFocus=focused;hasFocus=true;
            int interval=OnDemandRendering.renderFrameInterval;
            report.minRenderFrameInterval=Mathf.Min(report.minRenderFrameInterval,interval);report.maxRenderFrameInterval=Mathf.Max(report.maxRenderFrameInterval,interval);
            report.minVsync=Mathf.Min(report.minVsync,QualitySettings.vSyncCount);report.maxVsync=Mathf.Max(report.maxVsync,QualitySettings.vSyncCount);
            report.minTargetFps=Mathf.Min(report.minTargetFps,Application.targetFrameRate);report.maxTargetFps=Mathf.Max(report.maxTargetFps,Application.targetFrameRate);
            foreach(var slot in slots)
            {
                if(!slot.allocated||!slot.recorder.Valid||!slot.recorder.IsRunning||slot.recorder.Count==0){slot.report.noDataFrames++;continue;}
                double value=slot.isDouble?slot.recorder.LastValueAsDouble:slot.recorder.LastValue;
                if(double.IsNaN(value)||double.IsInfinity(value)){slot.report.noDataFrames++;continue;}
                slot.values.Add(value);
            }
        }
        void OnCamera(ScriptableRenderContext context,Camera camera)
        {
            if(disposed||!measuring||!camera)return;
            if(callbackFrame!=Time.frameCount){callbackFrame=Time.frameCount;gameThisFrame=0;report.callbackFrames++;}
            if(camera.cameraType==CameraType.Game){report.gameCallbacks++;gameThisFrame++;report.maxGameCallbacksPerFrame=Mathf.Max(report.maxGameCallbacksPerFrame,gameThisFrame);}
            else if(camera.cameraType==CameraType.Reflection)report.reflectionCallbacks++;else report.otherCallbacks++;
            int id=camera.GetInstanceID();if(!cameras.TryGetValue(id,out var row)){row=new CameraCount{id=id,name=camera.name,type=camera.cameraType.ToString()};cameras.Add(id,row);}row.callbacks++;
        }
        public void Write(string path,string district,string preset,int baselineFrames,int reached,int totalWaypoints)
        {
            EndLeg();report.district=district;report.preset=preset;report.baselineFrames=baselineFrames;report.routeReached=reached;report.routeWaypoints=totalWaypoints;
            foreach(var slot in slots)
            {
                var row=slot.report;row.samples=slot.values.Count;
                if(row.samples==0)continue;
                slot.values.Sort();row.status="VALID_SAMPLED";row.meanRaw=slot.values.Average();row.p99Raw=slot.values[Mathf.Clamp((int)(row.samples*.99),0,row.samples-1)];row.maxRaw=slot.values[^1];
                if(row.unit==ProfilerMarkerDataUnit.TimeNanoseconds.ToString()){row.millisecondsAvailable=true;row.meanMs=row.meanRaw/1e6;row.p99Ms=row.p99Raw/1e6;row.maxMs=row.maxRaw/1e6;}
            }
            if(report.profileSamples==0){report.minRenderFrameInterval=report.maxRenderFrameInterval=0;report.minVsync=report.maxVsync=0;report.minTargetFps=report.maxTargetFps=0;}
            report.validRecorders=slots.Count(s=>s.allocated&&s.recorder.Valid&&s.recorder.IsRunning);
            report.markers=slots.Select(s=>s.report).ToArray();report.cameras=cameras.Values.OrderBy(c=>c.id).ToArray();
            File.WriteAllText(path,JsonUtility.ToJson(report,true)+"\n");
        }
        public void Dispose()
        {
            if(disposed)return;EndLeg();disposed=true;RenderPipelineManager.beginCameraRendering-=OnCamera;
            foreach(var slot in slots)if(slot.allocated)slot.recorder.Dispose();
        }
    }
}
