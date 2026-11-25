using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Parasite.Missions;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Parasite.Testing
{
    /// <summary>Actual rendered floor/sea evidence plus physical/UV/PBR invariants; never a geometry-only graphics claim.</summary>
    public static class IslandVisualAcceptance
    {
        public static IEnumerator Run(Func<string,IEnumerator> capture=null)
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new InvalidOperationException("Island visual proof requires a real graphics device.");
            yield return IslandEntryAcceptance.EnterFromFerry(EndingOutcome.Peace,false,capture);
            var root=GameObject.Find(IslandDistrict.RootName);Check(root,"The actual island world exists.");
            Physics.SyncTransforms();
            var physical=root.transform.Find("Island physical shore").GetComponent<BoxCollider>();
            var paving=root.transform.Find("Island shore surface").GetComponent<MeshRenderer>();
            Check(Mathf.Abs(physical.bounds.max.y-.15f)<.001f&&Mathf.Abs(paving.bounds.max.y-.142f)<.001f,"Physical shore is unchanged while its visual underlay is8mm below authored floors: physical="+physical.bounds.max.y+", visual="+paving.bounds.max.y);
            Check(!root.transform.Find("Island physical shore").GetComponent<MeshRenderer>().enabled,"The coplanar physical primitive is not rendered.");
            var mesh=paving.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;var normals=mesh.normals;var uv=mesh.uv;
            float u0=float.PositiveInfinity,u1=float.NegativeInfinity,v0=float.PositiveInfinity,v1=float.NegativeInfinity;
            for(int i=0;i<vertices.Length;i++)if(normals[i].y>.5f){u0=Mathf.Min(u0,uv[i].x);u1=Mathf.Max(u1,uv[i].x);v0=Mathf.Min(v0,uv[i].y);v1=Mathf.Max(v1,uv[i].y);}
            Check(Mathf.Abs(u1-u0-104)<.01f&&Mathf.Abs(v1-v0-78)<.01f,"Generated shore uses physical one-metre UV tiles rather than stretching a normalized cube.");
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())foreach(var material in renderer.sharedMaterials)
                if(material&&material.name.Contains("island PBR")&&material.GetTexture("_MetallicGlossMap"))
                    Check(Mathf.Abs(material.GetFloat("_Smoothness")-1)<.001f,"Mapped roughness is not multiplied by an unrelated scalar smoothness.");
            var sea=root.transform.Find("Island tidal sea").GetComponent<MeshRenderer>();
            var cutout=root.GetComponent<SkylineWaterCutout>();Check(cutout&&cutout.Applied,"The island's actual sea footprint cuts the overlapping western skyline slab.");
            var proofCameraObject=new GameObject("Island geometry proof camera");var camera=proofCameraObject.AddComponent<Camera>();camera.enabled=false;
            var additional=camera.gameObject.AddComponent<UniversalAdditionalCameraData>();additional.renderPostProcessing=false;additional.antialiasing=AntialiasingMode.None;
            camera.cullingMask=(1<<0)|(1<<4);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.nearClipPlane=.04f;camera.farClipPlane=20;
            var green=new Material(MatFactory.Unlit(new Color(.07f,.85f,.14f)));var magenta=new Material(MatFactory.Unlit(new Color(.9f,.05f,.8f)));var blue=new Material(MatFactory.Unlit(new Color(.05f,.12f,.9f)));
            var original=new Dictionary<MeshRenderer,Material[]>();
            try
            {
                original[sea]=sea.sharedMaterials;sea.sharedMaterial=green;
                cutout.Dispose();
                Color before=Pixel(camera,IslandCampaign.At(-130,8,0),IslandCampaign.At(-130,-.3f,0),2);
                Check(cutout.ApplyToSkyline(new[]{sea.bounds}),"Reapply the rendering-only cut at the actual western sea bounds.");
                Color after=Pixel(camera,IslandCampaign.At(-130,8,0),IslandCampaign.At(-130,-.3f,0),2);
                Check(after.g>after.r+.3f&&after.g>after.b+.3f&&Vector3.Distance(new(before.r,before.g,before.b),new(after.r,after.g,after.b))>.3f,"Actual GPU pixels expose the sea at worldX490 where the legacy skyline previously hid it.");
                original[paving]=paving.sharedMaterials;paving.sharedMaterial=blue;
                var culvert=root.transform.Find("IslandTidalCulvert (Blender)");Check(culvert,"Authored culvert is present.");
                foreach(var renderer in culvert.GetComponentsInChildren<MeshRenderer>())
                {
                    var slots=renderer.sharedMaterials;var replacement=(Material[])slots.Clone();bool changed=false;
                    for(int i=0;i<slots.Length;i++)if(slots[i]&&slots[i].name.Contains("IslandConcrete")){replacement[i]=magenta;changed=true;}
                    if(changed){original[renderer]=slots;renderer.sharedMaterials=replacement;}
                }
                for(int i=0;i<3;i++)
                {
                    var eye=IslandCampaign.CulvertEntry+new Vector3((i-1)*.002f,1,2.5f);
                    var target=eye+Vector3.down;
                    Color floor=Pixel(camera,eye,target,.2f);
                    Check(floor.r>.65f&&floor.b>.65f&&floor.g<.45f,"Authored floor remains visible over the separated blue underlay across slight camera offsets.");
                }
            }
            finally
            {
                foreach(var item in original)if(item.Key)item.Key.sharedMaterials=item.Value;
                if(cutout&&!cutout.Applied)cutout.ApplyToSkyline(new[]{sea.bounds});
                UnityEngine.Object.Destroy(proofCameraObject);UnityEngine.Object.Destroy(green);UnityEngine.Object.Destroy(magenta);UnityEngine.Object.Destroy(blue);
            }
            bool reached=false;yield return AutoTest.WalkTo(IslandCampaign.At(-26,.15f,-33.5f),20,r=>reached=r,.4f);
            Check(reached,"A real controller reaches the southern sign approach.");
            Possession.PlayerCamera.Instance.SetYaw(0);Possession.PlayerCamera.Instance.SetPitch(0);yield return new WaitForSeconds(.3f);
            if(capture!=null)yield return capture("island-southern-sign-approach.png");
            Debug.Log("[IslandVisual] PASS one-shot arrival cue, metre UVs, separated floors, mapped PBR and rendered western sea/floor proofs");
        }

        static Color Pixel(Camera camera,Vector3 eye,Vector3 target,float size)
        {
            camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye,Vector3.forward));camera.orthographic=true;camera.orthographicSize=size;camera.aspect=1;
            var rt=new RenderTexture(128,128,24,RenderTextureFormat.ARGB32){antiAliasing=1};var pixels=new Texture2D(128,128,TextureFormat.RGB24,false);
            var old=RenderTexture.active;
            try
            {
                Check(rt.Create(),"GPU proof render target is available.");
                var request=new UniversalRenderPipeline.SingleCameraRequest{destination=rt};
                Check(RenderPipeline.SupportsRenderRequest(camera,request),"URP supports the actual island proof capture.");
                RenderPipeline.SubmitRenderRequest(camera,request);RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,128,128),0,0);pixels.Apply();return pixels.GetPixel(64,64);
            }
            finally{RenderTexture.active=old;rt.Release();UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(pixels);}
        }
        static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
