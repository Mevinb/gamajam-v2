using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Parasite.Tests
{
    /// <summary>Explicit A/B diagnosis of automatic city probe work; not a replacement for normal river acceptance.</summary>
    public class CityProbeBurstDiagnosticTests
    {
        RiverReflectionQualityTests original;
        ProbeState[] city;
        bool borrowedEnabledState;
        static readonly string[] Names =
        {
            "Probe_Plaza", "Probe_South", "Probe_Campus", "Probe_Lab", "Probe_Market", "Probe_Avenue", "Probe_Alley"
        };

        sealed class ProbeState
        {
            public ReflectionProbe probe;
            public bool enabled, hdr, box;
            public Vector3 position, size, center;
            public int resolution, mask, importance;
            public float near, far, blend, intensity;
            public ReflectionProbeClearFlags clear;
            public ProbeState(ReflectionProbe value)
            {
                probe = value; enabled = value.enabled; hdr = value.hdr; box = value.boxProjection;
                position = value.transform.position; size = value.size; center = value.center;
                resolution = value.resolution; mask = value.cullingMask; importance = value.importance;
                near = value.nearClipPlane; far = value.farClipPlane; blend = value.blendDistance; intensity = value.intensity;
                clear = value.clearFlags;
            }
            public void AssertAuthoredState()
            {
                Assert.IsTrue(probe, "The diagnostic must not destroy an automatic city probe.");
                Assert.AreEqual(ReflectionProbeMode.Realtime, probe.mode);
                Assert.AreEqual(ReflectionProbeRefreshMode.OnAwake, probe.refreshMode);
                Assert.AreEqual(ReflectionProbeTimeSlicingMode.NoTimeSlicing, probe.timeSlicingMode);
                Assert.AreEqual(position, probe.transform.position); Assert.AreEqual(size, probe.size); Assert.AreEqual(center, probe.center);
                Assert.AreEqual(resolution, probe.resolution); Assert.AreEqual(hdr, probe.hdr); Assert.AreEqual(box, probe.boxProjection);
                Assert.AreEqual(mask, probe.cullingMask); Assert.AreEqual(importance, probe.importance);
                Assert.AreEqual(near, probe.nearClipPlane); Assert.AreEqual(far, probe.farClipPlane);
                Assert.AreEqual(blend, probe.blendDistance); Assert.AreEqual(intensity, probe.intensity); Assert.AreEqual(clear, probe.clearFlags);
            }
        }

        [UnitySetUp]
        public IEnumerator LoadOriginalLowFixture()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("The controlled native capture diagnosis requires the real GPU.");
            original = new RiverReflectionQualityTests();
            yield return original.LoadActualLowRiver();
            Assert.AreEqual(0, GraphicsPreset.Current); Assert.IsFalse(QualitySettings.realtimeReflectionProbes);
            var probes = Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None)
                .Where(value => value.gameObject.scene.name == GameManager.CityScene && value.mode == ReflectionProbeMode.Realtime &&
                    value.refreshMode == ReflectionProbeRefreshMode.OnAwake).OrderBy(value => value.name).ToArray();
            Assert.AreEqual(7, probes.Length, "The experiment must identify the exact seven automatic City probes.");
            CollectionAssert.AreEquivalent(Names, probes.Select(value => value.name).ToArray());
            foreach (var probe in probes)
            {
                Assert.IsTrue(probe.enabled && probe.gameObject.activeInHierarchy);
                Assert.IsTrue(probe.transform.parent && probe.transform.parent.name == "Lighting");
                Assert.AreEqual(ReflectionProbeTimeSlicingMode.NoTimeSlicing, probe.timeSlicingMode);
                Assert.AreEqual(probe.name == "Probe_Alley" ? 128 : 256, probe.resolution);
                Assert.IsTrue(probe.hdr); Assert.AreEqual(-1, probe.cullingMask); Assert.AreEqual(250f, probe.farClipPlane);
                TestContext.WriteLine($"[CityProbeBurst] inventory name={probe.name} id={probe.GetInstanceID()} origin={probe.transform.position:F3} size={probe.size:F3} center={probe.center:F3} hdr={probe.hdr} res={probe.resolution} box={probe.boxProjection} mask={probe.cullingMask} far={probe.farClipPlane} mode={probe.mode} refresh={probe.refreshMode} slicing={probe.timeSlicingMode}");
            }
            city = probes.Select(value => new ProbeState(value)).ToArray();
        }

        [UnityTearDown]
        public IEnumerator RestoreOriginalFixture()
        {
            RestoreBorrowedEnabledStates();
            if (original != null) yield return original.Cleanup();
            original = null; city = null;
        }

        [UnityTest, Explicit("Root-controlled default arm of the native automatic-city-probe A/B diagnosis."), Timeout(60000)]
        public IEnumerator DefaultCityProbesRetainOriginalRapidRiverStress()
            => RunOriginalStress(false);

        [UnityTest, Explicit("Root-controlled experimental arm; disabled probes are not a production quality change."), Timeout(60000)]
        public IEnumerator OnlyAutomaticCityProbesDisabledRetainOriginalRapidRiverStress()
            => RunOriginalStress(true);

        IEnumerator RunOriginalStress(bool control)
        {
            Assert.IsFalse(QualitySettings.realtimeReflectionProbes, "Borrow enabled states only while the fixture is Low.");
            if (control)
            {
                foreach (var state in city) state.probe.enabled = false;
                borrowedEnabledState = true;
            }
            foreach (var state in city)
            {
                state.AssertAuthoredState();
                Assert.AreEqual(control ? false : state.enabled, state.probe.enabled);
            }
            TestContext.WriteLine($"[CityProbeBurst] begin control={control} disabledAutomatic={city.Count(value => !value.probe.enabled)}/7 native={QualitySettings.realtimeReflectionProbes}; original river ViaScripting/image/epoch/retirement assertions and all original waits follow unchanged");
            try
            {
                // Invoke the actual existing case, including Ready's image validation/deadline and NoUnexpectedReceived.
                yield return original.PendingDowngradeCancelsOldEpochAndFreshUpgradePublishesThenDisposes();
                foreach (var state in city) state.AssertAuthoredState();
                TestContext.WriteLine($"[CityProbeBurst] original stress PASS control={control}; no warning expectations or suppression were added");
            }
            finally { RestoreBorrowedEnabledStates(); }
        }

        void RestoreBorrowedEnabledStates()
        {
            if (!borrowedEnabledState) return;
            // The old scene is still live here. Stop native scheduling before restoring borrowed component states.
            GraphicsPreset.Apply(0, save: false);
            Assert.IsFalse(QualitySettings.realtimeReflectionProbes);
            foreach (var state in city)
                if (state.probe)
                {
                    state.AssertAuthoredState(); state.probe.enabled = state.enabled;
                    Assert.AreEqual(state.enabled, state.probe.enabled);
                }
            borrowedEnabledState = false;
            TestContext.WriteLine("[CityProbeBurst] exact automatic-probe enabled states restored while Low; original fixture cleanup restores the caller's scene/preferences next");
        }
    }
}
