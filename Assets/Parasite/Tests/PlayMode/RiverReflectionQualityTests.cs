using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Parasite.Missions;
using Parasite.Testing;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Parasite.Tests
{
    /// <summary>Actual river/probe quality transitions and resource ownership, using the GPU when image proof is required.</summary>
    public class RiverReflectionQualityTests
    {
        int oldPreference;
        GraphicsSettingsData oldSettings;
        string oldGraphicsPreference;
        bool hadPreference,hadGraphicsPreference,oldNativeFlag;
        AfterlakeWater water;
        RiverReflectionCapture capture;
        [UnitySetUp]
        public IEnumerator LoadActualLowRiver()
        {
            oldSettings=GraphicsPreset.CaptureSettings();oldNativeFlag=QualitySettings.realtimeReflectionProbes;
            hadPreference=PlayerPrefs.HasKey("parasite.graphics");oldPreference=PlayerPrefs.GetInt("parasite.graphics",-1234);
            hadGraphicsPreference=PlayerPrefs.HasKey(GraphicsSettingsStore.PreferenceKey);
            oldGraphicsPreference=PlayerPrefs.GetString(GraphicsSettingsStore.PreferenceKey,"");
            GraphicsPreset.Apply(0,save:false);
            yield return AutoTest.LoadCity();
            RunState.RecordLakesideOutcome(EndingOutcome.Peace);RunState.Flags.Add(MissionFlags.Escaped);
            Assert.IsTrue(RunState.BeginAfterlake());SceneManager.LoadScene(GameManager.CityScene);yield return null;
            float until=Time.realtimeSinceStartup+30;
            while((!World.CityDirector.Instance||!World.CityDirector.Instance.Ready)&&Time.realtimeSinceStartup<until)yield return null;
            Assert.IsTrue(World.CityDirector.Instance&&World.CityDirector.Instance.Ready);
            water=Object.FindFirstObjectByType<AfterlakeWater>();Assert.IsNotNull(water);
            capture=water.ReflectionCapture;Assert.IsNotNull(capture);
            yield return null;yield return null;
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if(water)Object.Destroy(water.gameObject);
            yield return null;yield return null;
            Time.timeScale=1;GameInput.Simulated=false;RunState.NewGame();SceneManager.LoadScene(GameManager.MenuScene);yield return null;
            GraphicsPreset.ApplySettings(oldSettings,save:false);QualitySettings.realtimeReflectionProbes=oldNativeFlag&&SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null;
            Assert.AreEqual(hadPreference,PlayerPrefs.HasKey("parasite.graphics"));
            Assert.AreEqual(oldPreference,PlayerPrefs.GetInt("parasite.graphics",-1234),"Functional checks must not save a different player preset.");
            Assert.AreEqual(hadGraphicsPreference,PlayerPrefs.HasKey(GraphicsSettingsStore.PreferenceKey));
            Assert.AreEqual(oldGraphicsPreference,PlayerPrefs.GetString(GraphicsSettingsStore.PreferenceKey,""));
        }
        static void NeedGpu()
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)Assert.Ignore("Actual reflection completion/image checks need the released GPU.");
        }
        void Fallback()
        {
            Assert.IsFalse(water.ReflectionReady);Assert.IsTrue(capture.UsingFallback);
            Assert.IsFalse(capture.Publisher.enabled);Assert.IsNull(capture.PublishedTexture);
            if(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null)
                Assert.IsTrue(RiverReflectionCapture.ValidateImage(ReflectionProbe.defaultTexture,out string error,ReflectionProbe.defaultTextureHDRDecodeValues,false),"Global sky/default image must be usable: "+error);
        }
        IEnumerator Ready()
        {
            float until=Time.realtimeSinceStartup+9;
            while(water&&!water.ReflectionReady&&Time.realtimeSinceStartup<until)yield return null;
            Assert.IsTrue(water&&water.ReflectionReady,water?water.ReflectionStatus:"river disappeared");
            Assert.AreEqual(capture.Epoch,capture.PublishedEpoch);
            Assert.IsTrue(capture.Publisher.enabled);Assert.AreEqual(ReflectionProbeMode.Custom,capture.Publisher.mode);
            Assert.AreSame(capture.PublishedTexture,capture.Publisher.customBakedTexture);
            Assert.IsTrue(RiverReflectionCapture.ValidateImage(capture.PublishedTexture,out string error),error);
            Assert.IsFalse(capture.HasPendingCapture);
        }
        [UnityTest,Timeout(60000)]
        public IEnumerator HiddenInfluenceStillCapturesAllSixMarkersAtTheActualRiverOrigin()
        {
            NeedGpu();
            var expectedOrigin=water.transform.TransformPoint(new Vector3(230,3,-41));
            var directions=new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
            var colors=new[]{Color.red,Color.green,Color.blue,Color.yellow,Color.magenta,Color.cyan};
            var markers=new GameObject("Owned river-origin proof markers");var materials=new List<Material>();
            var hidden=new List<Renderer>();
            try
            {
                // Isolate only this synthetic proof room from accidental facade intersections; normal river tests never do this.
                var room=new Bounds(expectedOrigin,Vector3.one*2.5f);
                foreach(var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                    if(renderer.enabled&&renderer.bounds.Intersects(room)){hidden.Add(renderer);renderer.enabled=false;}
                var shader=Shader.Find("Universal Render Pipeline/Unlit");Assert.IsNotNull(shader);
                for(int i=0;i<6;i++)
                {
                    var marker=GameObject.CreatePrimitive(PrimitiveType.Quad);marker.transform.SetParent(markers.transform,false);marker.layer=29;
                    marker.transform.position=expectedOrigin+directions[i];
                    marker.transform.rotation=Quaternion.LookRotation(directions[i],Mathf.Abs(directions[i].y)>.9f?Vector3.forward:Vector3.up);
                    marker.transform.localScale=Vector3.one*2.1f;
                    var collider=marker.GetComponent<Collider>();collider.enabled=false;Object.Destroy(collider);
                    var material=new Material(shader);material.SetColor("_BaseColor",colors[i]);material.SetInt("_Cull",0);
                    marker.GetComponent<Renderer>().sharedMaterial=material;materials.Add(material);
                }
                GraphicsPreset.Apply(2,save:false);
                Assert.Less(Vector3.Distance(capture.PendingProbe.transform.position,expectedOrigin),.001f);
                Assert.Greater(capture.PendingProbe.bounds.min.y,900);
                Assert.IsFalse(capture.Publisher.enabled);
                yield return Ready();
                for(int face=0;face<6;face++)AssertFaceSignature(capture.PublishedTexture,(CubemapFace)face,colors[face]);
            }
            finally
            {
                foreach(var renderer in hidden)if(renderer)renderer.enabled=true;
                Object.Destroy(markers);foreach(var material in materials)Object.Destroy(material);
            }
            // Actual water proof remains a separate fresh-scene fixture; no marker-contaminated cube is reused there.
        }
        static void AssertFaceSignature(Texture cube,CubemapFace face,Color expected)
        {
            using(var readback=new ReflectionCubeReadback(cube,new Vector4(1,1,0,0),cube.width,true))
            {
                var colors=readback.Read(face,0);Color sample=Color.clear;
                for(int y=cube.height/2-2;y<cube.height/2+2;y++)for(int x=cube.width/2-2;x<cube.width/2+2;x++)sample+=colors[y*cube.width+x];
                sample/=16;
                Assert.That(sample.r,expected.r>.5f?Is.GreaterThan(.7f):Is.LessThan(.25f),face+" red: "+sample);
                Assert.That(sample.g,expected.g>.5f?Is.GreaterThan(.7f):Is.LessThan(.25f),face+" green: "+sample);
                Assert.That(sample.b,expected.b>.5f?Is.GreaterThan(.7f):Is.LessThan(.25f),face+" blue: "+sample);
                var preview=new Texture2D(cube.width,cube.height,TextureFormat.RGB24,false);
                try
                {
                    preview.SetPixels(colors);preview.Apply(false);
                    string directory=Path.GetFullPath("Logs/reflection-quality-lifecycle/gpu");Directory.CreateDirectory(directory);
                    File.WriteAllBytes(Path.Combine(directory,"river-origin-"+face+".png"),preview.EncodeToPNG());
                }
                finally{Object.Destroy(preview);}
            }
        }
        [UnityTest,Timeout(60000)]
        public IEnumerator InitialLowToHighCompletesOneImageAndMediumReturnsToGlobalFallback()
        {
            NeedGpu();Assert.IsFalse(QualitySettings.realtimeReflectionProbes);Assert.AreEqual(0,capture.CaptureCount);Fallback();
            GraphicsPreset.Apply(2,save:false);Assert.IsTrue(QualitySettings.realtimeReflectionProbes);
            Assert.IsTrue(capture.HasPendingCapture);Assert.IsFalse(water.ReflectionReady);Assert.IsFalse(capture.Publisher.enabled);
            Assert.Greater(capture.PendingProbe.bounds.min.y,900,"Pending black target is outside every playable reflection volume.");
            yield return Ready();
            var complete=capture.PublishedTexture;int epoch=capture.Epoch,count=capture.CaptureCount;
            GraphicsPreset.Apply(3,save:false);yield return new WaitForSecondsRealtime(.3f);
            Assert.AreEqual(epoch,capture.Epoch);Assert.AreEqual(count,capture.CaptureCount);Assert.AreEqual(1,capture.PublishCount);
            Assert.AreSame(complete,capture.PublishedTexture,"High to Ultra does not trigger repeated captures of unchanged geometry.");
            Assert.IsTrue(water.ReflectionReady,capture.Status);
            Assert.IsTrue(RiverReflectionCapture.ValidateImage(complete,out string retainedError),retainedError);
            GraphicsPreset.Apply(1,save:false);Assert.IsFalse(QualitySettings.realtimeReflectionProbes);Fallback();
            yield return null;yield return null;Assert.IsFalse(complete,"Downgrade frees the completed owned texture.");
        }
        [UnityTest,Timeout(60000)]
        public IEnumerator CustomLowBaselineCanPublishRealHighReflectionsAndDisableThemWithoutChangingPreset()
        {
            NeedGpu();Fallback();
            var custom=GraphicsSettingsData.ForPreset(0);custom.reflectionLevel=3;
            GraphicsPreset.ApplySettings(custom,save:false);
            Assert.AreEqual(0,GraphicsPreset.Current);Assert.IsTrue(GraphicsPreset.IsCustom);
            Assert.IsTrue(GraphicsPreset.RealtimeReflectionsEnabled);Assert.IsTrue(capture.HasPendingCapture);
            yield return Ready();
            var firstImage=capture.PublishedTexture;var previousDirector=World.CityDirector.Instance;
            SceneManager.LoadScene(GameManager.CityScene);yield return null;
            float reloadUntil=Time.realtimeSinceStartup+30;
            while((!World.CityDirector.Instance||World.CityDirector.Instance==previousDirector||!World.CityDirector.Instance.Ready)&&Time.realtimeSinceStartup<reloadUntil)yield return null;
            Assert.IsTrue(World.CityDirector.Instance&&World.CityDirector.Instance!=previousDirector&&World.CityDirector.Instance.Ready);
            water=Object.FindFirstObjectByType<AfterlakeWater>();Assert.IsNotNull(water);capture=water.ReflectionCapture;
            yield return null;yield return null;
            Assert.IsFalse(firstImage,"A scene reload must retire its old owned publication.");
            Assert.AreEqual(0,GraphicsPreset.Current);Assert.IsTrue(GraphicsPreset.IsCustom);
            Assert.AreEqual(3,GraphicsPreset.CaptureSettings().reflectionLevel,"Scene loading must preserve the independent reflection request.");
            yield return Ready();
            var image=capture.PublishedTexture;int requests=capture.CaptureCount,epoch=capture.Epoch;
            custom.reflectionLevel=2;GraphicsPreset.ApplySettings(custom,save:false);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.AreEqual(requests,capture.CaptureCount);Assert.AreEqual(epoch,capture.Epoch);
            Assert.AreSame(image,capture.PublishedTexture);Assert.IsTrue(water.ReflectionReady,capture.Status);
            custom.reflectionLevel=1;GraphicsPreset.ApplySettings(custom,save:false);
            Assert.AreEqual(0,GraphicsPreset.Current,"Custom reflection edits retain the Low baseline.");
            Assert.IsFalse(QualitySettings.realtimeReflectionProbes);Fallback();
            yield return null;yield return null;Assert.IsFalse(image,"The independent reflection setting must retire the actual completed cube.");
        }
        [UnityTest,Timeout(60000)]
        public IEnumerator PendingDowngradeCancelsOldEpochAndFreshUpgradePublishesThenDisposes()
        {
            NeedGpu();GraphicsPreset.Apply(2,save:false);
            var canceled=capture.PendingTarget;var oldProbe=capture.PendingProbe;int oldEpoch=capture.Epoch;
            Assert.IsNotNull(canceled);Assert.IsFalse(capture.Publisher.enabled);
            // Change twice before a rendered frame: the old native request must never publish later.
            GraphicsPreset.Apply(0,save:false);Assert.IsFalse(capture.HasPendingCapture);Fallback();
            GraphicsPreset.Apply(2,save:false);Assert.Greater(capture.Epoch,oldEpoch);Assert.AreNotSame(canceled,capture.PendingTarget);
            yield return Ready();yield return null;yield return null;
            Assert.IsFalse(canceled);Assert.IsFalse(oldProbe);
            Assert.AreEqual(2,capture.CaptureCount);Assert.AreEqual(1,capture.PublishCount);
            var published=capture.PublishedTexture;var publisher=capture.Publisher;
            Object.Destroy(water.gameObject);yield return null;yield return null;
            Assert.IsFalse(published);Assert.IsFalse(publisher);
            GraphicsPreset.Apply(0,save:false);GraphicsPreset.Apply(2,save:false);
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest,Timeout(60000)]
        public IEnumerator RepeatedPendingDowngradesRetireTargetsWhileNativeSchedulingIsOff()
        {
            NeedGpu();
            for(int attempt=0;attempt<3;attempt++)
            {
                GraphicsPreset.Apply(2,save:false);
                var target=capture.PendingTarget;var probe=capture.PendingProbe;
                Assert.IsNotNull(target);Assert.IsNotNull(probe);
                GraphicsPreset.Apply(0,save:false);
                Assert.IsFalse(QualitySettings.realtimeReflectionProbes);Assert.IsFalse(capture.HasPendingCapture);
                yield return null;yield return null;
                Assert.IsFalse(target,"Canceled target must retire without ever waiting for native completion.");
                Assert.IsFalse(probe);Assert.AreEqual(0,capture.PublishCount);Fallback();
            }
            GraphicsPreset.Apply(2,save:false);yield return Ready();
            Assert.AreEqual(4,capture.CaptureCount);Assert.AreEqual(1,capture.PublishCount);
        }
        [Test]
        public void NativePolicyRoundTripsEveryPresetWithoutSaving()
        {
            foreach(int level in new[]{0,1,2,3,1,0})
            {
                GraphicsPreset.Apply(level,save:false);
                Assert.AreEqual(level>=2&&SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null,QualitySettings.realtimeReflectionProbes);
            }
            Assert.IsFalse(capture.HasPendingCapture);Assert.IsFalse(capture.Publisher.enabled);
        }
        [UnityTest,Timeout(45000)]
        public IEnumerator NullGraphicsNeverAllocatesOrRequestsEvenAfterHighSelection()
        {
            if(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null)Assert.Ignore("This focused contract runs in the headless device suite.");
            GraphicsPreset.Apply(2,save:false);yield return null;yield return null;
            Assert.IsFalse(QualitySettings.realtimeReflectionProbes,"Null must suppress automatic city probes as well as explicit river requests.");
            Assert.AreEqual(0,capture.CaptureCount);Assert.IsFalse(capture.HasPendingCapture);Assert.IsNull(capture.PendingTarget);
            Assert.IsFalse(water.ReflectionReady);Assert.IsFalse(capture.Publisher.enabled);StringAssert.Contains("no graphics device",capture.Status);
            var custom=GraphicsSettingsData.ForPreset(0);custom.reflectionLevel=3;
            GraphicsPreset.ApplySettings(custom,save:false);yield return null;yield return null;
            Assert.AreEqual(0,GraphicsPreset.Current);Assert.IsTrue(GraphicsPreset.IsCustom);
            Assert.IsFalse(GraphicsPreset.RealtimeReflectionsEnabled);Assert.IsFalse(QualitySettings.realtimeReflectionProbes);
            Assert.AreEqual(0,capture.CaptureCount);Assert.IsFalse(capture.HasPendingCapture);Assert.IsNull(capture.PendingTarget);
        }
    }
}
