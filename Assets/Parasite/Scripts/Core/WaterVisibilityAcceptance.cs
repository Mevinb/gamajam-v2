using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using Object = UnityEngine.Object;
using Parasite.Missions;
using Parasite.Testing;
using Parasite.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;


namespace Parasite.Testing
{
    public static class WaterVisibilityAcceptance
    {
        public static IEnumerator Run(string outputDirectory = null)
        {
            Assert.IsTrue(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null,"Scenario22 requires an actual rendered GPU player; headless logic cannot certify visibility.");
            string output = outputDirectory ?? DefaultOutputDirectory();
            yield return AutoTest.LoadCity();
            RunState.RecordLakesideOutcome(EndingOutcome.Peace);RunState.Flags.Add(MissionFlags.Escaped);Assert.IsTrue(RunState.BeginAfterlake());
            SceneManager.LoadScene(GameManager.CityScene);yield return null;
            float deadline=Time.realtimeSinceStartup+30;
            while((!CityDirector.Instance||!CityDirector.Instance.Ready)&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(CityDirector.Instance&&CityDirector.Instance.Ready);
            var water=Object.FindFirstObjectByType<AfterlakeWater>();var cutout=water?water.GetComponent<SkylineWaterCutout>():null;
            Assert.IsNotNull(cutout);Assert.IsTrue(cutout.Applied,cutout.LastFailure);
            var original=GameObject.Find(SkylineWaterCutout.SourcePath).GetComponent<MeshRenderer>();
            var sourceMesh=original.GetComponent<MeshFilter>().sharedMesh;
            var surfaces=water.GetComponentsInChildren<MeshRenderer>().Where(r=>r.gameObject.layer==4).ToArray();Assert.AreEqual(2,surfaces.Length);
            var originals=surfaces.Select(r=>r.sharedMaterials).ToArray();
            var bounds=surfaces.Select(r=>r.bounds).ToArray();
            Physics.SyncTransforms();
            var footing=GameObject.Find(AfterlakeDistrict.RootName).GetComponentsInChildren<Collider>(true)
                .Select(c=>(collider:c,box:c.bounds,enabled:c.enabled)).ToArray();
            var cameraObject=new GameObject("Water visibility proof camera");var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
            camera.cullingMask=(1<<0)|(1<<4);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            camera.nearClipPlane=.05f;camera.farClipPlane=20;camera.orthographic=true;
            var data=cameraObject.AddComponent<UniversalAdditionalCameraData>();data.renderPostProcessing=false;
            var proof=new Material(Shader.Find("Universal Render Pipeline/Unlit"));proof.SetColor("_BaseColor",new Color(.9f,.02f,.8f));
            try
            {
                foreach(var surface in surfaces)surface.sharedMaterial=proof;
                cutout.Dispose();yield return null;yield return null;
                Assert.IsTrue(original.enabled);
                Color beforeRiver=Capture(camera,new Vector3(350,8,-80),new Vector3(350,-.3f,-80),2,"before-river-proof",128,128,output);
                Color beforeBerth=Capture(camera,new Vector3(265.75f,8,-30),new Vector3(265.75f,-.3f,-30),.2f,"before-berth-proof",128,128,output);
                Assert.IsTrue(beforeRiver.r+beforeRiver.b<1f,"The actual old background slab blocks the lower river surface.");
                Assert.IsTrue(beforeBerth.r+beforeBerth.b<1f,"The actual old slab also blocks the physical ferry notch.");
                Assert.IsTrue(cutout.ApplyToSkyline(bounds),cutout.LastFailure);yield return null;
                Color afterRiver=Capture(camera,new Vector3(350,8,-80),new Vector3(350,-.3f,-80),2,"after-river-proof",128,128,output);
                Color afterBerth=Capture(camera,new Vector3(265.75f,8,-30),new Vector3(265.75f,-.3f,-30),.2f,"after-berth-proof",128,128,output);
                Assert.IsTrue(afterRiver.r+afterRiver.b>1.2f,"River proof surface is visible.");Assert.IsTrue(afterRiver.g<.4f);
                Assert.IsTrue(afterBerth.r+afterBerth.b>1.2f,"Berth proof surface is visible.");Assert.IsTrue(afterBerth.g<.4f);
                Assert.AreSame(sourceMesh,original.GetComponent<MeshFilter>().sharedMesh,"The original generated mesh stays untouched.");
                foreach(var state in footing){Assert.AreEqual(state.enabled,state.collider.enabled);Assert.AreEqual(state.box,state.collider.bounds,"Visual cuts must not change physical footing or NavMesh sources.");}
                for(int i=0;i<surfaces.Length;i++)surfaces[i].sharedMaterials=originals[i];
                var main=Parasite.Possession.PlayerCamera.Instance.Cam;
                var mainData=main.GetUniversalAdditionalCameraData();
                camera.orthographic=false;camera.fieldOfView=main.fieldOfView;camera.farClipPlane=main.farClipPlane;camera.nearClipPlane=main.nearClipPlane;
                camera.cullingMask=main.cullingMask;camera.clearFlags=CameraClearFlags.Skybox;camera.backgroundColor=main.backgroundColor;
                camera.allowHDR=main.allowHDR;camera.allowMSAA=main.allowMSAA;camera.useOcclusionCulling=main.useOcclusionCulling;
                camera.allowDynamicResolution=main.allowDynamicResolution;
                data.renderPostProcessing=mainData.renderPostProcessing;data.renderShadows=mainData.renderShadows;
                data.volumeLayerMask=mainData.volumeLayerMask;data.volumeTrigger=mainData.volumeTrigger;
                data.requiresDepthOption=mainData.requiresDepthOption;data.requiresColorOption=mainData.requiresColorOption;
                data.antialiasing=mainData.antialiasing;data.antialiasingQuality=mainData.antialiasingQuality;
                Capture(camera,new Vector3(282,8,-13),new Vector3(264,-.1f,-46),0,"normal-ferry-water",1600,900,output);
                Capture(camera,new Vector3(250,5,-44),new Vector3(235,-.1f,-95),0,"normal-river-water",1600,900,output);
                Debug.Log("[WaterVisibility] PASS actual before/after downward river+berth visibility, unchanged source mesh/footing; normal shaded captures saved.");
            }
            finally
            {
                for(int i=0;i<surfaces.Length;i++)if(surfaces[i])surfaces[i].sharedMaterials=originals[i];
                if(cutout&&!cutout.Applied)cutout.ApplyToSkyline(bounds);
                Object.Destroy(proof);Object.Destroy(cameraObject);Time.timeScale=1;GameInput.Simulated=false;
                SceneManager.LoadScene(GameManager.MenuScene);
            }
            yield return null;
        }

        static Color Capture(Camera camera,Vector3 eye,Vector3 target,float ortho,string name,int width,int height,string directory)
        {
            var direction=(target-eye).normalized;
            var up=Mathf.Abs(Vector3.Dot(direction,Vector3.up))>.99f?Vector3.forward:Vector3.up;
            camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(direction,up));
            if(camera.orthographic)camera.orthographicSize=ortho;
            var texture=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);texture.Create();
            var pixels=new Texture2D(width,height,TextureFormat.RGB24,false);var old=RenderTexture.active;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=texture});
                RenderTexture.active=texture;pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();
                var center=pixels.GetPixel(width/2,height/2);
                Directory.CreateDirectory(directory);File.WriteAllBytes(Path.Combine(directory,name+".png"),pixels.EncodeToPNG());
                return center;
            }
            finally{RenderTexture.active=old;texture.Release();Object.Destroy(texture);Object.Destroy(pixels);}
        }
        static string DefaultOutputDirectory()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-autotestOut");
            string root=i>=0&&i+1<args.Length?Path.GetDirectoryName(Path.GetFullPath(args[i+1])):Application.persistentDataPath;
            return Path.Combine(root,"water-occlusion");
        }

        static class Assert
        {
            public static void IsTrue(bool value,string message="Expected true") {if(!value)throw new InvalidOperationException(message);}
            public static void IsFalse(bool value,string message="Expected false")=>IsTrue(!value,message);
            public static void IsNotNull(UnityEngine.Object value,string message="Expected existing object")=>IsTrue(value,message);
            public static void AreEqual<T>(T expected,T actual,string message="")=>IsTrue(EqualityComparer<T>.Default.Equals(expected,actual),message+" expected "+expected+", actual "+actual);
            public static void AreSame(object expected,object actual,string message="")=>IsTrue(ReferenceEquals(expected,actual),message);
        }
    }
}
